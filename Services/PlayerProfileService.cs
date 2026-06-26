using System.Collections.Generic;
using System.Text;
using VampireCommandFramework;
using PlayerServices.Data;
using System.Linq;

namespace PlayerServices.Services;

internal static class PlayerProfileService
{
    public static void CheckByPlayerName(ChatCommandContext ctx, string targetInput)
    {
        if (!TryGetPlayerCache(ctx, targetInput, out var cache)) return;

        string knownAsDisplay = string.IsNullOrWhiteSpace(cache.KnownAs) ? "<color=yellow>Not Set</color>" : $"<color=green>{cache.KnownAs}</color>";
        string inGameNameDisplay = string.IsNullOrWhiteSpace(cache.InGameName) ? "<color=yellow>Unknown</color>" : $"<color=white>{cache.InGameName}</color>";
        string blacklistStatus = cache.IsBlacklisted? "<color=red>Yes</color>" : "<color=green>No</color>";
        string whitelistStatus = cache.IsWhitelisted? "<color=green>Yes</color>" : "<color=red>No</color>";

        var sb = new StringBuilder();
        sb.AppendLine($"<color=yellow>Player Profile:</color> {inGameNameDisplay}");
        sb.AppendLine($"SteamID: {cache.SteamID}");
        sb.AppendLine($"In-Game Name: {inGameNameDisplay}");
        sb.AppendLine($"Known As: {knownAsDisplay}");
        sb.AppendLine($"Blacklisted: {blacklistStatus}");
        sb.AppendLine($"Whitelisted: {whitelistStatus}");

        ctx.Reply(sb.ToString().TrimEnd());
    }

    public static void CheckByKnownAs(ChatCommandContext ctx, string knownAsQuery)
    {
        if (string.IsNullOrWhiteSpace(knownAsQuery) || knownAsQuery.Trim().Length < 2)
        {
            ctx.Reply("<color=yellow>Please enter at least 2 characters.</color>");
            return;
        }
        
        knownAsQuery = knownAsQuery.Trim();
        
        if (!PlayerDataService.TryFindPlayersCacheByKnownAs(knownAsQuery, out var matches, out var candidates))
        {
            if (candidates != null && candidates.Count > 0)
            {
                if (candidates.Count > 10)
                {
                    ctx.Reply($"<color=yellow>Too many Known As found for</color> <color=white>{knownAsQuery}</color>. Please enter a more specific name.");
                }
                else
                {
                    ctx.Reply($"<color=yellow>Multiple Known As matched</color> <color=white>{knownAsQuery}</color>: {string.Join(", ", candidates)}");
                }
            }
            else
            {
                ctx.Reply($"<color=red>No players found with Known As matching:</color> <color=white>{knownAsQuery}</color>");
            }
            return;
        }

        string exactKnownAs = matches[0].KnownAs;

        var sb = new StringBuilder();
        string profileLabel = matches.Count == 1 ? "Player Profile" : "Player Profiles";
        sb.AppendLine($"<color=yellow>{profileLabel}</color> ({matches.Count}) for Known As: <color=green>{exactKnownAs}</color>");
        
        int index = 1;

        foreach (var match in matches)
        {
            string inGameNameDisplay = string.IsNullOrWhiteSpace(match.InGameName)
                ? "<color=yellow>Unknown</color>"
                : $"<color=white>{match.InGameName}</color>";

            string statusText = "";

            if (match.IsBlacklisted)
                statusText += " | <color=red>Blacklisted</color>";

            if (match.IsWhitelisted)
                statusText += " | <color=green>Whitelisted</color>";

            sb.AppendLine(
                $"<color=yellow>[{index}]</color> " +
                $"SteamID: <color=white>{match.SteamID}</color> | " +
                $"In-Game Name: {inGameNameDisplay}{statusText}");

            index++;
        }

        ctx.Reply(sb.ToString().TrimEnd());
    }

    public static void SetKnownAs(ChatCommandContext ctx, string targetInput, string knownAs)
    {
        if (!TryGetPlayerCache(ctx, targetInput, out var cache, true)) return;

        cache.KnownAs = knownAs.Trim();
        PlayerDataService.SaveData();

        string inGameNameDisplay = string.IsNullOrWhiteSpace(cache.InGameName) ? $"SteamID: {cache.SteamID}" : cache.InGameName;
        ctx.Reply($"Set Known As for <color=white>{inGameNameDisplay}</color> to <color=yellow>{cache.KnownAs}</color>."); 
        Core.Log.LogInfo($"[PlayerProfile] Set Known As for {inGameNameDisplay} to {cache.KnownAs} ({cache.SteamID})");
    }

    public static void RemoveKnownAs(ChatCommandContext ctx, string targetInput)
    {
        if (!TryGetPlayerCache(ctx, targetInput, out var cache)) return;

        cache.KnownAs = string.Empty;

        if (string.IsNullOrWhiteSpace(cache.InGameName) && !cache.IsBlacklisted && !cache.IsWhitelisted && cache.LastOnlineTicks == 0)
        {
	        PlayerDataService.RemovePlayerCache(cache.SteamID);
        }
        else
        {
	        PlayerDataService.SaveData();
        }

        string inGameNameDisplay = string.IsNullOrWhiteSpace(cache.InGameName) ? $"SteamID: {cache.SteamID}" : cache.InGameName;
        ctx.Reply($"Removed Known As for <color=white>{inGameNameDisplay}</color>.");
        Core.Log.LogInfo($"[PlayerProfile] Removed Known As for {inGameNameDisplay} ({cache.SteamID})");
    }

    private static bool TryGetPlayerCache(ChatCommandContext ctx, string query, out PlayerCacheData cache, bool createIfMissing = false)
    {
        cache = null;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            ctx.Reply("<color=yellow>Please enter at least 2 characters.</color>");
            return false;
        }
        
        query = query.Trim();

        if (ulong.TryParse(query, out ulong steamId))
        {
            cache = createIfMissing ? PlayerDataService.GetOrCreatePlayerCache(steamId) : PlayerDataService.GetPlayerCache(steamId);
            
            if (cache == null)
            {
                ctx.Reply($"<color=red>Player not found in history:</color> <color=white>{query}</color>");
                return false;
            }
            return true;
        }
        else
        {
            if (PlayerDataService.TryFindPlayerCacheByPlayerName(query, out cache, out List<string> candidates))
            {
                return true;
            }

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
            }
            else
            {
                ctx.Reply($"<color=red>Player not found in history:</color> <color=white>{query}</color>");
            }
            return false;
        }
    }

    public static void ReplyHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<color=yellow>Player Profile Commands:</color>");
        sb.AppendLine("<color=green>.pls checkplayer <PlayerName/SteamID></color>");
        sb.AppendLine("<color=green>.pls checkknownas <KnownAs></color>");
        sb.AppendLine("<color=green>.pls addknownas <Name/SteamID> <KnownAs></color>");
        sb.AppendLine("<color=green>.pls removeknownas <Name/SteamID></color>");
        
        ctx.Reply(sb.ToString().TrimEnd());
    }
}