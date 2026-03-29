using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MySqlConnector;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Localization;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KNZEconomySystem;

public class RewardsConfig {
    public int Kill { get; set; } = 10;
    public int Assist { get; set; } = 5;
    public int Play15Min { get; set; } = 50;
    public int Wallbang { get; set; } = 15;
    public int Smoke { get; set; } = 20;
    public int Flash { get; set; } = 25;
    public int NoScope { get; set; } = 30;
}
public class MultipliersConfig {
    public Dictionary<string, float> Flags { get; set; } = new();
    public float NameMultiplier { get; set; } = 1.0f;
    public List<string> NameAds { get; set; } = new();
}
public class PluginConfig : BasePluginConfig {
    [JsonPropertyName("MySQL_Host")] public string Host { get; set; } = "localhost";
    [JsonPropertyName("MySQL_Database")] public string Database { get; set; } = "cs2";
    [JsonPropertyName("MySQL_User")] public string User { get; set; } = "root";
    [JsonPropertyName("MySQL_Password")] public string Password { get; set; } = "";
    [JsonPropertyName("MySQL_Port")] public int Port { get; set; } = 3306;
    [JsonPropertyName("UserSystemTable")] public string UserSystemTable { get; set; } = "user_system";
    [JsonPropertyName("EconomyTable")] public string EconomyTable { get; set; } = "user_credits";
    [JsonPropertyName("ChatPrefix")] public string ChatPrefix { get; set; } = "{purple}[KNZ] {default}";
    [JsonPropertyName("AdminFlag")] public string AdminFlag { get; set; } = "@css/rcon";
    [JsonPropertyName("DiscordWebhookURL")] public string WebhookUrl { get; set; } = "";
    public RewardsConfig Rewards { get; set; } = new();
    public MultipliersConfig Multipliers { get; set; } = new();
}

public class KNZEconomySystemPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "[KNZ] Economy System";
    public override string ModuleVersion => "1.0-Pre-Release";

    public PluginConfig Config { get; set; } = new();
    private string GetConnectionString() => $"Server={Config.Host};Port={Config.Port};Database={Config.Database};User Id={Config.User};Password={Config.Password};AllowUserVariables=True;Charset=utf8mb4;Connection Timeout=3;";
    
    private readonly Dictionary<int, int> _playerCredits = new(); 
    private readonly Dictionary<int, int> _playerUids = new(); 

    public void OnConfigParsed(PluginConfig config) { this.Config = config; }

    public override void Load(bool hotReload)
    {
        _ = Task.Run(async () => {
            try {
                using var conn = new MySqlConnection(GetConnectionString());
                await conn.OpenAsync();
                await new MySqlCommand($"CREATE TABLE IF NOT EXISTS `{Config.EconomyTable}` (userid INT PRIMARY KEY, name VARCHAR(128), credits INT DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;", conn).ExecuteNonQueryAsync();
            } catch (Exception ex) { Console.WriteLine($"[KNZ-Eco] DB CRITICAL: {ex.Message}"); }
        });

        AddTimer(900.0f, () => {
            foreach (var p in Utilities.GetPlayers().Where(x => x.IsValid && !x.IsBot && _playerCredits.ContainsKey(x.Slot)))
                AddCredits(p, Config.Rewards.Play15Min, "15 Min Playtime", true);
        }, TimerFlags.REPEAT);

        RegisterEventHandler<EventPlayerConnectFull>((ev, info) => {
            if (ev.Userid != null && ev.Userid.IsValid && !ev.Userid.IsBot) {
                var p = ev.Userid;
                AddTimer(12.0f, () => LoadPlayerData(p));
            }
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDisconnect>((ev, info) => {
            if (ev.Userid != null) { _playerCredits.Remove(ev.Userid.Slot); _playerUids.Remove(ev.Userid.Slot); }
            return HookResult.Continue;
        });

        RegisterEventHandler<EventPlayerDeath>((ev, info) => {
            if (ev?.Attacker == null || !ev.Attacker.IsValid || ev.Attacker.IsBot || ev.Attacker == ev.Userid) return HookResult.Continue;
            int total = Config.Rewards.Kill;
            List<string> r = new() { "Kill" };
            if (ev.Noscope) { total += Config.Rewards.NoScope; r.Add("NoScope"); }
            if (ev.Thrusmoke) { total += Config.Rewards.Smoke; r.Add("Smoke"); }
            if (ev.Attackerblind) { total += Config.Rewards.Flash; r.Add("Flash"); }
            if (ev.Penetrated > 0) { total += Config.Rewards.Wallbang; r.Add("Wallbang"); }
            AddCredits(ev.Attacker, total, string.Join(" + ", r), true);
            if (ev.Assister != null && ev.Assister.IsValid && !ev.Assister.IsBot) AddCredits(ev.Assister, Config.Rewards.Assist, "Assist", true);
            return HookResult.Continue;
        });
    }

    private void LoadPlayerData(CCSPlayerController player) {
        if (player == null || !player.IsValid || player.AuthorizedSteamID == null) return;
        string sid = player.AuthorizedSteamID.SteamId64.ToString();
        string name = player.PlayerName;
        int slot = player.Slot;

        _ = Task.Run(async () => {
            try {
                using var conn = new MySqlConnection(GetConnectionString());
                await conn.OpenAsync();
                string insQ = $"INSERT IGNORE INTO `{Config.EconomyTable}` (userid, name, credits) SELECT userid, @n, 0 FROM `{Config.UserSystemTable}` WHERE steamid64 = @s;";
                using var insCmd = new MySqlCommand(insQ, conn);
                insCmd.Parameters.AddWithValue("@n", name); insCmd.Parameters.AddWithValue("@s", sid);
                await insCmd.ExecuteNonQueryAsync();

                string selQ = $"SELECT e.userid, e.credits FROM `{Config.EconomyTable}` e JOIN `{Config.UserSystemTable}` u ON e.userid = u.userid WHERE u.steamid64 = @s LIMIT 1;";
                using var selCmd = new MySqlCommand(selQ, conn);
                selCmd.Parameters.AddWithValue("@s", sid);
                using var reader = await selCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync()) {
                    int uid = reader.GetInt32(0), cr = reader.GetInt32(1);
                    Server.NextFrame(() => { _playerUids[slot] = uid; _playerCredits[slot] = cr; });
                }
            } catch { }
        });
    }

    private string GetPlural(int amt) => Math.Abs(amt) == 1 ? "credit" : "credits";

    private void AddCredits(CCSPlayerController p, int baseAmt, string reason, bool applyMult) {
        if (p == null || !p.IsValid || !_playerUids.ContainsKey(p.Slot)) return;

        float m = 1.0f;
        string currentName = p.PlayerName;
        int slot = p.Slot;

        if (applyMult) {
            float fM = 1.0f;
            foreach (var f in Config.Multipliers.Flags) if (AdminManager.PlayerHasPermissions(p, f.Key)) fM = Math.Max(fM, f.Value);
            m = fM;
            if (Config.Multipliers.NameAds.Any(ad => currentName.Contains(ad, StringComparison.OrdinalIgnoreCase))) m *= Config.Multipliers.NameMultiplier;
        }

        int final = (int)(baseAmt * m);
        _playerCredits[slot] += final;
        int total = _playerCredits[slot], uid = _playerUids[slot];

        _ = Task.Run(async () => {
            try {
                using var conn = new MySqlConnection(GetConnectionString()); await conn.OpenAsync();
                var cmd = new MySqlCommand($"UPDATE `{Config.EconomyTable}` SET credits = @c, name = @n WHERE userid = @u", conn);
                cmd.Parameters.AddWithValue("@c", total); cmd.Parameters.AddWithValue("@n", currentName); cmd.Parameters.AddWithValue("@u", uid);
                await cmd.ExecuteNonQueryAsync();
                
                if (final > 0) Server.NextFrame(() => {
                    var pl = Utilities.GetPlayerFromSlot(slot);
                    if(pl != null && pl.IsValid) pl.PrintToChat(Msg("ReceivedCredits", final.ToString(), GetPlural(final), reason));
                });
            } catch { }
        });
    }

    [ConsoleCommand("css_offcredits")]
    public void OnCmdOff(CCSPlayerController? p, CommandInfo c) {
        if (p != null && !AdminManager.PlayerHasPermissions(p, Config.AdminFlag)) return;
        if (c.ArgCount < 2 || !int.TryParse(c.ArgByIndex(1), out int searchId)) { p?.PrintToChat(Msg("UsageOff")); return; }
        _ = Task.Run(async () => {
            try {
                using var conn = new MySqlConnection(GetConnectionString()); await conn.OpenAsync();
                var cmd = new MySqlCommand($"SELECT name, credits FROM `{Config.EconomyTable}` WHERE userid = @u", conn);
                cmd.Parameters.AddWithValue("@u", searchId);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) {
                    string n = r.GetString(0); int cr = r.GetInt32(1);
                    Server.NextFrame(() => p?.PrintToChat(Msg("OffFound", searchId.ToString(), n, cr.ToString("N0"), GetPlural(cr))));
                } else Server.NextFrame(() => p?.PrintToChat(Msg("OffNotFound", searchId.ToString())));
            } catch { }
        });
    }

    [ConsoleCommand("css_givecredits")]
    public void OnCmdGv(CCSPlayerController? p, CommandInfo c) {
        if (p != null && !AdminManager.PlayerHasPermissions(p, Config.AdminFlag)) return;
        if (c.ArgCount < 3 || !int.TryParse(c.ArgByIndex(2), out int amt)) { p?.PrintToChat(Msg("UsageGive")); return; }
        string aName = p?.PlayerName ?? "Console";
        if (c.ArgByIndex(1) == "@all") {
            foreach (var pl in Utilities.GetPlayers().Where(x => x.IsValid && !x.IsBot)) AddCredits(pl, amt, $"Admin: {aName}", false);
            Server.PrintToChatAll(Msg("GiveAllSuccess", aName, amt.ToString(), GetPlural(amt)));
            SendWebhook("📢 GIVE @ALL", $"Admin: **{aName}** a dat **{amt}** {GetPlural(amt)} tuturor.");
        } else {
            var t = Utilities.GetPlayers().FirstOrDefault(x => x.PlayerName.Contains(c.ArgByIndex(1), StringComparison.OrdinalIgnoreCase));
            if (t != null) {
                AddCredits(t, amt, $"Admin: {aName}", false);
                Server.PrintToChatAll(Msg("GiveSuccess", aName, amt.ToString(), GetPlural(amt), t.PlayerName));
                SendWebhook("💰 GIVE", $"Admin: **{aName}** -> **{t.PlayerName}** | **{amt}** {GetPlural(amt)}.");
            } else p?.PrintToChat(Msg("PlayerNotFound"));
        }
    }

    [ConsoleCommand("css_takecredits")]
    public void OnCmdTk(CCSPlayerController? p, CommandInfo c) {
        if (p != null && !AdminManager.PlayerHasPermissions(p, Config.AdminFlag)) return;
        if (c.ArgCount < 3 || !int.TryParse(c.ArgByIndex(2), out int amt)) { p?.PrintToChat(Msg("UsageTake")); return; }
        string aName = p?.PlayerName ?? "Console";
        var t = Utilities.GetPlayers().FirstOrDefault(x => x.PlayerName.Contains(c.ArgByIndex(1), StringComparison.OrdinalIgnoreCase));
        if (t != null) {
            AddCredits(t, -amt, $"Admin: {aName}", false);
            Server.PrintToChatAll(Msg("TakeSuccess", aName, amt.ToString(), GetPlural(amt), t.PlayerName));
            SendWebhook("🚫 TAKE", $"Admin: **{aName}** a luat **{amt}** {GetPlural(amt)} de la **{t.PlayerName}**.");
        } else p?.PrintToChat(Msg("PlayerNotFound"));
    }

    [ConsoleCommand("css_transfer")]
    public void OnCmdTr(CCSPlayerController? p, CommandInfo c) {
        if (p == null || c.ArgCount < 3 || !int.TryParse(c.ArgByIndex(2), out int amt) || amt <= 0) { p?.PrintToChat(Msg("UsageTransfer")); return; }
        var t = Utilities.GetPlayers().FirstOrDefault(x => x.PlayerName.Contains(c.ArgByIndex(1), StringComparison.OrdinalIgnoreCase));
        if (t != null && t != p) {
            if (!_playerCredits.ContainsKey(p.Slot) || _playerCredits[p.Slot] < amt) { p.PrintToChat(Msg("NoCredits")); return; }
            AddCredits(p, -amt, "Transfer", false); AddCredits(t, amt, "Transfer Primit", false);
            Server.PrintToChatAll(Msg("TransferBroadcast", p.PlayerName, t.PlayerName, amt.ToString(), GetPlural(amt)));
            SendWebhook("🔄 TRANSFER", $"**{p.PlayerName}** -> **{t.PlayerName}** | **{amt}** {GetPlural(amt)}.");
        } else p.PrintToChat(Msg("PlayerNotFound"));
    }

    [ConsoleCommand("css_credits")] 
    public void OnCmdCr(CCSPlayerController? p, CommandInfo c) { 
        if (p == null) return;
        
        if (c.ArgCount < 2) {
            if (_playerCredits.ContainsKey(p.Slot)) 
                p.PrintToChat(Msg("CreditsCount", _playerCredits[p.Slot].ToString("N0"), GetPlural(_playerCredits[p.Slot])));
            return;
        }

        var t = Utilities.GetPlayers().FirstOrDefault(x => x.PlayerName.Contains(c.ArgByIndex(1), StringComparison.OrdinalIgnoreCase));
        if (t != null && _playerCredits.ContainsKey(t.Slot)) {
            int cr = _playerCredits[t.Slot];
            p.PrintToChat(ParseColors("{prefix}Player-ul {purple}" + t.PlayerName + " {default}are {gold}" + cr.ToString("N0") + " {default}" + GetPlural(cr) + "."));
        } else {
            p.PrintToChat(Msg("PlayerNotFound"));
        }
    }

    [ConsoleCommand("css_topcredits")]
    public void OnCmdTp(CCSPlayerController? p, CommandInfo c) {
        if (p == null) return;
        _ = Task.Run(async () => {
            try {
                using var conn = new MySqlConnection(GetConnectionString()); await conn.OpenAsync();
                var r = await new MySqlCommand($"SELECT name, credits FROM `{Config.EconomyTable}` ORDER BY credits DESC LIMIT 10", conn).ExecuteReaderAsync();
                List<string> lines = new(); int j = 1;
                while (await r.ReadAsync()) {
                    int cr = r.GetInt32(1);
                    lines.Add(ParseColors($" {j++}. {r.GetString(0)} » {{gold}}{cr:N0} {{default}}{GetPlural(cr)}"));
                }
                Server.NextFrame(() => {
                    p.PrintToChat(ParseColors("{purple}▬▬▬▬▬ TOP CREDITS ▬▬▬▬▬"));
                    foreach(var l in lines) p.PrintToChat(l);
                    p.PrintToChat(ParseColors("{purple}▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬"));
                });
            } catch { }
        });
    }

    private async void SendWebhook(string title, string msg) {
        if (string.IsNullOrEmpty(Config.WebhookUrl)) return;
        try {
            using var client = new HttpClient();
            var payload = new { embeds = new[] { new { title = title, description = msg, color = 16766720, timestamp = DateTime.UtcNow } } };
            await client.PostAsync(Config.WebhookUrl, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        } catch { }
    }

    private string Msg(string key, params string[] args) {
        string t = Localizer[key] ?? key;
        for (int i = 0; i < args.Length; i++) t = t.Replace("{" + i + "}", args[i]);
        return ParseColors(t);
    }

    private string ParseColors(string m) {
        string processed = m.Replace("{prefix}", Config.ChatPrefix)
                            .Replace("{purple}", "\x03")
                            .Replace("{gold}", "\x10")
                            .Replace("{green}", "\x04")
                            .Replace("{default}", "\x01")
                            .Replace("{red}", "\x02");
        
        return processed.StartsWith("\x01") || processed.StartsWith("\x03") || processed.StartsWith("\x10") || processed.StartsWith("\x04") || processed.StartsWith("\x02") 
               ? processed 
               : "\x01" + processed;
    }
}