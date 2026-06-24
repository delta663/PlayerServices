# PlayerServices

**PlayerServices** is a server-side V Rising mod that combines player utility features, admin tools, player profile tracking, starter and daily kits, give sets, teleport points, aura purchases, name changes, blacklist/whitelist management, and Discord webhook support.

## Features

- **Admin Tools**
  - Observe mode and player tracking.
  - Force clan create, join, and leave commands.
  - Stone Form apply/remove tools.
  - Potion buff apply tool.
  - Centralized config reload without restarting the server.

- **Aura System**
  - Players can buy aura buffs using configured currencies.
  - Players can preview an aura before buying it.
  - Players can enable or disable owned auras.
  - Admins can grant or remove auras from players.
  - [Examples of auras that can be used.](https://docs.google.com/spreadsheets/d/1SDk_sXa2gTPuOzP8ikWq-xD91dRlleRdaPqNNEIcwRU)

- **Change Name**
  - Players can change their character name for a configurable currency cost.
  - Supports optional in-game broadcasts and Discord webhook notifications.
  - Admins can rename players directly.

- **Player Profiles**
  - Tracks player SteamID, current in-game name, Known As name, level data, clan data, castle data, aura ownership, kit claim status, and last online time.
  - Supports admin lookup by player name, SteamID, or Known As.
  - Supports blacklist and whitelist management.

- **Player Information**
  - `.pis` command is an upgraded version of the popular `.pi` command that many PvP players are already familiar with.
  - Displays detailed player information, including clan data, current level, highest level reached, castle ownership, online clan members, and the last online time of offline members.

- **Blacklist Players**
  - Designed for public servers.
  - Add players to the blacklist to prevent them from connecting to the server.
  - All other players can connect normally.

- **Whitelist System**
  - Designed for private servers only.
  - Only players listed in `player_data.json` are allowed to connect to the server.
  - You can pre-register players using the command: `.pls whitelist <SteamID> <KnownAs>`.
  - Blacklisted players are always blocked, even if they exist in `player_data.json`.

- **Teleport Points**
  - Admins can create teleport slots.
  - Teleport points can be admin-only or usable by players.
  - Player teleport delay is configurable and will be cancelled when the player moves, enters combat, or enters blocked states.

- **Give Sets**
  - Give item sets to a specific player, players in a radius, or online clan members.
  - Supports multipliers when giving sets.
  - Admins can add, remove, and list give set items.

- **Starter Kit**
  - Automatically grants a one-time starter kit to newly created characters.
  - Players can manually claim it if auto-grant fails.
  - Admins can add, remove, and list starter kit items.

- **Daily Kit**
  - Lets players claim a daily kit once per day.
  - Admins can add, remove, and list daily kit items.

- **Welcome Message**
  - Sends configurable welcome messages when players connect.

- **Logging**
  - Records kit claims, give actions, rename actions, and aura purchases in CSV log files.

## Requirements
1. [BepInEx 1.733.2](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/)
2. [VampireCommandFramework 0.11.0](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/)

## Installation
1. Install the required dependencies.
2. Place `PlayerServices.dll` into your server's BepInEx plugins folder.
3. Start the server once to generate the config files.
4. Edit the config files as needed.
5. Restart the server or use `.pls reload`.

## Commands

**Most commands use the `.pls` prefix, which stands for `PlayerServices`. But if it's easier, just remember it as "please". The server won't actually be more cooperative, but it feels more polite.**

### Player Commands

#### Starter Kit
- `.pls starterkit`
  - Claim your one-time starter kit.
  - Shortcut: *.pls sk*

#### Daily Kit
- `.pls dailykit`
  - Claim your daily kit.
  - Shortcut: *.pls dk*

#### Teleport
- `.pls tp <slot>`
  - Teleport to a saved teleport point.
  - Non-admin players can only use teleport points that are not admin-only.
  - Example: *.pls tp 1*

#### Player Info
- `.pis <player>`
  - Show player information.
  - Example: *.pis Del*

#### Change Name
- `.changename to <NewName>`
  - Change your character name.
  - Shortcut: *.cn to <NewName>*
  - Example: *.cn to Led*

- `.changename help`
  - Show change name help and cost.
  - Shortcut: *.cn help*

#### Aura
- `.aura on <id>`
  - Enable an owned aura.
  - Example: *.aura on 1*

- `.aura on all`
  - Enable all owned auras.

- `.aura off <id>`
  - Disable an owned aura.
  - Example: *.aura off 1*

- `.aura off all`
  - Disable all owned auras.

- `.aura preview <id>`
  - Preview an aura for a short time before buying it.
  - Example: *.aura preview 1*

- `.aura buy <id>` or `.buy aura <id>`
  - Buy an aura.
  - Example: *.aura buy 1*

- `.aura list`
  - Show available auras, prices, and ownership status.

- `.aura help`
  - Show aura commands and feature status.

### Admin Commands

#### General Admin
- `.pls reload`
  - Reload all PlayerServices configs.
  - Shortcut: *.pls rl*

- `.pls observe`
  - Toggle admin observe mode.
  - Shortcut: *.pls ob*

- `.pls track <player>`
  - Track a player while in observe mode.
  - Shortcut: *.pls tr <player>*
  - Example: *.pls tr Del*

- `.pls untrack`
  - Stop tracking the current player.
  - Shortcut: *.pls utr*
  - Example: *.pls utr*

- `.pls buff <player>`
  - Apply potion buffs to a target player.
  - Shortcut: *.pls bf <player>*
  - Example: *.pls bf Del*

- `.pls stoneform <player>`
  - Apply Stone Form to a target player.
  - Shortcut: *.pls sf <player>*
  - Example: *.pls sf Del*

- `.pls stoneformradius <radius>`
  - Apply Stone Form to players within a radius.
  - Shortcut: *.pls sfr <radius>*
  - Example: *.pls sfr 20*

- `.pls restoneform <player>`
  - Remove Stone Form from a target player.
  - Shortcut: *.pls rsf <player>*
  - Example: *.pls rsf Del*

- `.pls restoneformradius <radius>`
  - Remove Stone Form from players within a radius.
  - Shortcut: *.pls rsfr <radius>*
  - Example: *.pls rsfr 20*

#### Player Profiles
- `.pls checkplayer <Name/SteamID>`
  - Check a player profile by current in-game name or SteamID.
  - Shortcut: *.pls cp <Name/SteamID>*
  - Example: *.pls cp Del*

- `.pls checkknownas <KnownAs>`
  - Find player profiles by Known As.
  - Shortcut: *.pls cka <KnownAs>*
  - Example: *.pls cka Del*

- `.pls addknownas <Name/SteamID> <KnownAs>`
  - Set a player's Known As.
  - Shortcut: *.pls aka <Name/SteamID> <KnownAs>*
  - Example: *.pls aka 1234567890 Del*

- `.pls removeknownas <Name/SteamID>`
  - Remove Known As from a player profile.
  - Shortcut: *.pls rka <Name/SteamID>*
  - Example: *.pls rka 1234567890*

#### Blacklist & Whitelist
- `.pls blacklist <Name/SteamID>`
  - Add a player to the blacklist.
  - Shortcut: *.pls bl <Name/SteamID>*
  - Example: *.pls bl Del*

- `.pls unblacklist <Name/SteamID>`
  - Remove a player from the blacklist.
  - Shortcut: *.pls ubl <Name/SteamID>*
  - Example: *.pls ubl Del*

- `.pls showblacklist`
  - Show all blacklisted players.
  - Shortcut: *.pls sbl*

- `.pls whitelist <SteamID> <KnownAs>`
  - Pre-register a SteamID and Known As to the whitelist.
  - Shortcut: *.pls wl <SteamID> <KnownAs>*
  - Example: *.pls wl 1234567890 Del*

#### Starter Kit
- `.pls starterkitadd <prefabGuid> <quantity>`
  - Add or update an item in the starter kit.
  - Shortcut: *.pls ska <prefabGuid> <quantity>*
  - Example: *.pls ska 800879747 1*

- `.pls starterkitremove <prefabGuid>`
  - Remove an item from the starter kit.
  - Shortcut: *.pls skrm <prefabGuid>*
  - Example: *.pls skrm 800879747*

- `.pls starterkitlist`
  - Show all starter kit items.
  - Shortcut: *.pls skl*

#### Daily Kit
- `.pls dailykitadd <prefabGuid> <quantity>`
  - Add or update an item in the daily kit.
  - Shortcut: *.pls dka <prefabGuid> <quantity>*
  - Example: *.pls dka 800879747 1*

- `.pls dailykitremove <prefabGuid>`
  - Remove an item from the daily kit.
  - Shortcut: *.pls dkrm <prefabGuid>*
  - Example: *.pls dkrm 800879747*

- `.pls dailykitlist`
  - Show all daily kit items.
  - Shortcut: *.pls dkl*

#### Give Sets
- `.pls give <player> <setName> [multiplier]`
  - Give a set to a specific player.
  - Shortcut: *.pls g <player> <setName> [multiplier]*
  - Example: *.pls g Del potion*

- `.pls giveradius <radius> <setName> [multiplier]`
  - Give a set to players within a radius.
  - Shortcut: *.pls gr <radius> <setName> [multiplier]*
  - Example: *.pls gr 20 potion*

- `.pls giveclan <player> <setName> [multiplier]`
  - Give a set to all online members of a player's clan.
  - Shortcut: *.pls gc <player> <setName> [multiplier]*
  - Example: *.pls gc Del potion*

- `.pls giveadd <setName> <itemPrefab> <quantity>`
  - Add an item to a give set.
  - Shortcut: *.pls ga <setName> <itemPrefab> <quantity>*
  - Example: *.pls ga potion 800879747 1*

- `.pls giveremove <setName>`
  - Remove a give set.
  - Shortcut: *.pls grm <setName>*
  - Example: *.pls grm potion*

- `.pls givelist`
  - List all give sets.
  - Shortcut: *.pls gl*

- `.pls givehelp`
  - Show give command help.
  - Shortcut: *.pls gh*

#### Teleport Point
- `.pls tpadd <slot> [adminOnly] [description]`
  - Add a teleport point at your current position.
  - Shortcut: *.pls tpa <slot> [adminOnly] [description]*
  - Example: *.pls tpadd 1 false "Admin shop"*

- `.pls tpremove <slot>`
  - Remove a teleport point.
  - Shortcut: *.pls tprm <slot>*
  - Example: *.pls tprm 1*

- `.pls tplist`
  - List all teleport points.
  - Shortcut: *.pls tpl*

#### Aura
- `.aura add <player> <id>`
  - Grant an aura to a player.
  - Example: *.aura add Del 1*

- `.aura remove <player> <id>`
  - Remove an aura from a player.
  - Example: *.aura remove Del 1*

#### Change Name
- `.changename player <currentName> <newName>`
  - Change another player's character name.
  - Shortcut: *.cn player <currentName> <newName>*
  - Example: *.cn player Del Led*

- `.changename testwebhook`
  - Send a test message to Discord.
  - Shortcut: *.cn tw*

#### Clan
- `.clan forcecreate <player>`
  - Force a player to create their own clan.
  - Shortcut: *.c fc <player>*
  - Example: *.c fc Del*

- `.clan forcejoin <playerA> <playerB>`
  - Force two players into the same clan.
  - Shortcut: *.c fj <playerA> <playerB>*
  - Example: *.c fj Del Tha*

- `.clan forceleave <player>`
  - Force a player to leave their clan.
  - Shortcut: *.c fl <player>*
  - Example: *.c fl Del*

## Config Files
After the first server start, the following files will be created:
- `BepInEx/config/PlayerServices.cfg`
- `BepInEx/config/PlayerServices/player_data.json`
- `BepInEx/config/PlayerServices/gives.json`
- `BepInEx/config/PlayerServices/teleport_points.json`

### PlayerServices.cfg
This file contains the main feature toggles and settings.

Important sections include:
- `PlayerInformations`
  - Enable/disable the `.pis` command.
- `StarterKit`
  - Enable/disable starter kits and configure starter kit items.
- `DailyKit`
  - Enable/disable daily kits and configure daily kit items.
- `ChangeName`
  - Enable/disable name changes, configure currency cost, broadcasts, and Discord webhook messages.
- `Whitelist`
  - Enable/disable whitelist-only mode.
- `WelcomeMessage`
  - Enable/disable welcome messages and configure messages sent when players connect.
- `Aura`
  - Enable/disable aura system, Configure aura prefab GUIDs, aura costs, currencies, and broadcast messages.
- `Teleport`
  - Enable/disable player teleport and configure teleport delay.

### player_data.json
This file stores player profile data.

It may include:
- SteamID
- Current in-game name
- Known As
- Level data
- Clan data
- Castle region data
- Starter kit and daily kit state
- Aura ownership and active states
- Blacklist status
- Last online time

> Do not edit `player_data.json` unless you know exactly what you are doing.

## Security Notes
- Keep `player_data.json` private. It contains SteamIDs and server profile data.
- Keep `ChangeNameWebhookUrl` private. Discord webhook URLs should be treated as secrets.
- Only trusted server staff should have access to the config folder.
- Do not upload real server config, player data, logs, or webhook URLs publicly.

## Credits
- [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by **odjit** for the original code that inspired this mod.
- Special thanks to [**odjit**](https://thunderstore.io/c/v-rising/p/odjit/) and the [**V Rising Modding Community**](https://discord.com/invite/QG2FmueAG9).

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was first developed for my own server and was built to combine several small player-service features into one server-side utility mod.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9).
> **Del** (delta_663)
