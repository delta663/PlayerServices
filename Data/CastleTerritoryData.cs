using System;
using System.Collections.Generic;

namespace PlayerServices.Data;

internal static class CastleTerritoryData
{
    private class RegionDisplayInfo
    {
        public string FullName { get; }
        public string ShortName { get; }

        public RegionDisplayInfo(string fullName, string shortName)
        {
            FullName = fullName;
            ShortName = shortName;
        }
    }

    private static readonly Dictionary<string, RegionDisplayInfo> RegionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Strongblade", new RegionDisplayInfo("Oakveil Woodlands", "Oakveil") },
        { "Gloomrot_North", new RegionDisplayInfo("Gloomrot North", "Gloomrot N") },
        { "Gloomrot_South", new RegionDisplayInfo("Gloomrot South", "Gloomrot S") },
        { "CursedForest", new RegionDisplayInfo("Cursed Forest", "Forest") },
        { "DunleyFarmlands", new RegionDisplayInfo("Dunley Farmlands", "Dunley") },
        { "HallowedMountains", new RegionDisplayInfo("Hallowed Mountains", "Hallowed") },
        { "FarbaneWoods", new RegionDisplayInfo("Farbane Woods", "Farbane") },
        { "SilverlightHills", new RegionDisplayInfo("Silverlight Hills", "Silverlight") }
    };

    public static string GetFullName(string rawName)
    {
        if (RegionMap.TryGetValue(rawName, out var info))
        {
            return info.FullName;
        }
        
        return $"Unknown ({rawName})"; 
    }

    public static string GetShortName(string rawName)
    {
        if (RegionMap.TryGetValue(rawName, out var info))
        {
            return info.ShortName;
        }
        
        return $"Unknown ({rawName})"; 
    }
}
