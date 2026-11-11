using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace HeadphoneHub;

public sealed class SpectrumAnalyzer : IDisposable
{
    public const int Bands = 40;
    private const int N = 2048, M = 11;

    private readonly float[] _ring = new float[N];
    private readonly Complex[] _fft = new Complex[N];
    private readonly float[] _window = new float[N];
    private readonly float[] _bands = new float[Bands];
    private readonly object _gate = new();
    private int[] _edges = new int[Bands + 1];
    private int _pos;
    private long _lastTicks;
    private WasapiLoopbackCapture? _cap;

    public SpectrumAnalyzer()
    {
        for (int i = 0; i < N; i++)
            _window[i] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (N - 1))));
    }

    public void Start(MMDevice device)
    {
        Stop();
        try
        {
            var cap = new WasapiLoopbackCapture(device);
            int sr = cap.WaveFormat.SampleRate;
            double fmin = 45, fmax = Math.Min(14000, sr / 2.0 * 0.9);

            var edges = new int[Bands + 1];
            for (int b = 0; b <= Bands; b++)
            {
                double f = fmin * Math.Pow(fmax / fmin, (double)b / Bands);
                edges[b] = (int)Math.Round(f * N / sr);
            }
            for (int b = 1; b <= Bands; b++)
                if (edges[b] <= edges[b - 1]) edges[b] = edges[b - 1] + 1;
            _edges = edges;

            cap.DataAvailable += OnData;
            cap.StartRecording();
            _cap = cap;
        }
        catch { _cap = null; }
    }

    public void Stop()
    {
        var cap = _cap;
        _cap = null;
        if (cap == null) return;
        cap.DataAvailable -= OnData;
        try { cap.StopRecording(); } catch { }
        cap.Dispose();
    }

    public bool Fill(float[] dest)
    {
        lock (_gate)
        {
            if (Stopwatch.GetTimestamp() - _lastTicks > Stopwatch.Frequency / 4) return false;
            Array.Copy(_bands, dest, Bands);
            return true;
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var wf = ((WasapiLoopbackCapture)sender!).WaveFormat;
        int ch = Math.Max(1, wf.Channels), bytes = wf.BitsPerSample / 8;
        if (bytes != 4 && bytes != 2) return;
        int frames = e.BytesRecorded / (bytes * ch);
        if (frames == 0) return;

        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            for (int c = 0; c < ch; c++)
            {
                int idx = (f * ch + c) * bytes;
                sum += bytes == 4 ? BitConverter.ToSingle(e.Buffer, idx)
                                  : BitConverter.ToInt16(e.Buffer, idx) / 32768f;
            }
            _ring[_pos] = sum / ch;
            _pos = (_pos + 1) % N;
        }

        for (int i = 0; i < N; i++)
        {
            _fft[i].X = _ring[(_pos + i) % N] * _window[i];
            _fft[i].Y = 0;
        }
        FastFourierTransform.FFT(true, M, _fft);

        var edges = _edges;
        lock (_gate)
        {
            for (int b = 0; b < Bands; b++)
            {
                float peak = 0;
                for (int k = edges[b]; k < edges[b + 1] && k < N / 2; k++)
                {
                    float mag = MathF.Sqrt(_fft[k].X * _fft[k].X + _fft[k].Y * _fft[k].Y);
                    if (mag > peak) peak = mag;
                }
                float db = 20f * MathF.Log10(peak * 4f + 1e-9f);
                float v = (db + 80f) / 62f;
                _bands[b] = v <= 0 ? 0 : Math.Clamp(v + 0.15f * b / Bands, 0f, 1f);
            }
            _lastTicks = Stopwatch.GetTimestamp();
        }
    }

    public void Dispose() => Stop();
}

public sealed class SpectrumView : FrameworkElement
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(SpectrumView),
        new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MirrorProperty = DependencyProperty.Register(
        nameof(Mirror), typeof(bool), typeof(SpectrumView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(SpectrumView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public bool Mirror
    {
        get => (bool)GetValue(MirrorProperty);
        set => SetValue(MirrorProperty, value);
    }

    public SpectrumAnalyzer? Source { get; set; }

    private const int B = SpectrumAnalyzer.Bands;
    private readonly float[] _raw = new float[B], _cur = new float[B], _peak = new float[B];
    private readonly Brush _bg;
    private Brush _bar, _cap;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public SpectrumView()
    {
        _bg = Frozen(new SolidColorBrush(Color.FromRgb(0x0D, 0x0E, 0x13)));
        _bar = Frozen(new LinearGradientBrush(Color.FromRgb(0x8C, 0xC4, 0xFF), Color.FromRgb(0x2F, 0x6D, 0xC8), 90));
        _cap = Frozen(new SolidColorBrush(Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF)));
        RefreshTheme();

        Loaded += (_, _) => { CompositionTarget.Rendering += OnFrame; ThemeManager.Changed += OnThemeChanged; RefreshTheme(); };
        Unloaded += (_, _) => { CompositionTarget.Rendering -= OnFrame; ThemeManager.Changed -= OnThemeChanged; };
    }

    private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    private void OnThemeChanged() => Dispatcher.Invoke(RefreshTheme);

    private void RefreshTheme()
    {
        Color accent = Application.Current?.Resources["Accent"] is SolidColorBrush b
            ? b.Color
            : Color.FromRgb(0x2F, 0x6D, 0xC8);
        Color light = Lighten(accent, 0.45f);
        _bar = Frozen(new LinearGradientBrush(light, accent, 90));
        InvalidateVisual();
    }

    private static Color Lighten(Color c, float t) => Color.FromArgb(
        c.A,
        (byte)(c.R + (255 - c.R) * t),
        (byte)(c.G + (255 - c.G) * t),
        (byte)(c.B + (255 - c.B) * t));

    private void OnFrame(object? sender, EventArgs e)
    {
        double t = _clock.Elapsed.TotalSeconds;
        bool live = Source?.Fill(_raw) ?? false;

        double p = Math.Min(1.0, t / 1.1);
        float intro = (float)(1 - Math.Pow(1 - p, 3));

        for (int i = 0; i < B; i++)
        {
            float idle = 0.09f + 0.06f * (float)Math.Sin(t * 1.7 + i * 0.42) + 0.03f * (float)Math.Sin(t * 2.9 - i * 0.8);
            float target = (live ? Math.Max(_raw[i], 0.04f) : idle) * intro;

            float k = target > _cur[i] ? 0.45f : 0.12f;
            _cur[i] += (target - _cur[i]) * k;
            _peak[i] = Math.Max(_cur[i], _peak[i] - 0.007f);
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;

        dc.DrawRoundedRectangle(Fill ?? _bg, null, new Rect(0, 0, w, h), CornerRadius, CornerRadius);

        const double padX = 18, padY = 16, gap = 3;
        double usableW = w - 2 * padX, usableH = h - 2 * padY;
        double bw = (usableW - gap * (B - 1)) / B;
        if (bw < 1 || usableH < 4) return;

        double r = bw / 2, baseY = padY + usableH, midY = padY + usableH / 2;
        bool mirror = Mirror;

        for (int i = 0; i < B; i++)
        {
            double v = Math.Clamp(_cur[i], 0f, 1f);
            double bh = Math.Max(bw, v * usableH);
            double x = padX + i * (bw + gap);

            var rect = mirror
                ? new Rect(x, midY - bh / 2, bw, bh)
                : new Rect(x, baseY - bh, bw, bh);
            dc.DrawRoundedRectangle(_bar, null, rect, r, r);

            if (!mirror)
            {
                double pv = Math.Clamp(_peak[i], 0f, 1f);
                double capTop = baseY - Math.Max(bw, pv * usableH) - bw - 3;
                if (capTop < rect.Top - 4 && capTop > 0)
                    dc.DrawRoundedRectangle(_cap, null, new Rect(x, capTop, bw, bw * 0.8), r, r);
            }
        }
    }
}
