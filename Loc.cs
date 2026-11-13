using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace HeadphoneHub;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();

    private static readonly Dictionary<string, (string En, string Fa)> T = new()
    {
        ["Title"]        = ("Headphones", "هدفون‌ها"),
        ["Stopped"]      = ("Stopped", "متوقف"),
        ["Sharing"]      = ("Sharing to {0} device(s)", "پخش روی {0} دستگاه"),
        ["Share"]        = ("Share audio", "پخش هم‌زمان"),
        ["ShareSub"]     = ("Play to all enabled devices at once", "صدا را هم‌زمان روی همه دستگاه‌های روشن پخش کن"),
        ["Primary"]      = ("PRIMARY OUTPUT (WINDOWS DEFAULT)", "خروجی اصلی (پیش‌فرض ویندوز)"),
        ["Info"]         = ("This device plays directly and is mirrored to the outputs below. For the best sync, make your slowest device (usually Bluetooth) the Windows default.",
                            "این دستگاه مستقیم پخش می‌کند و صدا روی خروجی‌های زیر هم کپی می‌شود. برای هماهنگی بهتر، کندترین دستگاه (معمولاً بلوتوث) را پیش‌فرض ویندوز کنید."),
        ["Outputs"]      = ("OUTPUTS", "خروجی‌ها"),
        ["HideVirtual"]  = ("Hide virtual devices", "پنهان‌کردن دستگاه‌های مجازی"),
        ["Volume"]       = ("Volume", "صدا"),
        ["Delay"]        = ("Delay (sync)", "تأخیر (همگام‌سازی)"),
        ["Bluetooth"]    = ("Bluetooth", "بلوتوث"),
        ["WiredKind"]    = ("Wired / built-in", "سیمی / داخلی"),
        ["Wired"]        = ("Wired", "سیمی"),
        ["Ms"]           = ("ms", "میلی‌ثانیه"),
        ["NoDevice"]     = ("No output device found", "دستگاه خروجی پیدا نشد"),
        ["NoDefault"]    = ("No Windows default output device found.", "خروجی پیش‌فرض ویندوز پیدا نشد."),
        ["EnableOne"]    = ("Turn on at least one output below first.", "ابتدا دست‌کم یک خروجی را روشن کنید."),
        ["CouldntStart"] = ("Couldn't start: {0}", "شروع نشد: {0}"),
        ["Web"]          = ("Web", "وب"),
        ["WebIdle"]      = ("Stream to a browser on your network", "پخش روی مرورگر در شبکهٔ محلی"),
        ["WebStarting"]  = ("Starting…", "در حال راه‌اندازی…"),
        ["WebNobody"]    = ("No one listening yet", "هنوز شنونده‌ای نیست"),
        ["WebListeners"] = ("{0} listening", "{0} شنونده"),
        ["WebNoNetwork"] = ("No local network found, so only this PC can open the link.", "شبکهٔ محلی پیدا نشد؛ فقط همین رایانه می‌تواند لینک را باز کند."),
        ["WebFailed"]    = ("Couldn't start web streaming: {0}", "راه‌اندازی پخش وب ممکن نشد: {0}"),
        ["WebPort"]      = ("Port", "پورت"),
        ["WebPortRandom"]= ("Random", "تصادفی"),
        ["WebPortInvalid"]= ("Enter a port between 1024 and 65535.", "پورت را بین ۱۰۲۴ تا ۶۵۵۳۵ وارد کنید."),
        ["WebPortBusy"]  = ("Port {0} is already in use. Pick another one.", "پورت {0} از قبل استفاده می‌شود. پورت دیگری انتخاب کنید."),
        ["PortTitle"]    = ("Choose a port", "انتخاب پورت"),
        ["PortSub"]      = ("Pick a random port, or type the one you want (1024–65535).", "یک پورت تصادفی بگیرید یا پورت دلخواه را بنویسید (۱۰۲۴ تا ۶۵۵۳۵)."),
        ["PortRandomGo"] = ("Use a random port", "پورت تصادفی"),
        ["PortOr"]       = ("or", "یا"),
        ["PortHolder"]   = ("Port", "پورت"),
        ["PortEnter"]    = ("Enter", "تأیید"),
        ["Cancel"]       = ("Cancel", "لغو"),
        ["QrTitle"]      = ("Listen on your phone", "گوش دادن روی گوشی"),
        ["QrStep"]       = ("Connect your phone to the same Wi-Fi / internet as this PC, then scan the code with your camera.",
                            "گوشی را به همان وای‌فای / شبکهٔ این رایانه وصل کنید، سپس کد را با دوربین اسکن کنید."),
        ["QrFirewall"]   = ("Not opening? Allow Headphone Hub through Windows Firewall on private networks.",
                            "باز نمی‌شود؟ در فایروال ویندوز اجازهٔ دسترسی Headphone Hub را در شبکه‌های خصوصی بدهید."),
        ["CopyLink"]     = ("Copy link", "کپی لینک"),
        ["Copied"]       = ("Copied", "کپی شد"),
        ["Close"]        = ("Close", "بستن"),
        ["CouldntUpdate"]= ("Couldn't update {0}: {1}", "به‌روزرسانی {0} ممکن نشد: {1}"),
    };

    private static readonly FontFamily FaFont = new(new Uri("pack://application:,,,/"), "./Fonts/#Vazirmatn, Segoe UI");
    private static readonly FontFamily EnFont = new("Segoe UI Variable Display, Segoe UI");

    private string _lang = "en";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Lang
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFa)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Font)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Flow)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lang)));
        }
    }

    public bool IsFa => _lang == "fa";

    public FontFamily Font => IsFa ? FaFont : EnFont;

    public FlowDirection Flow => IsFa ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string this[string key] =>
        T.TryGetValue(key, out var v) ? (IsFa ? v.Fa : v.En) : key;

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    public string Pct => IsFa ? "٪" : "%";

    public string Num(int n)
    {
        var s = n.ToString(CultureInfo.InvariantCulture);
        if (!IsFa) return s;
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
        return new string(chars);
    }
}
