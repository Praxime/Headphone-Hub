using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HeadphoneHub;

public static class AppIcon
{
    private static readonly Brush Tile = Frozen(new SolidColorBrush(Color.FromRgb(10, 10, 10)));
    private static readonly Geometry Band = Frozen(Geometry.Parse("M15,28 A17.3,17.3 0 0 1 49,28"));

    public static BitmapSource Render(Brush glyph, int size = 256)
    {
        var pen = new Pen(glyph, 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 64.0, size / 64.0));
            dc.DrawRoundedRectangle(Tile, null, new Rect(2, 2, 60, 60), 7, 7);
            dc.DrawGeometry(null, pen, Band);
            dc.DrawRoundedRectangle(glyph, null, new Rect(14, 32, 8, 20), 4, 4);
            dc.DrawRoundedRectangle(glyph, null, new Rect(42, 32, 8, 20), 4, 4);
            dc.DrawRoundedRectangle(glyph, null, new Rect(9, 35, 4, 14), 2, 2);
            dc.DrawRoundedRectangle(glyph, null, new Rect(51, 35, 4, 14), 2, 2);
            dc.Pop();
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static T Frozen<T>(T item) where T : Freezable
    {
        item.Freeze();
        return item;
    }
}
