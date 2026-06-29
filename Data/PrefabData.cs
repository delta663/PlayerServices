using Stunlock.Core;
using System.Collections.Generic;

namespace PlayerServices.Data;

internal static class PrefabData
{
    public static readonly PrefabGUID TeleportWaiting = new PrefabGUID(-2061047741);
    public static readonly PrefabGUID Downed = new PrefabGUID(-1992158531);
	public static readonly PrefabGUID Spiderform = new PrefabGUID(124832551);
    public static readonly PrefabGUID Golemform = new PrefabGUID(914043867);
    public static readonly PrefabGUID Batform = new PrefabGUID(1205505492);
    public static readonly PrefabGUID Dominate = new PrefabGUID(-1447419822);

    public static readonly PrefabGUID Observe = new PrefabGUID(1880224358);

    public static readonly PrefabGUID TombCoffinSpawn = new PrefabGUID(722466953);

    public static readonly PrefabGUID SpellPowerPotion = new PrefabGUID(-1591827622);
    public static readonly PrefabGUID PhysicalPowerPotion = new PrefabGUID(-1591883586);
    public static readonly PrefabGUID HolyResistancePotion = new PrefabGUID(2099221856);
    public static readonly PrefabGUID FireResistancePotion = new PrefabGUID(-706770454);
    public static readonly PrefabGUID WranglerPotion = new PrefabGUID(387154469);
    public static readonly PrefabGUID SunResistancePotion = new PrefabGUID(112008974);

    public static readonly PrefabGUID MapIconPlayer = new PrefabGUID(-892362184);

    public static readonly PrefabGUID InCombatPvE = new PrefabGUID(581443919);
    public static readonly PrefabGUID InCombatPvP = new PrefabGUID(697095869);
    public static readonly PrefabGUID InCombatContest = new PrefabGUID(698151145);

    public static readonly HashSet<PrefabGUID> CombatBuffs = new()
    {
        InCombatPvE,
        InCombatPvP,
        InCombatContest
    };

    public static readonly HashSet<PrefabGUID> PotionBuff = new()
    {
        SpellPowerPotion,
        PhysicalPowerPotion,
        HolyResistancePotion,
        FireResistancePotion,
        WranglerPotion,
        SunResistancePotion
    };
}
