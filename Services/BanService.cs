using System;
using System.Text;
using VampireCommandFramework;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class BanService
{
    public static void AddBan(ChatCommandContext ctx, string targetInput)
    {
        PlayerCacheData cache = null;

        if (ulong.TryParse(targetInput, out ulong steamId))
        {
            cache = PlayerDataService.GetOrCreatePlayerCache(steamId);
            cache.IsBanned = true;
            PlayerDataService.SaveData();

            string identifier = string.IsNullOrWhiteSpace(cache.InGameName) ? $"{steamId}" : $"{cache.InGameName} ({steamId})";
            ctx.Reply($"<color=red>Banned player:</color> <color=white>{identifier}</color>");
            Core.Log.LogInfo($"[Banlist] Banned player: {identifier}");

            AccessControlService.KickIfOnline(steamId);
            return;
        }

        cache = PlayerDataService.GetPlayerCacheByName(targetInput);

        if (cache == null)
        {
            ctx.Reply($"<color=red>Player not found in history:</color> <color=white>{targetInput}</color>. " +
                      $"<color=yellow>To pre-ban a new player, please use their SteamID.</color>");
            return;
        }

        cache.IsBanned = true;
        PlayerDataService.SaveData();

        ctx.Reply($"<color=red>Banned player:</color> <color=white>{cache.InGameName}</color> ({cache.SteamID})");
        Core.Log.LogInfo($"[Banlist] Banned player: {cache.InGameName} ({cache.SteamID})");

        AccessControlService.KickIfOnline(cache.SteamID);
    }

    public static void RemoveBan(ChatCommandContext ctx, string targetInput)
    {
        PlayerCacheData cache = null;

        if (ulong.TryParse(targetInput, out ulong steamId))
        {
            cache = PlayerDataService.GetPlayerCache(steamId);

            if (cache == null || !cache.IsBanned)
            {
                ctx.Reply($"<color=white>{steamId}</color> <color=red>is not in the banlist.</color>");
                return;
            }

            cache.IsBanned = false;

            if (string.IsNullOrWhiteSpace(cache.InGameName) && string.IsNullOrWhiteSpace(cache.KnownAs) && !cache.IsWhitelisted && cache.LastOnlineTicks == 0)
            {
                PlayerDataService.RemovePlayerCache(steamId);
            }
            else
            {
                PlayerDataService.SaveData();
            }

            string identifier = string.IsNullOrWhiteSpace(cache.InGameName) ? $"{steamId}" : $"{cache.InGameName} ({steamId})";
            ctx.Reply($"<color=green>Unbanned player:</color> <color=white>{identifier}</color>");
            Core.Log.LogInfo($"[Banlist] Unbanned player: {identifier}");
            return;
        }

        cache = PlayerDataService.GetPlayerCacheByName(targetInput);

        if (cache == null || !cache.IsBanned)
        {
            ctx.Reply($"<color=white>{targetInput}</color> <color=red>is not in the banlist.</color>");
            return;
        }

        cache.IsBanned = false;
        PlayerDataService.SaveData();

        ctx.Reply($"<color=green>Unbanned player:</color> <color=white>{cache.InGameName}</color> ({cache.SteamID})");
        Core.Log.LogInfo($"[Banlist] Unbanned player: {cache.InGameName} ({cache.SteamID})");
    }

    public static void ShowBanlist(ChatCommandContext ctx)
    {
        var bannedPlayers = PlayerDataService.GetBannedPlayers();

        if (bannedPlayers.Count == 0)
        {
            ctx.Reply("<color=yellow>The banlist is empty.</color>");
            return;
        }

        int batchSize = 8;
        int totalPages = (int)Math.Ceiling((double)bannedPlayers.Count / batchSize);

        for (int i = 0; i < bannedPlayers.Count; i += batchSize)
        {
            var sb = new StringBuilder();

            int currentPage = (i / batchSize) + 1;
            sb.AppendLine($"<color=red>Banned Players</color> ({currentPage}/{totalPages})");

            var batch = bannedPlayers.GetRange(i, Math.Min(batchSize, bannedPlayers.Count - i));

            foreach (var player in batch)
            {
                string nameDisplay = string.IsNullOrWhiteSpace(player.InGameName) ? "Unknown" : player.InGameName;
                sb.AppendLine($"{player.SteamID} <color=white>{nameDisplay}</color>");
            }

            ctx.Reply(sb.ToString().TrimEnd());
        }
    }

    public static bool IsBannedPlayer(PlayerCacheData cache)
    {
        return cache != null && cache.IsBanned;
    }

    public static void ReplyBanHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<color=yellow>Ban Commands:</color>");
        sb.AppendLine("<color=green>.pls ban <Name/SteamID></color>");
        sb.AppendLine("<color=green>.pls unban <Name/SteamID></color>");
        sb.AppendLine("<color=green>.pls banlist</color>");
        sb.AppendLine("<color=green>.pls banhelp</color>");

        ctx.Reply(sb.ToString().TrimEnd());
    }
}
