using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class PlayerProfileCommands
{
    [Command("checkplayer", shortHand: "cp", description: "Check a player profile by In-Game Name or SteamID.", adminOnly: true)]
    public static void CheckByPlayerNameCommand(ChatCommandContext ctx, string nameOrSteamId = "")
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            PlayerProfileService.ReplyHelp(ctx);
            return;
        }

        PlayerProfileService.CheckByPlayerName(ctx, nameOrSteamId);
    }

    [Command("checkknownas", shortHand: "cka", description: "Find player profiles by Known As.", adminOnly: true)]
    public static void CheckByKnownAsCommand(ChatCommandContext ctx, string knownAs = "")
    {
        if (string.IsNullOrWhiteSpace(knownAs))
        {
            PlayerProfileService.ReplyHelp(ctx);
            return;
        }

        PlayerProfileService.CheckByKnownAs(ctx, knownAs);
    }

    [Command("addknownas", shortHand: "aka", description: "Set a player's Known As.", adminOnly: true)]
    public static void SetKnownAsCommand(ChatCommandContext ctx, string nameOrSteamId = "", string knownAs = "")
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId) || string.IsNullOrWhiteSpace(knownAs))
        {
            PlayerProfileService.ReplyHelp(ctx);
            return;
        }
        PlayerProfileService.SetKnownAs(ctx, nameOrSteamId, knownAs);
    }

    [Command("removeknownas", shortHand: "rka", description: "Remove Known As from a player profile.", adminOnly: true)]
    public static void RemoveKnownAsCommand(ChatCommandContext ctx, string nameOrSteamId = "")
    {
        if (string.IsNullOrWhiteSpace(nameOrSteamId))
        {
            PlayerProfileService.ReplyHelp(ctx);
            return;
        }

        PlayerProfileService.RemoveKnownAs(ctx, nameOrSteamId);
    }
}
