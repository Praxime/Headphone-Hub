using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;

namespace HeadphoneHub;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class DelaySampleProvider : ISampleProvider
{
    private const int MaxDelayMs = 2000;
    private readonly ISampleProvider _src;
    private readonly float[] _ring;
    private int _write;
    private volatile int _delaySamples;

    public DelaySampleProvider(ISampleProvider src)
    {
        _src = src;
        _ring = new float[(src.WaveFormat.SampleRate * src.WaveFormat.Channels * (MaxDelayMs + 100)) / 1000];
    }

    public WaveFormat WaveFormat => _src.WaveFormat;

    public int DelayMs
    {
        set
        {
            int ch = WaveFormat.Channels;
            int samples = value * WaveFormat.SampleRate / 1000 * ch;
            _delaySamples = Math.Clamp(samples - samples % ch, 0, _ring.Length - ch);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int n = _src.Read(buffer, offset, count);
        int delay = _delaySamples;
        for (int i = 0; i < n; i++)
        {
            _ring[_write] = buffer[offset + i];
            int read = _write - delay;
            if (read < 0) read += _ring.Length;
            buffer[offset + i] = _ring[read];
            if (++_write == _ring.Length) _write = 0;
        }
        return n;
    }
}

public sealed record DeviceSetting(bool Enabled, int Volume, int DelayMs);

public sealed class AppSettings
{
    public bool HideVirtual { get; set; } = true;
    public string Lang { get; set; } = "en";
    public string Theme { get; set; } = "system";
    public string Accent { get; set; } = "blue";
    public int WebVolume { get; set; } = 100;
    public int WebPort { get; set; }
    public Dictionary<string, DeviceSetting> Devices { get; set; } = new();
}

public static class SettingsStore
{
    private static readonly string PathFile = System.IO.Path.Combine(AppPaths.DataDir, "settings.json");

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(PathFile)) ?? new(); }
        catch { return new(); }
    }

    public static void Save(AppSettings s)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathFile)!);
            File.WriteAllText(PathFile, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public static class BatteryService
{
    private const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private static readonly string[] Protocols =
    {
        "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}",
        "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}"
    };

    public static async Task<Dictionary<string, int>> ReadAsync()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var nodes = await PnpObject.FindAllAsync(
                PnpObjectType.Device, new[] { BatteryKey, "System.ItemNameDisplay" });

            foreach (var n in nodes)
            {
                try
                {
                    if (!n.Properties.TryGetValue(BatteryKey, out var v) || v == null) continue;
                    string? name = n.Properties.TryGetValue("System.ItemNameDisplay", out var nm) ? nm as string : null;
                    int pct = Convert.ToInt32(v);
                    if (!string.IsNullOrWhiteSpace(name) && pct is >= 0 and <= 100)
                        result[name] = pct;
                }
                catch { }
            }
        }
        catch { }

        foreach (var protocol in Protocols)
        {
            try
            {
                var infos = await DeviceInformation.FindAllAsync(
                    $"System.Devices.Aep.ProtocolId:=\"{protocol}\"",
                    new[] { BatteryKey },
                    DeviceInformationKind.AssociationEndpoint);

                foreach (var d in infos)
                {
                    if (d.Properties.TryGetValue(BatteryKey, out var v) && v != null && !result.ContainsKey(d.Name))
                        result[d.Name] = Convert.ToInt32(v);
                }
            }
            catch { }
        }
        return result;
    }
}
