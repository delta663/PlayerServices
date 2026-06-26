using HarmonyLib;
using Unity.Entities;
using System;
using ProjectM;
using ProjectM.Network;
using Stunlock.Network;
using PlayerServices.Services;
using Stunlock.Core;
using Unity.Collections;

namespace PlayerServices.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUserConnected))]
public static class PlayerConnectionPatch     // Credit Bloodcraft
{
    [HarmonyPostfix]
    public static void Postfix(ServerBootstrapSystem __instance, NetConnectionId netConnectionId)
    {
        try
        {
            if (!__instance._NetEndPointToApprovedUserIndex.TryGetValue(netConnectionId, out int userIndex))
                return;

            ServerBootstrapSystem.ServerClient serverClient = __instance._ApprovedUsersLookup[userIndex];

            Entity userEntity = serverClient.UserEntity;
            User user = __instance.EntityManager.GetComponentData<User>(userEntity);

            BlacklistService.CheckUserLogin(userEntity, user);
            WelcomeService.SendWelcomeMessageDelayed(userEntity, user);
            AuraService.RefreshOnConnect(userEntity, user);
        }

        catch (Exception e)
        {
            Core.LogException(e);
        }
    }
}

[HarmonyPatch(typeof(Destroy_TravelBuffSystem), nameof(Destroy_TravelBuffSystem.OnUpdate))]
public static class PlayerCreationPatch     // Credit KindredCommands
{
    private static readonly PrefabGUID CoffinSpawnBuff = new PrefabGUID(722466953);

    [HarmonyPrefix]
    public static void Prefix(Destroy_TravelBuffSystem __instance)
    {
        NativeArray<Entity> entities = default;

        try
        {
            var em = __instance.EntityManager;

            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<TravelBuff>(),
                ComponentType.ReadOnly<EntityOwner>(),
                ComponentType.ReadOnly<PrefabGUID>(),
                ComponentType.ReadOnly<DestroyTag>()
            );

            entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var guid = em.GetComponentData<PrefabGUID>(entity);

                if (guid != CoffinSpawnBuff) continue;

                var owner = em.GetComponentData<EntityOwner>(entity).Owner;
                if (!em.HasComponent<PlayerCharacter>(owner)) continue;

                var playerChar = em.GetComponentData<PlayerCharacter>(owner);
                var userEntity = playerChar.UserEntity;
                var user = em.GetComponentData<User>(userEntity);

                string playerName = user.CharacterName.ToString();

                Core.Log.LogInfo($"[Connection] Player created: {playerName} ({user.PlatformId})");

                StarterKitService.GiveKit(userEntity, user);
            }
        }
        catch (Exception ex)
        {
            Core.LogException(ex);
        }
        finally
        {
            if (entities.IsCreated)
                entities.Dispose();
        }
    }
}