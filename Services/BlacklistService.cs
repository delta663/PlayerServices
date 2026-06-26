using System;
using ProjectM.Network;
using Unity.Entities;
using VampireCommandFramework;
using PlayerServices.Data;
using Unity.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace PlayerServices.Services;

internal static class BlacklistService
{

    // ---------------------------------------------------------------------
    // Checking
    // ---------------------------------------------------------------------

    public static void CheckUserLogin(Entity userEntity, User user)
    {
	    if (user.PlatformId == 0) return;

	    var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

	    // Blacklist
	    if (cache != null && cache.IsBlacklisted)
	    {
		    KickPlayer(userEntity);
		    Core.Log.LogInfo($"[Blacklist] Kicked blacklisted player: {user.PlatformId} ({user.CharacterName})");
		    return;
	    }

	    // Whitelist
	    if (Plugin.onlyWhitelistEnable.Value)
	    {
		    if (!IsWhitelistedPlayer(cache))
		    {
			    KickPlayer(userEntity);
			    Core.Log.LogInfo($"[Whitelist] Kicked non-whitelisted player: {user.PlatformId} ({user.CharacterName})");
			    return;
		    }
	    }
    }

    // ---------------------------------------------------------------------
    // Blacklist
    // ---------------------------------------------------------------------

    public static void AddBlacklist(ChatCommandContext ctx, string targetInput)
    {
        PlayerCacheData cache = null;

        if (ulong.TryParse(targetInput, out ulong steamId))
        {
            cache = PlayerDataService.GetOrCreatePlayerCache(steamId);
            cache.IsBlacklisted = true;
            PlayerDataService.SaveData();

            string identifier = string.IsNullOrWhiteSpace(cache.InGameName) ? $"{steamId}" : $"{steamId} ({cache.InGameName})";
            ctx.Reply($"<color=red>Blacklisted player:</color> <color=white>{identifier}</color>");
            Core.Log.LogInfo($"[Blacklist] Blacklisted player: {identifier}");

            KickIfOnline(steamId);
        }
        else
        {
            cache = PlayerDataService.GetPlayerCacheByName(targetInput);
            
            if (cache == null)
            {
                ctx.Reply($"<color=red>Player not found in history:</color> <color=white>{targetInput}</color>. " +
                          $"<color=yellow>To pre-blacklist a new player, please use their SteamID.</color>");
                return;
            }

            cache.IsBlacklisted = true;
            PlayerDataService.SaveData();

            ctx.Reply($"<color=red>Blacklisted player:</color> {cache.SteamID} <color=white>({cache.InGameName})</color> ");
            Core.Log.LogInfo($"[Blacklist] Blacklisted player: {cache.SteamID} ({cache.InGameName})");

            KickIfOnline(cache.SteamID);
        }
    }

    public static void RemoveBlacklist(ChatCommandContext ctx, string targetInput)
    {
        PlayerCacheData cache = null;

        if (ulong.TryParse(targetInput, out ulong steamId))
        {
            cache = PlayerDataService.GetPlayerCache(steamId);
            
            if (cache == null || !cache.IsBlacklisted)
            {
                ctx.Reply($"<color=white>{steamId}</color> <color=red>is not in the blacklist.</color>");
                return;
            }

            cache.IsBlacklisted = false;

            if (string.IsNullOrWhiteSpace(cache.InGameName) && string.IsNullOrWhiteSpace(cache.KnownAs) && !cache.IsWhitelisted && cache.LastOnlineTicks == 0)
            {
	            PlayerDataService.RemovePlayerCache(steamId);
            }
            else
            {
	            PlayerDataService.SaveData();
            }

            string identifier = string.IsNullOrWhiteSpace(cache.InGameName) ? $"{steamId}" : $"{steamId} ({cache.InGameName})";
            ctx.Reply($"<color=green>Unblacklisted player:</color> <color=white>{identifier}</color>");
            Core.Log.LogInfo($"[Blacklist] Unblacklisted player: {identifier}");
        }
        
        else
        {
            cache = PlayerDataService.GetPlayerCacheByName(targetInput);
            
            if (cache == null || !cache.IsBlacklisted)
            {
                ctx.Reply($"<color=white>{targetInput}</color> <color=red>is not in the blacklist.</color>");
                return;
            }

            cache.IsBlacklisted = false;
            PlayerDataService.SaveData();

            ctx.Reply($"<color=green>Unblacklisted player:</color> {cache.SteamID} <color=white>({cache.InGameName})</color> ");
            Core.Log.LogInfo($"[Blacklist] Unblacklisted player: {cache.SteamID} ({cache.InGameName})");
        }
    }

    public static void ShowBlacklist(ChatCommandContext ctx)
    {
        var blacklistedPlayers = PlayerDataService.GetBlacklistedPlayers();

        if (blacklistedPlayers.Count == 0)
        {
            ctx.Reply("<color=yellow>The blacklist is empty.</color>");
            return;
        }

        int batchSize = 8;
        int totalPages = (int)Math.Ceiling((double)blacklistedPlayers.Count / batchSize);

        for (int i = 0; i < blacklistedPlayers.Count; i += batchSize)
        {
            var sb = new StringBuilder();
            
            int currentPage = (i / batchSize) + 1;
            sb.AppendLine($"<color=red>Blacklisted Players</color> ({currentPage}/{totalPages})");

            var batch = blacklistedPlayers.GetRange(i, Math.Min(batchSize, blacklistedPlayers.Count - i));
            
            foreach (var player in batch)
            {
                string nameDisplay = string.IsNullOrWhiteSpace(player.InGameName) ? "Unknown" : player.InGameName;
                sb.AppendLine($"{player.SteamID} <color=white>{nameDisplay}</color>");
            }

            ctx.Reply(sb.ToString().TrimEnd());
        }
    }

    // ---------------------------------------------------------------------
    // Whitelist
    // ---------------------------------------------------------------------

    public static void AddWhitelist(ChatCommandContext ctx, string steamIdInput, string knownAs)
    {
	    if (string.IsNullOrWhiteSpace(steamIdInput) || string.IsNullOrWhiteSpace(knownAs))
	    {
		    ReplyWhitelistHelp(ctx);
		    return;
	    }

	    knownAs = knownAs.Trim();

	    if (string.IsNullOrWhiteSpace(knownAs))
	    {
		    ReplyWhitelistHelp(ctx);
		    return;
	    }

	    if (!ulong.TryParse(steamIdInput, out ulong steamId) || steamId == 0)
	    {
		    ctx.Reply("<color=red>Please provide a valid SteamID (numbers only).</color>");
		    return;
	    }

	    var cache = PlayerDataService.GetOrCreatePlayerCache(steamId);

	    cache.KnownAs = knownAs;
	    cache.IsWhitelisted = true;

	    PlayerDataService.SaveData();

	    ctx.Reply($"<color=green>Whitelisted</color> <color=white>{cache.KnownAs}</color> (<color=white>{steamId}</color>).");
	    Core.Log.LogInfo($"[Whitelist] Added {steamId} with Known As {cache.KnownAs}");
    }

    public static void RemoveWhitelist(ChatCommandContext ctx, string targetInput)
    {
        if (!TryFindWhitelistTarget(ctx, targetInput, out var cache))
            return;

        if (cache == null || !cache.IsWhitelisted)
        {
            ctx.Reply($"<color=white>{targetInput}</color> <color=red>is not in the whitelist.</color>");
            return;
        }

        string displayName = GetWhitelistDisplayName(cache);

        cache.IsWhitelisted = false;
        PlayerDataService.SaveData();

        if (Plugin.onlyWhitelistEnable.Value)
	    {
		    KickIfOnline(cache.SteamID);
	    }

        ctx.Reply($"<color=green>Removed from whitelist:</color> <color=white>{displayName}</color>");
        Core.Log.LogInfo($"[Whitelist] Removed SteamID {cache.SteamID} ({displayName}) from whitelist");
    }



    public static void ShowWhitelist(ChatCommandContext ctx)
    {
        var whitelistedPlayers = GetWhitelistedPlayers();

        if (whitelistedPlayers.Count == 0)
        {
            ctx.Reply("<color=yellow>The whitelist is empty.</color>");
            return;
        }

        const string header = "<color=green>Whitelisted Players</color>";
        const string continuedHeader = "<color=green>Whitelisted Players</color> (continued)";

        var sb = new StringBuilder();
        sb.AppendLine(header);

        foreach (var player in whitelistedPlayers)
        {
            string line = GetWhitelistDisplayName(player);

            if (sb.Length + line.Length + Environment.NewLine.Length > Core.MAX_REPLY_LENGTH &&
                sb.Length > header.Length + Environment.NewLine.Length)
            {
                ctx.Reply(sb.ToString().TrimEnd());

                sb.Clear();
                sb.AppendLine(continuedHeader);
            }

            sb.AppendLine(line);
        }

        if (sb.Length > header.Length + Environment.NewLine.Length)
        {
            ctx.Reply(sb.ToString().TrimEnd());
        }
    }

    private static List<PlayerCacheData> GetWhitelistedPlayers()
    {
        var list = new List<PlayerCacheData>();

        foreach (var player in PlayerDataService.GetAllPlayerCaches())
        {
            if (IsWhitelistedPlayer(player))
                list.Add(player);
        }

        list.Sort((a, b) => string.Compare(GetWhitelistDisplayName(a), GetWhitelistDisplayName(b), StringComparison.OrdinalIgnoreCase));

        return list;
    }

    private static bool TryFindWhitelistTarget(ChatCommandContext ctx, string targetInput, out PlayerCacheData cache)
    {
	    cache = null;

	    if (string.IsNullOrWhiteSpace(targetInput))
		    return false;

	    targetInput = targetInput.Trim();

	    if (ulong.TryParse(targetInput, out ulong steamId))
	    {
		    cache = PlayerDataService.GetPlayerCache(steamId);
		    return true;
	    }

	    var whitelistedPlayers = GetWhitelistedPlayers();

	    var exactMatches = whitelistedPlayers
		    .Where(x => string.Equals(x.KnownAs, targetInput, StringComparison.OrdinalIgnoreCase))
		    .ToList();

	    if (exactMatches.Count == 1)
	    {
		    cache = exactMatches[0];
		    return true;
	    }

	    if (exactMatches.Count > 1)
	    {
		    ctx.Reply(
			    $"<color=yellow>Multiple whitelisted players use Known As</color> <color=white>{targetInput}</color> " +
			    $"(<color=white>{exactMatches.Count}</color> profiles). Please use SteamID instead.");

		    return false;
	    }

	    var partialMatches = whitelistedPlayers
		    .Where(x => !string.IsNullOrWhiteSpace(x.KnownAs) && x.KnownAs.IndexOf(targetInput, StringComparison.OrdinalIgnoreCase) >= 0)
		    .Select(x => x.KnownAs)
		    .Distinct(StringComparer.OrdinalIgnoreCase)
		    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
		    .ToList();

	    if (partialMatches.Count == 1)
	    {
		    string matchedKnownAs = partialMatches[0];

		    var matchedProfiles = whitelistedPlayers
			    .Where(x => string.Equals(x.KnownAs, matchedKnownAs, StringComparison.OrdinalIgnoreCase))
			    .ToList();

		    if (matchedProfiles.Count == 1)
		    {
			    cache = matchedProfiles[0];
			    return true;
		    }

		    ctx.Reply(
			    $"<color=yellow>Multiple whitelisted players use Known As</color> <color=white>{matchedKnownAs}</color> " +
			    $"(<color=white>{matchedProfiles.Count}</color> profiles). Please use SteamID instead.");

		    return false;
	    }

	    if (partialMatches.Count > 0)
	    {
		    if (partialMatches.Count > 10)
		    {
			    ctx.Reply(
				    $"<color=yellow>Multiple whitelisted Known As matched</color> <color=white>{targetInput}</color> " +
				    $"(<color=white>{partialMatches.Count}</color> results). Please narrow your search or use SteamID.");

			    return false;
		    }

		    ctx.Reply(
			    $"<color=yellow>Multiple whitelisted Known As matched</color> <color=white>{targetInput}</color>: " +
			    string.Join(", ", partialMatches));

		    return false;
	    }

	    return true;
    }

    private static bool IsWhitelistedPlayer(PlayerCacheData cache)
    {
        return cache != null &&
               cache.SteamID != 0 &&
               cache.IsWhitelisted;
    }

    private static string GetWhitelistDisplayName(PlayerCacheData cache)
    {
        if (cache == null)
            return "Unknown";

        if (!string.IsNullOrWhiteSpace(cache.KnownAs))
            return cache.KnownAs.Trim();

        if (!string.IsNullOrWhiteSpace(cache.InGameName))
            return cache.InGameName.Trim();

        return "Unknown";
    }

    // ---------------------------------------------------------------------
    // Kick
    // ---------------------------------------------------------------------
    
    public static void KickPlayer(Entity userEntity)
    {
        EntityManager entityManager = Core.Server.EntityManager;
        User user = userEntity.Read<User>();

        if (!user.IsConnected || user.PlatformId == 0) return;

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

    private static void KickIfOnline(ulong steamId)
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

    // ---------------------------------------------------------------------
    // ReplyHelp
    // ---------------------------------------------------------------------

    public static void ReplyBlacklistHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<color=yellow>Blacklist Commands:</color>");
        sb.AppendLine("<color=green>.pls addblacklist <Name/SteamID></color>");
        sb.AppendLine("<color=green>.pls removeblacklist <Name/SteamID></color>");
        sb.AppendLine("<color=green>.pls showblacklist</color>");
        sb.AppendLine("<color=green>.pls helpblacklist</color>");
        
        ctx.Reply(sb.ToString().TrimEnd());
    }

    public static void ReplyWhitelistHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();

        bool isEnabled = Plugin.onlyWhitelistEnable.Value;
        string status = isEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";
        
        sb.AppendLine("<color=yellow>Whitelist Commands:</color>");
        sb.AppendLine("<color=green>.pls addwhitelist <SteamID> <KnownAs></color>");
        sb.AppendLine("<color=green>.pls removewhitelist <SteamID/KnownAs></color>");
        sb.AppendLine("<color=green>.pls showwhitelist</color>");
        sb.AppendLine("<color=green>.pls helpwhitelist</color>");

        sb.AppendLine($"<color=yellow>Whitelist-only mode:</color> {status}");
        
        ctx.Reply(sb.ToString().TrimEnd());
    }
}