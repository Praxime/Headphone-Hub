using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace HeadphoneHub;

public sealed class AudioEngine : IDisposable
{
    private sealed record Route(
        BufferedWaveProvider Buffer,
        DelaySampleProvider Delay,
        VolumeSampleProvider Volume,
        WasapiOut Output);

    private readonly object _gate = new();
    private readonly Dictionary<string, Route> _routes = new();
    private WasapiLoopbackCapture? _capture;

    public bool IsRunning => _capture != null;

    public void Start(MMDevice source, IEnumerable<DeviceVM> targets)
    {
        Stop();
        _capture = new WasapiLoopbackCapture(source);
        _capture.DataAvailable += OnData;

        foreach (var t in targets.Where(t => t.Enabled))
            AddRoute(t);

        _capture.StartRecording();
    }

    public void Stop()
    {
        var cap = _capture;
        _capture = null;
        if (cap != null)
        {
            cap.DataAvailable -= OnData;
            try { cap.StopRecording(); } catch { }
            cap.Dispose();
        }

        lock (_gate)
        {
            foreach (var r in _routes.Values)
            {
                try { r.Output.Stop(); } catch { }
                r.Output.Dispose();
            }
            _routes.Clear();
        }
    }

    public void Apply(DeviceVM d)
    {
        if (_capture == null) return;

        Route? route;
        lock (_gate) _routes.TryGetValue(d.Id, out route);

        if (!d.Enabled)
        {
            if (route != null) RemoveRoute(d.Id);
            return;
        }

        if (route == null)
        {
            AddRoute(d);
            return;
        }

        route.Volume.Volume = d.Volume / 100f;
        route.Delay.DelayMs = d.DelayMs;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            foreach (var r in _routes.Values)
                r.Buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void AddRoute(DeviceVM d)
    {
        var cap = _capture;
        if (cap == null) return;

        var buffer = new BufferedWaveProvider(cap.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(1),
            ReadFully = true
        };
        var delay = new DelaySampleProvider(buffer.ToSampleProvider()) { DelayMs = d.DelayMs };
        var volume = new VolumeSampleProvider(delay) { Volume = d.Volume / 100f };

        var output = new WasapiOut(d.Device, AudioClientShareMode.Shared, true, 100);
        output.Init(volume.ToWaveProvider());
        output.Play();

        lock (_gate) _routes[d.Id] = new Route(buffer, delay, volume, output);
    }

    private void RemoveRoute(string id)
    {
        Route? r;
        lock (_gate)
        {
            if (!_routes.Remove(id, out r)) return;
        }
        try { r.Output.Stop(); } catch { }
        r.Output.Dispose();
    }

    public void Dispose() => Stop();
}
