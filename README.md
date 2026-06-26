# PlayerServices

Server-side V Rising utility mod that adds player services such as player info, starter/daily kits, give sets, teleport points, aura purchases, name change, blacklist/whitelist, and admin tools.

## Features

- **Admin Tools**
  - Observe mode and player tracking.
  - Force clan create, join, and leave commands.
  - Potion buff tool.
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
  - Admins can configure whether clan castle information and offline clan member last online times are shown to players.
  - Admins can always see full .pis information, including clan data, current level, highest level reached, castle ownership, online clan members, and the last online time of offline members.

- **Blacklist Players**
  - Designed for public servers.
  - Add players to the blacklist to prevent them from connecting to the server.
  - All other players can connect normally.

- **Whitelist System**
  - Designed for private servers only.
  - When whitelist-only mode is enabled, only players marked as whitelisted are allowed to connect to the server.
  - You can pre-register players using the command: `.pls addwhitelist <SteamID> <KnownAs>`.
  - Blacklisted players are always blocked, even if they match the whitelist requirement.

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

#### Player Info
- `.pis <player>`
  - Show player information.
  - Example: *.pis Del*
  
#### Starter Kit
- `.pls starterkit`
  - Claim your one-time starter kit if auto-grant fails.
  - Shortcut: *.pls sk*

- `.pls addstarterkit <prefabGuid> <quantity>` 🔒 *Admin only*
  - Add or update an item in the starter kit.
  - Shortcut: *.pls ask <prefabGuid> <quantity>*
  - Example: *.pls ask 800879747 1*

- `.pls removestarterkit <prefabGuid>` 🔒 *Admin only*
  - Remove an item from the starter kit.
  - Shortcut: *.pls rsk <prefabGuid>*
  - Example: *.pls rsk 800879747*

- `.pls liststarterkit` 🔒 *Admin only*
  - Show all starter kit items.
  - Shortcut: *.pls lsk*

#### Daily Kit
- `.pls dailykit`
  - Claim your daily kit.
  - Shortcut: *.pls dk*
  
- `.pls adddailykit <prefabGuid> <quantity>` 🔒 *Admin only*
  - Add or update an item in the daily kit.
  - Shortcut: *.pls adk <prefabGuid> <quantity>*
  - Example: *.pls adk 800879747 1*

- `.pls removedailykit <prefabGuid>` 🔒 *Admin only*
  - Remove an item from the daily kit.
  - Shortcut: *.pls rdk <prefabGuid>*
  - Example: *.pls rdk 800879747*

- `.pls listdailykit` 🔒 *Admin only*
  - Show all daily kit items.
  - Shortcut: *.pls ldk*

#### Teleport
- `.pls tp <slot>`
  - Teleport to a saved teleport point.
  - Non-admin players can only use teleport points that are not admin-only.
  - Example: *.pls tp 1*

- `.pls helptp`
  - Show teleport point commands and feature status.
  - Shortcut: *.pls htp*

- `.pls addtp <slot> [adminOnly] [description]` 🔒 *Admin only*
  - Add a teleport point at your current position.
  - Shortcut: *.pls atp <slot> [adminOnly] [description]*
  - Example: *.pls atp 1 false "Admin shop"*

- `.pls removetp <slot>` 🔒 *Admin only*
  - Remove a teleport point.
  - Shortcut: *.pls rtp <slot>*
  - Example: *.pls rtp 1*

- `.pls listtp` 🔒 *Admin only*
  - List all teleport points.
  - Shortcut: *.pls ltp*

#### Change Name
- `.changename to <NewName>`
  - Change your character name.
  - Shortcut: *.cn to <NewName>*
  - Example: *.cn to Led*

- `.changename help`
  - Show change name help and cost.
  - Shortcut: *.cn help*
  
- `.changename player <currentName> <newName>` 🔒 *Admin only*
  - Change another player's character name.
  - Shortcut: *.cn player <currentName> <newName>*
  - Example: *.cn player Del Led*

- `.changename testwebhook` 🔒 *Admin only*
  - Send a test message to Discord.
  - Shortcut: *.cn tw*

#### Aura
- `.aura on <id>`
  - Turn on an owned aura.
  - Example: *.aura on 1*

- `.aura on all`
  - Turn on all owned auras.

- `.aura off <id>`
  - Turn off an owned aura.
  - Example: *.aura off 1*

- `.aura off all`
  - Turn off all owned auras.

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

- `.aura add <player> <id>` 🔒 *Admin only*
  - Grant an aura to a player.
  - Example: *.aura add Del 1*

- `.aura remove <player> <id>` 🔒 *Admin only*
  - Remove an aura from a player.
  - Example: *.aura remove Del 1*

### Admin Commands

#### General Admin 🔒 *Admin only*
- `.pls observe`
  - Toggle admin observe mode.
  - Shortcut: *.pls ob*

- `.pls track <player>`
  - Continuously track a player while in observe mode.
  - Shortcut: *.pls tr <player>*
  - Example: *.pls tr Del*

- `.pls untrack`
  - Stop tracking the current player.
  - Shortcut: *.pls utr*
  - Example: *.pls utr*

- `.pls buff <player>`
  - Apply Potion Buffs to a target player or yourself.
  - Shortcut: *.pls bf <player>*
  - Example: *.pls bf Del*

- `.pls reload`
  - Reload all PlayerServices configs.
  - Shortcut: *.pls rl*

#### Player Profiles 🔒 *Admin only*
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

#### Blacklist 🔒 *Admin only*
- `.pls addblacklist <Name/SteamID>`
  - Add a player to the blacklist and kick them.
  - Shortcut: *.pls abl <Name/SteamID>*
  - Example: *.pls abl Del*

- `.pls removeblacklist <Name/SteamID>`
  - Remove a player from the blacklist.
  - Shortcut: *.pls rbl <Name/SteamID>*
  - Example: *.pls rbl Del*

- `.pls showblacklist`
  - Show all blacklisted players.
  - Shortcut: *.pls sbl*

- `.pls helpblacklist`
  - Show blacklist commands.
  - Shortcut: *.pls hbl*

#### Whitelist 🔒 *Admin only*
- `.pls addwhitelist <SteamID> <KnownAs>`
  - Pre-register a SteamID and Known As to the whitelist.
  - Shortcut: *.pls awl <SteamID> <KnownAs>*
  - Example: *.pls awl 1234567890 Del*

- `.pls removewhitelist <SteamID/KnownAs>`
  - Remove a player from the whitelist.
  - This removes whitelist access but keeps the player's profile and Known As.
  - Shortcut: *.pls rwl <SteamID/KnownAs>*
  - Example: *.pls rwl Del*

- `.pls showwhitelist`
  - Show all whitelisted players.
  - Shortcut: *.pls swl*

- `.pls helpwhitelist`
  - Show whitelist commands and feature status.
  - Shortcut: *.pls hwl*

#### Give Sets 🔒 *Admin only*
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

- `.pls addgive <setName> <itemPrefab> <quantity>`
  - Add an item to a give set.
  - Shortcut: *.pls ag <setName> <itemPrefab> <quantity>*
  - Example: *.pls ag potion 800879747 1*

- `.pls removegive <setName>`
  - Remove a give set.
  - Shortcut: *.pls rg <setName>*
  - Example: *.pls rg potion*

- `.pls listgive`
  - List all give sets.
  - Shortcut: *.pls lg*

- `.pls helpgive`
  - Show give command help.
  - Shortcut: *.pls hg*

#### Clan 🔒 *Admin only*
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
- **PlayerInformations**: Enable/disable the `.pis` command, configure whether clan castle information is shown, and configure whether last online time is shown for offline clan members.
- **StarterKit**: Enable/disable starter kits and configure starter kit items.
- **DailyKit**: Enable/disable daily kits and configure daily kit items.
- **ChangeName**: Enable/disable name changes, configure currency cost, broadcasts, and Discord webhook messages.
- **Whitelist**: Enable/disable whitelist-only mode.
- **WelcomeMessage**: Enable/disable welcome messages and configure messages sent when players connect.
- **Aura**: Enable/disable aura system, configure aura prefab GUIDs, aura costs, currencies, and broadcast messages.
- **Teleport**: Enable/disable player teleport and configure teleport delay.

### player_data.json
This file stores player profile data.

It may include:
- **SteamID**
- **in-game name**
- **Known As**
- **Current level**
- **Max level**
- **Castle region**
- **Last online time**
- **Last DailyKit claim**
- **StarterKit claim**
- **Blacklist status**
- **Whitelist status**
- **Aura ownership and active states**

Do not edit `player_data.json` unless you know exactly what you are doing.

## Security Notes
- Keep `player_data.json` private. It contains SteamIDs and server profile data.
- Keep `ChangeNameWebhookUrl` private. Discord webhook URLs should be treated as secrets.
- Only trusted server staff should have access to the config folder.
- Do not upload real server config, player data, logs, or webhook URLs publicly.

## Credits
- [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) by **odjit** for the original codebase and architecture that inspired this mod.
- [Bloodcraft](https://thunderstore.io/c/v-rising/p/zfolmt/Bloodcraft/) by **zfolmt** for the original PlayerConnectionPatch implementation.
- [RaidForge](https://thunderstore.io/c/v-rising/p/Darrean/RaidForge/) by **Darrean** for the Raid Time checking logic.
- [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9)

## License
This project is licensed under the AGPL-3.0 license.

## Notes
> - This mod was originally developed for my own server and built on top of [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/). Special thanks to **odjit** for the amazing mod and inspiration behind this project.
> - If you have any problems or run into bugs, please report them to me in the [V Rising Modding Community](https://discord.com/invite/QG2FmueAG9).
> **Del** (delta_663)
