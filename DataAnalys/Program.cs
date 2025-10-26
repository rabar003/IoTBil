using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataAnalys;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Ladda konfig
        var cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(cfgPath))
        {
            WriteError("Hittar inte appsettings.json i DataAnalys. Kontrollera filens placering.");
            return 1;
        }

        var json = await File.ReadAllTextAsync(cfgPath);
        var cfg = JsonSerializer.Deserialize<RootConfig>(json) ?? new();

        WriteBanner();

        // Personlig meny
        Console.WriteLine("Hej Rabar! Välj vad du vill analysera:");
        Console.WriteLine("  [1]  Senaste 24 timmarna");
        Console.WriteLine("  [2]  Senaste 100 datapunkterna");
        Console.Write("Ditt val (1/2): ");

        var choice = Console.ReadLine();
        var url = BuildFeedsUrl(cfg, choice);

        if (url is null)
        {
            WriteWarn("Ogiltigt val. Avslutar.");
            return 0;
        }

        try
        {
            using var http = new HttpClient();
            var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                WriteError($"Kunde inte hämta data. HTTP {(int)resp.StatusCode}");
                return 0;
            }

            var body = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<ThingSpeakResponse>(body);

            if (model?.feeds == null || model.feeds.Length == 0)
            {
                WriteWarn("Inga datapunkter hittades för vald period.");
                return 0;
            }

            // Plocka ut alla fyra fält
            var speed = model.feeds.Select(f => TryParse(f.field1)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var rpm = model.feeds.Select(f => TryParse(f.field2)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var fuel = model.feeds.Select(f => TryParse(f.field3)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var temp = model.feeds.Select(f => TryParse(f.field4)).Where(v => v.HasValue).Select(v => v.Value).ToArray();

            double? avgSpeed = speed.Length > 0 ? speed.Average() : (double?)null;
            double? avgRpm = rpm.Length > 0 ? rpm.Average() : (double?)null;
            double? avgFuel = fuel.Length > 0 ? fuel.Average() : (double?)null;
            double? avgTemp = temp.Length > 0 ? temp.Average() : (double?)null;

            // Personlig, tydlig utskrift
            Console.WriteLine();
            Console.WriteLine("┌──────────────────────────── RABAR – ANALYS ────────────────────────────┐");
            Console.WriteLine("│  Översikt av medelvärden                                                │");
            Console.WriteLine("├────────────────────────────────────────────────────────────────────────┤");
            Console.WriteLine(FormatRow("Medelhastighet", avgSpeed, "km/h", "inga data"));
            Console.WriteLine(FormatRow("Medel-RPM", avgRpm, "", "inga data", noDecimals: true));
            Console.WriteLine(FormatRow("Medel-bränsle", avgFuel, "%", "inga data"));
            Console.WriteLine(FormatRow("Medel-temp", avgTemp, "°C", "inga data"));
            Console.WriteLine("└────────────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            WriteError($"Fel vid hämtning/analys: {ex.Message}");
        }

        return 0;
    }

    // ---------- Hjälpmetoder för utskrift ----------
    static void WriteBanner()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                         Rabar • IoT DataAnalys                       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    static void WriteWarn(string msg)
    {
        Console.WriteLine($"[Varning] {msg}");
    }

    static void WriteError(string msg)
    {
        Console.WriteLine($"[Fel] {msg}");
    }

    static string FormatRow(string label, double? value, string unit, string fallback, bool noDecimals = false)
    {
        if (!value.HasValue)
            return $"│  {label,-22}: {fallback,-40}│";

        var formatted = noDecimals ? $"{value.Value:F0} {unit}".Trim()
                                   : $"{value.Value:F1} {unit}".Trim();
        return $"│  {label,-22}: {formatted,-40}│";
    }

    // ---------- URL & parsing ----------
    static string? BuildFeedsUrl(RootConfig cfg, string? choice)
    {
        var baseUrl = $"https://api.thingspeak.com/channels/{cfg.ThingSpeak.ChannelId}/feeds.json";
        if (choice == "1")
        {
            var end = DateTime.UtcNow;
            var start = end.AddHours(-24);
            var q = $"start={Uri.EscapeDataString(start.ToString("yyyy-MM-dd HH:mm:ss"))}&end={Uri.EscapeDataString(end.ToString("yyyy-MM-dd HH:mm:ss"))}";
            if (!string.IsNullOrWhiteSpace(cfg.ThingSpeak.ReadApiKey))
                q += $"&api_key={Uri.EscapeDataString(cfg.ThingSpeak.ReadApiKey)}";
            return $"{baseUrl}?{q}";
        }
        else if (choice == "2")
        {
            var q = "results=100";
            if (!string.IsNullOrWhiteSpace(cfg.ThingSpeak.ReadApiKey))
                q += $"&api_key={Uri.EscapeDataString(cfg.ThingSpeak.ReadApiKey)}";
            return $"{baseUrl}?{q}";
        }
        return null;
    }

    static double? TryParse(string? s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    // ---------- Modeller ----------
    public class RootConfig
    {
        public ThingSpeakConfig ThingSpeak { get; set; } = new();
    }

    public class ThingSpeakConfig
    {
        public int ChannelId { get; set; }
        public string ReadApiKey { get; set; } = "";
    }

    public class ThingSpeakResponse
    {
        public Feed[]? feeds { get; set; }
    }

    public class Feed
    {
        public string? field1 { get; set; } // Speed
        public string? field2 { get; set; } // RPM
        public string? field3 { get; set; } // Fuel
        public string? field4 { get; set; } // Temp
    }
}
