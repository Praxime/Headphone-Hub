using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;

namespace HeadphoneHub;

public sealed class DeviceVM : Observable
{
    private bool _enabled, _bluetooth;
    private int _volume = 100, _delay;
    private int? _battery;

    public DeviceVM(MMDevice device, bool bluetooth)
    {
        Device = device;
        Id = device.ID;
        _bluetooth = bluetooth;

        var friendly = device.FriendlyName;
        var m = Regex.Match(friendly, @"\((.*)\)");
        var inner = m.Success ? m.Groups[1].Value : friendly;
        BaseName = Regex.Replace(inner, @"\s+(Stereo|Hands-Free.*)$", "", RegexOptions.IgnoreCase).Trim();
        if (BaseName.Length == 0) BaseName = friendly;
    }

    public event Action<DeviceVM>? Changed;

    public MMDevice Device { get; }
    public string Id { get; }
    public string BaseName { get; }

    public bool Enabled
    {
        get => _enabled;
        set { if (Set(ref _enabled, value)) Changed?.Invoke(this); }
    }

    public int Volume
    {
        get => _volume;
        set { if (Set(ref _volume, Math.Clamp(value, 0, 100))) { Raise(nameof(VolumeText)); Changed?.Invoke(this); } }
    }

    public int DelayMs
    {
        get => _delay;
        set { if (Set(ref _delay, Math.Clamp(value, 0, 1000))) { Raise(nameof(DelayText)); Changed?.Invoke(this); } }
    }

    public bool IsBluetooth
    {
        get => _bluetooth;
        set
        {
            if (!Set(ref _bluetooth, value)) return;
            Raise(nameof(Kind));
        }
    }

    public int? Battery
    {
        get => _battery;
        set
        {
            if (!Set(ref _battery, value)) return;
            Raise(nameof(BatteryText));
        }
    }

    public string BatteryText => Battery is int b ? Loc.I.Num(b) + Loc.I.Pct : (IsBluetooth ? "—" : Loc.I["Wired"]);
    public string VolumeText => Loc.I.Num(Volume) + Loc.I.Pct;
    public string DelayText => Loc.I.Num(DelayMs) + " " + Loc.I["Ms"];

    public void RefreshLoc()
    {
        Raise(nameof(Kind));
        Raise(nameof(BatteryText));
        Raise(nameof(VolumeText));
        Raise(nameof(DelayText));
    }
    public override string ToString() => BaseName;

    public string Kind => IsBluetooth ? Loc.I["Bluetooth"] : Loc.I["WiredKind"];

    public void Load(DeviceSetting s)
    {
        _enabled = s.Enabled;
        _volume = Math.Clamp(s.Volume, 0, 100);
        _delay = Math.Clamp(s.DelayMs, 0, 1000);
    }
}
