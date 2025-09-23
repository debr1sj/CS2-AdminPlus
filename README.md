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

### 🔗 Dependencies

AdminPlus requires the following plugins to be installed:

- **[MenuManagerCS2](https://github.com/NickFox007/MenuManagerCS2)**
- **[PlayerSettingsCS2](https://github.com/NickFox007/PlayerSettingsCS2)**
- **[AnyBaseLibCS2](https://github.com/NickFox007/AnyBaseLibCS2)**

> ⚠️ **Important**: All dependencies must be installed and running before AdminPlus can function properly.

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

## 🎮 Advanced Menu System

The plugin features a powerful interactive HTML menu system powered by [MenuManagerCS2](https://github.com/NickFox007/MenuManagerCS2), accessible via `css_admin`:

### 📋 Menu Categories
- **👥 Admin Management**: Add/remove admins with immunity levels and group management
- **🔨 Player Commands**: Ban, kick, mute, gag, slay, slap players with intuitive interface
- **🌍 Server Commands**: Change map, restart round, cleanup operations
- **🎯 Fun Commands**: Teleport, freeze, blind, drug effects, and visual modifications
- **🔫 Weapon Management**: Give weapons, strip weapons, and weapon control
- **⚡ Physics Control**: Noclip, godmode, speed, health, and movement modifications
- **💬 Communication**: Chat controls, announcements, and player messaging
- **🗳️ Voting System**: Map votes, kick votes, ban votes, and custom voting

### ✨ Menu Features
- **Responsive Design**: Works on all screen resolutions
- **Real-time Updates**: Live player information and status
- **Quick Actions**: One-click commands for common tasks
- **Permission-based**: Shows only commands you have access to
- **Search & Filter**: Find players quickly with search functionality

## 🌍 Localization

The plugin currently supports **English** language with customizable messages through translation files.

### 🚀 Multi-language Support Coming Soon!

We're working on adding support for multiple languages including:
- 🇹🇷 Turkish
- 🇩🇪 German  
- 🇪🇸 Spanish
- 🇫🇷 French
- 🇷🇺 Russian
- 🇦🇷 Arabic
- 🇮🇷 Farsi
- 🇱🇻 Latvian
- 🇵🇱 Polish
- 🇧🇷 Brazilian Portuguese
- 🇵🇹 Portuguese
- 🇨🇳 Chinese (Simplified)

### 🤝 Contribute Translations

**Want to help translate AdminPlus to your language?** 

If you'd like to contribute translations for your language or help improve existing ones, please contact us on Discord! We'd love to have your help in making AdminPlus accessible to players worldwide.

- **Discord**: debr1s
- **GitHub Issues**: [Create a translation request](https://github.com/debr1sj/CS2-AdminPlus/issues)

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

## 📋 Changelog

### 🎉 Version 1.0.1 (Latest)
- ✨ **MenuManager Integration**: Added advanced HTML menu system with MenuManager dependency
- 🏷️ **Prefix System**: Centralized prefix management from language files
- 🐛 **Fixed hrespawn**: Players now respawn at their last death position instead of team spawn
- 🧹 **Code Cleanup**: Removed unused reservation status command
- 📝 **Translation Updates**: Cleaned up language files

## 🗺️ Roadmap

### 🚀 Upcoming Features (v1.1.0)
- 🌍 **Multi-language Support**: Complete translation system with 12+ languages
- 🎮 **Enhanced Menu System**: More interactive features and improved UI
- 📊 **Advanced Statistics**: Player statistics and admin activity tracking
- 🔧 **Configuration System**: Web-based configuration panel

### 🎯 Future Versions
- **v1.2.0**: Advanced reporting and analytics
- **v1.3.0**: Plugin API for third-party integrations
- **v2.0.0**: Complete rewrite with modern architecture

## 🆘 Support

### 🐛 **Bug Reports**
If you encounter any issues or bugs:
1. Check the [GitHub Issues](https://github.com/debr1sj/CS2-AdminPlus/issues) first
2. Create a new issue with:
   - **Plugin version**: AdminPlus v1.0.1
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
