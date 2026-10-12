using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace LGClimaTray.Services;

/// <summary>Real device connection through the official LG ThinQ Connect API.</summary>
public sealed class LgThinQAirConditionerService : IAirConditionerService
{
    public const string TokenVariable = "LG_THINQ_PAT";

    // Public constant shipped in LG's official SDK (pythinqconnect); not a secret
    private const string ApiKey = "v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshDelayAfterCommand = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api-aic.lgthinq.com/"), Timeout = TimeSpan.FromSeconds(15) };
    private readonly string _token;
    private readonly string _clientId = $"lgclimatray-{Environment.MachineName.ToLowerInvariant()}";
    private string? _deviceId;

    public AirConditionerState State { get; private set; } = new(IsSimulated: false, IsConnected: false);
    public event Action<AirConditionerState>? StateChanged;
    public event Action<string>? ErrorOccurred;

    private LgThinQAirConditionerService(string token) => _token = token;

    /// <summary>Returns null when no token is configured, so the app can fall back to the simulation.</summary>
    public static LgThinQAirConditionerService? TryCreateFromEnvironment()
    {
        var token = Environment.GetEnvironmentVariable(TokenVariable)
                    ?? Environment.GetEnvironmentVariable(TokenVariable, EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(token) ? null : new LgThinQAirConditionerService(token.Trim());
    }

    public async Task StartAsync()
    {
        var devices = await SendAsync(HttpMethod.Get, "devices");
        var airConditioner = devices?.AsArray()
            .FirstOrDefault(d => (string?)d?["deviceInfo"]?["deviceType"] == "DEVICE_AIR_CONDITIONER")
            ?? throw new InvalidOperationException("Nenhum ar-condicionado encontrado nesta conta LG ThinQ.");

        _deviceId = (string?)airConditioner["deviceId"];
        await RefreshAsync();
        _ = PollAsync();
    }

    public Task SetPowerAsync(bool isOn) =>
        ControlAsync("operation", new() { ["airConOperationMode"] = isOn ? "POWER_ON" : "POWER_OFF" }, State with { IsOn = isOn });

    public Task SetTemperatureAsync(double temperature)
    {
        var value = Math.Clamp(Math.Round(temperature * 2) / 2, IAirConditionerService.MinTemperature, IAirConditionerService.MaxTemperature);
        return ControlAsync("temperature", new() { ["targetTemperature"] = value }, State with { TargetTemperature = value });
    }

    public Task SetModeAsync(ClimateMode mode) =>
        ControlAsync("airConJobMode", new() { ["currentJobMode"] = ToApi(mode) }, State with { Mode = mode });

    public Task SetFanAsync(FanSpeed fan) =>
        ControlAsync("airFlow", new() { ["windStrength"] = ToApi(fan) }, State with { Fan = fan, NaturalWind = false });

    public Task SetSwingVerticalAsync(bool enabled) =>
        ControlAsync("windDirection", new() { ["rotateUpDown"] = enabled }, State with { SwingVertical = enabled });

    public Task SetSwingHorizontalAsync(bool enabled) =>
        ControlAsync("windDirection", new() { ["rotateLeftRight"] = enabled }, State with { SwingHorizontal = enabled });

    // Natural wind lives in windStrengthDetail; turning it off restores the regular fan speed
    public Task SetNaturalWindAsync(bool enabled) => enabled
        ? ControlAsync("airFlow", new() { ["windStrengthDetail"] = "NATURE" }, State with { NaturalWind = true })
        : ControlAsync("airFlow", new() { ["windStrength"] = ToApi(State.Fan) }, State with { NaturalWind = false });

    public Task SetPowerSaveAsync(bool enabled) =>
        ControlAsync("powerSave", new() { ["powerSaveEnabled"] = enabled }, State with { PowerSave = enabled });

    public Task SetDisplayLightAsync(bool on) =>
        ControlAsync("display", new() { ["light"] = on ? "ON" : "OFF" }, State with { DisplayLight = on });

    public Task SetSleepTimerAsync(TimeSpan? duration)
    {
        if (duration is not { } d) return ControlAsync("sleepTimer", new() { ["relativeStopTimer"] = "UNSET" }, State with { SleepTimer = null });

        // Same rule as LG's SDK: minutes are only sent when non-zero
        var props = new JsonObject { ["relativeHourToStop"] = (int)d.TotalHours };
        if (d.Minutes != 0) props["relativeMinuteToStop"] = d.Minutes;
        return ControlAsync("sleepTimer", props, State with { SleepTimer = d });
    }

    public Task SetStartTimeAsync(TimeOnly? time) => time is { } t
        ? ControlAsync("timer", new() { ["absoluteHourToStart"] = t.Hour, ["absoluteMinuteToStart"] = t.Minute }, State with { StartTime = t })
        : ControlAsync("timer", new() { ["absoluteStartTimer"] = "UNSET" }, State with { StartTime = null });

    public Task SetStopTimeAsync(TimeOnly? time) => time is { } t
        ? ControlAsync("timer", new() { ["absoluteHourToStop"] = t.Hour, ["absoluteMinuteToStop"] = t.Minute }, State with { StopTime = t })
        : ControlAsync("timer", new() { ["absoluteStopTimer"] = "UNSET" }, State with { StopTime = null });

    private async Task ControlAsync(string resource, JsonObject properties, AirConditionerState expected)
    {
        if (_deviceId is null) throw new InvalidOperationException("Ainda conectando à LG ThinQ, tente de novo em instantes.");

        var body = new JsonObject { [resource] = properties };
        await SendAsync(HttpMethod.Post, $"devices/{_deviceId}/control", body);

        // Show the change right away, then confirm with the device's real state
        Publish(expected);
        _ = RefreshLaterAsync();
    }

    private async Task RefreshAsync()
    {
        var s = await SendAsync(HttpMethod.Get, $"devices/{_deviceId}/state");
        if (s is null) return;

        Publish(State with
        {
            IsConnected = true,
            IsOn = (string?)s["operation"]?["airConOperationMode"] == "POWER_ON",
            Mode = FromApiMode((string?)s["airConJobMode"]?["currentJobMode"]) ?? State.Mode,
            Fan = FromApiFan((string?)s["airFlow"]?["windStrength"]) ?? State.Fan,
            TargetTemperature = (double?)s["temperature"]?["targetTemperature"] ?? State.TargetTemperature,
            RoomTemperature = (double?)s["temperature"]?["currentTemperature"] ?? State.RoomTemperature,
            SwingVertical = (bool?)s["windDirection"]?["rotateUpDown"] ?? State.SwingVertical,
            SwingHorizontal = (bool?)s["windDirection"]?["rotateLeftRight"] ?? State.SwingHorizontal,
            NaturalWind = (string?)s["airFlow"]?["windStrengthDetail"] == "NATURE",
            PowerSave = (bool?)s["powerSave"]?["powerSaveEnabled"] ?? State.PowerSave,
            DisplayLight = (string?)s["display"]?["light"] != "OFF",
            SleepTimer = ReadDuration(s["sleepTimer"]),
            StartTime = ReadClockTime(s["timer"], "absoluteStartTimer", "absoluteHourToStart", "absoluteMinuteToStart"),
            StopTime = ReadClockTime(s["timer"], "absoluteStopTimer", "absoluteHourToStop", "absoluteMinuteToStop"),
            FilterUsedHours = (double?)s["filterInfo"]?["usedTime"],
            HasError = (string?)s["runState"]?["currentState"] == "ERROR",
        });
    }

    private static TimeSpan? ReadDuration(JsonNode? sleepTimer)
    {
        if ((string?)sleepTimer?["relativeStopTimer"] != "SET") return null;
        return new TimeSpan((int?)sleepTimer!["relativeHourToStop"] ?? 0, (int?)sleepTimer["relativeMinuteToStop"] ?? 0, 0);
    }

    private static TimeOnly? ReadClockTime(JsonNode? timer, string flag, string hour, string minute)
    {
        if ((string?)timer?[flag] != "SET" || (int?)timer![hour] is not { } h) return null;
        return new TimeOnly(h % 24, (int?)timer[minute] ?? 0);
    }

    private async Task RefreshLaterAsync()
    {
        await Task.Delay(RefreshDelayAfterCommand);
        try { await RefreshAsync(); }
        catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync())
        {
            try { await RefreshAsync(); }
            catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }
        }
    }

    private void Publish(AirConditionerState next)
    {
        State = next;
        StateChanged?.Invoke(State);
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Add("x-country", "BR");
        request.Headers.Add("x-message-id", NewMessageId());
        request.Headers.Add("x-client-id", _clientId);
        request.Headers.Add("x-api-key", ApiKey);
        request.Headers.Add("x-service-phase", "OP");
        if (body is not null)
        {
            request.Headers.Add("x-conditional-control", "true");
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try { response = await _http.SendAsync(request); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException("Sem conexão com a LG ThinQ. Verifique a internet.", ex);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            var json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
            if (response.IsSuccessStatusCode) return json?["response"];

            var code = (string?)json?["error"]?["code"];
            var message = (string?)json?["error"]?["message"] ?? response.ReasonPhrase;
            throw new InvalidOperationException(DescribeError(code, message));
        }
    }

    private static string DescribeError(string? code, string? message) => code switch
    {
        "1301" or "1302" => "Token LG inválido ou expirado. Gere um novo em connect-pat.lgthinq.com.",
        "1305" or "1309" => "O token LG não tem permissão para isso. Gere um novo marcando todas as permissões.",
        "1306" => "Limite de chamadas da LG atingido. Aguarde alguns minutos.",
        "1308" => "O aparelho está sendo controlado pelo app ThinQ no momento.",
        _ => $"LG ThinQ recusou o pedido: {message} ({code ?? "sem código"})"
    };

    private static string NewMessageId() =>
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ToApi(ClimateMode mode) => mode switch
    {
        ClimateMode.Frio => "COOL",
        ClimateMode.Quente => "HEAT",
        ClimateMode.Automatico => "AUTO",
        ClimateMode.Ventilacao => "FAN",
        ClimateMode.Desumidificar => "AIR_DRY",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static ClimateMode? FromApiMode(string? value) => value switch
    {
        "COOL" => ClimateMode.Frio,
        "HEAT" => ClimateMode.Quente,
        "AUTO" => ClimateMode.Automatico,
        "FAN" => ClimateMode.Ventilacao,
        "AIR_DRY" => ClimateMode.Desumidificar,
        _ => null
    };

    private static string ToApi(FanSpeed fan) => fan switch
    {
        FanSpeed.Baixo => "LOW",
        FanSpeed.Medio => "MID",
        FanSpeed.Alto => "HIGH",
        FanSpeed.Automatico => "AUTO",
        _ => throw new ArgumentOutOfRangeException(nameof(fan))
    };

    private static FanSpeed? FromApiFan(string? value) => value switch
    {
        "LOW" => FanSpeed.Baixo,
        "MID" => FanSpeed.Medio,
        "HIGH" => FanSpeed.Alto,
        "AUTO" => FanSpeed.Automatico,
        _ => null
    };
}
