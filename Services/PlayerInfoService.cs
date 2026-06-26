using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProjectM.Network;
using Unity.Entities;
using ProjectM;
using PlayerServices.Data;
using VampireCommandFramework;

namespace PlayerServices.Services;

internal static class PlayerInfoService
{
    private const int MEMBERS_PER_REPLY = 4;

    public static bool TryGetPlayerInfo(ChatCommandContext ctx, string player)
    {
        if (!Plugin.pisCommandEnabled.Value)
        {
            ctx.Reply("<color=red>This command is currently disabled.</color>");
            return false;
        }

        if (string.IsNullOrWhiteSpace(player) || player.Trim().Length < 2)
        {
            ctx.Reply("<color=yellow>Please enter at least 2 characters.</color>");
            return false;
        }

        string query = player.Trim();

        if (!Helper.TryFindUserByName(query, out Entity userEntity, out User user, out List<string> candidates))
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

        BuildPlayerInfo(ctx, userEntity, user);
        return true;
    }

    private static void BuildPlayerInfo(ChatCommandContext ctx, Entity userEntity, User user)
    {
	    var clanEntity = user.ClanEntity.GetEntityOnServer();
	    var targetName = Helper.TrimName(user.CharacterName.ToString());
	    var targetCache = PlayerDataService.GetPlayerCache(user.PlatformId);
    	var targetMaxLevel = targetCache != null ? targetCache.MaxLevel : 0;
	    var targetCurrentLevel = targetCache != null ? targetCache.CurrentLevel : 0;
	    var targetCastleRegions = targetCache?.CastleRegions;
	    int targetCastleCount = targetCastleRegions?.Values.Sum() ?? 0;

	    bool isAdmin = ctx.Event.User.IsAdmin;
	    bool showClanCastleInfo = Plugin.playerInfoShowClanCastleInfo.Value || isAdmin;
	    bool showClanMemberLastOnline = Plugin.playerInfoShowClanMemberLastOnline.Value || isAdmin;

	    if (clanEntity.Equals(Entity.Null))
	    {
		    HandleSoloPlayer(ctx, user, targetName, targetMaxLevel, targetCurrentLevel, targetCastleCount, targetCastleRegions, showClanCastleInfo, showClanMemberLastOnline);
	    }
	    else
	    {
		    HandleClanPlayer(ctx, user, clanEntity, targetName, targetMaxLevel, targetCurrentLevel, targetCastleCount, showClanCastleInfo, showClanMemberLastOnline);
	    }
    }

    private static void HandleSoloPlayer(
	    ChatCommandContext ctx,
	    User user,
	    string targetName,
	    int maxLevel,
	    int currentLevel,
	    int castleCount,
	    Dictionary<string, int> castleRegions,
	    bool showClanCastleInfo,
	    bool showClanMemberLastOnline)
    {
	    string soloHeader = $"<color=white>{targetName}</color> - <color=#87CEFA>Solo Player</color>";
	    var sb = new StringBuilder();

	    sb.AppendLine(soloHeader);

	    string levelLine = $"Max Level: {maxLevel} | Current Level: {currentLevel}";

	    if (showClanCastleInfo)
		    levelLine += $" | Castles Owned: {castleCount}";

	    sb.AppendLine(levelLine);

	    if (showClanCastleInfo)
		    sb.AppendLine(FormatRegionCounts(castleRegions, castleCount, isClan: false));

	    sb.AppendLine(FormatUserLine(user, showClanMemberLastOnline));

	    ctx.Reply(sb.ToString());
    }

    private static void HandleClanPlayer(
	    ChatCommandContext ctx,
	    User user,
	    Entity clanEntity,
	    string targetName,
	    int maxLevel,
	    int currentLevel,
	    int castleCount,
	    bool showClanCastleInfo,
	    bool showClanMemberLastOnline)
    {
	    var clanName = clanEntity.Read<ClanTeam>().Name.ToString();
	    var userBuffer = Core.EntityManager.GetBuffer<SyncToUserBuffer>(clanEntity);
	    var members = GetSortedClanMembers(userBuffer);

	    Dictionary<string, int> clanCounts = null;
	    int totalClanCastles = 0;

	    if (showClanCastleInfo)
	    {
		    clanCounts = CalculateClanCastleCounts(members);
		    totalClanCastles = clanCounts.Values.Sum();
	    }

	    string clanHeader = $"<color=white>{targetName}</color> - Member of Clan <color=#87CEFA>{clanName}</color>";

	    var sb = new StringBuilder();
	    sb.AppendLine(clanHeader);

	    string levelLine = $"Max Level: {maxLevel} | Current Level: {currentLevel}";

	    if (showClanCastleInfo)
		    levelLine += $" | Castles Owned: {castleCount}";

	    sb.AppendLine(levelLine);

	    if (showClanCastleInfo)
		    sb.AppendLine(FormatRegionCounts(clanCounts, totalClanCastles, isClan: true));

	    sb.AppendLine($"Clan Members ({members.Count}):");

	    foreach (var m in members.Take(MEMBERS_PER_REPLY))
	    {
		    sb.AppendLine(FormatUserLine(m.usr, showClanMemberLastOnline));
	    }

	    ctx.Reply(sb.ToString());

	    if (members.Count > MEMBERS_PER_REPLY)
	    {
		    SendRemainingMembers(ctx, members.Skip(MEMBERS_PER_REPLY).ToList(), clanName, showClanMemberLastOnline);
	    }
    }

    private static void SendRemainingMembers(
	    ChatCommandContext ctx,
	    List<(Entity userEnt, User usr)> remainingMembers,
	    string clanName,
	    bool showClanMemberLastOnline)
    {
	    var sb = new StringBuilder();

	    for (int i = 0; i < remainingMembers.Count; i++)
	    {
		    if (i % MEMBERS_PER_REPLY == 0)
			    sb.AppendLine($"More Members of Clan <color=#87CEFA>{clanName}</color>");

		    sb.AppendLine(FormatUserLine(remainingMembers[i].usr, showClanMemberLastOnline));

		    if ((i + 1) % MEMBERS_PER_REPLY == 0 || i == remainingMembers.Count - 1)
		    {
			    ctx.Reply(sb.ToString());
			    sb.Clear();
		    }
	    }
    }

    private static string FormatUserLine(User usr, bool showClanMemberLastOnline)
    {
	    var trimmedName = Helper.TrimName(usr.CharacterName.ToString());
	    var nameColored = usr.IsConnected
		    ? $"<color=green>{trimmedName}</color>"
		    : $"<color=red>{trimmedName}</color>";

	    var cache = PlayerDataService.GetPlayerCache(usr.PlatformId);
	    var maxLevel = cache != null ? cache.MaxLevel : 0;

	    string lastOnline = "";

	    if (!usr.IsConnected && showClanMemberLastOnline)
	    {
		    TimeSpan lastSeen = PlayerDataService.GetTimeSinceLastOnline(cache?.LastOnlineTicks ?? 0, usr.TimeLastConnected);
		    lastOnline = $" ({FormatTimeAgo(lastSeen)})";
	    }

	    return $"[<color=yellow>{maxLevel}</color>] {nameColored}{lastOnline}";
    }

    private static string FormatTimeAgo(TimeSpan span)
    {
        var parts = new List<string>();
        if (span.Days > 0) parts.Add($"{span.Days}d");
        if (span.Hours > 0) parts.Add($"{span.Hours}h");
        if (span.Minutes > 0) parts.Add($"{span.Minutes}m");
        return parts.Count == 0 ? "just now" : string.Join(" ", parts) + " ago";
    }

    private static string FormatRegionCounts(Dictionary<string, int> counts, int totalCount, bool isClan)
    {
        string castleText = totalCount == 1 ? "Castle" : "Castles";
        string clanText = isClan ? "Clan" : "Player";
        string prefix = $"{clanText} {castleText} ({totalCount}):";

        if (counts == null || counts.Count == 0 || totalCount == 0) 
            return $"{prefix} (None)";
        
        var parts = counts.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                          .Select(kv => 
                          {
                              string displayName = CastleTerritoryData.GetShortName(kv.Key);
                              return $"{displayName} ({kv.Value})";
                          });
                          
        return $"{prefix} {string.Join(", ", parts)}";
    }

    private static List<(Entity userEnt, User usr)> GetSortedClanMembers(DynamicBuffer<SyncToUserBuffer> userBuffer)
    {
        var members = new List<(Entity userEnt, User usr)>();
        for (var i = 0; i < userBuffer.Length; ++i)
        {
            var ent = userBuffer[i].UserEntity;
            members.Add((ent, ent.Read<User>()));
        }

        return members.OrderByDescending(m => m.usr.IsConnected)
                      .ThenByDescending(m => m.usr.TimeLastConnected)
                      .ToList();
    }

    private static Dictionary<string, int> CalculateClanCastleCounts(List<(Entity userEnt, User usr)> members)
    {
        var clanCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in members)
        {
            var cache = PlayerDataService.GetPlayerCache(m.usr.PlatformId);
            if (cache != null && cache.CastleRegions != null)
            {
                foreach (var kv in cache.CastleRegions)
                {
                    clanCounts.TryGetValue(kv.Key, out var c);
                    clanCounts[kv.Key] = c + kv.Value;
                }
            }
        }
        return clanCounts;
    }
}
