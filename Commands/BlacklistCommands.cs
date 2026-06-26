using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class BlacklistCommands
{
    [Command("addblacklist", shortHand: "abl", description: "Add a player to the blacklist and kick them.", adminOnly: true)]
    public static void AddBlacklistCommand(ChatCommandContext ctx, string nameOrSteamId)
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            BlacklistService.ReplyBlacklistHelp(ctx);
            return;
        }
        
        BlacklistService.AddBlacklist(ctx, nameOrSteamId);
    }

    [Command("removeblacklist", shortHand: "rbl", description: "Remove a player from the blacklist.", adminOnly: true)]
    public static void RemoveblacklistCommand(ChatCommandContext ctx, string nameOrSteamId)
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            BlacklistService.ReplyBlacklistHelp(ctx);
            return;
        }
        
        BlacklistService.RemoveBlacklist(ctx, nameOrSteamId);
    }

    [Command("showblacklist", shortHand: "sbl", description: "Show all blacklisted players.", adminOnly: true)]
    public static void ShowBlacklistCommand(ChatCommandContext ctx)
    {
        BlacklistService.ShowBlacklist(ctx);
    }

    [Command("helpblacklist", shortHand: "hbl", description: "Show blacklist commands.", adminOnly: true)]
    public static void HelpBlacklistCommand(ChatCommandContext ctx)
    {
        BlacklistService.ReplyBlacklistHelp(ctx);
    }

    [Command("addwhitelist", shortHand: "awl", description: "Pre-register a SteamID and Known As to the whitelist.", adminOnly: true)]
    public static void AddWhitelistCommand(ChatCommandContext ctx, string steamIdInput, string knownAs)
    {
	    if (string.IsNullOrWhiteSpace(steamIdInput) || string.IsNullOrWhiteSpace(knownAs))
	    {
		    BlacklistService.ReplyWhitelistHelp(ctx);
		    return;
	    }

	    BlacklistService.AddWhitelist(ctx, steamIdInput, knownAs);
    }

    [Command("removewhitelist", shortHand: "rwl", description: "Remove a player from the whitelist.", adminOnly: true)]
    public static void RemoveWhitelistCommand(ChatCommandContext ctx, string steamIdOrKnownAs)
    {
        if (string.IsNullOrWhiteSpace(steamIdOrKnownAs))
        {
            BlacklistService.ReplyWhitelistHelp(ctx);
            return;
        }

	    BlacklistService.RemoveWhitelist(ctx, steamIdOrKnownAs);
    }

    [Command("showwhitelist", shortHand: "swl", description: "Show all whitelisted players.", adminOnly: true)]
    public static void ShowWhitelistCommand(ChatCommandContext ctx)
    {
        BlacklistService.ShowWhitelist(ctx);
    }

    [Command("helpwhitelist", shortHand: "hwl", description: "Show whitelist commands and feature status.", adminOnly: true)]
    public static void HelpWhitelistCommand(ChatCommandContext ctx)
    {
        BlacklistService.ReplyWhitelistHelp(ctx);
    }
}