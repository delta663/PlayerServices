using PlayerServices.Services;
using VampireCommandFramework;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class TeleportPointsCommands
{
    [Command("tp", description: "Teleport to a saved teleport point.", adminOnly: false)]
    public static void TeleportCommand(ChatCommandContext ctx, string slot = "")
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            TeleportPointsService.ReplyHelp(ctx);
            return;
        }

        TeleportPointsService.Teleport(ctx, slot);
    }
    
    [Command("helptp", shortHand: "htp", description: "Show teleport point commands and feature status.", adminOnly: false)]
    public static void HelpTeleportCommand(ChatCommandContext ctx)
    {
        TeleportPointsService.ReplyHelp(ctx);
    }


    [Command("addtp", shortHand: "atp", description: "Add a teleport point at your current position.", adminOnly: true)]
    public static void AddTeleportCommand(ChatCommandContext ctx, string slot = "", string adminOnly = "true", string description = "")
    {
        if (string.IsNullOrWhiteSpace(slot) || string.IsNullOrWhiteSpace(adminOnly))
        {
            TeleportPointsService.ReplyHelp(ctx);
            return;
        }
        
        TeleportPointsService.Add(ctx, slot, adminOnly, description);
    }

    [Command("removetp", shortHand: "rtp", description: "Remove a teleport point.", adminOnly: true)]
    public static void RemoveTeleportCommand(ChatCommandContext ctx, string slot = "")
    {
        if (string.IsNullOrWhiteSpace(slot))
        {
            TeleportPointsService.ReplyHelp(ctx);
            return;
        }
        
        TeleportPointsService.Remove(ctx, slot);
    }

    [Command("listtp", shortHand: "ltp", description: "List all teleport points.", adminOnly: true)]
    public static void ListTeleportCommand(ChatCommandContext ctx)
    {
        TeleportPointsService.List(ctx);
    }
}
