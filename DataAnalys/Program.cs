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

        // Läs konfig
        var cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(cfgPath))
        {
            Console.WriteLine("❌ Hittar inte appsettings.json i DataAnalys.");
            return 1;
        }
        var json = await File.ReadAllTextAsync(cfgPath);
        var cfg = JsonSerializer.Deserialize<RootConfig>(json) ?? new();

        Console.WriteLine("====== ANALYS (4 fält) ======");
        Console.WriteLine("1) Senaste 24 timmarna");
        Console.WriteLine("2) Senaste 100 datapunkterna");
        Console.Write("Välj (1/2): ");
        var choice = Console.ReadLine();

        string? url = BuildFeedsUrl(cfg, choice);

        if (url is null)
        {
            Console.WriteLine("Ogiltigt val. Avslutar.");
            return 0;
        }

        try
        {
            using var http = new HttpClient();
            var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"⚠️ Kunde inte hämta data. HTTP {(int)resp.StatusCode}");
                return 0;
            }

            var body = await resp.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<ThingSpeakResponse>(body);

            if (model?.feeds == null || model.feeds.Length == 0)
            {
                Console.WriteLine("ℹ️ Inga datapunkter hittades.");
                return 0;
            }

            // =====  vi tar med ALLA FYRA FÄLT =====
            var speed = model.feeds.Select(f => TryParse(f.field1)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var rpm = model.feeds.Select(f => TryParse(f.field2)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var fuel = model.feeds.Select(f => TryParse(f.field3)).Where(v => v.HasValue).Select(v => v.Value).ToArray();
            var temp = model.feeds.Select(f => TryParse(f.field4)).Where(v => v.HasValue).Select(v => v.Value).ToArray();

            // Medelvärden
            double? avgSpeed = speed.Length > 0 ? speed.Average() : (double?)null;
            double? avgRpm = rpm.Length > 0 ? rpm.Average() : (double?)null;
            double? avgFuel = fuel.Length > 0 ? fuel.Average() : (double?)null;
            double? avgTemp = temp.Length > 0 ? temp.Average() : (double?)null;

            Console.WriteLine("\n===== RESULTAT =====");
            Console.WriteLine(avgSpeed.HasValue ? $"Genomsnittlig hastighet: {avgSpeed.Value:F1} km/h" : "Genomsnittlig hastighet: saknas");
            Console.WriteLine(avgRpm.HasValue ? $"Genomsnittligt motorvarvtal (RPM): {avgRpm.Value:F0}" : "Genomsnittligt motorvarvtal (RPM): saknas");
            Console.WriteLine(avgFuel.HasValue ? $"Genomsnittlig bränslenivå: {avgFuel.Value:F1} %" : "Genomsnittlig bränslenivå: saknas");
            Console.WriteLine(avgTemp.HasValue ? $"Genomsnittlig motortemperatur: {avgTemp.Value:F1} °C" : "Genomsnittlig motortemperatur: saknas");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Fel vid hämtning/analys: {ex.Message}");
        }

        return 0;
    }

    // === Hjälpfunktioner ===

    static string? BuildFeedsUrl(RootConfig cfg, string? choice)
    {
        var baseUrl = $"https://api.thingspeak.com/channels/{cfg.ThingSpeak.ChannelId}/feeds.json";

        string query;
        if (choice == "1")
        {
            // 24h bakåt (UTC)
            var end = DateTime.UtcNow;
            var start = end.AddHours(-24);
            query = $"start={Uri.EscapeDataString(start.ToString("yyyy-MM-dd HH:mm:ss"))}" +
                    $"&end={Uri.EscapeDataString(end.ToString("yyyy-MM-dd HH:mm:ss"))}";
        }
        else if (choice == "2")
        {
            query = "results=100";
        }
        else
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(cfg.ThingSpeak.ReadApiKey))
            query += $"&api_key={Uri.EscapeDataString(cfg.ThingSpeak.ReadApiKey)}";

        return $"{baseUrl}?{query}";
    }

    static double? TryParse(string? s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    // === Modeller & konfig (bara 4 fält) ===

    public class RootConfig
    {
        public ThingSpeakConfig ThingSpeak { get; set; } = new();
    }

    public class ThingSpeakConfig
    {
        public int ChannelId { get; set; }
        public string ReadApiKey { get; set; } = "";
        public FieldMap Fields { get; set; } = new(); 
    }

    public class FieldMap
    {
        public string Speed { get; set; } = "field1";
        public string Rpm { get; set; } = "field2";
        public string Fuel { get; set; } = "field3";
        public string Temp { get; set; } = "field4";
    }

    public class ThingSpeakResponse
    {
        public Channel? channel { get; set; }
        public Feed[]? feeds { get; set; }
    }

    public class Channel
    {
        public int id { get; set; }
        public string? name { get; set; }
    }

    //  field1..field4
    public class Feed
    {
        public string? created_at { get; set; }
        public int entry_id { get; set; }
        public string? field1 { get; set; } // Speed
        public string? field2 { get; set; } // RPM
        public string? field3 { get; set; } // Fuel
        public string? field4 { get; set; } // Temp
    }
}
