# 💰 [KNZ] Economy System for CS2

![Version](https://img.shields.io/badge/version-26.0.0--ThreadSafe--Final-purple)
![License](https://img.shields.io/badge/license-GPL--v3-blue)
![Framework](https://img.shields.io/badge/Framework-CounterStrikeSharp-orange)

A high-performance, **thread-safe** economy and rewards system for Counter-Strike 2. This plugin handles player credits, multi-layered rewards, and administrative tools with seamless MySQL integration.

---

## ✨ Key Features

* **🎯 Advanced Reward Logic**: Earn credits for kills, assists, and specialized actions like `NoScope`, `Wallbang`, `Smoke kills`, and `Flashed kills`.
* **⏰ Playtime Incentives**: Automatically rewards players for every 15 minutes of active playtime.
* **📈 Multiplier System**: 
    * **Group Multipliers**: Assign higher reward rates to specific CSS flags (VIPs/Admins).
    * **Name Advertising**: Give a bonus multiplier (e.g., `1.5x`) to players who have your community tag in their Steam name.
* **🛠️ Database Stability**: Thread-safe operations ensure zero server lag when saving or loading data from MySQL.
* **📡 Discord Integration**: Full logging for admin actions and player transfers via Discord Webhooks.
* **🌍 Localization Support**: Custom chat messages through the `lang` folder.

---

## 🛠️ Requirements

* [CounterStrikeSharp](https://github.com/rokk0/CounterStrikeSharp) (Latest Version)
* MySQL / MariaDB Server
* [CS2-UserSystem](https://github.com/xKnz1337/cs2-UserSystem) // Without this it doesn't work as it identifies players based on their userid.

---

## 📋 Commands

### 👤 Player Commands
| Command | Description |
| :--- | :--- |
| `css_credits` | Check your current credit balance. |
| `css_credits <name>` | View another player's credit balance. |
| `css_transfer <name> <amount>` | Securely transfer credits to another player. |
| `css_topcredits` | Display the top 10 richest players on the server. |

### 🛡️ Admin Commands (Default flag: `@css/rcon`)
| Command | Description |
| :--- | :--- |
| `css_givecredits <name/@all> <amount>` | Grant credits to a specific player or the entire server. |
| `css_takecredits <name> <amount>` | Remove credits from a player. |
| `css_offcredits <userid>` | Query a player's balance directly from the DB using their Unique ID. |

---

## ⚙️ Configuration

The configuration file (`KNZEconomySystem.json`) is generated automatically on the first run. You can customize:
* **MySQL Settings**: Host, User, Password, Port, and Database.
* **Rewards**: Define values for kills, assists, and skill shots.
* **Multipliers**: Set custom percentages for specific flags and name ads.
* **Discord Webhook**: Add your URL to enable live logging.

---

## ⚡ BE AWARE !

* **The `lang` folder is not generating itself**, it has to be manually placed in the plugin directory for messages to display.
* **The most important note**: The plugin is made entirely with AI. I only know how to "talk" to AI; I don't have any idea how to do this manually, BUT I gave it a try and it worked. 
* **If you like the idea** and are an actual developer, you are more than welcome to use this code as a base, but it **must remain public to everyone** under the **GPL v3 license**.
* **If you would like to help me** with this project (where I make multiple plugins strictly with AI), you are free to leave suggestions on **Discord: .kenzo1337**.
