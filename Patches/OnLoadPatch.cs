using HarmonyLib;
using Unity.Scenes;

namespace PlayerServices.Patches;		// Credit KindredSacrifice

[HarmonyPatch(typeof(SceneSectionStreamingSystem), nameof(SceneSectionStreamingSystem.ShutdownAsynchrnonousStreamingSupport))]
public static class InitializationPatch
{
	[HarmonyPostfix]
	public static void OneShot_AfterLoad_InitializationPatch()
	{
		if (!Core.IsServer) return;

		Core.InitializeAfterLoaded();
	 //	Core.Log.LogInfo("[OnLoad] PlayerServices initialized.");
        
		Plugin.Harmony?.Unpatch(typeof(SceneSectionStreamingSystem).GetMethod("ShutdownAsynchrnonousStreamingSupport"), typeof(InitializationPatch).GetMethod("OneShot_AfterLoad_InitializationPatch"));
	}
}