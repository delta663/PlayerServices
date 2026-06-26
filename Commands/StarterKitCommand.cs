using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class StarterKitCommand
{
    [Command("starterkit", shortHand: "sk", description: "Claim your one-time starter kit if auto-grant fails.", adminOnly: false)]
    public static void Claim(ChatCommandContext ctx)
    {
        StarterKitService.ClaimKit(ctx);
    }


    [Command("addstarterkit", shortHand: "ask", description: "Add or update an item in the starter kit.", adminOnly: true)]
    public static void Add(ChatCommandContext ctx, int prefabGuid, int quantity)
    {
        StarterKitService.AddKit(ctx, prefabGuid, quantity);
    }

    [Command("removestarterkit", shortHand: "rsk", description: "Remove an item from the starter kit.", adminOnly: true)]
    public static void Remove(ChatCommandContext ctx, int prefabGuid)
    {
        StarterKitService.RemoveKit(ctx, prefabGuid);
    }

    [Command("liststarterkit", shortHand: "lsk", description: "Show all items in the starter kit.", adminOnly: true)]
    public static void List(ChatCommandContext ctx)
    {
        StarterKitService.ListKit(ctx);
    }
}