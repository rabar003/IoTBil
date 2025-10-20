using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarSimulator;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Läs konfig
        var cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(cfgPath))
        {
            Console.WriteLine("❌ Hittar inte appsettings.json i CarSimulator.");
            return 1;
        }
        var json = await File.ReadAllTextAsync(cfgPath);
        var cfg = JsonSerializer.Deserialize<RootConfig>(json) ?? new();

        var sim = new Car(cfg.ThingSpeak);
        await sim.StartSimulationAsync();

        Console.WriteLine("✅ Simulationen är klar.");
        return 0;
    }

    // Konfigmodeller
    public class RootConfig
    {
        public ThingSpeakConfig ThingSpeak { get; set; } = new();
    }

    public class ThingSpeakConfig
    {
        public string WriteApiKey { get; set; } = "";
        public int ChannelId { get; set; }
        public string FieldSpeed { get; set; } = "field1";
        public string FieldRpm { get; set; } = "field2";
        public string FieldFuel { get; set; } = "field3";
        public string FieldTemp { get; set; } = "field4";
        public int UpdateIntervalSeconds { get; set; } = 15;
        public int TripDurationMinutes { get; set; } = 10;
    }
}
