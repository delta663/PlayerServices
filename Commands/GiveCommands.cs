using System.Globalization;
using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class GiveCommands
{
    [Command("give", shortHand: "g", description: "Give an item set to a specific player.", adminOnly: true)]
    public static void GivePlayerCommand(ChatCommandContext ctx, string playerName = "", string setName = "", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(setName))
        {
            GiveService.ReplyHelp(ctx);
            return;
        }

        if (quantity <= 0)
        {
            ctx.Reply("<color=red>Quantity must be greater than 0.</color>");
            return;
        }

        GiveService.GiveToPlayer(ctx, playerName, setName, quantity);
    }

    [Command("giveradius", shortHand: "gr", description: "Give an item set to players within a radius.", adminOnly: true)]
    public static void GiveRadiusCommand(ChatCommandContext ctx, string radiusToken = "", string setName = "", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(radiusToken) || string.IsNullOrWhiteSpace(setName))
        {
            GiveService.ReplyHelp(ctx);
            return;
        }

        if (!float.TryParse(radiusToken, NumberStyles.Float, CultureInfo.InvariantCulture, out float radius))
        {
            ctx.Reply("<color=red>Invalid radius. It must be a number.</color>");
            return;
        }

        if (radius <= 0f)
        {
            ctx.Reply("<color=red>Radius must be greater than 0.</color>");
            return;
        }

        if (radius > 50f)
        {
            ctx.Reply("<color=red>Radius must be less than or equal to 50.</color>");
            return;
        }

        if (quantity <= 0)
        {
            ctx.Reply("<color=red>Quantity must be greater than 0.</color>");
            return;
        }

        GiveService.GiveToRadius(ctx, radius, setName, quantity);
    }

    [Command("giveclan", shortHand: "gc", description: "Give an item set to all online members of a player's clan.", adminOnly: true)]
    public static void GiveClanCommand(ChatCommandContext ctx, string playerName = "", string setName = "", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(setName))
        {
            GiveService.ReplyHelp(ctx);
            return;
        }

        if (quantity <= 0)
        {
            ctx.Reply("<color=red>Quantity must be greater than 0.</color>");
            return;
        }

        GiveService.GiveToClan(ctx, playerName, setName, quantity);
    }

    [Command("addgive", shortHand: "ag", description: "Add an item to a give set.", adminOnly: true)]
    public static void AddCommand(ChatCommandContext ctx, string setName = "", string itemPrefab = "", int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(setName) || string.IsNullOrWhiteSpace(itemPrefab))
        {
            GiveService.ReplyHelp(ctx);
            return;
        }

        if (quantity <= 0)
        {
            ctx.Reply("<color=red>Quantity must be greater than 0.</color>");
            return;
        }
        
        GiveService.AddGiveItem(ctx, setName, itemPrefab, quantity);
    }

    [Command("removegive", shortHand: "rg", description: "Remove a give set.", adminOnly: true)]
    public static void RemoveCommand(ChatCommandContext ctx, string setName = "")
    {
        if (string.IsNullOrWhiteSpace(setName))
        {
            GiveService.ReplyHelp(ctx);
            return;
        }

        GiveService.RemoveGiveSet(ctx, setName);
    }

    [Command("listgive", shortHand: "lg", description: "List all give sets.", adminOnly: true)]
    public static void ListCommand(ChatCommandContext ctx)
    {
        GiveService.ListGiveSets(ctx);
    }

    [Command("helpgive", shortHand: "hg", description: "Show give command help.", adminOnly: true)]
    public static void HelpCommand(ChatCommandContext ctx)
    {
        GiveService.ReplyHelp(ctx);
    }
}