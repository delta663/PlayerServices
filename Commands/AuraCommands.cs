using PlayerServices.Services;
using VampireCommandFramework;

namespace PlayerServices.Commands;

[CommandGroup("aura")]
internal static class AuraCommands
{
    [Command("on", description: "Turn an aura on.", adminOnly: false)]
    public static void AuraOnCommand(ChatCommandContext ctx, string id = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }

        AuraService.SetAuraActive(ctx, id, true);
    }

    [Command("off", description: "Turn an aura off.", adminOnly: false)]
    public static void AuraOffCommand(ChatCommandContext ctx, string id = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }
        
        AuraService.SetAuraActive(ctx, id, false);
    }

    [Command("preview", description: "Preview an aura for a short time before buying it.", adminOnly: false)]
	public static void AuraPreviewCommand(ChatCommandContext ctx, string id = "")
	{
        if (string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }
        
		AuraService.PreviewAura(ctx, id);
	}
        
    [Command("buy", description: "Buy an aura.", adminOnly: false)]
    public static void AuraBuyCommand(ChatCommandContext ctx, string id = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }

        AuraService.BuyAura(ctx, id);
    }
        
    [Command("list", description: "Show available auras, prices, and ownership status.", adminOnly: false)]
    public static void AuraListCommand(ChatCommandContext ctx)
    {
        AuraService.ListAuras(ctx);
    }
    
    [Command("help", description: "Show aura commands and feature status.", adminOnly: false)]
    public static void AuraHelpCommand(ChatCommandContext ctx)
    {
        AuraService.ReplyHelp(ctx);
    }
    

    [Command("add", description: "Grant an aura to a player.", adminOnly: true)]
    public static void AuraAddCommand(ChatCommandContext ctx, string playerName = "", string id = "")
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }
        
        AuraService.AdminAddAura(ctx, playerName, id);
    }

    [Command("remove", description: "Remove an aura from a player.", adminOnly: true)]
    public static void AuraRemoveCommand(ChatCommandContext ctx, string playerName = "", string id = "")
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }

        AuraService.AdminRemoveAura(ctx, playerName, id);
    }
}

[CommandGroup("buy")]
internal static class AuraBuyCommands
{
    [Command("aura", description: "Buy an aura.", adminOnly: false)]
    public static void BuyAuraCommand(ChatCommandContext ctx, string id = "")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            AuraService.ReplyHelp(ctx);
            return;
        }
        
        AuraService.BuyAura(ctx, id);
    }
}
