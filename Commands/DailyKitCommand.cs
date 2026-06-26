using VampireCommandFramework;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("pls")]
internal static class DailyKitCommand
{
    [Command("dailykit", shortHand: "dk", description: "Claim your daily kit.", adminOnly: false)]
    public static void Claim(ChatCommandContext ctx)
    {
        DailyKitService.ClaimKit(ctx);
    }


    [Command("adddailykit", shortHand: "adk", description: "Add or update an item in the daily kit.", adminOnly: true)]
    public static void Add(ChatCommandContext ctx, int prefabGuid, int quantity)
    {
        DailyKitService.AddKit(ctx, prefabGuid, quantity);
    }

    [Command("removedailykit", shortHand: "rdk", description: "Remove an item from the daily kit.", adminOnly: true)]
    public static void Remove(ChatCommandContext ctx, int prefabGuid)
    {
        DailyKitService.RemoveKit(ctx, prefabGuid);
    }

    [Command("listdailykit", shortHand: "ldk", description: "Show all daily kit items.", adminOnly: true)]
    public static void List(ChatCommandContext ctx)
    {
        DailyKitService.ListKit(ctx);
    }
}