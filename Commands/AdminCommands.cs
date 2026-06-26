using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class AdminCommands
{
    [Command("observe", shortHand: "ob", description: "Toggle admin observe mode.", adminOnly: true)]
    public static void ObserveCommand(ChatCommandContext ctx)
    {
        AdminService.ToggleObserve(ctx);
    }

    [Command("track", shortHand: "tr", description: "Continuously track a player while in observe mode.", adminOnly: true)]
    public static void TrackCommand(ChatCommandContext ctx, string playerName = "")
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            ctx.Reply("Usage: <color=green>.pls track <playerName></color>");
            return;
        }

        AdminService.TrackPlayer(ctx, playerName);
    }

    [Command("untrack", shortHand: "utr", description: "Stop tracking the current player.", adminOnly: true)]
    public static void UntrackCommand(ChatCommandContext ctx)
    {
        AdminService.UntrackPlayer(ctx);
    }

    [Command("buff", shortHand: "bf", description: "Apply Potion Buffs to a target player or yourself.", adminOnly: true)]
    public static void PotionBuffCommand(ChatCommandContext ctx, string playerName = "")
    {
        AdminService.ApplyPotionBuffs(ctx, playerName);
    }

    [Command("reload", shortHand: "rl", description: "Reload all PlayerServices configs.", adminOnly: true)]
	public static void ReloadCommand(ChatCommandContext ctx)
	{
		AdminService.ReloadAll(ctx);
	}
}

