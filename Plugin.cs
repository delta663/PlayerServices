using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using VampireCommandFramework;
using UnityEngine;
using BepInEx.Configuration;
using PlayerServices.Services;
using System.Collections;

namespace PlayerServices;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{

    internal static Harmony Harmony;
    internal static ManualLogSource PluginLog;

    internal static ConfigFile PluginConfig;
    internal static ConfigEntry<bool> pisCommandEnabled;
    internal static ConfigEntry<bool> playerInfoShowClanCastleInfo;
    internal static ConfigEntry<bool> playerInfoShowClanMemberLastOnline;

    internal static ConfigEntry<bool> dailyKitEnabled;
    internal static ConfigEntry<string> dailyKitItems;

    internal static ConfigEntry<bool> starterKitEnabled;
    internal static ConfigEntry<string> starterKitItems;

    internal static ConfigEntry<bool> changeNameFeatureEnabled;
    internal static ConfigEntry<bool> adminChangeNameBroadcastAndWebhookEnabled;
    internal static ConfigEntry<bool> playerChangeNameBroadcastEnabled;
    internal static ConfigEntry<bool> playerChangeNameWebhookEnabled;
    internal static ConfigEntry<string> changeNameCurrencyName;
    internal static ConfigEntry<int> changeNameCurrencyPrefab;
    internal static ConfigEntry<int> changeNameCurrencyCost;
    internal static ConfigEntry<string> changeNameBroadcastMessage;
    internal static ConfigEntry<string> changeNameWebhookMessage;
    internal static ConfigEntry<string> changeNameWebhookUrl;

    internal static ConfigEntry<bool> onlyWhitelistEnable;

    internal static ConfigEntry<bool> welcomeMessageEnabled;
    internal static ConfigEntry<string> welcomeMessageText1;
    internal static ConfigEntry<string> welcomeMessageText2;

    internal static ConfigEntry<bool> auraFeatureEnabled;
    internal static ConfigEntry<string> auraBroadcastMessage;
    internal static ConfigEntry<string> auraCurrencyName;
    internal static ConfigEntry<string> auraCurrencyPrefabGuid;
    internal static ConfigEntry<string> auraPrefabGuids;
    internal static ConfigEntry<string> auraCosts;

    internal static ConfigEntry<bool> playerTeleportEnabled;
	internal static ConfigEntry<int> playerTeleportDelaySeconds;



    public override void Load()
    {
        if (Application.productName != "VRisingServer")
            return;

        PluginLog = Log;
        PluginConfig = Config;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

        pisCommandEnabled = Config.Bind("PlayerInformations", "pisCommandEnabled", true, "Enable the player information command.");
        playerInfoShowClanCastleInfo = Config.Bind("PlayerInformations", "ShowClanCastleInfo", false, "Show clan castle information in .pis command. Admins can always see this information.");
        playerInfoShowClanMemberLastOnline = Config.Bind("PlayerInformations", "ShowClanMemberLastOnline", false, "Show last online time for offline clan members in .pis command. Admins can always see this information.");

        dailyKitEnabled = Config.Bind("DailyKit", "DailyKitEnabled", true, "Enable players to claim Daily Kits.");
        dailyKitItems = Config.Bind("DailyKit", "dailyKitItemsAndQuantity", "429052660:10,800879747:10", "List of daily kit items in the format PrefabGuid:Quantity, separated by commas.");
        starterKitEnabled = Config.Bind("StarterKit", "StarterKitEnabled", true, "Enable automatically granting a one-time Starter Kit to new players upon character creation.");
        starterKitItems = Config.Bind("StarterKit", "StarterKitItemsAndQuantity", "1821405450:208,-1222725729:64,-219760992:1,-1593377811:300,-1531666018:300,862477668:100", "List of starter kit items in the format PrefabGuid:Quantity separated by commas.");

        changeNameFeatureEnabled = Config.Bind("ChangeName", "ChangeNameFeatureEnabled", true, "Enable the character name change feature.");
        adminChangeNameBroadcastAndWebhookEnabled = Config.Bind("ChangeName", "AdminChangeNameIngameBroadcastAndWebhookEnabled", false, "Enable in-game broadcasts and Discord webhook notifications when an admin changes a player's name.");
        playerChangeNameBroadcastEnabled = Config.Bind("ChangeName", "PlayerChangeNameIngameBroadcastEnabled", true, "Enable in-game broadcast messages when players change their names.");
        playerChangeNameWebhookEnabled = Config.Bind("ChangeName", "PlayerChangeNameWebhookEnabled", false, "Enable Discord webhook messages when players change their names.");
        changeNameCurrencyName = Config.Bind("ChangeName", "ChangeNameCurrencyName", "Primal Stygian Shards", "Currency name required to change a character name.");
        changeNameCurrencyPrefab = Config.Bind("ChangeName", "ChangeNameCurrencyPrefabGuid", 28358550, "Currency prefab Guid required to change a character name.");
        changeNameCurrencyCost = Config.Bind("ChangeName", "ChangeNameCurrencyCost", 1000, "Currency cost required to change a character name.");
        changeNameBroadcastMessage = Config.Bind("ChangeName", "ChangeNameIngameBroadcastMessage", "<color=white>#oldname#</color> has changed their name to <color=white>#newname#</color>. For more info, type <color=green>.changename help</color>", "Format of the in-game broadcast message.");
        changeNameWebhookMessage = Config.Bind("ChangeName", "ChangeNameWebhookMessage", "**[Change Name]** - **#oldname#** has changed their name to **#newname#**", "Format of the Discord webhook message.");
        changeNameWebhookUrl = Config.Bind("ChangeName", "ChangeNameWebhookUrl", "", "Webhook URL. Example: https://discord.com/api/webhooks/xxxxxxxxxxxxxxxxx/xxxxxxxxxxxxxxxxxxxxx");

        onlyWhitelistEnable = Config.Bind("Whitelist", "OnlyWhitelistEnable", false, "Set the server to private and only allow players marked as whitelisted to join.");

        welcomeMessageEnabled = Config.Bind("WelcomeMessage", "WelcomeMessageEnabled", true, "Enable the welcome message sent to players after they connect.");
        welcomeMessageText1 = Config.Bind("WelcomeMessage", "WelcomeMessageText1", "Welcome to the server, <color=white>#player#</color>!", "Text for the first welcome message.");
        welcomeMessageText2 = Config.Bind("WelcomeMessage", "WelcomeMessageText2", "", "Text for the second welcome message. Leave blank to disable it.");

        auraFeatureEnabled = Config.Bind("Aura","AuraFeatureEnabled",true,"Enable the aura feature.");
        auraBroadcastMessage = Config.Bind("Aura", "AuraBroadcastMessage", "<color=white>#player#</color> bought aura #aura#. For more info, type <color=green>.aura help</color>", "Format of the in-game broadcast message.");
        auraCurrencyName = Config.Bind("Aura", "AuraCurrencyName", "Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards,Primal Stygian Shards", "Currency names for each aura, separated by comma.");
        auraCurrencyPrefabGuid = Config.Bind("Aura", "AuraCurrencyPrefabGuid", "28358550,28358550,28358550,28358550,28358550,28358550,28358550,28358550,28358550,28358550,28358550", "Currency prefab GUIDs for each aura, separated by comma.");
        auraCosts = Config.Bind("Aura","AuraCosts","100,200,300,400,500,600,700,800,900,0,0","Comma-separated aura costs. The order must match AuraPrefabGuids. Cost must be greater than 0 to allow purchase; use 0 to make that aura admin-only/not for sale. Example: 100,0,300");
        auraPrefabGuids = Config.Bind("Aura","AuraPrefabGuids","-1242403012,-1887712500,-1083643277,1343911070,784366378,1237097606,647429443,-646349605,-1124645803,-1640482518,1163490655","Comma-separated aura prefab GUIDs. The order must match AuraCosts, AuraCurrencyName, and AuraCurrencyPrefabGuid.");
        
        playerTeleportEnabled = Config.Bind("Teleport", "PlayerTeleportEnabled", true, "Enable players to use .pls tp where AdminOnly is false. Admins can still teleport.");
		playerTeleportDelaySeconds = Config.Bind("Teleport", "PlayerTeleportDelaySeconds", 10, new ConfigDescription("Delay in seconds before player teleport. Set to 0 for instant teleport.", new AcceptableValueRange<int>(0, 20)));

        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        CommandRegistry.RegisterAll();
    }

	public override bool Unload()
	{
		PlayerDataService.ForceSaveIfDirty();
		Core.StopCoroutines();

		CommandRegistry.UnregisterAssembly();
		Harmony?.UnpatchSelf();

		return true;
	}
}
