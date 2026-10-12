namespace LGClimaTray.Services;

public enum ClimateMode { Frio, Quente, Automatico, Ventilacao, Desumidificar }
public enum FanSpeed { Baixo, Medio, Alto, Automatico }

public sealed record AirConditionerState(
    bool IsOn = false,
    double TargetTemperature = 23,
    ClimateMode Mode = ClimateMode.Frio,
    FanSpeed Fan = FanSpeed.Automatico,
    double RoomTemperature = 25,
    bool IsSimulated = true,
    bool IsConnected = true,
    bool SwingVertical = false,
    bool SwingHorizontal = false,
    bool NaturalWind = false,
    bool PowerSave = false,
    bool DisplayLight = true,
    TimeSpan? SleepTimer = null,
    TimeOnly? StartTime = null,
    TimeOnly? StopTime = null,
    double? FilterUsedHours = null,
    bool HasError = false);

/// <summary>Abstraction for the device connection.</summary>
public interface IAirConditionerService
{
    const double MinTemperature = 18;
    const double MaxTemperature = 30;
    const double TemperatureStep = 0.5;

    AirConditionerState State { get; }
    event Action<AirConditionerState>? StateChanged;
    event Action<string>? ErrorOccurred;
    Task StartAsync();
    Task SetPowerAsync(bool isOn);
    Task SetTemperatureAsync(double temperature);
    Task SetModeAsync(ClimateMode mode);
    Task SetFanAsync(FanSpeed fan);
    Task SetSwingVerticalAsync(bool enabled);
    Task SetSwingHorizontalAsync(bool enabled);
    Task SetNaturalWindAsync(bool enabled);
    Task SetPowerSaveAsync(bool enabled);
    Task SetDisplayLightAsync(bool on);
    /// <summary>Turns the unit off after <paramref name="duration"/>; null cancels.</summary>
    Task SetSleepTimerAsync(TimeSpan? duration);
    /// <summary>Turns the unit on at a clock time; null cancels.</summary>
    Task SetStartTimeAsync(TimeOnly? time);
    /// <summary>Turns the unit off at a clock time; null cancels.</summary>
    Task SetStopTimeAsync(TimeOnly? time);
}

/// <summary>Local simulation: no requests are sent to LG or the internet.</summary>
public sealed class SimulatedAirConditionerService : IAirConditionerService
{
    public AirConditionerState State { get; private set; } = new(FilterUsedHours: 120);
    public event Action<AirConditionerState>? StateChanged;
    public event Action<string>? ErrorOccurred { add { } remove { } }

    public Task StartAsync() => Task.CompletedTask;
    public Task SetPowerAsync(bool isOn) => UpdateAsync(State with { IsOn = isOn });
    public Task SetTemperatureAsync(double temperature) => UpdateAsync(State with { TargetTemperature = Math.Clamp(temperature, IAirConditionerService.MinTemperature, IAirConditionerService.MaxTemperature) });
    public Task SetModeAsync(ClimateMode mode) => UpdateAsync(State with { Mode = mode });
    public Task SetFanAsync(FanSpeed fan) => UpdateAsync(State with { Fan = fan, NaturalWind = false });
    public Task SetSwingVerticalAsync(bool enabled) => UpdateAsync(State with { SwingVertical = enabled });
    public Task SetSwingHorizontalAsync(bool enabled) => UpdateAsync(State with { SwingHorizontal = enabled });
    public Task SetNaturalWindAsync(bool enabled) => UpdateAsync(State with { NaturalWind = enabled });
    public Task SetPowerSaveAsync(bool enabled) => UpdateAsync(State with { PowerSave = enabled });
    public Task SetDisplayLightAsync(bool on) => UpdateAsync(State with { DisplayLight = on });
    public Task SetSleepTimerAsync(TimeSpan? duration) => UpdateAsync(State with { SleepTimer = duration });
    public Task SetStartTimeAsync(TimeOnly? time) => UpdateAsync(State with { StartTime = time });
    public Task SetStopTimeAsync(TimeOnly? time) => UpdateAsync(State with { StopTime = time });

    private Task UpdateAsync(AirConditionerState next)
    {
        State = next;
        StateChanged?.Invoke(State);
        return Task.CompletedTask;
    }
}
