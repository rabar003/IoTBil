using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CarSimulator;

/// <summary>
/// Enkel bilsimulator. Skickar 4 fält (Speed, RPM, Fuel, Temp) till ThingSpeak.
/// Svenska kommentarer & utskrifter.
/// </summary>
internal class Car
{
    // Rimliga konstanter
    private const double MaxFuelConsumptionPerSecond = 0.005;
    private const int IdleRpm = 800;
    private const int MaxRpm = 6000;
    private const int MinSpeed = 0;
    private const int MaxSpeed = 120;

    // Tillstånd
    private double _currentSpeed = 0;
    private double _currentRpm = IdleRpm;
    private double _currentFuel = 100.0;
    private readonly Random _rnd = new();
    private readonly HttpClient _http = new();

    private readonly Program.ThingSpeakConfig _cfg;

    public Car(Program.ThingSpeakConfig cfg) => _cfg = cfg;

    public async Task StartSimulationAsync()
    {
        if (string.IsNullOrWhiteSpace(_cfg.WriteApiKey))
        {
            Console.WriteLine("❌ WriteApiKey saknas i appsettings.json.");
            return;
        }

        Console.WriteLine("🚗 Startar biltelemetri (4 fält)...");
        var start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalMinutes < _cfg.TripDurationMinutes)
        {
            SimulateStep();

            var speed = Math.Round(_currentSpeed, 2);
            var rpm = Math.Round(_currentRpm, 2);
            var fuel = Math.Round(_currentFuel, 2);
            var temp = Math.Round(GenerateEngineTemp(), 2);

            Console.WriteLine(
                $"Hastighet: {speed:F0} km/h | RPM: {rpm:F0} | Bränsle: {fuel:F1}% | Temp: {temp:F0}°C");

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
                    Console.WriteLine($"⚠️ Misslyckades att skicka: HTTP {(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Nätverksfel: {ex.Message}");
            }

            Thread.Sleep(TimeSpan.FromSeconds(_cfg.UpdateIntervalSeconds));
        }

        Console.WriteLine("🛑 Simulationen stoppad (körtid nådd).");
    }

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
