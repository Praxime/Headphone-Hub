using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HeadphoneHub;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, e) => { e.Handled = true; ReportCrash(e.Exception); Shutdown(1); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash(e.ExceptionObject as Exception);
    }

    internal static void ReportCrash(Exception? ex)
    {
        var text = ex?.ToString() ?? "Unknown error";
        try
        {
            var dir = AppPaths.DataDir;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "crash.log"), text);
        }
        catch { }
        MessageBox.Show(text, "Headphone Hub crashed", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeManager.Init();
        base.OnStartup(e);
    }
}

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private bool _closing, _switchingLang;

    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction Spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Viz.Source = _vm.Spectrum;
        _vm.Refresh();

        UpdateLangPill(animate: false);
        UpdateThemeIcon(animate: false);
        UpdateAccentDots(animate: false);
        UpdateWindowIcon();
        Loc.I.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Loc.Lang)) UpdateLangPill(animate: true);
        };
        ThemeManager.Changed += () => Dispatcher.BeginInvoke(UpdateWindowIcon);

        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)));
        var pop = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(480)) { EasingFunction = Spring };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        for (int i = 0; i < Stack.Children.Count; i++)
        {
            var el = Stack.Children[i];
            if (el is System.Windows.Controls.ItemsControl) continue;

            var move = new TranslateTransform(0, 18);
            el.RenderTransform = move;
            el.Opacity = 0;

            var begin = TimeSpan.FromMilliseconds(120 + i * 70);
            el.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { BeginTime = begin });
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(480)) { BeginTime = begin, EasingFunction = EaseOut });
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (_closing) return;
        _closing = true;

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) => Close();
        var shrink = new DoubleAnimation(1, 0.95, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        Root.BeginAnimation(OpacityProperty, fade);
    }

    private void OnLang(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_switchingLang) return;
        _switchingLang = true;

        var down = new DoubleAnimation(1, 0.15, TimeSpan.FromMilliseconds(120));
        down.Completed += (_, _) =>
        {
            _vm.ToggleLang();
            var up = new DoubleAnimation(0.15, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut };
            up.Completed += (_, _) => _switchingLang = false;
            Stack.BeginAnimation(OpacityProperty, up);
        };
        Stack.BeginAnimation(OpacityProperty, down);
    }

    private void UpdateLangPill(bool animate)
    {
        double x = Loc.I.IsFa ? 38 : 0;
        double en = Loc.I.IsFa ? 0.4 : 1, fa = Loc.I.IsFa ? 1 : 0.4;

        if (!animate)
        {
            LangT.X = x;
            LabelEn.Opacity = en;
            LabelFa.Opacity = fa;
            return;
        }

        LangT.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(x, TimeSpan.FromMilliseconds(320)) { EasingFunction = Spring });
        LabelEn.BeginAnimation(OpacityProperty, new DoubleAnimation(en, TimeSpan.FromMilliseconds(200)));
        LabelFa.BeginAnimation(OpacityProperty, new DoubleAnimation(fa, TimeSpan.FromMilliseconds(200)));
    }

    private void OnTheme(object sender, RoutedEventArgs e)
    {
        _vm.CycleTheme();
        UpdateThemeIcon(animate: true);
    }

    private void UpdateThemeIcon(bool animate)
    {
        SetIcon(ThemeSys, ThemeSysS, _vm.ThemeMode == "system");
        SetIcon(ThemeLight, ThemeLightS, _vm.ThemeMode == "light");
        SetIcon(ThemeDark, ThemeDarkS, _vm.ThemeMode == "dark");

        void SetIcon(UIElement icon, ScaleTransform scale, bool on)
        {
            double op = on ? 1 : 0, sc = on ? 1 : 0.4;
            if (!animate)
            {
                icon.Opacity = op;
                scale.ScaleX = scale.ScaleY = sc;
                return;
            }
            icon.BeginAnimation(OpacityProperty, new DoubleAnimation(op, TimeSpan.FromMilliseconds(220)));
            var grow = new DoubleAnimation(sc, TimeSpan.FromMilliseconds(320)) { EasingFunction = on ? Spring : EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }

    private void OnAccent(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement el || el.Tag is not string name) return;
        _vm.SetAccent(name);
        UpdateAccentDots(animate: true);
        UpdateWindowIcon();
    }

    private void UpdateAccentDots(bool animate)
    {
        SetRing(RingBlue, "blue");
        SetRing(RingRed, "red");
        SetRing(RingGreen, "green");
        SetRing(RingYellow, "yellow");
        SetRing(RingOrange, "orange");
        SetRing(RingPink, "pink");

        void SetRing(Ellipse ring, string name)
        {
            double op = _vm.AccentName == name ? 1 : 0;
            if (!animate) { ring.Opacity = op; return; }
            ring.BeginAnimation(OpacityProperty, new DoubleAnimation(op, TimeSpan.FromMilliseconds(220)));
        }
    }

    private void UpdateWindowIcon()
    {
        var accent = Application.Current.Resources["Accent"] as Brush ?? Brushes.DeepSkyBlue;
        Icon = AppIcon.Render(accent);
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        RefreshRot.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(550)) { EasingFunction = EaseOut });
        _vm.Refresh();
    }
}
