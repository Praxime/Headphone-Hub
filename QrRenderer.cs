using System.Windows;
using System.Windows.Media;
using QRCoder;

namespace HeadphoneHub;

public static class QrRenderer
{
    public static DrawingImage Render(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        int n = matrix.Count;

        var modules = new StreamGeometry();
        using (var ctx = modules.Open())
        {
            for (int y = 0; y < n; y++)
            {
                int x = 0;
                while (x < n)
                {
                    if (!matrix[y][x]) { x++; continue; }
                    int start = x;
                    while (x < n && matrix[y][x]) x++;

                    ctx.BeginFigure(new Point(start, y), true, true);
                    ctx.LineTo(new Point(x, y), false, false);
                    ctx.LineTo(new Point(x, y + 1), false, false);
                    ctx.LineTo(new Point(start, y + 1), false, false);
                }
            }
        }
        modules.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(-2, -2, n + 4, n + 4))));
        group.Children.Add(new GeometryDrawing(Brushes.Black, null, modules));
        group.Freeze();

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
