using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BepInEx;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;
using System.Collections;
using UnityEngine;
using PlayerServices.Data;

namespace PlayerServices.Services;

internal static class TeleportPointsService
{
    private static readonly string CONFIG_DIR = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "teleport_points.json");

	private const float CHECK_INTERVAL = 1f;
	private const float MOVE_TOLERANCE_XZ = 0.2f;
	private const float MOVE_TOLERANCE_Y = 0.8f;

	private static readonly HashSet<ulong> _pendingTeleports = new();
    private static readonly Dictionary<int, TeleportPoint> _points = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private static EntityArchetype _netEventArchetype;
    private static bool _archetypeReady;
    private static bool _initialized;
	
    private struct TeleportPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public bool AdminOnly { get; set; }
        public string Description { get; set; }
    }

    public static void Initialize()
    {
        if (_initialized)
            return;

        LoadConfig();
        TryInitArchetype();

        _initialized = true;
        Core.Log.LogInfo($"[Teleport] Initialized and loaded {_points.Count} point(s).");
    }

    // ---------------------------------------------------------------------
    // Add, Remove, List
    // ---------------------------------------------------------------------

    public static void Add(ChatCommandContext ctx, string slotToken, string adminOnlyToken, string description)
    {
        EnsureInitialized();

        if (!TryParseSlot(ctx, slotToken, out int slot))
            return;

        if (!bool.TryParse(adminOnlyToken, out bool adminOnly))
        {
            ctx.Reply("<color=red>AdminOnly must be true or false.</color>");
            return;
        }

        var em = Core.EntityManager;
        var character = ctx.Event.SenderCharacterEntity;

        if (character == Entity.Null || !em.Exists(character) || !em.HasComponent<Translation>(character))
        {
            ctx.Reply("<color=yellow>Cannot read your position right now.</color>");
            return;
        }

        var pos = em.GetComponentData<Translation>(character).Value;

        if (_points.ContainsKey(slot))
        {
            ctx.Reply("<color=yellow>That slot is already occupied.</color>");
            return;
        }

        description ??= string.Empty;

        _points[slot] = new TeleportPoint
        {
            X = pos.x,
            Y = pos.y,
            Z = pos.z,
            AdminOnly = adminOnly,
            Description = description
        };

        Save();

        string descText = string.IsNullOrWhiteSpace(description) ? string.Empty : $" [{description}]";

        ctx.Reply($"Saved <color=white>tp {slot}</color> at <color=yellow>[{pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}]</color> [AdminOnly: {adminOnly}]{descText}");
        Core.Log.LogInfo($"[Teleport] Saved slot {slot} at [{pos.x:0.0}, {pos.y:0.0}, {pos.z:0.0}], AdminOnly: {adminOnly}, Description: {description}");
    }

    public static void Remove(ChatCommandContext ctx, string slotToken)
    {
        EnsureInitialized();

        if (!TryParseSlot(ctx, slotToken, out int slot))
            return;

        if (!_points.Remove(slot))
        {
            ctx.Reply("<color=yellow>No teleport point saved in that slot.</color>");
            return;
        }

        Save();

        ctx.Reply($"Removed <color=white>tp {slot}</color>.");
        Core.Log.LogInfo($"[Teleport] Removed slot {slot}");
    }

    public static void List(ChatCommandContext ctx)
    {
        EnsureInitialized();

        if (_points.Count == 0)
        {
            ctx.Reply("<color=yellow>No teleport points saved.</color>");
            return;
        }

        var sb = new StringBuilder(256);
        sb.AppendLine("<color=yellow>Saved teleport points</color>");

        foreach (var kvp in _points)
        {
            int slot = kvp.Key;
            var point = kvp.Value;

            sb.Append('[').Append(slot).Append("] [")
                .Append(point.X.ToString("0.0")).Append(", ")
                .Append(point.Y.ToString("0.0")).Append(", ")
                .Append(point.Z.ToString("0.0")).Append("]");

            if (point.AdminOnly)
                sb.Append(" [AdminOnly]");

            if (!string.IsNullOrWhiteSpace(point.Description))
                sb.Append(" [").Append(point.Description).Append(']');

            sb.AppendLine();
        }

        ctx.Reply(sb.ToString().TrimEnd());
    }

	public static void ReplyHelp(ChatCommandContext ctx)
    {
        var sb = new StringBuilder();

		int delay = Plugin.playerTeleportDelaySeconds?.Value ?? 0;
		bool isEnabled = Plugin.playerTeleportEnabled.Value;
		string status = isEnabled ? "<color=green>Enabled</color>" : "<color=red>Disabled</color>";

        if (isEnabled)
        {
            sb.AppendLine("<color=yellow>Teleport Commands:</color>");
            sb.AppendLine("<color=green>.pls tp <slot></color>");
        
            if (ctx.Event.User.IsAdmin)
            {
                sb.AppendLine("<color=green>.pls addtp <slot> [true/false] [description]</color>");
                sb.AppendLine("<color=green>.pls removetp <slot></color>");
				sb.AppendLine("<color=green>.pls listtp</color>");
            }

			sb.AppendLine($"<color=yellow>Player Teleport Delay:</color> {delay} second(s)");
        }

        sb.AppendLine($"<color=yellow>Teleport Status:</color> {status}");
		
		ctx.Reply(sb.ToString().TrimEnd());
    }

    // ---------------------------------------------------------------------
    // Process Teleport
    // ---------------------------------------------------------------------

    public static void Teleport(ChatCommandContext ctx, string slotToken)
	{
		EnsureInitialized();

		if (!TryParseSlot(ctx, slotToken, out int slot))
			return;

		if (!_points.TryGetValue(slot, out var point))
		{
			ctx.Reply("<color=yellow>No teleport point saved in that slot.</color>");
			return;
		}

		var target = new float3(point.X, point.Y, point.Z);
		bool isAdmin = ctx.Event.User.IsAdmin;

		if (isAdmin)
		{
			if (!TryTeleportSelf(ctx, target))
			{
				ctx.Reply("<color=red>Teleport failed.</color>");
				return;
			}

			string adminDesc = string.IsNullOrWhiteSpace(point.Description)
				? string.Empty
				: $" <color=yellow>{point.Description}</color>";

			ctx.Reply($"Teleported to slot <color=white>{slot}</color>{adminDesc}.");
			Core.Log.LogInfo($"[Teleport] Admin: {ctx.Event.User.CharacterName} teleported to slot {slot} [{point.X:0.0}, {point.Y:0.0}, {point.Z:0.0}]");
			return;
		}

		ulong steamId = ctx.Event.User.PlatformId;

		if (steamId == 0)
		{
			ctx.Reply("<color=red>Cannot read your SteamID right now.</color>");
			return;
		}

		if (_pendingTeleports.Contains(steamId))
		{
			ctx.Reply("<color=yellow>Teleport is already in progress.</color>");
			return;
		}

		if (!CanPlayerTeleport(ctx, point, out string reason))
		{
			ctx.Reply(reason);
			return;
		}

		var em = Core.EntityManager;
		var userEntity = ctx.Event.SenderUserEntity;
		var character = ctx.Event.SenderCharacterEntity;

		if (userEntity == Entity.Null || character == Entity.Null ||
			!em.Exists(userEntity) || !em.Exists(character) ||
			!em.HasComponent<Translation>(character))
		{
			ctx.Reply("<color=red>Cannot start teleport right now.</color>");
			return;
		}

		var startPosition = em.GetComponentData<Translation>(character).Value;
		string playerName = ctx.Event.User.CharacterName.ToString();
		int delaySeconds = Plugin.playerTeleportDelaySeconds.Value;

		if (delaySeconds <= 0)
		{
			if (!TryTeleport(userEntity, character, target))
			{
				ctx.Reply("<color=red>Teleport failed.</color>");
				return;
			}

			string desc = string.IsNullOrWhiteSpace(point.Description)
				? string.Empty
				: $" <color=yellow>{point.Description}</color>";

			ctx.Reply($"Teleported to slot <color=white>{slot}</color>{desc}.");
			Core.Log.LogInfo($"[Teleport] Player: {playerName} teleported to slot {slot} [{point.X:0.0}, {point.Y:0.0}, {point.Z:0.0}]");
			return;
		}

		_pendingTeleports.Add(steamId);
		TryApplyTeleportWaitBuff(userEntity, character, delaySeconds);

		ctx.Reply($"<color=yellow>Teleporting in {delaySeconds} seconds.</color>");

		Core.StartCoroutine(DelayedPlayerTeleportRoutine(userEntity, character, steamId, playerName, slot, delaySeconds, point, target, startPosition));
	}


    private static bool CanPlayerTeleport(ChatCommandContext ctx, TeleportPoint point, out string reason)
	{
		reason = string.Empty;

		if (!Plugin.playerTeleportEnabled.Value)
		{
			reason = "<color=red>Player teleport is currently disabled.</color>";
			return false;
		}

		if (point.AdminOnly)
		{
			reason = "<color=yellow>This teleport point is for admins only.</color>";
			return false;
		}

		var character = ctx.Event.SenderCharacterEntity;

		if (character == Entity.Null || !Core.EntityManager.Exists(character))
		{
			reason = "<color=red>Cannot read your character right now.</color>";
			return false;
		}

		if (!Core.EntityManager.HasComponent<Translation>(character))
		{
			reason = "<color=red>Cannot read your position right now.</color>";
			return false;
		}

		if (HasTeleportBlockBuff(character, TeleportBlockMessageMode.BeforeStart, out reason))
			return false;

		return true;
	}

	// ---------------------------------------------------------------------
    // Process Delay
    // ---------------------------------------------------------------------

	private static IEnumerator DelayedPlayerTeleportRoutine(Entity userEntity, Entity character, ulong steamId, string playerName, int slot, int delaySeconds, TeleportPoint point, float3 target, float3 startPosition)
	{
		float elapsed = 0f;

		try
		{
			while (elapsed < delaySeconds)
			{
				yield return new WaitForSeconds(CHECK_INTERVAL);
				elapsed += CHECK_INTERVAL;

				if (!IsDelayedTeleportStillValid(userEntity, character, startPosition, out string cancelMessage))
				{
					NotifyTeleportUser(userEntity, cancelMessage);
					yield break;
				}
			}

			if (!IsDelayedTeleportStillValid(userEntity, character, startPosition, out string finalCancelMessage))
			{
				NotifyTeleportUser(userEntity, finalCancelMessage);
				yield break;
			}

			if (!TryTeleport(userEntity, character, target))
			{
				NotifyTeleportUser(userEntity, "<color=red>Teleport failed.</color>");
				yield break;
			}

			string desc = string.IsNullOrWhiteSpace(point.Description)
				? string.Empty
				: $" <color=yellow>{point.Description}</color>";

			NotifyTeleportUser(userEntity, $"Teleported to slot <color=white>{slot}</color>{desc}.");
			Core.Log.LogInfo($"[Teleport] Player: {playerName} teleported to slot {slot} [{point.X:0.0}, {point.Y:0.0}, {point.Z:0.0}]");
		}
		finally
		{
			_pendingTeleports.Remove(steamId);
			TryRemoveTeleportWaitBuff(character);
		}
	}

	// ---------------------------------------------------------------------
    // Cancel Teleport
    // ---------------------------------------------------------------------

	private static bool IsDelayedTeleportStillValid(Entity userEntity, Entity character, float3 startPosition, out string cancelMessage)
	{
		cancelMessage = string.Empty;

		try
		{
			var em = Core.EntityManager;

			if (userEntity == Entity.Null || character == Entity.Null ||
				!em.Exists(userEntity) || !em.Exists(character))
			{
				cancelMessage = "<color=red>Teleport cancelled because your character is not available.</color>";
				return false;
			}

			if (!em.HasComponent<User>(userEntity))
			{
				cancelMessage = "<color=red>Teleport cancelled because your user data is not available.</color>";
				return false;
			}

			var user = em.GetComponentData<User>(userEntity);

			if (!user.IsConnected)
			{
				cancelMessage = "<color=red>Teleport cancelled because you disconnected.</color>";
				return false;
			}

			if (HasTeleportBlockBuff(character, TeleportBlockMessageMode.DuringDelay, out cancelMessage))
				return false;

			if (HasMovedFromStart(character, startPosition))
			{
				cancelMessage = "<color=red>Teleport cancelled because you moved.</color>";
				return false;
			}

			return true;
		}
		catch (Exception e)
		{
			Core.LogException(e);
			cancelMessage = "<color=red>Teleport cancelled because an error occurred.</color>";
			return false;
		}
	}

	private static bool HasTeleportBlockBuff(Entity character, TeleportBlockMessageMode mode, out string reason)
	{
		reason = string.Empty;

		var em = Core.EntityManager;

		if (character == Entity.Null || !em.Exists(character))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because your character is not available.</color>"
				: "<color=red>Cannot read your character right now.</color>";

			return true;
		}

		if (BuffUtility.HasBuff(em, character, PrefabData.Downed))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you are downed.</color>"
				: "<color=red>You cannot teleport while you are downed.</color>";

			return true;
		}

		if (BuffUtility.HasBuff(em, character, PrefabData.Spiderform))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you entered Spider Form.</color>"
				: "<color=red>You cannot teleport while in Spider Form.</color>";

			return true;
		}

		if (BuffUtility.HasBuff(em, character, PrefabData.Batform))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you entered Bat Form.</color>"
				: "<color=red>You cannot teleport while in Bat Form.</color>";

			return true;
		}

		if (BuffUtility.HasBuff(em, character, PrefabData.Dominate))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you entered Dominate.</color>"
				: "<color=red>You cannot teleport while in Dominate.</color>";

			return true;
		}

		if (BuffUtility.HasBuff(em, character, PrefabData.Golemform))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you entered Golem Form.</color>"
				: "<color=red>You cannot teleport while in Golem Form.</color>";

			return true;
		}

		if (Helper.IsInCombat(character))
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because you entered combat.</color>"
				: "<color=red>You cannot teleport while in combat.</color>";

			return true;
		}

		if (Helper.IsRaidTime())
		{
			reason = mode == TeleportBlockMessageMode.DuringDelay
				? "<color=red>Teleport cancelled because Raid Time started.</color>"
				: "<color=red>You cannot teleport during Raid Time.</color>";

			return true;
		}

		return false;
	}

	private static bool HasMovedFromStart(Entity character, float3 startPosition)
	{
		var em = Core.EntityManager;

		if (character == Entity.Null || !em.Exists(character) || !em.HasComponent<Translation>(character))
			return true;

		var currentPosition = em.GetComponentData<Translation>(character).Value;

		float dx = currentPosition.x - startPosition.x;
		float dz = currentPosition.z - startPosition.z;
		float horizontalDistanceSquared = (dx * dx) + (dz * dz);

		if (horizontalDistanceSquared > MOVE_TOLERANCE_XZ * MOVE_TOLERANCE_XZ)
			return true;

		float dy = math.abs(currentPosition.y - startPosition.y);

		return dy > MOVE_TOLERANCE_Y;
	}

	private enum TeleportBlockMessageMode
	{
		BeforeStart,
		DuringDelay
	}

	// ---------------------------------------------------------------------
    // Buff
    // ---------------------------------------------------------------------

	private static void TryApplyTeleportWaitBuff(Entity userEntity, Entity character, int delaySeconds)
	{
		try
		{
			if (delaySeconds <= 2)
				return;

			if (userEntity == Entity.Null || character == Entity.Null)
				return;

			var em = Core.EntityManager;

			if (!em.Exists(userEntity) || !em.Exists(character))
				return;

			Buffs.AddBuff(userEntity, character, PrefabData.TeleportWaiting, -1, false);
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

	private static void TryRemoveTeleportWaitBuff(Entity character)
	{
		try
		{
			if (character == Entity.Null)
				return;

			var em = Core.EntityManager;

			if (!em.Exists(character))
				return;

			if (BuffUtility.HasBuff(em, character, PrefabData.TeleportWaiting))
			{
				Buffs.RemoveBuff(character, PrefabData.TeleportWaiting);
			}
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

    // ---------------------------------------------------------------------
    // Helper
    // ---------------------------------------------------------------------

	private static void NotifyTeleportUser(Entity userEntity, string message)
	{
		try
		{
			var em = Core.EntityManager;

			if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
				return;

			Helper.NotifyUser(userEntity, message);
		}
		catch (Exception e)
		{
			Core.LogException(e);
		}
	}

    private static bool TryParseSlot(ChatCommandContext ctx, string slotToken, out int slot)
    {
        slot = 0;

        if (!int.TryParse(slotToken, out slot) || slot <= 0)
        {
            if (ctx.Event.User.IsAdmin)
            {
                ctx.Reply("<color=yellow>Admin usage:</color>\n.pls tp <slot>\n.pls addtp <slot> <true/false> [description]\n.pls removetp <slot>\n.pls listtp");
            }
            else
            {
                ctx.Reply("<color=yellow>Player usage:</color> .pls tp <slot> Example: <color=green>.pls tp 1</color>");
            }

            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------------
    // Disk & Logging
    // ---------------------------------------------------------------------

    private static void EnsureInitialized()
    {
        if (!_initialized)
            Initialize();
    }

	public static bool ReloadConfig(out int pointCount)
	{
		bool ok = LoadConfig();
		pointCount = _points.Count;
		return ok;
	}

	private static bool LoadConfig()
	{
		try
		{
			_points.Clear();

			if (!File.Exists(CONFIG_FILE))
				return true;

			string json = File.ReadAllText(CONFIG_FILE);

			if (string.IsNullOrWhiteSpace(json))
				return true;

			var data = JsonSerializer.Deserialize<Dictionary<int, TeleportPoint>>(json, _jsonOptions);

			if (data == null || data.Count == 0)
				return true;

			foreach (var kvp in data)
			{
				_points[kvp.Key] = kvp.Value;
			}

			return true;
		}
		catch (Exception e)
		{
			Core.Log.LogError($"[Teleport] Failed to load teleport_points.json: {e.Message}");
			_points.Clear();
			return false;
		}
	}

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            string json = JsonSerializer.Serialize(_points, _jsonOptions);
            File.WriteAllText(CONFIG_FILE, json);
        }
        catch (Exception e)
        {
            Core.Log.LogError($"[Teleport] Failed to save teleport_points.json: {e.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // MethodImpl
    // ---------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryTeleportSelf(ChatCommandContext ctx, float3 target)
	{
		var userEntity = ctx.Event.SenderUserEntity;
		var character = ctx.Event.SenderCharacterEntity;

		return TryTeleport(userEntity, character, target);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool TryTeleport(Entity userEntity, Entity character, float3 target)
	{
		try
		{
			var em = Core.EntityManager;

			if (!_archetypeReady)
			{
				TryInitArchetype();

				if (!_archetypeReady)
					return false;
			}

			if (userEntity == Entity.Null || character == Entity.Null)
				return false;

			if (!em.Exists(userEntity) || !em.Exists(character))
				return false;

			if (target.y < 0f)
				target.y = 0f;

			var safePosition = target;
			safePosition.y += 0.25f;

			var entity = em.CreateEntity(_netEventArchetype);

			em.SetComponentData(entity, new FromCharacter
			{
				User = userEntity,
				Character = character
			});

			em.SetComponentData(entity, new PlayerTeleportDebugEvent
			{
				Position = safePosition
			});

			return true;
		}
		catch (Exception e)
		{
			Core.LogException(e);
			return false;
		}
	}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryInitArchetype()
    {
        if (_archetypeReady)
            return;

        try
        {
            var em = Core.EntityManager;

            _netEventArchetype = em.CreateArchetype(
                ComponentType.ReadWrite<FromCharacter>(),
                ComponentType.ReadWrite<PlayerTeleportDebugEvent>(),
                ComponentType.ReadWrite<SendNetworkEventTag>()
            );

            _archetypeReady = true;
        }
        catch (Exception e)
        {
            _archetypeReady = false;
            Core.LogException(e);
        }
    }
}
