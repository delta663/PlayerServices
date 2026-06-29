using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VampireCommandFramework;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class WhitelistService
{
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

        ctx.Reply($"<color=green>Added player to the whitelist:</color> <color=white>{cache.KnownAs}</color> ({steamId}).");
        Core.Log.LogInfo($"[Whitelist] Added player to the whitelist: {cache.KnownAs} ({steamId})");
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
            AccessControlService.KickIfOnline(cache.SteamID);
        }

        ctx.Reply($"<color=green>Removed player from the whitelist:</color> <color=white>{displayName}</color> ({cache.SteamID}).");
        Core.Log.LogInfo($"[Whitelist] Removed player from the whitelist: {displayName} ({cache.SteamID})");
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

    public static bool IsWhitelistedPlayer(PlayerCacheData cache)
    {
        return cache != null &&
               cache.SteamID != 0 &&
               cache.IsWhitelisted;
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
}
