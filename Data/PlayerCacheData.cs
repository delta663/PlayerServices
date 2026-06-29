using System.Collections.Generic;

namespace PlayerServices.Data;

internal class PlayerCacheData
{
    public ulong SteamID { get; set; }
    public string InGameName { get; set; } = string.Empty;
    public string KnownAs { get; set; } = string.Empty;
    public int CurrentLevel { get; set; }
    public int MaxLevel { get; set; }
    public Dictionary<string, int> CastleRegions { get; set; } = new();
    public long LastOnlineTicks { get; set; } = 0;
    public string LastDailyKitClaim { get; set; } = string.Empty;
    public bool HasReceivedStarterKit { get; set; } = false;
    public bool IsBanned { get; set; } = false;
    public bool IsWhitelisted { get; set; } = false;
    public List<PlayerAuraData> Auras { get; set; } = new();
}

internal class PlayerAuraData
{
    public int PrefabGuid { get; set; }
    public bool Active { get; set; } = true;
}
