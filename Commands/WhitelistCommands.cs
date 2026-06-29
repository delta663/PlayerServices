using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class WhitelistCommands
{
    [Command("addwhitelist", shortHand: "awl", description: "Pre-register a SteamID and Known As to the whitelist.", adminOnly: true)]
    public static void AddWhitelistCommand(ChatCommandContext ctx, string steamIdInput = "", string knownAs = "")
    {
        if (string.IsNullOrWhiteSpace(steamIdInput) || string.IsNullOrWhiteSpace(knownAs))
        {
            WhitelistService.ReplyWhitelistHelp(ctx);
            return;
        }

        WhitelistService.AddWhitelist(ctx, steamIdInput, knownAs);
    }

    [Command("removewhitelist", shortHand: "rwl", description: "Remove a player from the whitelist by SteamID or Known As.", adminOnly: true)]
    public static void RemoveWhitelistCommand(ChatCommandContext ctx, string steamIdOrKnownAs = "")
    {
        if (string.IsNullOrWhiteSpace(steamIdOrKnownAs))
        {
            WhitelistService.ReplyWhitelistHelp(ctx);
            return;
        }

        WhitelistService.RemoveWhitelist(ctx, steamIdOrKnownAs);
    }

    [Command("showwhitelist", shortHand: "swl", description: "Show all whitelisted players.", adminOnly: true)]
    public static void ShowWhitelistCommand(ChatCommandContext ctx)
    {
        WhitelistService.ShowWhitelist(ctx);
    }

    [Command("helpwhitelist", shortHand: "hwl", description: "Show whitelist commands and feature status.", adminOnly: true)]
    public static void HelpWhitelistCommand(ChatCommandContext ctx)
    {
        WhitelistService.ReplyWhitelistHelp(ctx);
    }
}
