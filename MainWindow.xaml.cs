using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

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

    public static readonly DependencyProperty SmoothOffsetProperty =
        DependencyProperty.Register(nameof(SmoothOffset), typeof(double), typeof(MainWindow),
            new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).Scroller.ScrollToVerticalOffset((double)e.NewValue)));

    public double SmoothOffset
    {
        get => (double)GetValue(SmoothOffsetProperty);
        set => SetValue(SmoothOffsetProperty, value);
    }

    private double _scrollTarget;
    private bool _smoothing;
    private int _scrollId;

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (Scroller.ScrollableHeight <= 0) return;
        e.Handled = true;

        double basis = _smoothing ? _scrollTarget : Scroller.VerticalOffset;
        _scrollTarget = Math.Clamp(basis - e.Delta * 0.9, 0, Scroller.ScrollableHeight);

        double from = Scroller.VerticalOffset;
        if (Math.Abs(_scrollTarget - from) < 0.5) return;

        _smoothing = true;
        int id = ++_scrollId;
        double target = _scrollTarget;
        var anim = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(520))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) =>
        {
            if (id != _scrollId) return;
            BeginAnimation(SmoothOffsetProperty, null);
            SmoothOffset = target;
            _smoothing = false;
        };
        BeginAnimation(SmoothOffsetProperty, anim);
    }

    private readonly List<FrameworkElement> _pendingCards = new();
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private bool _revealQueued;

    private void OnCardLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement card && !_pendingCards.Contains(card)) _pendingCards.Add(card);
        QueueReveal();
    }

    private void OnScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (_pendingCards.Count > 0) QueueReveal();
    }

    private void QueueReveal()
    {
        if (_revealQueued) return;
        _revealQueued = true;
        Dispatcher.BeginInvoke(RevealVisibleCards, DispatcherPriority.Background);
    }

    private void RevealVisibleCards()
    {
        _revealQueued = false;
        _pendingCards.RemoveAll(c => !c.IsLoaded);
        double viewport = Scroller.ViewportHeight;
        if (_pendingCards.Count == 0) return;
        if (viewport <= 0) { QueueReveal(); return; }

        bool startup = (DateTime.UtcNow - _startedAt).TotalMilliseconds < 1500;
        double baseDelay = startup ? 120 + (Stack.Children.Count - 1) * 70 : 0;
        int k = 0;

        foreach (var card in _pendingCards.ToList())
        {
            double top;
            try { top = card.TranslatePoint(new Point(0, 0), Scroller).Y; }
            catch { continue; }
            if (top > viewport - 24) continue;

            _pendingCards.Remove(card);
            var begin = TimeSpan.FromMilliseconds(baseDelay + k++ * 70);
            card.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { BeginTime = begin });
            var move = new TranslateTransform(0, 18);
            card.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(480)) { BeginTime = begin, EasingFunction = EaseOut });
        }
    }

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
        _vm.PortPromptRequested += OpenPortPrompt;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WebEnabled) && !_vm.WebEnabled) CloseQr();
            if (e.PropertyName == nameof(MainViewModel.WebState)) UpdateWebSwitch(animate: true);
        };

        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWebSwitch(animate: false);
        QueueReveal();
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

    private void OnWebToggle(object sender, RoutedEventArgs e) => _vm.ToggleWeb();

    private void OnPortInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void OnPortKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not System.Windows.Controls.TextBox box) return;
        e.Handled = true;
        System.Windows.Data.BindingOperations.GetBindingExpression(box, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        Keyboard.ClearFocus();
    }

    private void OnRandomPort(object sender, RoutedEventArgs e)
    {
        PortBox.Text = "";
        System.Windows.Data.BindingOperations.GetBindingExpression(PortBox, System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
        Keyboard.ClearFocus();
    }

    private void UpdateWebSwitch(bool animate)
    {
        WebToggle.ApplyTemplate();
        if (WebToggle.Template.FindName("KnobT", WebToggle) is not TranslateTransform knob ||
            WebToggle.Template.FindName("On", WebToggle) is not UIElement track ||
            WebToggle.Template.FindName("Pulse", WebToggle) is not UIElement pulse) return;

        string state = _vm.WebState;
        double x = state switch { "On" => 18, "Busy" => 9, _ => 0 };
        double green = state == "On" ? 1 : 0;

        pulse.BeginAnimation(OpacityProperty, null);
        pulse.Opacity = 0;

        if (!animate)
        {
            knob.BeginAnimation(TranslateTransform.XProperty, null);
            knob.X = x;
            track.BeginAnimation(OpacityProperty, null);
            track.Opacity = green;
            return;
        }

        var ease = state == "Off" ? EaseOut : Spring;
        knob.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(x, TimeSpan.FromMilliseconds(state == "Off" ? 280 : 320)) { EasingFunction = ease });
        track.BeginAnimation(OpacityProperty, new DoubleAnimation(green, TimeSpan.FromMilliseconds(220)));

        if (state == "Busy")
        {
            pulse.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.1, 0.55, TimeSpan.FromMilliseconds(550))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
    }

    private void OnQr(object sender, RoutedEventArgs e)
    {
        if (!_vm.WebEnabled) return;

        QrImage.Source = QrRenderer.Render(_vm.WebUrl);
        CopyLabel.Opacity = 1;
        CopiedLabel.Opacity = 0;

        QrOverlay.Visibility = Visibility.Visible;
        QrOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        var pop = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Spring };
        QrScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        QrScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    private void CloseQr()
    {
        if (QrOverlay.Visibility != Visibility.Visible) return;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) =>
        {
            if (QrOverlay.Opacity == 0) QrOverlay.Visibility = Visibility.Collapsed;
        };
        QrOverlay.BeginAnimation(OpacityProperty, fade);

        var shrink = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut };
        QrScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        QrScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
    }

    private void OnQrBackdrop(object sender, MouseButtonEventArgs e) => CloseQr();

    private void OnQrCard(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnQrCloseClick(object sender, RoutedEventArgs e) => CloseQr();

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (PortOverlay.Visibility == Visibility.Visible) { e.Handled = true; ClosePortPrompt(); return; }
        if (QrOverlay.Visibility != Visibility.Visible) return;
        e.Handled = true;
        CloseQr();
    }

    private void OpenPortPrompt()
    {
        PromptBox.Text = _vm.SavedPortText;
        PromptError.Visibility = Visibility.Collapsed;

        PortOverlay.Visibility = Visibility.Visible;
        PortOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        var pop = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Spring };
        PortScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        PortScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        Dispatcher.BeginInvoke(() => { PromptBox.Focus(); PromptBox.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void ClosePortPrompt()
    {
        if (PortOverlay.Visibility != Visibility.Visible) return;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        fade.Completed += (_, _) =>
        {
            if (PortOverlay.Opacity == 0) PortOverlay.Visibility = Visibility.Collapsed;
        };
        PortOverlay.BeginAnimation(OpacityProperty, fade);

        var shrink = new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut };
        PortScale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        PortScale.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        Keyboard.ClearFocus();
    }

    private void OnPortBackdrop(object sender, MouseButtonEventArgs e) => ClosePortPrompt();

    private void OnPromptCancel(object sender, RoutedEventArgs e) => ClosePortPrompt();

    private void OnPromptRandom(object sender, RoutedEventArgs e)
    {
        ClosePortPrompt();
        _vm.StartWebOnPort(0);
    }

    private void OnPromptEnter(object sender, RoutedEventArgs e) => SubmitPrompt();

    private void OnPromptKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        SubmitPrompt();
    }

    private void OnPromptChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (PromptError != null) PromptError.Visibility = Visibility.Collapsed;
    }

    private void SubmitPrompt()
    {
        if (!_vm.TryParsePort(PromptBox.Text, out int port))
        {
            PromptError.Visibility = Visibility.Visible;
            return;
        }
        ClosePortPrompt();
        _vm.StartWebOnPort(port);
    }

    private async void OnCopyLink(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(_vm.WebUrl); }
        catch { return; }

        var quick = TimeSpan.FromMilliseconds(160);
        CopyLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, quick));
        CopiedLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, quick));

        await Task.Delay(1400);

        CopyLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(1, quick));
        CopiedLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, quick));
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        RefreshRot.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(550)) { EasingFunction = EaseOut });
        _vm.Refresh();
    }
}
