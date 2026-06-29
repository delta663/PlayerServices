using PlayerServices.Services;
using VampireCommandFramework;

namespace PlayerServices.Commands;

[CommandGroup("clan", "c")]
internal static class ClanCommands
{
	[Command("forcecreate", shortHand: "fc", description: "Force a player to create their own clan.", adminOnly: true)]
	public static void CreateClanCommand(ChatCommandContext ctx, string playerName = "")
	{
		if (string.IsNullOrWhiteSpace(playerName))
		{
			ClanService.ReplyHelp(ctx);
			return;
		}

		ClanService.CreateClanForPlayer(ctx, playerName);
	}

	[Command("forcejoin", shortHand: "fj", description: "Force two players into the same clan.", adminOnly: true)]
	public static void JoinClanCommand(ChatCommandContext ctx, string playerAName = "", string playerBName = "")
	{
		if (string.IsNullOrWhiteSpace(playerAName) || string.IsNullOrWhiteSpace(playerBName))
		{
			ClanService.ReplyHelp(ctx);
			return;
		}

		ClanService.JoinPlayerToPlayerClan(ctx, playerAName, playerBName);
	}

	[Command("forceleave", shortHand: "fl", description: "Force a player to leave their clan.", adminOnly: true)]
	public static void LeaveClanCommand(ChatCommandContext ctx, string playerName = "")
	{
		if (string.IsNullOrWhiteSpace(playerName))
		{
			ClanService.ReplyHelp(ctx);
			return;
		}

		ClanService.LeaveClan(ctx, playerName);
	}
}
