using System;
using System.Collections;
using ProjectM.Network;
using Unity.Entities;
using UnityEngine;

namespace PlayerServices.Services;

internal static class WelcomeService
{
    private const float MESSAGE_DELAY_SECONDS = 2f;

    private static IEnumerator SendWelcomeMessageRoutine(Entity userEntity, ulong steamId)
    {
        yield return new WaitForSeconds(MESSAGE_DELAY_SECONDS);

        SendWelcomeMessage(userEntity, steamId);
    }

    public static void SendWelcomeMessageDelayed(Entity userEntity, User user)
    {
        if (!Plugin.welcomeMessageEnabled.Value)
            return;

        if (user.PlatformId == 0)
            return;

        Core.StartCoroutine(SendWelcomeMessageRoutine(userEntity, user.PlatformId));
    }

    private static void SendWelcomeMessage(Entity userEntity, ulong steamId)
    {
        try
        {
            if (!Plugin.welcomeMessageEnabled.Value)
                return;

            var em = Core.EntityManager;

            if (userEntity == Entity.Null || !em.Exists(userEntity) || !em.HasComponent<User>(userEntity))
                return;

            var user = em.GetComponentData<User>(userEntity);

            if (!user.IsConnected || user.PlatformId == 0 || user.PlatformId != steamId)
                return;

            var cache = PlayerDataService.GetPlayerCache(user.PlatformId);

            if (Plugin.onlyWhitelistEnable.Value && (cache == null || !cache.IsWhitelisted))
                return;

            if (cache != null && cache.IsBanned)
                return;

            string playerName = user.CharacterName.ToString();

            if (string.IsNullOrWhiteSpace(playerName))
                playerName = "Vampire";

            SendFormattedMessage(userEntity, Plugin.welcomeMessageText1.Value, playerName);
            SendFormattedMessage(userEntity, Plugin.welcomeMessageText2.Value, playerName);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    private static void SendFormattedMessage(Entity userEntity, string message, string playerName)
    {
	    if (string.IsNullOrWhiteSpace(message))
		    return;

	    message = message.Replace("#player#", playerName);

	    if (message.Length > Core.MAX_REPLY_LENGTH)
	    {
		    Core.Log.LogWarning($"[Welcome] Welcome message is too long ({message.Length}/{Core.MAX_REPLY_LENGTH}). It has been trimmed.");

		    int maxLength = Core.MAX_REPLY_LENGTH - 3;

		    if (maxLength <= 0)
			    return;

		    message = message.Substring(0, maxLength) + "...";
	    }

	    Helper.NotifyUser(userEntity, message);
    }
}
