using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using PlayerServices.Services;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using Unity.Entities;
using UnityEngine;
using ProjectM.CastleBuilding;

namespace PlayerServices;

internal static class Core
{
    private static World _server;
    private static bool _hasInitialized = false;

    public const int MAX_REPLY_LENGTH = 509;

    public static World Server
    {
        get
        {
            _server ??= GetWorld("Server");
            return _server;
        }
    }

    public static EntityManager EntityManager => Server.EntityManager;
    public static bool IsServer => Application.productName == "VRisingServer";
    public static GameDataSystem GameDataSystem => Server.GetExistingSystemManaged<GameDataSystem>();
    public static GenerateCastleSystem GenerateCastle { get; private set; }
    public static PrefabCollectionSystem PrefabCollectionSystem { get; internal set; }
    public static PrefabCollectionSystem PrefabCollection => Server.GetExistingSystemManaged<PrefabCollectionSystem>();
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static ServerGameSettingsSystem ServerGameSettingsSystem { get; internal set; }
    
    static MonoBehaviour monoBehaviour;

    public static ManualLogSource Log => Plugin.PluginLog;

    private static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name)
                return world;
        }

        return null;
    }

    public static void LogException(System.Exception e, [CallerMemberName] string caller = null)
    {
        Log.LogError($"Failure in {caller}\nMessage: {e.Message} Inner: {e.InnerException?.Message}\n\nStack: {e.StackTrace}\nInner Stack: {e.InnerException?.StackTrace}");
    }

    internal static void InitializeAfterLoaded()
    {
        if (_hasInitialized) return;

        PrefabCollectionSystem = Server.GetExistingSystemManaged<PrefabCollectionSystem>();
        ServerScriptMapper = Server.GetExistingSystemManaged<ServerScriptMapper>();
        GenerateCastle = Server.GetOrCreateSystemManaged<GenerateCastleSystem>();
        ServerGameSettingsSystem = Server.GetExistingSystemManaged<ServerGameSettingsSystem>();

        PlayerDataService.Initialize();
        DailyKitService.Initialize();
        GiveService.Initialize();
        StarterKitService.Initialize();
        TeleportPointsService.Initialize();
        AuraService.Initialize();

        _hasInitialized = true;
        Log.LogInfo($"{nameof(InitializeAfterLoaded)} completed.");
    }

    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (monoBehaviour == null)
        {
            var go = new GameObject("PlayerServices");
            monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            Object.DontDestroyOnLoad(go);
        }

        return monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }

    public static void StopCoroutines()
    {
        if (monoBehaviour != null)
        {
            monoBehaviour.StopAllCoroutines();
            UnityEngine.Object.Destroy(monoBehaviour.gameObject);
            monoBehaviour = null;
        }
    }
}
