using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class BanCommands
{
    [Command("ban", shortHand: "b", description: "Ban a player by Name or SteamID and kick them.", adminOnly: true)]
    public static void BanCommand(ChatCommandContext ctx, string nameOrSteamId = "")
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            BanService.ReplyBanHelp(ctx);
            return;
        }
        
        BanService.AddBan(ctx, nameOrSteamId);
    }

    [Command("unban", shortHand: "ub", description: "Unban a player by Name or SteamID.", adminOnly: true)]
    public static void UnbanCommand(ChatCommandContext ctx, string nameOrSteamId = "")
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            BanService.ReplyBanHelp(ctx);
            return;
        }
        
        BanService.RemoveBan(ctx, nameOrSteamId);
    }

    [Command("banlist", shortHand: "bl", description: "Show all banned players.", adminOnly: true)]
    public static void ShowBanlistCommand(ChatCommandContext ctx)
    {
        BanService.ShowBanlist(ctx);
    }

    [Command("banhelp", shortHand: "bh", description: "Show ban commands.", adminOnly: true)]
    public static void HelpBanCommand(ChatCommandContext ctx)
    {
        BanService.ReplyBanHelp(ctx);
    }
}
