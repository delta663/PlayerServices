using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using VampireCommandFramework;
using System;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class AdminService
{
    private static readonly Dictionary<ulong, (Entity AdminChar, Entity TargetChar)> _trackers = new();
    private static bool _trackingMonitorRunning = false;

    private const float TRACKING_INTERVAL = 0.04f;
    private const float REBUFF_DELAY_SECONDS = 0.2f;

    // ---------------------------------------------------------------------
    // Monitor Tracking
    // ---------------------------------------------------------------------

	private static IEnumerator TrackPlayerRoutine(ChatCommandContext ctx, ulong adminSteamId, Entity adminChar, Entity targetChar, User targetUser, float delay)
	{
		if (delay > 0f)
			yield return new WaitForSeconds(delay);

		try
		{
			var em = Core.EntityManager;

			if (adminChar == Entity.Null || !em.Exists(adminChar) || !em.HasComponent<Translation>(adminChar) ||
				targetChar == Entity.Null || !em.Exists(targetChar) || !em.HasComponent<Translation>(targetChar) ||
				!BuffUtility.HasBuff(em, adminChar, PrefabData.Observe))
			{
				Core.Log.LogWarning($"[Admin] Failed to start tracking {targetUser.CharacterName} after delay.");
				yield break;
			}

			_trackers[adminSteamId] = (adminChar, targetChar);
			EnsureTrackingMonitorRunning();

			var targetPos = targetChar.Read<Translation>().Value;
			adminChar.Write(new Translation { Value = targetPos });

			ctx.Reply($"<color=yellow>Now tracking:</color> <color=white>{targetUser.CharacterName}</color>");
			ctx.Reply("Usage: <color=green>.pls track <player></color> to track others or <color=green>.pls untrack</color> to stop.");
			ctx.Reply("Usage: <color=green>.pls observe</color> to toggle Observe mode and untrack.");
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

    private static void EnsureTrackingMonitorRunning()
    {
        if (_trackingMonitorRunning)
            return;

        _trackingMonitorRunning = true;
        Core.StartCoroutine(MonitorTrackingCoroutine());
    }

	private static IEnumerator MonitorTrackingCoroutine()
	{
		try
		{
			while (_trackers.Count > 0)
			{
				try
				{
					MonitorTrackingTick();
				}
				catch (Exception e)
				{
					Core.LogException(e);
					_trackers.Clear();
				}

				yield return new WaitForSeconds(TRACKING_INTERVAL);
			}
		}
		finally
		{
			_trackingMonitorRunning = false;
		}
	}

    private static void MonitorTrackingTick()
	{
		var em = Core.EntityManager;
		var toRemove = new List<ulong>();

		foreach (var kvp in _trackers)
		{
			ulong adminId = kvp.Key;
			var (adminChar, targetChar) = kvp.Value;

			if (adminChar == Entity.Null || targetChar == Entity.Null ||
				!em.Exists(adminChar) || !em.Exists(targetChar) ||
				!BuffUtility.HasBuff(em, adminChar, PrefabData.Observe))
			{
				toRemove.Add(adminId);
				continue;
			}

			if (!em.HasComponent<Translation>(adminChar) || !em.HasComponent<Translation>(targetChar))
			{
				toRemove.Add(adminId);
				continue;
			}

			var targetPos = targetChar.Read<Translation>().Value;
			adminChar.Write(new Translation { Value = targetPos });
		}

		foreach (var adminId in toRemove)
		{
			_trackers.Remove(adminId);
		}
	}

    // ---------------------------------------------------------------------
    // Observe, Track
    // ---------------------------------------------------------------------

    public static void ToggleObserve(ChatCommandContext ctx)
    {
        var adminUser = ctx.Event.SenderUserEntity;
        var adminChar = ctx.Event.SenderCharacterEntity;
        var em = Core.EntityManager;
        ulong steamId = adminUser.Read<User>().PlatformId;

        bool isObserving = BuffUtility.HasBuff(em, adminChar, PrefabData.Observe);

        if (isObserving)
        {
            Buffs.RemoveBuff(adminChar, PrefabData.Observe);
            _trackers.Remove(steamId);
            
            ctx.Reply("<color=yellow>Observe mode:</color> <color=red>Off</color>. Usage: <color=green>.pls observe</color> to toggle.");
            Core.Log.LogInfo($"[Admin] {ctx.Event.SenderUserEntity.Read<User>().CharacterName} disabled observe mode.");
        }
        else
        {
            Buffs.AddBuff(adminUser, adminChar, PrefabData.Observe, -1, true);
            ctx.Reply("<color=yellow>Observe mode:</color> <color=green>On</color>. Usage: <color=green>.pls observe</color> to toggle.");
            ctx.Reply("Usage: <color=green>.pls track <player></color> to track a player.");
            Core.Log.LogInfo($"[Admin] {ctx.Event.SenderUserEntity.Read<User>().CharacterName} enabled observe mode.");
        }
    }

    public static void TrackPlayer(ChatCommandContext ctx, string targetName)
    {
        if (!TryFindPlayerName(ctx, targetName, out var targetUserEntity, out var targetChar, out var targetUser))
            return;

        var em = Core.EntityManager;

        var adminUserEntity = ctx.Event.SenderUserEntity;
        var adminChar = ctx.Event.SenderCharacterEntity;
        var adminUser = adminUserEntity.Read<User>();
        ulong adminSteamId = adminUser.PlatformId;

        if (adminSteamId == targetUser.PlatformId)
        {
            ctx.Reply("<color=red>You cannot track yourself.</color>");
            return;
        }

        if (adminChar == Entity.Null || !em.Exists(adminChar) || !em.HasComponent<Translation>(adminChar))
        {
            ctx.Reply("<color=red>Could not read your character position.</color>");
            return;
        }

        if (targetChar == Entity.Null || !em.Exists(targetChar) || !em.HasComponent<Translation>(targetChar))
        {
            ctx.Reply("<color=red>Could not read target position.</color>");
            return;
        }

        bool addedObserveBuff = false;

        if (!BuffUtility.HasBuff(em, adminChar, PrefabData.Observe))
        {
            if (!Buffs.AddBuff(adminUserEntity, adminChar, PrefabData.Observe, -1, true))
            {
                ctx.Reply("<color=red>Failed to enter observe mode.</color>");
                return;
            }

            addedObserveBuff = true;
        }

        Core.StartCoroutine(TrackPlayerRoutine(ctx, adminSteamId, adminChar, targetChar, targetUser, addedObserveBuff ? 0.2f : 0f));
    }
    
    public static void UntrackPlayer(ChatCommandContext ctx)
    {
        ulong adminSteamId = ctx.Event.SenderUserEntity.Read<User>().PlatformId;

        if (_trackers.ContainsKey(adminSteamId))
        {
            _trackers.Remove(adminSteamId);
            ctx.Reply("<color=yellow>Tracking stopped.</color> Usage: <color=green>.pls track <player></color> to track.");
            ctx.Reply("Usage: <color=green>.pls observe</color> to toggle Observe mode and untrack.");
        }
        else
        {
            ctx.Reply("<color=yellow>You are not currently tracking anyone.</color>");
        }
    }

    // ---------------------------------------------------------------------
    // Potion buff
    // ---------------------------------------------------------------------

    public static void ApplyPotionBuffs(ChatCommandContext ctx, string targetName)
    {
        Entity targetUserEntity;
        Entity targetCharEntity;
        User targetUser;

        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetUserEntity = ctx.Event.SenderUserEntity;
            targetCharEntity = ctx.Event.SenderCharacterEntity;
            targetUser = targetUserEntity.Read<User>();
        }
        else
        {
            if (!TryFindPlayerName(ctx, targetName, out targetUserEntity, out targetCharEntity, out targetUser))
                return;
        }

        string playerName = targetUser.CharacterName.ToString();

        ctx.Reply($"Applied <color=yellow>Potion Buffs</color> to <color=white>{playerName}</color>.");
        Helper.NotifyUser(targetUserEntity, "You have received <color=green>Potion Buffs</color>.");
        Core.Log.LogInfo($"[Admin] {ctx.Event.SenderUserEntity.Read<User>().CharacterName} applied Potion Buffs to {playerName}");

        Core.StartCoroutine(ApplyBuffsRoutine(targetUserEntity, targetCharEntity));
    }

	private static IEnumerator ApplyBuffsRoutine(Entity targetUserEntity, Entity targetCharEntity)
	{
		bool hasRemovedAny;

		try
		{
			hasRemovedAny = RemoveExistingPotionBuffs(targetCharEntity);
		}
		catch (Exception e)
		{
			Core.LogException(e);
			yield break;
		}

		if (hasRemovedAny)
		{
			yield return new WaitForSeconds(REBUFF_DELAY_SECONDS);
		}

		try
		{
			ApplyPotionBuffsAfterDelay(targetUserEntity, targetCharEntity);
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

    private static bool RemoveExistingPotionBuffs(Entity targetCharEntity)
	{
		var em = Core.EntityManager;

		if (targetCharEntity == Entity.Null || !em.Exists(targetCharEntity))
			return false;

		bool hasRemovedAny = false;

		foreach (var buffGuid in PrefabData.PotionBuff)
		{
			if (BuffUtility.HasBuff(em, targetCharEntity, buffGuid))
			{
				Buffs.RemoveBuff(targetCharEntity, buffGuid);
				hasRemovedAny = true;
			}
		}

		return hasRemovedAny;
	}

	private static void ApplyPotionBuffsAfterDelay(Entity targetUserEntity, Entity targetCharEntity)
	{
		var em = Core.EntityManager;

		if (targetCharEntity == Entity.Null || targetUserEntity == Entity.Null ||
			!em.Exists(targetCharEntity) || !em.Exists(targetUserEntity))
		{
			return;
		}

		foreach (var buffGuid in PrefabData.PotionBuff)
		{
			Buffs.AddBuff(targetUserEntity, targetCharEntity, buffGuid);
		}
	}

    // ---------------------------------------------------------------------
    // Find Player
    // ---------------------------------------------------------------------

    private static bool TryFindPlayerName(ChatCommandContext ctx, string targetName, out Entity targetUserEntity, out Entity targetCharEntity, out User targetUser)
    {
        targetUserEntity = Entity.Null;
        targetCharEntity = Entity.Null;
        targetUser = default;

        if (string.IsNullOrWhiteSpace(targetName) || targetName.Trim().Length < 2)
        {
            ctx.Reply("<color=yellow>Please enter at least 2 characters.</color>");
            return false;
        }

        string query = targetName.Trim();

        if (!Helper.TryFindUserByName(query, out targetUserEntity, out targetUser, out List<string> candidates))
        {
            if (candidates != null && candidates.Count > 0)
            {
                if (candidates.Count > 10)
                {
                    ctx.Reply($"<color=yellow>Too many players found for</color> <color=white>{query}</color>. Please enter a more specific name.");
                }
                else
                {
                    ctx.Reply($"<color=yellow>Multiple players matched</color> <color=white>{query}</color>: {string.Join(", ", candidates)}");
                }

                return false;
            }

            ctx.Reply($"<color=red>No player found matching</color> <color=white>{query}</color>");
            return false;
        }

        if (!targetUser.IsConnected)
        {
            ctx.Reply($"<color=white>{targetUser.CharacterName}</color> <color=yellow>is offline.</color>");
            return false;
        }

        targetCharEntity = targetUser.LocalCharacter.GetEntityOnServer();

        if (targetCharEntity == Entity.Null || !Core.EntityManager.Exists(targetCharEntity))
        {
            ctx.Reply("<color=red>Target character not found.</color>");
            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Reload All
    // ---------------------------------------------------------------------

    public static void ReloadAll(ChatCommandContext ctx)
	{
		try
		{
			Plugin.PluginConfig?.Reload();

			int dailyKitCount = DailyKitService.ReloadConfig();
			int starterKitCount = StarterKitService.ReloadConfig();
            int auraCount = AuraService.ReloadConfig();
            
			bool teleportOk = TeleportPointsService.ReloadConfig(out int teleportPointCount);
			bool giveOk = GiveService.ReloadConfig(out int giveSetCount);

			var sb = new StringBuilder();
			sb.AppendLine("<color=green>PlayerServices configs reloaded.</color>");
			sb.AppendLine($"DailyKit: <color=white>{dailyKitCount}</color> item(s)");
			sb.AppendLine($"StarterKit: <color=white>{starterKitCount}</color> item(s)");
			sb.AppendLine($"Aura: reloaded <color=white>{auraCount}</color> aura(s)");
			sb.AppendLine($"Teleport: <color=white>{teleportPointCount}</color> point(s)" + (teleportOk ? "" : " <color=red>(failed)</color>"));
			sb.AppendLine($"Give: <color=white>{giveSetCount}</color> set(s)" + (giveOk ? "" : " <color=red>(failed)</color>"));

			ctx.Reply(sb.ToString().TrimEnd());

			Core.Log.LogInfo($"[Admin] Reloaded all configs. DailyKit={dailyKitCount}, StarterKit={starterKitCount}, Aura={auraCount}, Teleport={teleportPointCount}, TeleportOk={teleportOk}, Give={giveSetCount}, GiveOk={giveOk}");
		}
		catch (Exception e)
		{
			Core.LogException(e);
			ctx.Reply("<color=red>Failed to reload PlayerServices configs.</color> Check the server log.");
		}
	}
}