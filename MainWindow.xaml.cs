using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LGClimaTray.Services;

namespace LGClimaTray;

public partial class MainWindow : Window
{
    private static readonly Brush ActiveForeground = new SolidColorBrush(Color.FromRgb(16, 34, 30));

    private readonly IAirConditionerService _climate =
        (IAirConditionerService?)LgThinQAirConditionerService.TryCreateFromEnvironment() ?? new SimulatedAirConditionerService();

    // Waits for the slider to settle before sending, so dragging doesn't fire one command per step
    private readonly DispatcherTimer _temperatureDebounce = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private bool _isUpdating;
    private string? _lastError;
    public bool AllowClose { get; set; }
    public IAirConditionerService Climate => _climate;

    public MainWindow()
    {
        InitializeComponent();
        _climate.StateChanged += state => Dispatcher.Invoke(() => RenderState(state));
        _climate.ErrorOccurred += message => Dispatcher.Invoke(() => ShowError(message));
        _temperatureDebounce.Tick += async (_, _) =>
        {
            _temperatureDebounce.Stop();
            await RunAsync(() => _climate.SetTemperatureAsync(TemperatureSlider.Value));
        };
        RenderState(_climate.State);
    }

    /// <summary>Connects even while the panel is hidden, so the tray icon shows the real state.</summary>
    public Task ConnectAsync() => RunAsync(_climate.StartAsync);

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }

    private void RenderState(AirConditionerState state)
    {
        _isUpdating = true;
        if (!_temperatureDebounce.IsEnabled)
        {
            TemperatureText.Text = state.TargetTemperature.ToString("0.#");
            TemperatureSlider.Value = state.TargetTemperature;
        }
        RoomTemperatureText.Text = state.IsSimulated
            ? $"Ambiente: {state.RoomTemperature:0.#}°C (valor fictício)"
            : state.IsConnected ? $"Ambiente: {state.RoomTemperature:0.#}°C" : "Ambiente: —";
        PowerBadge.Text = state.IsOn ? "LIGADO" : "DESLIGADO";
        PowerBadge.Foreground = state.IsOn ? (Brush)FindResource("Accent") : (Brush)FindResource("Muted");
        StatusDot.Fill = state.HasError ? new SolidColorBrush(Color.FromRgb(238, 139, 132))
            : !state.IsSimulated && state.IsConnected ? new SolidColorBrush(Color.FromRgb(155, 230, 208))
            : new SolidColorBrush(Color.FromRgb(240, 198, 116));
        StatusText.Text = state.IsSimulated ? "Modo de demonstração"
            : !state.IsConnected ? "Conectando à LG ThinQ…"
            : state.HasError ? "Conectado · aparelho reporta ERRO"
            : state.IsOn ? "Conectado · ligado" : "Conectado · desligado";
        if (_lastError is null) FooterText.Text = FooterMessage(state);
        PowerButtonText.Text = state.IsOn ? "Desligar aparelho" : "Ligar aparelho";
        PowerButton.Background = state.IsOn ? new SolidColorBrush(Color.FromRgb(238, 139, 132)) : (Brush)FindResource("Accent");
        PowerButton.Foreground = ActiveForeground;

        Highlight(CoolButton, state.Mode == ClimateMode.Frio);
        Highlight(HeatButton, state.Mode == ClimateMode.Quente);
        Highlight(AutoButton, state.Mode == ClimateMode.Automatico);
        Highlight(FanModeButton, state.Mode == ClimateMode.Ventilacao);
        Highlight(DryButton, state.Mode == ClimateMode.Desumidificar);

        Highlight(FanAutoButton, !state.NaturalWind && state.Fan == FanSpeed.Automatico);
        Highlight(FanLowButton, !state.NaturalWind && state.Fan == FanSpeed.Baixo);
        Highlight(FanMidButton, !state.NaturalWind && state.Fan == FanSpeed.Medio);
        Highlight(FanHighButton, !state.NaturalWind && state.Fan == FanSpeed.Alto);

        Highlight(SwingVerticalButton, state.SwingVertical);
        Highlight(SwingHorizontalButton, state.SwingHorizontal);
        Highlight(NaturalWindButton, state.NaturalWind);
        Highlight(PowerSaveButton, state.PowerSave);

        DisplayLightButton.Content = state.DisplayLight ? "Acesa" : "Apagada";
        Highlight(DisplayLightButton, state.DisplayLight);
        SleepStatusText.Text = state.SleepTimer is { } sleep ? $"Desliga daqui a {FormatDuration(sleep)}" : "Nenhum sleep ativo";
        TimerStatusText.Text = (state.StartTime, state.StopTime) switch
        {
            (null, null) => "Nenhum horário programado",
            ({ } on, null) => $"Liga às {on:HH\\:mm}",
            (null, { } off) => $"Desliga às {off:HH\\:mm}",
            ({ } on, { } off) => $"Liga às {on:HH\\:mm} · desliga às {off:HH\\:mm}",
        };
        FilterText.Text = state.FilterUsedHours is { } hours ? $"{hours:0} h de uso" : "Sem informação";
        ConnectionInfoText.Text = state.IsSimulated
            ? $"Modo simulado. Para controlar o aparelho real, gere um token em connect-pat.lgthinq.com e salve na variável de ambiente {LgThinQAirConditionerService.TokenVariable}. Depois reabra o programa."
            : "API oficial LG ThinQ Connect (servidor Américas). O estado é atualizado a cada 30 segundos e logo após cada comando.";
        _isUpdating = false;
    }

    private void Highlight(Button button, bool active)
    {
        button.Background = active ? (Brush)FindResource("Accent") : (Brush)FindResource("PanelLight");
        button.Foreground = active ? ActiveForeground : (Brush)FindResource("TextMain");
    }

    private static string FormatDuration(TimeSpan d) =>
        d.Hours > 0 && d.Minutes > 0 ? $"{d.Hours} h {d.Minutes} min" : d.Hours > 0 ? $"{d.Hours} h" : $"{d.Minutes} min";

    private static string FooterMessage(AirConditionerState state) => state.IsSimulated
        ? $"Simulação: defina a variável {LgThinQAirConditionerService.TokenVariable} para controlar o aparelho real"
        : "Conectado via LG ThinQ Connect · atualiza a cada 30 s";

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            if (_lastError is not null) { _lastError = null; FooterText.Text = FooterMessage(_climate.State); FooterText.Foreground = (Brush)FindResource("Muted"); }
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message)
    {
        _lastError = message;
        FooterText.Text = "⚠ " + message;
        FooterText.Foreground = new SolidColorBrush(Color.FromRgb(240, 198, 116));
    }

    private async void PowerButton_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetPowerAsync(!_climate.State.IsOn));

    private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating || !IsLoaded) return;
        TemperatureText.Text = e.NewValue.ToString("0.#");
        _temperatureDebounce.Stop();
        _temperatureDebounce.Start();
    }
    private void TempDown_Click(object sender, RoutedEventArgs e) => TemperatureSlider.Value = Math.Max(TemperatureSlider.Minimum, TemperatureSlider.Value - IAirConditionerService.TemperatureStep);
    private void TempUp_Click(object sender, RoutedEventArgs e) => TemperatureSlider.Value = Math.Min(TemperatureSlider.Maximum, TemperatureSlider.Value + IAirConditionerService.TemperatureStep);

    private async void Cool_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetModeAsync(ClimateMode.Frio));
    private async void Heat_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetModeAsync(ClimateMode.Quente));
    private async void Auto_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetModeAsync(ClimateMode.Automatico));
    private async void FanMode_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetModeAsync(ClimateMode.Ventilacao));
    private async void Dry_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetModeAsync(ClimateMode.Desumidificar));

    private async void Fan_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<FanSpeed>(tag, out var fan))
            await RunAsync(() => _climate.SetFanAsync(fan));
    }

    private async void SwingVertical_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetSwingVerticalAsync(!_climate.State.SwingVertical));
    private async void SwingHorizontal_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetSwingHorizontalAsync(!_climate.State.SwingHorizontal));
    private async void NaturalWind_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetNaturalWindAsync(!_climate.State.NaturalWind));
    private async void PowerSave_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetPowerSaveAsync(!_climate.State.PowerSave));
    private async void DisplayLight_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetDisplayLightAsync(!_climate.State.DisplayLight));

    private async void Sleep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var minutes)) return;
        await RunAsync(() => _climate.SetSleepTimerAsync(minutes > 0 ? TimeSpan.FromMinutes(minutes) : null));
    }

    private async void StartTimeSet_Click(object sender, RoutedEventArgs e)
    {
        if (ParseTime(StartTimeBox.Text) is { } time) await RunAsync(() => _climate.SetStartTimeAsync(time));
    }
    private async void StopTimeSet_Click(object sender, RoutedEventArgs e)
    {
        if (ParseTime(StopTimeBox.Text) is { } time) await RunAsync(() => _climate.SetStopTimeAsync(time));
    }
    private async void StartTimeClear_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetStartTimeAsync(null));
    private async void StopTimeClear_Click(object sender, RoutedEventArgs e) => await RunAsync(() => _climate.SetStopTimeAsync(null));

    /// <summary>Accepts "7", "07:30", "7:30" or "7h30".</summary>
    private TimeOnly? ParseTime(string text)
    {
        var value = text.Trim().Replace('h', ':').TrimEnd(':');
        if (int.TryParse(value, out var hour) && hour is >= 0 and < 24) return new TimeOnly(hour, 0);
        if (TimeOnly.TryParseExact(value, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return time;
        ShowError("Horário inválido. Use o formato 07:30.");
        return null;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MainPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
    }
    private void SettingsBack_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Visible;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
