using VampireCommandFramework;
using ProjectM.Network;
using PlayerServices.Services;

namespace PlayerServices.Commands;

[CommandGroup("changename", "cn")]
internal static class ChangeNameCommand
{
    [Command("to", description: "Change your character name.", adminOnly: false)]
    public static void ChangeNameSelf(ChatCommandContext ctx, string newName = "")
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            ChangeNameService.ReplyHelp(ctx, "<color=red>Please specify a new name.</color>");
            return;
        }

        var userEntity = ctx.Event.SenderUserEntity;
        var charEntity = ctx.Event.SenderCharacterEntity;
        
        var user = userEntity.Read<User>();
        string oldName = user.CharacterName.ToString();
        ulong steamId = user.PlatformId;

        ChangeNameService.ProcessRename(ctx, userEntity, charEntity, oldName, newName, steamId, isAdminOverride: false);
    }
    
    [Command("help", description: "Show help and info for change name command.", adminOnly: false)]
    public static void ChangeNameHelp(ChatCommandContext ctx)
    {
        ChangeNameService.ReplyHelp(ctx);
    }


    [Command("player", description: "Admin command to change a player's character name.", adminOnly: true)]
    public static void ChangeNameOther(ChatCommandContext ctx, string currentName = "", string newName = "")
    {
        if (string.IsNullOrWhiteSpace(currentName) || string.IsNullOrWhiteSpace(newName))
        {
            ctx.Reply("Usage: <color=green>.changename player <currentName> <newName></color>");
            return;
        }

        if (!ChangeNameService.TryFindUserByName(currentName, out var targetUserEntity, out var targetCharEntity, out var targetUser))
        {
            ctx.Reply($"<color=red>Player not found:</color> <color=white>{currentName}</color>");
            return;
        }

        string oldName = targetUser.CharacterName.ToString();
        ulong targetSteamId = targetUser.PlatformId;

        ChangeNameService.ProcessRename(ctx, targetUserEntity, targetCharEntity, oldName, newName, targetSteamId, isAdminOverride: true);
    }

    [Command("testwebhook", shortHand: "tw", description: "Send a test message to Discord.", adminOnly: true)]
    public static void TestChangeNameWebhook(ChatCommandContext ctx)
    {
        if (string.IsNullOrWhiteSpace(Plugin.changeNameWebhookUrl?.Value))
        {
            ctx.Reply("<color=red>Webhook URL is empty in the config.</color>");
            return;
        }

        string testMsg = $"Webhook test message from {MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} by Del";
        
        ctx.Reply("<color=yellow>Sending a webhook test message.</color>");

        _ = WebhookService.SendAsync(testMsg); 
    }
}
