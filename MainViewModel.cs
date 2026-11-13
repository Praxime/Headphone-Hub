using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Media;
using System.Windows;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace HeadphoneHub;

internal sealed class DeviceWatcher : IMMNotificationClient
{
    private readonly Action _changed;
    public DeviceWatcher(Action changed) => _changed = changed;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _changed();
    public void OnDeviceAdded(string pwstrDeviceId) => _changed();
    public void OnDeviceRemoved(string deviceId) => _changed();
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Render && role == Role.Multimedia) _changed();
    }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}

public sealed class MainViewModel : Observable, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly AudioEngine _engine = new();
    private readonly WebStreamer _web = new();
    private readonly AppSettings _settings = SettingsStore.Load();
    private readonly DispatcherTimer _batteryTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DeviceWatcher _watcher;

    private readonly Dictionary<string, int> _batteryCache = new();
    private MMDevice? _primary;
    private bool _running, _hideVirtual;
    private string _webState = "Off", _webUrl = "";
    private int _webVolume, _webPort;
    private string? _hintKey;
    private object[] _hintArgs = Array.Empty<object>();
    private readonly SpectrumAnalyzer _spectrum = new();

    public MainViewModel()
    {
        _hideVirtual = _settings.HideVirtual;
        _webVolume = Math.Clamp(_settings.WebVolume, 0, 100);
        _web.Volume = _webVolume / 100f;
        _webPort = _settings.WebPort is >= 1024 and <= 65535 ? _settings.WebPort : 0;
        ThemeManager.Changed += PushTheme;
        _web.ListenersChanged += () =>
            Application.Current?.Dispatcher.BeginInvoke(() => Raise(nameof(WebSub)));
        Loc.I.Lang = _settings.Lang;
        ThemeManager.SetAccentName(_settings.Accent, animate: false);
        ThemeManager.SetMode(_settings.Theme, animate: false);
        Loc.I.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Loc.Lang)) OnLangChanged(); };
        _batteryTimer.Tick += async (_, _) => await UpdateBatteryAsync();
        _batteryTimer.Start();

        _debounce.Tick += (_, _) => { _debounce.Stop(); Refresh(); };
        _watcher = new DeviceWatcher(() =>
            Application.Current?.Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); }));
        _enumerator.RegisterEndpointNotificationCallback(_watcher);
    }

    public ObservableCollection<DeviceVM> Devices { get; } = new();

    public bool HideVirtual
    {
        get => _hideVirtual;
        set
        {
            if (!Set(ref _hideVirtual, value)) return;
            _settings.HideVirtual = value;
            SettingsStore.Save(_settings);
            Refresh();
        }
    }

    private static readonly string[] VirtualMarkers =
        { "Voicemeeter", "VB-Audio", "VB-Cable", "CABLE", "Virtual", "Steam Streaming", "Remote Audio", "FxSound" };

    private static bool IsVirtual(string name) =>
        VirtualMarkers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase));

    public SpectrumAnalyzer Spectrum => _spectrum;

    public string WebState
    {
        get => _webState;
        private set
        {
            if (!Set(ref _webState, value)) return;
            Raise(nameof(WebEnabled));
            Raise(nameof(WebSub));
            Raise(nameof(Status));
        }
    }

    public bool WebEnabled => _webState == "On";
    public string WebUrl => _webUrl;
    public string WebVolumeText => Loc.I.Num(_webVolume) + Loc.I.Pct;

    public int WebVolume
    {
        get => _webVolume;
        set
        {
            if (!Set(ref _webVolume, Math.Clamp(value, 0, 100))) return;
            _web.Volume = _webVolume / 100f;
            _settings.WebVolume = _webVolume;
            SettingsStore.Save(_settings);
            Raise(nameof(WebVolumeText));
        }
    }

    public string WebPortText
    {
        get => _webPort == 0 ? "" : _webPort.ToString();
        set
        {
            int port = 0;
            var text = (value ?? "").Trim();
            if (text.Length > 0 && (!int.TryParse(text, out port) || port is < 1024 or > 65535))
            {
                SetHint("WebPortInvalid");
                Application.Current.Dispatcher.BeginInvoke(() => Raise(nameof(WebPortText)));
                return;
            }
            if (port == _webPort)
            {
                Application.Current.Dispatcher.BeginInvoke(() => Raise(nameof(WebPortText)));
                return;
            }

            _webPort = port;
            _settings.WebPort = port;
            SettingsStore.Save(_settings);
            SetHint(null);
            Raise();
            if (_webState == "On") _ = RestartWebAsync();
        }
    }

    public void UseRandomPort() => WebPortText = "";

    public string WebSub => _webState switch
    {
        "Busy" => Loc.I["WebStarting"],
        "On" => _web.ListenerCount == 0
            ? Loc.I["WebNobody"]
            : Loc.I.Format("WebListeners", Loc.I.Num(_web.ListenerCount)),
        _ => Loc.I["WebIdle"]
    };

    private void PushTheme()
    {
        var res = Application.Current.Resources;
        string Hex(string key) => res[key] is SolidColorBrush b ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : "#000000";

        _web.SetTheme(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["dark"] = ThemeManager.IsDark,
            ["bg"] = Hex("Bg"),
            ["card"] = Hex("Card"),
            ["fg"] = Hex("Label"),
            ["sub"] = Hex("Secondary"),
            ["fill"] = Hex("Fill"),
            ["accent"] = Hex("Accent"),
            ["viz"] = Hex("VizBg")
        }));
    }

    public event Action? PortPromptRequested;

    public async void ToggleWeb()
    {
        if (_webState == "Busy") return;
        if (_webState == "On") { StopWeb(); return; }
        if (PortPromptRequested != null) { PortPromptRequested.Invoke(); return; }
        await StartWebAsync();
    }

    public string SavedPortText => _webPort == 0 ? "" : _webPort.ToString();

    public bool TryParsePort(string? text, out int port)
    {
        port = 0;
        return int.TryParse((text ?? "").Trim(), out port) && port is >= 1024 and <= 65535;
    }

    public async void StartWebOnPort(int port)
    {
        if (_webState != "Off") return;
        _webPort = port;
        _settings.WebPort = port;
        SettingsStore.Save(_settings);
        Raise(nameof(WebPortText));
        await StartWebAsync();
    }

    private async Task StartWebAsync()
    {
        WebState = "Busy";
        SetHint(null);
        PushTheme();
        try
        {
            var start = Task.Run(() => (Port: _web.Start(_webPort), Address: LocalNetwork.FindAddress()));
            await Task.WhenAll(start, Task.Delay(700));
            var (port, address) = start.Result;

            _webUrl = $"http://{address ?? IPAddress.Loopback}:{port}";
            Raise(nameof(WebUrl));
            _engine.AttachWeb(_web);
            WebState = "On";
            if (address == null) SetHint("WebNoNetwork");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            _web.Stop();
            WebState = "Off";
            SetHint("WebPortBusy", _webPort);
        }
        catch (Exception ex)
        {
            _web.Stop();
            WebState = "Off";
            SetHint("WebFailed", ex.Message);
        }
    }

    private async Task RestartWebAsync()
    {
        _engine.AttachWeb(null);
        _web.Stop();
        await StartWebAsync();
    }

    private void StopWeb()
    {
        _engine.AttachWeb(null);
        _web.Stop();
        WebState = "Off";
        SetHint(null);
    }

    public string PrimaryName => _primary?.FriendlyName ?? Loc.I["NoDevice"];
    public string Info => _primary == null ? "" : Loc.I["Info"];
    public string Hint => _hintKey == null ? "" : Loc.I.Format(_hintKey, _hintArgs);

    private void SetHint(string? key, params object[] args)
    {
        _hintKey = key;
        _hintArgs = args;
        Raise(nameof(Hint));
    }

    public string ThemeMode => _settings.Theme;

    public string AccentName => _settings.Accent;
    public static IReadOnlyList<string> AccentNames => ThemeManager.AccentNames;

    public void SetAccent(string name)
    {
        if (_settings.Accent == name) return;
        _settings.Accent = name;
        SettingsStore.Save(_settings);
        ThemeManager.SetAccentName(name, animate: true);
        Raise(nameof(AccentName));
    }

    public void CycleTheme()
    {
        var next = _settings.Theme switch { "system" => "light", "light" => "dark", _ => "system" };
        _settings.Theme = next;
        SettingsStore.Save(_settings);
        ThemeManager.SetMode(next, animate: true);
        Raise(nameof(ThemeMode));
    }

    public void ToggleLang() => Loc.I.Lang = Loc.I.IsFa ? "en" : "fa";

    private void OnLangChanged()
    {
        _settings.Lang = Loc.I.Lang;
        SettingsStore.Save(_settings);
        Raise(nameof(Status));
        Raise(nameof(PrimaryName));
        Raise(nameof(Info));
        Raise(nameof(Hint));
        Raise(nameof(WebSub));
        Raise(nameof(WebVolumeText));
        foreach (var d in Devices) d.RefreshLoc();
    }

    public bool IsRunning
    {
        get => _running;
        set
        {
            if (value == _running) return;
            if (value)
            {
                if (!StartEngine())
                {
                    Application.Current.Dispatcher.BeginInvoke(() => Raise(nameof(IsRunning)));
                    return;
                }
            }
            else
            {
                _engine.Stop();
            }
            _running = value;
            Raise();
            Raise(nameof(Status));
        }
    }

    public string Status => IsRunning
        ? Loc.I.Format("Sharing", Loc.I.Num(Devices.Count(d => d.Enabled) + (WebEnabled ? 1 : 0)))
        : Loc.I["Stopped"];

    public void Refresh()
    {
        bool wasRunning = _running;
        if (_running) IsRunning = false;

        try { _primary = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { _primary = null; }

        Raise(nameof(PrimaryName));
        Raise(nameof(Info));
        SetHint(null);

        if (_primary != null) _spectrum.Start(_primary); else _spectrum.Stop();

        BuildDevices();
        _ = UpdateBatteryAsync();

        if (wasRunning && _primary != null) IsRunning = true;
    }

    private void BuildDevices()
    {
        string? primaryId = _primary?.ID;

        var wanted = new List<MMDevice>();
        foreach (var d in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (d.ID == primaryId) continue;
            if (d.FriendlyName.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)) continue;
            if (_hideVirtual && IsVirtual(d.FriendlyName)) continue;
            wanted.Add(d);
        }

        var keep = wanted.Select(d => d.ID).ToHashSet();
        for (int i = Devices.Count - 1; i >= 0; i--)
        {
            if (keep.Contains(Devices[i].Id)) continue;
            Devices[i].Changed -= OnDeviceChanged;
            Devices.RemoveAt(i);
        }

        int idx = 0;
        foreach (var d in wanted)
        {
            var existing = Devices.FirstOrDefault(x => x.Id == d.ID);
            if (existing == null)
            {
                var vm = new DeviceVM(d, DetectBluetooth(d));
                if (_settings.Devices.TryGetValue(vm.Id, out var cfg)) vm.Load(cfg);
                if (_batteryCache.TryGetValue(vm.Id, out var cached)) { vm.IsBluetooth = true; vm.Battery = cached; }
                vm.Changed += OnDeviceChanged;
                Devices.Insert(idx, vm);
            }
            else
            {
                int cur = Devices.IndexOf(existing);
                if (cur != idx) Devices.Move(cur, idx);
            }
            idx++;
        }
        Raise(nameof(Status));
    }

    private void OnDeviceChanged(DeviceVM d)
    {
        _settings.Devices[d.Id] = new DeviceSetting(d.Enabled, d.Volume, d.DelayMs);
        SettingsStore.Save(_settings);

        if (IsRunning)
        {
            try { _engine.Apply(d); SetHint(null); }
            catch (Exception ex) { SetHint("CouldntUpdate", d.BaseName, ex.Message); }
        }
        Raise(nameof(Status));
    }

    private bool StartEngine()
    {
        if (_primary == null) { SetHint("NoDefault"); return false; }
        if (!Devices.Any(d => d.Enabled) && !WebEnabled) { SetHint("EnableOne"); return false; }

        try
        {
            _engine.Start(_primary, Devices);
            SetHint(null);
            return true;
        }
        catch (Exception ex)
        {
            _engine.Stop();
            SetHint("CouldntStart", ex.Message);
            return false;
        }
    }

    private static bool DetectBluetooth(MMDevice d)
    {
        try
        {
            var key = new PropertyKey(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 24);
            if (d.Properties.Contains(key) && d.Properties[key].Value is string s)
                return s.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        return false;
    }

    private async Task UpdateBatteryAsync()
    {
        var map = await BatteryService.ReadAsync();
        foreach (var d in Devices)
        {
            var hit = map.FirstOrDefault(kv =>
                d.BaseName.Length > 0 && kv.Key.Length > 0 &&
                (d.BaseName.Contains(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                 kv.Key.Contains(d.BaseName, StringComparison.OrdinalIgnoreCase)));

            if (hit.Key != null)
            {
                d.IsBluetooth = true;
                d.Battery = hit.Value;
                _batteryCache[d.Id] = hit.Value;
            }
        }
    }

    public void Dispose()
    {
        ThemeManager.Changed -= PushTheme;
        _batteryTimer.Stop();
        _debounce.Stop();
        try { _enumerator.UnregisterEndpointNotificationCallback(_watcher); } catch { }
        _engine.Dispose();
        _web.Dispose();
        _spectrum.Dispose();
    }
}
