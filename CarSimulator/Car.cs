using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CarSimulator;

/// <summary>
/// Enkel bilsimulator. Skickar 4 fält (Speed, RPM, Fuel, Temp) till ThingSpeak.
/// Personlig och tydlig konsol-output för Rabar. Logiken oförändrad.
/// </summary>
internal class Car
{
    // Rimliga konstanter (oförändrade)
    private const double MaxFuelConsumptionPerSecond = 0.005;
    private const int IdleRpm = 800;
    private const int MaxRpm = 6000;
    private const int MinSpeed = 0;
    private const int MaxSpeed = 120;

    // Tillstånd (oförändrat)
    private double _currentSpeed = 0;
    private double _currentRpm = IdleRpm;
    private double _currentFuel = 100.0;
    private readonly Random _rnd = new();
    private readonly HttpClient _http = new();

    private readonly Program.ThingSpeakConfig _cfg;

    // För snygg utskrift
    private bool _printedHeader = false;

    public Car(Program.ThingSpeakConfig cfg) => _cfg = cfg;

    public async Task StartSimulationAsync()
    {
        if (string.IsNullOrWhiteSpace(_cfg.WriteApiKey))
        {
            WriteError("WriteApiKey saknas i appsettings.json.");
            return;
        }

        PrintBanner();

        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMinutes < _cfg.TripDurationMinutes)
        {
            SimulateStep();

            var speed = Math.Round(_currentSpeed, 2);
            var rpm = Math.Round(_currentRpm, 2);
            var fuel = Math.Round(_currentFuel, 2);
            var temp = Math.Round(GenerateEngineTemp(), 2);

            // Skriv tabellhuvud första gången
            if (!_printedHeader)
            {
                Console.WriteLine("┌─────────┬──────────┬──────────┬──────────┬──────────┐");
                Console.WriteLine("│  TID s  │  HAST.   │   RPM    │  BRÄNSLE │   TEMP   │");
                Console.WriteLine("│         │ (km/h)   │          │    (%)   │   (°C)   │");
                Console.WriteLine("├─────────┼──────────┼──────────┼──────────┼──────────┤");
                _printedHeader = true;
            }

            var elapsedS = (DateTime.UtcNow - start).TotalSeconds;

            Console.WriteLine(
                $"│ {elapsedS,7:F0} │ {speed,8:F0} │ {rpm,8:F0} │ {fuel,8:F1} │ {temp,8:F0} │");

            try
            {
                var url =
                    $"https://api.thingspeak.com/update?api_key={_cfg.WriteApiKey}" +
                    $"&{_cfg.FieldSpeed}={speed}" +
                    $"&{_cfg.FieldRpm}={rpm}" +
                    $"&{_cfg.FieldFuel}={fuel}" +
                    $"&{_cfg.FieldTemp}={temp}";

                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                    WriteWarn($"Misslyckades att skicka: HTTP {(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                WriteWarn($"Nätverksfel: {ex.Message}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(_cfg.UpdateIntervalSeconds));
        }

        Console.WriteLine("└─────────┴──────────┴──────────┴──────────┴──────────┘");
        Console.WriteLine();
        Console.WriteLine("Tack för åkturen, Rabar!  Sändningen är avslutad.");
    }

    // --- UTSKRIFTSHJÄLP ---

    private static void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              Rabar • Car Telemetry Simulator             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine("Startar biltelemetri (4 fält: Speed, RPM, Fuel, Temp) …");
        Console.WriteLine();
    }

    private static void WriteWarn(string msg) => Console.WriteLine($"[Varning] {msg}");
    private static void WriteError(string msg) => Console.WriteLine($"[Fel] {msg}");

    // --- LOGIK (OFÖRÄNDRAD) ---

    private void SimulateStep()
    {
        // Enkel faslogik: accelerera → cruisa → decelerera
        string phase = _currentSpeed < 30 ? "Accelerating" :
                       _currentSpeed < 90 ? "Cruising" : "Decelerating";

        switch (phase)
        {
            case "Accelerating":
                _currentSpeed += _rnd.Next(2, 5);
                break;
            case "Cruising":
                _currentSpeed += _rnd.NextDouble() * 2 - 1;
                break;
            default:
                _currentSpeed -= _rnd.Next(2, 5);
                break;
        }

        _currentSpeed = Math.Max(MinSpeed, Math.Min(MaxSpeed, _currentSpeed));
        _currentRpm = CalcRpm(_currentSpeed);

        var rate = (_currentSpeed / MaxSpeed) * (_currentRpm / MaxRpm) * MaxFuelConsumptionPerSecond;
        _currentFuel = Math.Max(0, _currentFuel - rate * _cfg.UpdateIntervalSeconds);
    }

    private static double CalcRpm(double speed)
        => speed < 1 ? IdleRpm : IdleRpm + (speed / MaxSpeed) * (MaxRpm - IdleRpm);

    private double GenerateEngineTemp()
    {
        var baseTemp = 85.0;
        var factor = _currentSpeed / MaxSpeed;
        var temp = baseTemp + factor * _rnd.Next(0, 15);
        return Math.Max(80, Math.Min(110, temp));
    }
}
