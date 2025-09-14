# AdminPlus - CounterStrikeSharp Admin Plugin

[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API-blue.svg)](https://github.com/roflmuffin/CounterStrikeSharp)

> ⚠️ **Important Notice**: If you are using other admin plugins or AdminList plugins, conflicts may occur and cause errors. I am continuously updating the plugin and waiting for your bug reports. The first version will definitely have bugs, but they will be resolved over time.

Advanced CounterStrikeSharp admin plugin with comprehensive features: ban/kick system, interactive HTML menus, voting system, fun commands, communication control, and reservation system. No database required - file-based storage, easy setup.

## ✨ Features

- 🔨 **Ban System**: SteamID & IP bans with temporary/permanent options
- 👥 **Admin Management**: Add/remove admins with immunity levels
- 💬 **Communication Control**: Mute, gag, and silence players
- 🗳️ **Voting System**: Map votes, kick votes, ban votes, and custom votes
- 🎮 **Interactive Menus**: HTML-based admin menus for easy management
- 🎯 **Fun Commands**: Teleport, freeze, blind, drug effects, and more
- 📢 **Chat Commands**: Admin say, center say, HUD messages
- 🔒 **Reservation System**: Admin priority slots and player management
- 🌍 **Multi-language**: English support
- 📝 **Comprehensive Logging**: All actions logged to files

## 📋 Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) framework
- [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) plugin

## 🚀 Installation

1. Download the latest release
2. Extract all files from the zip to `csgo/addons/counterstrikesharp/plugins/`
3. Copy language files to `csgo/addons/counterstrikesharp/AdminPlus/lang/`
4. Restart your server

## ⚙️ Configuration

The plugin uses file-based storage:
- `csgo/addons/counterstrikesharp/configs/admins.json` - Admin permissions
- `csgo/cfg/banned_user.cfg` - SteamID bans
- `csgo/cfg/banned_ip.cfg` - IP bans
- `csgo/addons/counterstrikesharp/plugins/AdminPlus/communication_data.json` - Mute/gag data

## 📖 Commands

### 🔨 Ban System Commands
```
css_ban <target> [duration] [reason]        # Ban a player temporarily or permanently [@css/ban]
css_ipban <target> [reason]                 # IP ban a player [@css/ban]
css_unban <steamid/ip>                      # Remove ban from player [@css/unban]
css_lastban                                 # Show recently disconnected players for banning [@css/ban]
css_baninfo <steamid/ip>                    # Get ban information [@css/ban]
css_cleanbans                               # Clear all bans [@css/root]
css_cleanipbans                             # Clear all IP bans [@css/root]
css_cleansteambans                          # Clear all SteamID bans [@css/root]
css_banlist                                 # Show ban list menu [@css/ban]
```

### 👥 Admin Management Commands
```
css_addadmin <steamid64> <group> <immunity>  # Add new admin [@css/root]
css_removeadmin <steamid64>                  # Remove admin [@css/root]
css_adminlist                                # List all admins [@css/root]
css_admins                                   # Show online admins [All players]
```

### 💬 Communication Commands
```
css_mute <target> [duration] [reason]       # Mute player voice [@css/chat]
css_unmute <target>                         # Unmute player [@css/chat]
css_gag <target> [duration] [reason]        # Gag player chat [@css/chat]
css_ungag <target>                          # Ungag player [@css/chat]
css_silence <target> [duration] [reason]    # Mute + gag player [@css/chat]
css_unsilence <target>                      # Remove mute + gag [@css/chat]
css_mutelist                                # Show muted players [@css/chat]
css_gaglist                                 # Show gagged players [@css/chat]
css_cleanexpired                            # Clean expired punishments [@css/root]
css_cleanall                                # Clean all punishments [@css/root]
css_cleanmute                               # Clean all mute records [@css/root]
css_cleangag                                # Clean all gag records [@css/root]
```

### 🎮 Player Commands
```
css_kick <target> [reason]                   # Kick player from server [@css/generic]
css_slay <target>                            # Kill player instantly [@css/slay]
css_slap <target> [damage]                   # Slap player with damage [@css/slay]
css_rename <target> <new_name>               # Rename player [@css/slay]
css_money <target> <amount>                  # Set player money [@css/slay]
css_armor <target> <amount>                  # Set player armor [@css/slay]
css_team <target> <t/ct/spec>                # Change player team [@css/kick]
css_swap <target>                            # Swap player to opposite team [@css/kick]
```

### 🎯 Fun Commands
```
css_freeze <target> [seconds]                # Freeze player movement [@css/slay]
css_unfreeze <target>                        # Unfreeze player [@css/slay]
css_beacon <target> <0|1>                    # Toggle beacon on player [@css/slay]
css_blind <target> <seconds>                 # Blind player [@css/slay]
css_unblind <target>                         # Unblind player [@css/slay]
css_drug <target> <seconds>                  # Apply drug effect [@css/slay]
css_undrug <target>                          # Remove drug effect [@css/slay]
css_shake <target> <seconds>                 # Shake player screen [@css/slay]
css_unshake <target>                         # Stop screen shake [@css/slay]
css_glow <target> <color>                    # Make player glow [@css/slay]
css_color <target> <color>                   # Change player color [@css/slay]
css_bury <target>                            # Bury player underground [@css/slay]
css_unbury <target>                          # Unbury player [@css/slay]
css_gravity <value>                          # Change server gravity [@css/slay]
css_clean                                    # Remove all weapons from ground [@css/slay]
```

### 🎮 Player Control Commands
```
css_goto <target>                            # Teleport to player [@css/slay]
css_bring <target>                           # Bring player to you [@css/slay]
css_hrespawn <target>                        # Respawn player at last position [@css/slay]
css_respawn <target>                         # Respawn dead player [@css/cheats]
css_noclip <target> <0|1>                    # Toggle noclip [@css/cheats]
css_god <target> <0|1>                       # Toggle godmode [@css/cheats]
css_speed <target> <multiplier>              # Change player speed [@css/cheats]
css_unspeed <target>                         # Reset player speed [@css/cheats]
css_hp <target> <health>                     # Set player health [@css/cheats]
css_sethp <team> <health>                    # Set team default health [@css/cheats]
```

### 🔫 Weapon Commands
```
css_weapon <target> <weapon>                 # Give weapon to player [@css/cheats]
css_strip <target> [filter]                  # Remove weapons from player [@css/cheats]
```

### 🌍 Server Commands
```
css_map <map>                                # Change map [@css/generic]
css_wsmap <workshop_id>                      # Change workshop map [@css/generic]
css_rcon <command>                           # Execute RCON command [@css/root]
css_cvar <cvar> [value]                      # Get/set cvar value [@css/generic]
css_who                                      # Show player information [@css/generic]
css_rr                                       # Restart current round [@css/generic]
css_players                                  # List all players in console [@css/root]
```

### 📢 Chat Commands
```
css_asay <message>                           # Admin-only message [@css/chat]
css_csay <message>                           # Center message to all [@css/chat]
css_hsay <message>                           # HUD message to all [@css/chat]
css_psay <target> <message>                  # Private message [@css/chat]
css_say <message>                            # Say to all players [@css/chat]
```

### 🗳️ Voting Commands
```
css_vote <question> <option1> <option2> [options...]  # Create custom vote [@css/generic]
css_votemap <map1> <map2> [maps...]            # Map vote [@css/generic]
css_votekick <target>                          # Kick vote [@css/generic]
css_voteban <target>                           # Ban vote [@css/generic]
css_votemute <target>                          # Mute vote [@css/generic]
css_votegag <target>                           # Gag vote [@css/generic]
css_votesilence <target>                       # Silence vote [@css/generic]
css_rvote                                     # Revote [Player only]
css_cancelvote                                # Cancel active vote [@css/generic]
```

### 🎮 Menu Commands
```
css_admin                                    # Open admin menu [@css/ban]
```

### 📚 Help Commands
```
css_adminhelp                                # Show detailed command help [@css/generic]
```

## 🎮 Menu System

The plugin features interactive HTML menus accessible via `css_admin`:

- **Admin Management**: Add/remove admins with immunity levels
- **Player Commands**: Ban, kick, mute, gag, slay, slap players
- **Server Commands**: Change map, restart round, cleanup
- **Fun Commands**: Teleport, freeze, blind, drug effects
- **Weapon Management**: Give weapons, strip weapons
- **Physics Control**: Noclip, godmode, speed, health

## 🔒 Reservation System

The plugin automatically manages admin slots with the following features:

- **Admin Priority**: Admins with `@css/admin` or `@css/reservation` permissions get priority access
- **Automatic Kicking**: When server is full, non-admin players are automatically kicked to make room for admins
- **Smart Selection**: Kicks players based on highest ping, longest connection time, or random selection
- **Slot Management**: Maximum 3 admin reservations at once
- **Auto-Cleanup**: Removes expired player data and invalid admin reservations

### **How it works:**
1. When an admin tries to join a full server, the system automatically kicks a non-admin player
2. Regular players are kicked when server reaches maximum capacity
3. Admin reservations are tracked and managed automatically
4. The system respects admin immunity levels

### **Requirements:**
- Admin permissions: `@css/admin` or `@css/reservation`
- Server must be configured with proper max player settings

## 🌍 Localization

The plugin supports English language with customizable messages through translation files.

## 🔒 Permission System

The plugin uses CounterStrikeSharp's permission system:

- `@css/root` - Full access to all commands
- `@css/admin` - Admin-level access
- `@css/ban` - Ban-related commands
- `@css/chat` - Communication commands
- `@css/generic` - Basic admin commands
- `@css/slay` - Fun and punishment commands
- `@css/kick` - Player management commands
- `@css/cheats` - Cheat-related commands
- `@css/unban` - Unban commands
- `@css/reservation` - Reservation system access

## 📝 Logging

All admin actions are logged to:
- `csgo/addons/counterstrikesharp/logs/adminplus.log`

Logs include timestamp, admin name, action performed, target, and reason.

## 🛠️ Target Selection

Most commands support flexible target selection:

- **Player Name**: `PlayerName` or partial name
- **User ID**: `#1`, `#2`, etc.
- **SteamID**: Full SteamID64
- **Team Targets**: `@t` (terrorist), `@ct` (counter-terrorist), `@spec` (spectator), `@all` (all players)

## 🎯 Map Aliases

Quick map access with aliases:
- `mirage`, `vertigo`, `inferno`, `nuke`, `overpass`, `ancient`, `dust2`, `anubis`, `train`

## 🤝 Contributing

1. Fork the [repository](https://github.com/debr1sj/CS2-AdminPlus)
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## 🗺️ Roadmap

### 🚀 Upcoming Features
- [ ] **Advanced Menu System**: Integrate advanced menu systems

### 🎯 Future Versions
- **v1.1.0**: Advanced Menu System

## 🆘 Support

### 🐛 **Bug Reports**
If you encounter any issues or bugs:
1. Check the [GitHub Issues](https://github.com/debr1sj/CS2-AdminPlus/issues) first
2. Create a new issue with:
   - **Plugin version**: AdminPlus v1.0.0
   - **CounterStrikeSharp version**: Your CSS version
   - **Error logs**: Any console errors
   - **Steps to reproduce**: How to trigger the bug
   - **Expected behavior**: What should happen
   - **Actual behavior**: What actually happens

### 💬 **Contact & Support**
- **GitHub Issues**: [Create an issue](https://github.com/debr1sj/CS2-AdminPlus/issues)
- **Discord**: debr1s
- **Documentation**: Check this README for common solutions

### 🔧 **Common Issues**
- **Plugin not loading**: Check CounterStrikeSharp installation
- **Commands not working**: Verify admin permissions in `admins.json`
- **Language files**: Ensure `lang/en.json` is in correct directory

---

**AdminPlus** - Professional admin management for Counter-Strike 2 servers.

💡 **Note**: This plugin includes many advanced features not listed here, such as automatic notifications when banned players try to reconnect with different accounts, and many other smart admin tools. Check the full command list above to discover all capabilities!
