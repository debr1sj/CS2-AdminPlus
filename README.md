# AdminPlus - CounterStrikeSharp Admin Plugin

[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API-blue.svg)](https://github.com/roflmuffin/CounterStrikeSharp)

> ⚠️ **Important Notice**: If you are using other admin plugins or AdminList plugins, conflicts may occur and cause errors. You should not use plugins like !calladmin and !report as they will conflict with AdminPlus. I am continuously updating the plugin and waiting for your bug reports.

Advanced CounterStrikeSharp admin plugin with comprehensive features: ban/kick system, easy menu system, voting system, fun commands, communication control, and reservation system. No database required - file-based storage, easy setup.

## ✨ Features

- 🔨 **Ban System**: SteamID & IP bans with temporary/permanent options
- 👥 **Admin Management**: Add/remove admins with immunity levels
- 💬 **Communication Control**: Mute, gag, and silence players
- 🗳️ **Voting System**: Map votes, kick votes, ban votes, and custom votes
- 🎮 **Interactive Menus**: Easy menu system for management
- 🎯 **Fun Commands**: Teleport, freeze, blind, drug effects, and more
- 📢 **Chat Commands**: Admin say, center say, HUD messages
- 🔒 **Reservation System**: Admin priority slots and player management
- 📊 **Report System**: Player-to-player reporting with Discord integration  
- 🌍 **Multi-language**: English, Turkish, French, Russian, German support
- 📝 **Advanced Logging**: All actions logged to files and 7 different Discord webhook channels
- 🔗 **Discord Integration**: Server status, ban logs, admin commands, communication logs, connection tracking, chat logs, and report system
- 🔔 **Smart Notifications**: Intelligent admin alerts based on action importance and type
- 🛡️ **Security & Performance**: Memory leak protection, input validation, and optimized performance

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
- `csgo/addons/counterstrikesharp/plugins/AdminPlus/discord_config.json` - Discord webhook configuration

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

### 📊 Report System Commands
```
css_report <target> <reason>                   # Report a player for admin review [All players]
css_calladmin <reason>                        # Call admin for help/assistance [All players]
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

### 📊 Report & Notification System
```
css_report <target> <reason>                   # Report a player [All players]
css_calladmin <reason>                        # Call admin assistance [All players]
```

### 📚 Help Commands
```
css_adminhelp                                # Show detailed command help [@css/generic]
```

## 🔗 Discord Configuration

Discord entegrasyonu için `csgo/addons/counterstrikesharp/plugins/AdminPlus/discord_config.json` dosyasını oluşturun:

```json
{
  "discordWebhooks": {
    "banWebhook": "https://discord.com/api/webhooks/0123456789/abc123def456",
    "adminActionsWebhook": "https://discord.com/api/webhooks/0123456789/xyz789ghi012", 
    "communicationWebhook": "https://discord.com/api/webhooks/0123456789/jkl345mno678",
    "serverStatusWebhook": "https://discord.com/api/webhooks/0123456789/pqr901stu234",
    "connectionLogsWebhook": "https://discord.com/api/webhooks/0123456789/vwx567yza890",
    "chatLogsWebhook": "https://discord.com/api/webhooks/0123456789/bcd123efg456",
    "reportAndCalladminWebhook": "https://discord.com/api/webhooks/0123456789/hij789klm012",
    "reportAndCalladminWebhookMentionUserId": "@everyone"
  }
}
```

**⚠️ Açıklama / Description:**
- 🇹🇷 Her webhook URL'nizi Discord Kanal Ayarları > Entegrasyonlar > Webhooks bölümünden kopyalayabilirsiniz.
- 🇺🇸 You can copy your webhook URLs from Discord Channel Settings > Integrations > Webhooks section.

#### 📊 Discord Webhook Kanal Bilgilendirmesi:
- **🔨 banWebhook**: 
  - 🇺🇸 Sends detailed information to your Discord channel for ban and unban operations
- **⚡ adminActionsWebhook**: 
  - 🇺🇸 Sends notifications to Discord channel for admin commands (kick, slay, teleport, etc.)
- **💬 communicationWebhook**: 
  - 🇺🇸 Sends logs to Discord channel for mute, gag, silence operations
- **🖥️ serverStatusWebhook**: 
  - 🇺🇸 Sends server status and player information to Discord channel
- **🔌 connectionLogsWebhook**: 
  - 🇺🇸 Sends player join/leave information to Discord channel
- **💭 chatLogsWebhook**: 
  - 🇺🇸 Sends in-game messages to Discord channel
- **📢 reportAndCalladminWebhook**: 
  - 🇺🇸 Sends notifications to your Discord channel for report and calladmin operations

### 📸 Discord Log Özellikleri

Check the images folder examples to see how your Discord logs will look:

#### 🖥️ **Server Status Logs**
![Server Status](/images/ServerStatus.png)
- 🇺🇸 Server status and player count

#### 🔨 **Ban Management Logs**
![Ban Logs](/images/Ban.png)
- 🇺🇸 Details of ban and unban operations

#### ⚡ **Admin Commands Logs**
![Admin Commands](/images/AdminCommand.png)
- 🇺🇸 Commands and operations used by admins

#### 💬 **Communication Logs**
![Communication Logs](/images/MuteGag.png)
- 🇺🇸 Mute, gag, silence operations

#### 🔌 **Connection & Disconnect Logs**
![Connection Disconnect](/images/ConnectionDisconnect.png)
- 🇺🇸 Player join/leave logs

#### 💭 **Chat Message Logs**
![Chat Logs](/images/ChatLog.png)
- 🇺🇸 In-game message logs

#### 📢 **Report & CallAdmin Logs**
![Report Logs](/images/CallReportLog.png)
- 🇺🇸 Player report and admin calling logs

## 🎮 Advanced Menu System

The plugin features a powerful easy menu system powered by [MenuManagerCS2](https://github.com/NickFox007/MenuManagerCS2), accessible via `css_admin`:

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

The plugin currently supports **English**, **Turkish**, **French**, **Russian**, and **German** languages with customizable messages through translation files.

### 🌍 Multi-language Support

Current language support:
- 🇺🇸 **English** - Primary language ✅
- 🇹🇷 **Turkish** - Full translation with modern color-coded messages ✅
- 🇫🇷 **French** - Full translation with modern color-coded messages (Thanks to felyjyn) ✅
- 🇷🇺 **Russian** - Full translation with modern color-coded messages added in v1.0.3 ✅
- 🇩🇪 **German** - Full translation with modern color-coded messages added in v1.0.3 ✅

### 🚀 More Languages Coming Soon!

We're working on adding support for additional languages:
- 🇪🇸 Spanish (Español)
- 🇦🇷 Arabic (العربية)
- 🇮🇷 Farsi (فارسی)
- 🇱🇻 Latvian (Latviešu)
- 🇵🇱 Polish (Polski)
- 🇧🇷 Brazilian Portuguese (Português Brasileiro)
- 🇵🇹 Portuguese (Português)
- 🇨🇳 Chinese (Simplified) (中文简体)

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

### 🎉 Version 1.0.3 (Latest)
- 📊 **NEW: Report System**: Player-to-player reporting with `/report` and `/calladmin` commands
- 🔗 **Advanced Discord Integration**: **7 diferent webhook types** for comprehensive logging:
  - 🔨 **Ban Logs**: Real-time ban/unban notifications
  - ⚡ **Admin Commands**: All admin command logs (slay, noclip, god, teleport, etc.)
  - 💬 **Communication Logs**: Mute, gag, silence operations logging
  - 🖥️ **Server Status**: Live server monitoring with player counts and uptime
  - 🔌 **Connection Logs**: Player join/leave activity tracking
  - 💭 **Chat Logs**: In-game message logging for moderation
  - 📢 **Reports & CallAdmin**: Player report notifications with mentions
- ☁️ **Advanced Machine Integration**: Automatic Discord connection backup and failover protection
- 🌍 **Language Support**: Russian 🇷🇺 and German 🇩🇪 translations added
- 🛡️ **Security Enhancements**: Advanced input validation and memory leak protection
- ⚡ **Performance Improvements**: Code cleanup, debug log removal, and memory optimization
- 🔧 **Bug Fixes**: 
  - Fixed map re-opening issue on first map changes
  - Enhanced command improvements and user experience
  - Memory leak protection and garbage collection cleanup
- 🎯 **Menu Integration**: Report commands integrated into admin menu system
- 🔒 **Cooldown System**: 3-minute report cooldown with custom messages
- 🔔 **Smart Notifications**: Intelligent admin mention system for reports
- 🧹 **Memory Management**: Complete cleanup of unused objects and optimized cache


## 🗺️ Roadmap

### 🚀 Upcoming Features (v1.0.4+)
- 🌍 **Expanded Multi-language Support**: Complete translation system with 12+ languages
- 🔧 **Code Improvements & Bug Fixes**: Performance optimizations, security patches, bug fixes, and stability enhancements

## 🆘 Support

### 🐛 **Bug Reports**
If you encounter any issues or bugs:
1. Check the [GitHub Issues](https://github.com/debr1sj/CS2-AdminPlus/issues) first
2. Create a new issue with:
   - **Plugin version**: AdminPlus v1.0.3
   - **CounterStrikeSharp version**: Your CSS version
   - **Error logs**: Any console errors
   - **Steps to reproduce**: How to trigger the bug
   - **Expected behavior**: What should happen
   - **Actual behavior**: What actually happens

### 💬 **Contact & Support**
- **GitHub Issues**: [Create an issue](https://github.com/debr1sj/CS2-AdminPlus/issues)
- **Discord**: debr1s
- **Steam Trade**: [🔗 Support me](https://steamcommunity.com/tradeoffer/new/?partner=888667064&token=hfAqp-37) - If you'd like to show your appreciation
- **Documentation**: Check this README for common solutions

### 🔧 **Common Issues**
- **Plugin not loading**: Check CounterStrikeSharp installation
- **Commands not working**: Verify admin permissions in `admins.json`
- **Language files**: Ensure `lang/en.json`, `tr.json`, `fr.json`, `de.json`, `ru.json` are in correct directory

---

**AdminPlus** - Professional admin management for Counter-Strike 2 servers.
