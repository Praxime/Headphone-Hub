using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace HeadphoneHub;

public static class ThemeManager
{
    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static readonly Dictionary<string, Color> Light = new()
    {
        ["Bg"] = C("#F2F2F7"),        ["Card"] = C("#FFFFFF"),     ["Label"] = C("#000000"),
        ["Secondary"] = C("#8E8E93"), ["Accent"] = C("#007AFF"),   ["Green"] = C("#34C759"),
        ["Fill"] = C("#E9E9EA"),      ["Pill"] = C("#E5E5EA"),     ["PillThumb"] = C("#FFFFFF"),
        ["IconFg"] = C("#3C3C43"),    ["Hover"] = C("#14000000"),  ["VizBg"] = C("#E6E6EC"),
    };

    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["Bg"] = C("#0E0E11"),        ["Card"] = C("#1C1C1F"),     ["Label"] = C("#FFFFFF"),
        ["Secondary"] = C("#98989F"), ["Accent"] = C("#0A84FF"),   ["Green"] = C("#30D158"),
        ["Fill"] = C("#3A3A3E"),      ["Pill"] = C("#2A2A2E"),     ["PillThumb"] = C("#55555A"),
        ["IconFg"] = C("#D1D1D6"),    ["Hover"] = C("#22FFFFFF"),  ["VizBg"] = C("#16171C"),
    };

    private static readonly Dictionary<string, (Color Light, Color Dark)> Accents = new()
    {
        ["blue"] = (C("#007AFF"), C("#0A84FF")),
        ["red"] = (C("#FF3B30"), C("#FF453A")),
        ["green"] = (C("#34C759"), C("#30D158")),
        ["yellow"] = (C("#FFCC00"), C("#FFD60A")),
        ["orange"] = (C("#FF9500"), C("#FF9F0A")),
        ["pink"] = (C("#FF2D55"), C("#FF375F")),
    };

    public static IReadOnlyList<string> AccentNames { get; } = new[] { "blue", "red", "green", "yellow", "orange", "pink" };

    private const double FadeMs = 320;

    private static readonly Dictionary<string, Color> Shown = new();
    private static Dictionary<string, Color> _from = new(), _to = new();
    private static readonly Stopwatch Clock = new();
    private static string _mode = "system";
    private static string _accent = "blue";
    private static bool _ready, _animating;

    public static bool IsDark { get; private set; }
    public static string AccentName => _accent;

    public static event Action? Changed;

    public static void Init()
    {
        if (_ready) return;
        _ready = true;

        Apply(animate: false);

        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && _mode == "system")
                Application.Current?.Dispatcher.BeginInvoke(() => Apply(animate: true));
        };
    }

    public static void SetMode(string mode, bool animate)
    {
        _mode = mode is "light" or "dark" ? mode : "system";
        if (_ready) Apply(animate);
    }

    public static void SetAccentName(string name, bool animate = true)
    {
        _accent = Accents.ContainsKey(name) ? name : "blue";
        if (_ready) Apply(animate);
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1) is int v && v == 0;
        }
        catch { return false; }
    }

    private static void Apply(bool animate)
    {
        IsDark = _mode == "dark" || (_mode == "system" && SystemPrefersDark());
        var pal = new Dictionary<string, Color>(IsDark ? Dark : Light);
        var accent = Accents.TryGetValue(_accent, out var a) ? a : Accents["blue"];
        pal["Accent"] = IsDark ? accent.Dark : accent.Light;

        _from = new Dictionary<string, Color>(Shown);
        _to = new Dictionary<string, Color>(pal);

        if (!animate || Shown.Count == 0)
        {
            StopFade();
            Push(_to);
            Changed?.Invoke();
            return;
        }

        Clock.Restart();
        if (!_animating)
        {
            _animating = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    private static void OnFrame(object? sender, EventArgs e)
    {
        double p = Math.Min(1.0, Clock.Elapsed.TotalMilliseconds / FadeMs);
        double k = 1 - Math.Pow(1 - p, 3);

        var mix = new Dictionary<string, Color>();
        foreach (var (key, to) in _to)
            mix[key] = Lerp(_from.TryGetValue(key, out var f) ? f : to, to, k);
        Push(mix);

        if (p >= 1.0)
        {
            StopFade();
            Changed?.Invoke();
        }
    }

    private static void StopFade()
    {
        if (!_animating) return;
        _animating = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private static void Push(Dictionary<string, Color> colors)
    {
        var res = Application.Current.Resources;
        foreach (var (key, color) in colors)
        {
            Shown[key] = color;
            var b = new SolidColorBrush(color);
            b.Freeze();
            res[key] = b;
        }

        var bg = colors["Bg"];
        var fade = new LinearGradientBrush(Color.FromArgb(0, bg.R, bg.G, bg.B), bg, 90);
        fade.Freeze();
        res["Fade"] = fade;
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * t),
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
