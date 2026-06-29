using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

internal static class PlayerInfoCommands
{
    [Command("pis", description: "Show player information.", adminOnly: false)]
    public static void Pis(ChatCommandContext ctx, string player = null)
    {
        PlayerInfoService.TryGetPlayerInfo(ctx, player ?? "");
    }
}
