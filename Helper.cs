using System.Collections.Generic;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Gameplay.Clan;
using ProjectM.Network;
using System;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Linq;
using System.Collections;
using PlayerServices.Data;

namespace PlayerServices;

internal static partial class Helper
{

	public static NativeArray<Entity> GetEntitiesByComponentType<T1>(bool includeAll = false, bool includeDisabled = false, bool includeSpawn = false, bool includePrefab = false, bool includeDestroyed = false)
	{
		EntityQueryOptions options = EntityQueryOptions.Default;
		if (includeAll) options |= EntityQueryOptions.IncludeAll;
		if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
		if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
		if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
		if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

		var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
			.AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
			.WithOptions(options);

		var query = Core.EntityManager.CreateEntityQuery(ref entityQueryBuilder);

		var entities = query.ToEntityArray(Allocator.Temp);
		return entities;
	}

	public static bool TryGetInventoryEntity(Entity characterEntity, out Entity inventoryEntity)
    {
        return InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, characterEntity, out inventoryEntity);
    }

    public static int GetItemCountInInventory(Entity characterEntity, PrefabGUID itemPrefab)
    {
        var em = Core.EntityManager;
        if (!TryGetInventoryEntity(characterEntity, out var inv))
            return 0;

        if (em.HasComponent<InventoryBuffer>(inv))
        {
            var buffer = em.GetBuffer<InventoryBuffer>(inv);
            int total = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                var slot = buffer[i];
                if (slot.ItemType.GuidHash == itemPrefab.GuidHash)
                    total += slot.Amount;
            }
            return total;
        }

        int sum = 0;
        for (int i = 0; i < 36; i++)
        {
            if (InventoryUtilities.TryGetItemAtSlot(em, characterEntity, i, out InventoryBuffer item))
            {
                if (item.ItemType.GuidHash == itemPrefab.GuidHash)
                    sum += item.Amount;
            }
        }
        return sum;
    }

    public static Entity AddItemToInventory(Entity recipient, PrefabGUID guid, int amount)
    {
        try
        {
            var inventoryResponse = Core.ServerGameManager.TryAddInventoryItem(recipient, guid, amount);
            return inventoryResponse.NewEntity;
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }

        return Entity.Null;
    }

    public static bool TryRemoveItemsFromInventory(Entity characterEntity, PrefabGUID itemPrefab, int amount)
    {
        var em = Core.EntityManager;
        if (!TryGetInventoryEntity(characterEntity, out var inv))
            return false;

        if (!em.HasComponent<InventoryBuffer>(inv))
            return false;

        var buffer = em.GetBuffer<InventoryBuffer>(inv);
        int toRemove = amount;

        for (int i = buffer.Length - 1; i >= 0 && toRemove > 0; i--)
        {
            var slot = buffer[i];
            if (slot.ItemType.GuidHash != itemPrefab.GuidHash)
                continue;
            if (slot.Amount <= 0)
                continue;

            int take = math.min(slot.Amount, toRemove);
            slot.Amount -= take;
            toRemove -= take;

            if (slot.Amount <= 0)
            {
                slot.ItemType = new PrefabGUID(0);
                slot.Amount = 0;
            }

            buffer[i] = slot;
        }

        return toRemove == 0;
    }

	public static bool IsInCombat(Entity characterEntity)
	{
		var em = Core.EntityManager;

		if (characterEntity == Entity.Null || !em.Exists(characterEntity))
			return false;

		foreach (var buffGuid in PrefabData.CombatBuffs)
		{
			if (BuffUtility.HasBuff(em, characterEntity, buffGuid))
			{
				return true;
			}
		}

		return false;
	}

	public static bool IsRaidTime()
    {
        EntityQuery query = default;
        try
        {
            var em = Core.EntityManager;
            
            query = em.CreateEntityQuery(ComponentType.ReadOnly<ServerGameBalanceSettings>());
            if (!query.HasSingleton<ServerGameBalanceSettings>()) 
            {
                return false;
            }
            
            var balanceSettings = query.GetSingleton<ServerGameBalanceSettings>();

            if (balanceSettings.CastleDamageMode == CastleDamageMode.Always) return true;
            if (balanceSettings.CastleDamageMode == CastleDamageMode.Never) return false;
            if (balanceSettings.CastleDamageMode == CastleDamageMode.TimeRestricted)
            {
                var settings = Core.ServerGameSettingsSystem.Settings;
                if (settings == null) return false;

                var interactionSettings = settings.PlayerInteractionSettings;
                
                DayOfWeek today = DateTime.Now.DayOfWeek;
        	    bool isWeekend = today == DayOfWeek.Saturday || today == DayOfWeek.Sunday;

                var raidSchedule = isWeekend ? interactionSettings.VSCastleWeekendTime : interactionSettings.VSCastleWeekdayTime;

                if (raidSchedule.StartHour == 0 && raidSchedule.StartMinute == 0 && 
                    raidSchedule.EndHour == 0 && raidSchedule.EndMinute == 0)
                {
                    return false;
                }

                TimeSpan raidStart = new TimeSpan(raidSchedule.StartHour, raidSchedule.StartMinute, 0);
                TimeSpan raidEnd = new TimeSpan(raidSchedule.EndHour, raidSchedule.EndMinute, 0);
                TimeSpan now = DateTime.Now.TimeOfDay;

                if (raidStart <= raidEnd)
                {
                    return now >= raidStart && now <= raidEnd;
                }
                else
                {
                    return now >= raidStart || now <= raidEnd; 
                }
            }

            return false;
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return false; 
        }
        finally
        {
            if (query != default)
            {
                query.Dispose();
            }
        }
    }

	public static string TrimName(string name, int maxLength = 20)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return name.Length > maxLength ? name.Substring(0, maxLength) + "…" : name;
    }

    public static bool TryFindUserByExactName(string playerName, out Entity userEntity, out User user)
	{
		userEntity = Entity.Null;
		user = default;

		if (string.IsNullOrWhiteSpace(playerName))
			return false;

		var em = Core.EntityManager;
		NativeArray<Entity> userEntities = default;

		try
		{
			userEntities = Helper.GetEntitiesByComponentType<User>();

			foreach (var entity in userEntities)
			{
				if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<User>(entity))
					continue;

				var currentUser = em.GetComponentData<User>(entity);
				string currentName = currentUser.CharacterName.ToString();

				if (!string.Equals(currentName, playerName, StringComparison.OrdinalIgnoreCase))
					continue;

				userEntity = entity;
				user = currentUser;
				return true;
			}
		}
		finally
		{
			if (userEntities.IsCreated)
				userEntities.Dispose();
		}

		return false;
	}

    public static bool TryFindUserByName(string query, out Entity userEntity, out User user, out List<string> candidates)
    {
        userEntity = Entity.Null;
        user = default;
        candidates = null;

        var all = new List<(Entity ent, User usr, string name)>();
        NativeArray<Entity> ents = default;

        try
        {
            ents = GetEntitiesByComponentType<User>();

            for (int i = 0; i < ents.Length; i++)
            {
                var ent = ents[i];

                if (!Core.EntityManager.Exists(ent) || !Core.EntityManager.HasComponent<User>(ent))
                    continue;

                var u = ent.Read<User>();
                var name = u.CharacterName.ToString();

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                all.Add((ent, u, name));
            }
        }
        finally
        {
            if (ents.IsCreated)
                ents.Dispose();
        }

        var exact = all.Where(x => string.Equals(x.name, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            userEntity = exact[0].ent;
            user = exact[0].usr;
            return true;
        }

        var contains = all.Where(x => x.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        if (contains.Count == 1)
        {
            userEntity = contains[0].ent;
            user = contains[0].usr;
            return true;
        }

        if (contains.Count > 1)
        {
            candidates = contains.Select(x => x.name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return false;
    }

    public static bool TryFindUsersByRadius(Entity centerCharacter, float radius, out List<(Entity UserEntity, Entity CharacterEntity, User User, float Distance)> players, ulong excludeSteamId = 0, bool includeAdmins = false)
    {
        players = new List<(Entity UserEntity, Entity CharacterEntity, User User, float Distance)>();

        var em = Core.EntityManager;

        if (radius <= 0f) return false;

        if (centerCharacter == Entity.Null || !em.Exists(centerCharacter) || !em.HasComponent<LocalToWorld>(centerCharacter)) return false;

        float3 center = em.GetComponentData<LocalToWorld>(centerCharacter).Position;

        NativeArray<Entity> users = default;

        try
        {
            users = GetEntitiesByComponentType<User>();

            foreach (var userEntity in users)
            {
                if (!em.Exists(userEntity) || !em.HasComponent<User>(userEntity)) continue;

                var user = userEntity.Read<User>();

                if (!user.IsConnected || (excludeSteamId != 0 && user.PlatformId == excludeSteamId) || (!includeAdmins && user.IsAdmin)) continue;

                var charEntity = user.LocalCharacter.GetEntityOnServer();

                if (charEntity == Entity.Null || !em.Exists(charEntity) || !em.HasComponent<LocalToWorld>(charEntity)) continue;

                float3 pos = em.GetComponentData<LocalToWorld>(charEntity).Position;
                float distance = math.distance(pos, center);

                if (distance > radius) continue;

                players.Add((userEntity, charEntity, user, distance));
            }
        }
        finally
        {
            if (users.IsCreated)
                users.Dispose();
        }

        return players.Count > 0;
    }

    public static void NotifyUser(Entity userEntity, string message)
    {
        try
        {
            var em = Core.EntityManager;
            var user = em.GetComponentData<User>(userEntity);
            var fs = new FixedString512Bytes(message);
            ServerChatUtils.SendSystemMessageToClient(em, user, ref fs);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
