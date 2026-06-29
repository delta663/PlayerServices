using System;
using System.Collections;
using System.Linq;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;
using System.Text;

namespace PlayerServices.Services;

internal static class ClanService
{
	private const float AUTO_JOIN_DELAY_SECONDS = 1f;

	public static void CreateClanForPlayer(ChatCommandContext ctx, string playerName)
	{
		if (!TryFindPlayer(ctx, playerName, out var userEntity, out var charEntity, out var user, out string characterName))
			return;

		if (TryGetClanEntity(user, out var existingClanEntity))
		{
			string existingClanName = GetClanName(existingClanEntity);
			ctx.Reply($"<color=white>{characterName}</color> is already in clan <color=#87CEFA>{existingClanName}</color>.");
			return;
		}

		if (!TryCreateClanRequest(userEntity, charEntity, characterName, out string clanName, out string reason))
		{
			ctx.Reply(reason);
			return;
		}

		ctx.Reply($"Force create clan for <color=white>{characterName}</color>: <color=#87CEFA>{clanName}</color>.");
	}

	public static void JoinPlayerToPlayerClan(ChatCommandContext ctx, string playerAName, string playerBName)
	{
		if (!TryFindPlayer(ctx, playerAName, out var userAEntity, out _, out var userA, out string nameA))
			return;

		if (!TryFindPlayer(ctx, playerBName, out var userBEntity, out var charBEntity, out var userB, out string nameB))
			return;

		if (userAEntity == userBEntity)
		{
			ctx.Reply("<color=yellow>Player A and Player B cannot be the same.</color>");
			return;
		}

		bool hasClanA = TryGetClanEntity(userA, out var clanAEntity);
		bool hasClanB = TryGetClanEntity(userB, out var clanBEntity);

		if (hasClanA && hasClanB)
		{
			string clanAName = GetClanName(clanAEntity);
			string clanBName = GetClanName(clanBEntity);

			if (clanAEntity == clanBEntity)
			{
				ctx.Reply($"<color=white>{nameA}</color> and <color=white>{nameB}</color> are already in the same clan <color=#87CEFA>{clanAName}</color>.");
				return;
			}

			ctx.Reply($"<color=white>{nameA}</color> and <color=white>{nameB}</color> already have clans.");
			return;
		}

		if (hasClanA)
		{
			if (!TryAddUserToClan(userBEntity, clanAEntity, out string joinedClanName, out string reason))
			{
				ctx.Reply(reason);
				return;
			}

			ctx.Reply($"Forced <color=white>{nameB}</color> to join <color=white>{nameA}</color>'s clan <color=#87CEFA>{joinedClanName}</color>.");
			return;
		}

		if (hasClanB)
		{
			if (!TryAddUserToClan(userAEntity, clanBEntity, out string joinedClanName, out string reason))
			{
				ctx.Reply(reason);
				return;
			}

			ctx.Reply($"Forced <color=white>{nameA}</color> to join <color=white>{nameB}</color>'s clan <color=#87CEFA>{joinedClanName}</color>.");
			return;
		}

		if (!TryCreateClanRequest(userBEntity, charBEntity, nameB, out string createdClanName, out string createReason))
		{
			ctx.Reply(createReason);
			return;
		}

		ctx.Reply($"<color=white>{nameA}</color> and <color=white>{nameB}</color> have no clan. Creating clan for <color=white>{nameB}</color>.");

		Core.StartCoroutine(JoinAfterClanCreateRoutine(ctx.Event.SenderUserEntity, userAEntity, userBEntity, nameA, nameB, createdClanName));
	}

	public static void LeaveClan(ChatCommandContext ctx, string playerName)
	{
		if (!TryFindPlayer(ctx, playerName, out var userEntity, out var charEntity, out var user, out string characterName))
			return;

		if (!TryGetClanEntity(user, out var clanEntity))
		{
			ctx.Reply($"<color=white>{characterName}</color> is not in a clan.");
			return;
		}

		if (!TryLeaveClanRequest(userEntity, charEntity, clanEntity, out string clanName, out string reason))
		{
			ctx.Reply(reason);
			return;
		}

		ctx.Reply($"<color=white>{characterName}</color> has been forced to leave clan <color=#87CEFA>{clanName}</color>.");
	}

	private static IEnumerator JoinAfterClanCreateRoutine(Entity adminUserEntity, Entity userAEntity, Entity userBEntity, string nameA, string nameB, string expectedClanName)
	{
		yield return new WaitForSeconds(AUTO_JOIN_DELAY_SECONDS);

		try
		{
			var em = Core.EntityManager;

			if (userAEntity == Entity.Null || userBEntity == Entity.Null || !em.Exists(userAEntity) || !em.Exists(userBEntity))
			{
				NotifyAdmin(adminUserEntity, "<color=red>Clan join failed because player data is no longer available.</color>");
				yield break;
			}

			if (!em.HasComponent<User>(userAEntity) || !em.HasComponent<User>(userBEntity))
			{
				NotifyAdmin(adminUserEntity, "<color=red>Clan join failed because user data is no longer available.</color>");
				yield break;
			}

			var userA = em.GetComponentData<User>(userAEntity);
			var userB = em.GetComponentData<User>(userBEntity);

			if (TryGetClanEntity(userA, out var clanAEntity))
			{
				string clanAName = GetClanName(clanAEntity);
				NotifyAdmin(adminUserEntity, $"<color=white>{nameA}</color> is already in clan <color=#87CEFA>{clanAName}</color>.");
				yield break;
			}

			Entity clanBEntity = Entity.Null;

			if (!TryGetClanEntity(userB, out clanBEntity))
			{
				FindClan(expectedClanName, out clanBEntity);
			}

			if (clanBEntity == Entity.Null || !em.Exists(clanBEntity) || !em.HasComponent<ClanTeam>(clanBEntity))
			{
				NotifyAdmin(adminUserEntity, $"<color=red>Clan join failed because <color=white>{nameB}</color>'s clan was not created yet.</color>");
				yield break;
			}

			RefreshClanTagOnCharacter(userBEntity, GetClanName(clanBEntity));

			if (!TryAddUserToClan(userAEntity, clanBEntity, out string joinedClanName, out string reason))
			{
				NotifyAdmin(adminUserEntity, reason);
				yield break;
			}

			NotifyAdmin(adminUserEntity, $"Forced <color=white>{nameA}</color> to join <color=white>{nameB}</color>'s clan <color=#87CEFA>{joinedClanName}</color>.");
		}
		catch (Exception e)
		{
			Core.LogException(e);
			NotifyAdmin(adminUserEntity, "<color=red>Clan join failed because an error occurred.</color>");
		}
	}

	private static bool TryCreateClanRequest(Entity userEntity, Entity charEntity, string characterName, out string clanName, out string reason)
	{
		clanName = string.Empty;
		reason = string.Empty;

		var em = Core.EntityManager;

		if (userEntity == Entity.Null || charEntity == Entity.Null || !em.Exists(userEntity) || !em.Exists(charEntity))
		{
			reason = "<color=red>Failed: target character entity not found.</color>";
			return false;
		}

		clanName = GenerateUniqueClanName(characterName);

		if (string.IsNullOrWhiteSpace(clanName))
		{
			reason = "<color=red>Failed: cannot generate clan name.</color>";
			return false;
		}

		var fromCharacter = new FromCharacter
		{
			User = userEntity,
			Character = charEntity
		};

		var archetype = em.CreateArchetype(
			ComponentType.ReadWrite<FromCharacter>(),
			ComponentType.ReadWrite<ClanEvents_Client.CreateClan_Request>()
		);

		var requestEntity = em.CreateEntity(archetype);

		em.SetComponentData(requestEntity, fromCharacter);
		em.SetComponentData(requestEntity, new ClanEvents_Client.CreateClan_Request
		{
			ClanName = new FixedString64Bytes(clanName),
			ClanMotto = new FixedString64Bytes(clanName)
		});

		return true;
	}

	private static bool TryAddUserToClan(Entity userEntity, Entity clanEntity, out string clanName, out string reason)
	{
		clanName = string.Empty;
		reason = string.Empty;

		var em = Core.EntityManager;

		if (userEntity == Entity.Null || clanEntity == Entity.Null || !em.Exists(userEntity) || !em.Exists(clanEntity))
		{
			reason = "<color=red>Failed to resolve user or clan entity.</color>";
			return false;
		}

		if (!em.HasComponent<User>(userEntity))
		{
			reason = "<color=red>Failed to read target user data.</color>";
			return false;
		}

		if (!em.HasComponent<ClanTeam>(clanEntity))
		{
			reason = "<color=red>Failed to resolve clan data.</color>";
			return false;
		}

		var user = em.GetComponentData<User>(userEntity);

		if (TryGetClanEntity(user, out var existingClanEntity))
		{
			string existingClanName = GetClanName(existingClanEntity);
			reason = $"<color=white>{user.CharacterName}</color> is already in clan <color=#87CEFA>{existingClanName}</color>.";
			return false;
		}

		TeamUtility.AddUserToClan(em, clanEntity, userEntity, ref user, CastleHeartLimitType.User);
		em.SetComponentData(userEntity, user);

		SetClanRoleMember(userEntity, clanEntity);
		clanName = GetClanName(clanEntity);
		RefreshClanTagOnCharacter(userEntity, clanName);

		return true;
	}

	private static bool TryLeaveClanRequest(Entity userEntity, Entity charEntity, Entity clanEntity, out string clanName, out string reason)
	{
		clanName = GetClanName(clanEntity);
		reason = string.Empty;

		var em = Core.EntityManager;

		if (userEntity == Entity.Null || charEntity == Entity.Null || clanEntity == Entity.Null ||
			!em.Exists(userEntity) || !em.Exists(charEntity) || !em.Exists(clanEntity))
		{
			reason = "<color=red>Failed to resolve user, character, or clan entity.</color>";
			return false;
		}

		if (!em.HasComponent<NetworkId>(clanEntity))
		{
			reason = "<color=red>Failed to read clan NetworkId.</color>";
			return false;
		}

		var fromCharacter = new FromCharacter
		{
			User = userEntity,
			Character = charEntity
		};

		var clanId = em.GetComponentData<NetworkId>(clanEntity);

		var archetype = em.CreateArchetype(
			ComponentType.ReadWrite<FromCharacter>(),
			ComponentType.ReadWrite<ClanEvents_Client.LeaveClan>()
		);

		var requestEntity = em.CreateEntity(archetype);

		em.SetComponentData(requestEntity, fromCharacter);
		em.SetComponentData(requestEntity, new ClanEvents_Client.LeaveClan
		{
			ClanId = clanId
		});

		RefreshClanTagOnCharacter(userEntity, string.Empty);

		return true;
	}

	private static void SetClanRoleMember(Entity userEntity, Entity clanEntity)
	{
		var em = Core.EntityManager;

		if (em.HasBuffer<ClanMemberStatus>(clanEntity) && em.HasBuffer<SyncToUserBuffer>(clanEntity))
		{
			var members = em.GetBuffer<ClanMemberStatus>(clanEntity);
			var userBuffer = em.GetBuffer<SyncToUserBuffer>(clanEntity);

			for (int i = 0; i < members.Length && i < userBuffer.Length; i++)
			{
				if (!userBuffer[i].UserEntity.Equals(userEntity))
					continue;

				var member = members[i];
				member.ClanRole = ClanRoleEnum.Member;
				members[i] = member;
				break;
			}
		}

		if (em.HasComponent<ClanRole>(userEntity))
		{
			var clanRole = em.GetComponentData<ClanRole>(userEntity);
			clanRole.Value = ClanRoleEnum.Member;
			em.SetComponentData(userEntity, clanRole);
		}
	}

	private static bool TryFindPlayer(ChatCommandContext ctx, string playerName, out Entity userEntity, out Entity charEntity, out User user, out string characterName)
	{
		userEntity = Entity.Null;
		charEntity = Entity.Null;
		user = default;
		characterName = string.Empty;

		if (!Helper.TryFindUserByExactName(playerName, out userEntity, out user))
		{
			ctx.Reply($"<color=red>Player not found:</color> <color=white>{playerName}</color>");
			return false;
		}

		var em = Core.EntityManager;

		if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
		{
			ctx.Reply("<color=red>Target user entity is not available.</color>");
			return false;
		}

		user = em.GetComponentData<User>(userEntity);
		charEntity = user.LocalCharacter.GetEntityOnServer();
		characterName = user.CharacterName.ToString();

		if (charEntity == Entity.Null || !em.Exists(charEntity))
		{
			ctx.Reply($"<color=red>Target character entity not found:</color> <color=white>{characterName}</color>");
			return false;
		}

		return true;
	}

	private static bool TryGetClanEntity(User user, out Entity clanEntity)
	{
		clanEntity = Entity.Null;

		if (user.ClanEntity.Equals(NetworkedEntity.Empty))
			return false;

		clanEntity = user.ClanEntity.GetEntityOnServer();

		return clanEntity != Entity.Null && Core.EntityManager.Exists(clanEntity);
	}

	private static string GenerateUniqueClanName(string characterName)
	{
		string raw = string.IsNullOrWhiteSpace(characterName) ? "CLAN" : characterName;
		string clean = new string(raw.Where(char.IsLetterOrDigit).ToArray());

		if (string.IsNullOrWhiteSpace(clean))
			clean = "CLAN";

		clean = clean.ToUpperInvariant();

		string baseClanName = clean.Length <= 4 ? clean : clean.Substring(0, 4);
		string finalClanName = baseClanName;
		int suffix = 2;

		while (FindClan(finalClanName, out _))
		{
			finalClanName = $"{baseClanName}{suffix}";
			suffix++;

			if (suffix > 99)
				return string.Empty;
		}

		return finalClanName;
	}

	private static bool FindClan(string clanName, out Entity clanEntity)
	{
		clanEntity = Entity.Null;

		var em = Core.EntityManager;
		NativeArray<Entity> clans = default;

		try
		{
			clans = Helper.GetEntitiesByComponentType<ClanTeam>();

			foreach (var clan in clans)
			{
				if (clan == Entity.Null || !em.Exists(clan) || !em.HasComponent<ClanTeam>(clan))
					continue;

				var clanTeam = em.GetComponentData<ClanTeam>(clan);

				if (!string.Equals(clanTeam.Name.ToString(), clanName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (em.HasBuffer<ClanMemberStatus>(clan) && em.GetBuffer<ClanMemberStatus>(clan).Length == 0)
					continue;

				clanEntity = clan;
				return true;
			}
		}
		finally
		{
			if (clans.IsCreated)
				clans.Dispose();
		}

		return false;
	}

	private static string GetClanName(Entity clanEntity)
	{
		var em = Core.EntityManager;

		if (clanEntity == Entity.Null || !em.Exists(clanEntity) || !em.HasComponent<ClanTeam>(clanEntity))
			return "Unknown";

		return em.GetComponentData<ClanTeam>(clanEntity).Name.ToString();
	}

	private static void RefreshClanTagOnCharacter(Entity userEntity, string clanName)
	{
		try
		{
			var em = Core.EntityManager;

			if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
				return;

			var user = em.GetComponentData<User>(userEntity);
			var charEntity = user.LocalCharacter.GetEntityOnServer();

			if (charEntity == Entity.Null || !em.Exists(charEntity) || !em.HasComponent<PlayerCharacter>(charEntity))
				return;

			var playerCharacter = em.GetComponentData<PlayerCharacter>(charEntity);
			playerCharacter.SmartClanName = string.IsNullOrWhiteSpace(clanName)
				? default
				: ClanUtility.GetSmartClanName(clanName);

			em.SetComponentData(charEntity, playerCharacter);
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

	private static void NotifyAdmin(Entity adminUserEntity, string message)
	{
		try
		{
			var em = Core.EntityManager;

			if (adminUserEntity == Entity.Null || !em.Exists(adminUserEntity) || !em.HasComponent<User>(adminUserEntity))
				return;

			Helper.NotifyUser(adminUserEntity, message);
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

	public static void ReplyHelp(ChatCommandContext ctx)
	{
		var sb = new StringBuilder();

			sb.AppendLine("<color=yellow>Clan Commands:</color>");
			sb.AppendLine("<color=green>.clan forcecreate <player></color> or <color=green>.c fc</color>");
			sb.AppendLine("<color=green>.clan forcejoin <playerA> <playerB></color> or <color=green>.c fj</color>");
			sb.AppendLine("<color=green>.clan forceleave <player></color> or <color=green>.c fl</color>");
			
		ctx.Reply(sb.ToString().TrimEnd());
	}
}
