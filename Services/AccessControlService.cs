using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;

namespace PlayerServices.Services;

internal static class AccessControlService
{
    public static void CheckUserLogin(Entity userEntity, User user)
    {
        if (user.PlatformId == 0)
            return;

        var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

        if (BanService.IsBannedPlayer(cache))
        {
            KickPlayer(userEntity);
            Core.Log.LogInfo($"[Banlist] Banned player was kicked: {user.CharacterName} ({user.PlatformId})");
            return;
        }

	    if (Plugin.onlyWhitelistEnable.Value)
	    {
		    if (!WhitelistService.IsWhitelistedPlayer(cache))
		    {
			    KickPlayer(userEntity);
			    Core.Log.LogInfo($"[Whitelist] Non-whitelisted player was kicked: {user.CharacterName} ({user.PlatformId})");
			    return;
		    }

		    string knownAs = string.IsNullOrWhiteSpace(cache.KnownAs)
			    ? "Unknown"
			    : cache.KnownAs;

		    Core.Log.LogInfo($"[Whitelist] Whitelisted player connected: {user.CharacterName} ({user.PlatformId}) | Known As: {knownAs}");
            return;
	    }
        
        string knownAsText = cache == null || string.IsNullOrWhiteSpace(cache.KnownAs)
	        ? ""
	        : $" | Known As: {cache.KnownAs}";

        Core.Log.LogInfo($"[Connected] Player connected: {user.CharacterName} ({user.PlatformId}){knownAsText}");
    }

    public static void KickPlayer(Entity userEntity)
    {
        EntityManager entityManager = Core.Server.EntityManager;
        User user = userEntity.Read<User>();

        if (!user.IsConnected || user.PlatformId == 0)
            return;

        Entity entity = entityManager.CreateEntity(new ComponentType[3]
        {
            ComponentType.ReadOnly<NetworkEventType>(),
            ComponentType.ReadOnly<SendEventToUser>(),
            ComponentType.ReadOnly<KickEvent>()
        });

        entity.Write(new KickEvent()
        {
            PlatformId = user.PlatformId
        });
        entity.Write(new SendEventToUser()
        {
            UserIndex = user.Index
        });
        entity.Write(new NetworkEventType()
        {
            EventId = NetworkEvents.EventId_KickEvent,
            IsAdminEvent = false,
            IsDebugEvent = false
        });
    }

    public static void KickIfOnline(ulong steamId)
    {
        NativeArray<Entity> users = default;

        try
        {
            users = Helper.GetEntitiesByComponentType<User>();

            foreach (var userEnt in users)
            {
                var user = userEnt.Read<User>();

                if (user.PlatformId == steamId && user.IsConnected)
                {
                    KickPlayer(userEnt);
                    break;
                }
            }
        }
        finally
        {
            if (users.IsCreated)
                users.Dispose();
        }
    }
}
