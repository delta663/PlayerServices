using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using VampireCommandFramework;
using ProjectM.CastleBuilding;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class ChangeNameService
{
    private static readonly string LOG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string LOG_FILE = Path.Combine(LOG_DIR, "rename_log.csv");
    private static readonly object LOG_LOCK = new();

    // ---------------------------------------------------------------------
    // Process Rename
    // ---------------------------------------------------------------------
    public static void ProcessRename(ChatCommandContext ctx, Entity userEntity, Entity charEntity, string oldName, string newName, ulong steamId, bool isAdminOverride)
    {
        if (!Plugin.changeNameFeatureEnabled.Value)
        {
            ctx.Reply("<color=red>Change name feature is currently disabled.</color>");
            return;
        }

        if (!isAdminOverride && Helper.IsRaidTime())
        {
            ctx.Reply("<color=red>You cannot change your name during active Raid Time.</color>");
            return;
        }

        if (!isAdminOverride && Helper.IsInCombat(charEntity))
        {
            ctx.Reply("<color=red>You cannot change your name while in combat.</color>");
            return;
        }

        if (!ValidateNewName(newName, oldName, steamId, out string normalizedName, out string valLogReason, out string valReplyMessage))
        {
            ReplyHelp(ctx, valReplyMessage);
            AppendRenameLog(steamId, oldName, newName, false, valLogReason);
            return;
        }

        newName = normalizedName;

        bool currencySpent = false;

        if (!isAdminOverride)
        {
            if (!TrySpendCurrency(charEntity, out string spendLogReason, out string spendReplyMessage))
            {
                ctx.Reply(spendReplyMessage);
                AppendRenameLog(steamId, oldName, newName, false, spendLogReason);
                return;
            }

            currencySpent = true;
        }

        try
        {
            PerformRenameECS(userEntity, charEntity, oldName, newName);
        }
        catch (Exception ex)
        {
			HandleRenameFailureAfterPayment(ctx, charEntity, steamId, oldName, newName, ex, currencySpent);
			return;
        }

        string successReason = isAdminOverride ? "admin_rename" : "successful";

        SendRenameNotifications(ctx, userEntity, oldName, newName, isAdminOverride);

        AppendRenameLog(steamId, oldName, newName, true, successReason);
        Core.Log.LogInfo($"[ChangeName] Player {oldName} has changed name to {newName} ({steamId}) (Admin Override: {isAdminOverride})");
    }

    private static bool ValidateNewName(string newName, string oldName, ulong currentSteamId, out string normalizedName, out string logReason, out string replyMessage)
    {
        normalizedName = string.Empty;
        logReason = string.Empty;
        replyMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(newName))
        {
            logReason = "empty_name";
            replyMessage = "<color=red>Name cannot be empty.</color>";
            return false;
        }

        normalizedName = newName.Trim().Normalize(NormalizationForm.FormC);
        newName = normalizedName;

        string normalizedOldName = (oldName ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);

        if (string.Equals(newName, normalizedOldName, StringComparison.OrdinalIgnoreCase))
        {
            logReason = "same_name";
            replyMessage = "<color=yellow>This is already your current name.</color>";
            return false;
        }

        if (!Regex.IsMatch(newName, @"^[\p{L}\p{M}\p{N}]+$"))
        {
            logReason = "invalid_chars";
            replyMessage = "<color=red>Name can only contain letters and numbers.</color>";
            return false;
        }

        int byteLen = Encoding.UTF8.GetByteCount(newName);
        if (byteLen > 56)
        {
            logReason = "too_long";
            replyMessage = "<color=red>Name is too long.</color>";
            return false;
        }

        NativeArray<Entity> userEntities = default;

        try
        {
            userEntities = Helper.GetEntitiesByComponentType<User>();
            string lowerNewName = newName.ToLowerInvariant();

            foreach (var entity in userEntities)
            {
                if (!Core.EntityManager.Exists(entity) || !Core.EntityManager.HasComponent<User>(entity))
                    continue;

                var u = entity.Read<User>();

                if (u.PlatformId == currentSteamId)
                    continue;

                if (u.CharacterName.ToString().ToLowerInvariant() == lowerNewName)
                {
                    logReason = "duplicate_name";
                    replyMessage = "<color=red>This name is already in use.</color>";
                    return false;
                }
            }
        }
        finally
        {
            if (userEntities.IsCreated)
                userEntities.Dispose();
        }

        return true;
    }

    private static void PerformRenameECS(Entity userEntity, Entity charEntity, string oldName, string newName)
	{
		var em = Core.EntityManager;
		var fixedName = new FixedString64Bytes(newName);

		UpdateUserNameAndClanName(em, userEntity, oldName, newName, fixedName);
		SendRenameDebugEvent(em, userEntity, charEntity, fixedName);
		UpdatePlayerMapIconName(em, charEntity, fixedName);
		RefreshCastleOwnershipAfterRename(em, userEntity);
	}

    // ---------------------------------------------------------------------
    // Update after rename
    // ---------------------------------------------------------------------

	private static void UpdateUserNameAndClanName(EntityManager em, Entity userEntity, string oldName, string newName, FixedString64Bytes fixedName)
	{
		if (userEntity == Entity.Null || !em.Exists(userEntity))
			throw new InvalidOperationException("User entity is not available.");

		if (!em.HasComponent<User>(userEntity))
			throw new InvalidOperationException("User entity does not have User component.");

		var user = em.GetComponentData<User>(userEntity);
		user.CharacterName = fixedName;
		em.SetComponentData(userEntity, user);

		UpdateDefaultClanName(em, user.ClanEntity._Entity, oldName, newName);
	}

	private static void UpdateDefaultClanName(EntityManager em, Entity clanEntity, string oldName, string newName)
	{
		if (clanEntity == Entity.Null || !em.Exists(clanEntity) || !em.HasComponent<ClanTeam>(clanEntity))
			return;

		var clanTeam = em.GetComponentData<ClanTeam>(clanEntity);
		string clanName = clanTeam.Name.ToString();

		if (clanName != oldName && clanName != $"{oldName}'s Clan")
			return;

		string updatedClanName = clanName.Replace(oldName, newName);
		clanTeam.Name = new FixedString64Bytes(updatedClanName);
		em.SetComponentData(clanEntity, clanTeam);
	}

	private static void SendRenameDebugEvent(EntityManager em, Entity userEntity, Entity charEntity, FixedString64Bytes fixedName)
	{
		if (userEntity == Entity.Null || !em.Exists(userEntity))
			throw new InvalidOperationException("User entity is not available.");

		if (charEntity == Entity.Null || !em.Exists(charEntity))
			throw new InvalidOperationException("Character entity is not available.");

		if (!em.HasComponent<NetworkId>(userEntity))
			throw new InvalidOperationException("User entity does not have NetworkId.");

		var des = Core.Server.GetExistingSystemManaged<DebugEventsSystem>();
		var networkId = em.GetComponentData<NetworkId>(userEntity);

		var renameEvent = new RenameUserDebugEvent
		{
			NewName = fixedName,
			Target = networkId
		};

		var fromCharacter = new FromCharacter
		{
			User = userEntity,
			Character = charEntity
		};

		des.RenameUser(fromCharacter, renameEvent);
	}

	private static void UpdatePlayerMapIconName(EntityManager em, Entity charEntity, FixedString64Bytes fixedName)
	{
		if (!em.Exists(charEntity) || !em.HasBuffer<AttachedBuffer>(charEntity))
			return;

		var attachedBuffer = em.GetBuffer<AttachedBuffer>(charEntity);

		foreach (var entry in attachedBuffer)
		{
			if (entry.PrefabGuid.GuidHash != PrefabData.MapIconPlayer.GuidHash)
				continue;

			if (!em.Exists(entry.Entity) || !em.HasComponent<PlayerMapIcon>(entry.Entity))
				continue;

			var pmi = em.GetComponentData<PlayerMapIcon>(entry.Entity);
			pmi.UserName = fixedName;
			em.SetComponentData(entry.Entity, pmi);
		}
	}

	private static void RefreshCastleOwnershipAfterRename(EntityManager em, Entity userEntity)
	{
		if (!em.Exists(userEntity))
			return;

		Entity renamedUserClan = Entity.Null;

		if (em.HasComponent<User>(userEntity))
		{
			renamedUserClan = em.GetComponentData<User>(userEntity).ClanEntity._Entity;
		}

		NativeArray<Entity> castleHearts = default;

		try
		{
			castleHearts = Helper.GetEntitiesByComponentType<CastleHeart>();

			foreach (var heartEntity in castleHearts)
			{
				if (!em.Exists(heartEntity) || !em.HasComponent<UserOwner>(heartEntity))
					continue;

				var userOwner = em.GetComponentData<UserOwner>(heartEntity);
				var castleOwnerEntity = userOwner.Owner.GetEntityOnServer();

				if (!ShouldRefreshCastleForRenamedUser(em, castleOwnerEntity, userEntity, renamedUserClan))
					continue;

				TeamUtility.ClaimCastle(em, castleOwnerEntity, heartEntity, CastleHeartLimitType.User);
			}
		}
		finally
		{
			if (castleHearts.IsCreated)
				castleHearts.Dispose();
		}
	}

	private static bool ShouldRefreshCastleForRenamedUser(EntityManager em, Entity castleOwnerEntity, Entity renamedUserEntity, Entity renamedUserClan)
	{
		if (castleOwnerEntity == Entity.Null || !em.Exists(castleOwnerEntity))
			return false;

		if (castleOwnerEntity == renamedUserEntity)
			return true;

		if (renamedUserClan == Entity.Null)
			return false;

		if (!em.HasComponent<User>(castleOwnerEntity))
			return false;

		var castleOwnerUser = em.GetComponentData<User>(castleOwnerEntity);

		return castleOwnerUser.ClanEntity._Entity == renamedUserClan;
	}

    // ---------------------------------------------------------------------
    // Spend and Refund
    // ---------------------------------------------------------------------

    private static bool TrySpendCurrency(Entity characterEntity, out string spendLogReason, out string spendReplyMessage)
    {
        spendLogReason = string.Empty;
        spendReplyMessage = string.Empty;

        try
        {
            var currencyPrefab = new PrefabGUID(Plugin.changeNameCurrencyPrefab.Value);
            int cost = Plugin.changeNameCurrencyCost.Value;
            string currencyName = Plugin.changeNameCurrencyName.Value;

            if (cost <= 0) return true;

            int have = Helper.GetItemCountInInventory(characterEntity, currencyPrefab);
            if (have < cost)
            {
                spendLogReason = $"not_enough_currency_{have}/{cost}";
                spendReplyMessage = $"<color=red>Not enough {currencyName} ({have}/{cost}).</color>";
                return false;
            }

            if (!Helper.TryRemoveItemsFromInventory(characterEntity, currencyPrefab, cost))
            {
                spendLogReason = "remove_items_failed";
                spendReplyMessage = "<color=red>Failed to remove the required currency.</color>";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            spendLogReason = "exception: " + e.Message;
            spendReplyMessage = "<color=red>Error: An unexpected error occurred while spending currency.</color>";
            Core.LogException(e);
            return false;
        }
    }

    private static void HandleRenameFailureAfterPayment(ChatCommandContext ctx, Entity charEntity, ulong steamId, string oldName, string newName, Exception ex, bool currencySpent)
	{
		string logReason = "rename_exception: " + ex.Message;

		if (currencySpent)
		{
			if (TryRefundCurrency(charEntity, out string refundLogReason))
			{
				ctx.Reply("<color=red>Failed to change name.</color> <color=yellow>Your currency has been refunded.</color>");
				logReason += " | refund_success";
			}
			else
			{
				ctx.Reply("<color=red>Failed to change name.</color> <color=yellow>Currency refund failed. Please contact an admin.</color>");
				logReason += " | refund_failed: " + refundLogReason;

				Core.Log.LogError($"[ChangeName] Rename failed and refund failed for {oldName} ({steamId}): {refundLogReason}");
			}
		}
		else
		{
			ctx.Reply("<color=red>Failed to change name.</color>");
		}

		AppendRenameLog(steamId, oldName, newName, false, logReason);
		Core.LogException(ex);
	}

    private static bool TryRefundCurrency(Entity characterEntity, out string refundLogReason)
    {
        refundLogReason = string.Empty;

        try
        {
            var currencyPrefab = new PrefabGUID(Plugin.changeNameCurrencyPrefab.Value);
            int cost = Plugin.changeNameCurrencyCost.Value;

            if (cost <= 0)
                return true;

            var itemEntity = Helper.AddItemToInventory(characterEntity, currencyPrefab, cost);

            if (itemEntity == Entity.Null)
            {
                refundLogReason = "add_item_failed";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            refundLogReason = "exception: " + e.Message;
            Core.LogException(e);
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // Utilities & Helpers
    // ---------------------------------------------------------------------

    public static void ReplyHelp(ChatCommandContext ctx, string warningLine = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(warningLine))
        {
            sb.AppendLine(warningLine);
        }

        bool isEnabled = Plugin.changeNameFeatureEnabled.Value;
        string status = isEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";
        int cost = Plugin.changeNameCurrencyCost.Value;
        string currencyName = Plugin.changeNameCurrencyName.Value; 

        if (isEnabled)
        {
            sb.AppendLine("<color=yellow>Change Name Feature</color>");
            sb.AppendLine("<color=yellow>Command:</color> <color=green>.cn to <NewName></color>");
            sb.AppendLine("<color=yellow>Example:</color> <color=green>.cn to ShadowHunter</color>");
            sb.AppendLine($"<color=yellow>Cost:</color> <color=#87CEFA>{cost} {currencyName}</color>");
            sb.AppendLine("<color=yellow>Allowed characters:</color> letters from any language and numbers");
        }

        sb.AppendLine($"<color=yellow>Change Name Status:</color> {status}");

        ctx.Reply(sb.ToString().TrimEnd());
    }

    private static void SendRenameNotifications(ChatCommandContext ctx, Entity userEntity, string oldName, string newName, bool isAdminOverride)
    {
        if (isAdminOverride)
        {
            ctx.Reply($"Renamed <color=white>{oldName}</color> to <color=white>{newName}</color>.");
            
            if (userEntity != Entity.Null && Core.EntityManager.Exists(userEntity) && userEntity.Has<User>())
            {
                var targetUser = userEntity.Read<User>();
                if (targetUser.IsConnected)
                {
                    var msg = new FixedString512Bytes($"Your name has been changed to <color=white>{newName}</color> by an admin.");
                    ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, targetUser, ref msg);
                }
            }
        }
        else
        {
            ctx.Reply($"Your name has been changed to <color=white>{newName}</color>.");
        }

        string broadcastMsgStr = Plugin.changeNameBroadcastMessage.Value
            .Replace("#oldname#", oldName)
            .Replace("#newname#", newName);
            
        string discordMsg = Plugin.changeNameWebhookMessage.Value
            .Replace("#oldname#", oldName)
            .Replace("#newname#", newName);

        if (isAdminOverride)
        {
            if (Plugin.adminChangeNameBroadcastAndWebhookEnabled.Value)
            {
                
		        if (!string.IsNullOrWhiteSpace(broadcastMsgStr))
		        {
			        var broadcastMsg = new FixedString512Bytes(broadcastMsgStr);
			        ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref broadcastMsg);
		        }
                
                _ = WebhookService.SendAsync(discordMsg);
            }
        }
        else
        {
            if (Plugin.playerChangeNameBroadcastEnabled.Value && !string.IsNullOrWhiteSpace(broadcastMsgStr))
            {
                var broadcastMsg = new FixedString512Bytes(broadcastMsgStr);
                ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref broadcastMsg);
            }

            if (Plugin.playerChangeNameWebhookEnabled.Value)
            {
                _ = WebhookService.SendAsync(discordMsg);
            }
        }
    }

    public static bool TryFindUserByName(string query, out Entity userEntity, out Entity charEntity, out User user)
    {
        userEntity = Entity.Null;
        charEntity = Entity.Null;
        user = default;

        NativeArray<Entity> userEntities = default;

        try
        {
            userEntities = Helper.GetEntitiesByComponentType<User>();

            foreach (var ent in userEntities)
            {
                if (!Core.EntityManager.Exists(ent) || !Core.EntityManager.HasComponent<User>(ent))
                    continue;

                var u = ent.Read<User>();

                if (string.Equals(u.CharacterName.ToString(), query, StringComparison.OrdinalIgnoreCase))
                {
                    userEntity = ent;
                    user = u;
                    charEntity = u.LocalCharacter.GetEntityOnServer();
                    return true;
                }
            }
        }
        finally
        {
            if (userEntities.IsCreated)
                userEntities.Dispose();
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // Disk IO & Logging
    // ---------------------------------------------------------------------

    private static void AppendRenameLog(ulong steamId, string oldName, string newName, bool success, string reason = "")
    {
        try
        {
            lock (LOG_LOCK)
            {
                Directory.CreateDirectory(LOG_DIR);
                bool newFile = !File.Exists(LOG_FILE);
                using var fs = new FileStream(LOG_FILE, FileMode.Append, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs, new UTF8Encoding(false));

                if (newFile)
                    sw.WriteLine("times,steam_id,player_name_old,player_name_new,success,reason");
                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{steamId},{Helper.Csv(oldName)},{Helper.Csv(newName)},{(success ? "true" : "false")},{Helper.Csv(reason)}");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}
