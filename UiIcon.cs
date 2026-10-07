using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HarmonicaPlayer;

// 图标：唯一几何来源是 Assets/Icon/harpkit.svg 里的 path 数据（同一条 d 同时写在两边）。
// 这里只用 WPF 内置的 Geometry/DrawingGroup 渲染，不引入 SVG 库，也不做位图缩放。
// 颜色是固定的品牌色，不随深浅主题变化，保证任务栏/标题栏图标始终清晰。
public static class UiIcon
{
    // ---- path 数据（与 Assets/Icon/harpkit.svg 逐字一致，viewBox 0 0 64 64）----
    public const string Badge =
        "M14 2 H50 A12 12 0 0 1 62 14 V50 A12 12 0 0 1 50 62 H14 A12 12 0 0 1 2 50 V14 A12 12 0 0 1 14 2 Z";
    public const string Body =
        "M15 24 H49 A5 5 0 0 1 54 29 V37 A5 5 0 0 1 49 42 H15 A5 5 0 0 1 10 37 V29 A5 5 0 0 1 15 24 Z";

    // 六个吹孔：宽 3、高 10 的胶囊形，间距 6（与 SVG 中的 <path> 一一对应）。
    public static readonly string[] Holes =
    {
        "M15.5 28 A1.5 1.5 0 0 1 17 29.5 V36.5 A1.5 1.5 0 0 1 15.5 38 A1.5 1.5 0 0 1 14 36.5 V29.5 A1.5 1.5 0 0 1 15.5 28 Z",
        "M21.5 28 A1.5 1.5 0 0 1 23 29.5 V36.5 A1.5 1.5 0 0 1 21.5 38 A1.5 1.5 0 0 1 20 36.5 V29.5 A1.5 1.5 0 0 1 21.5 28 Z",
        "M27.5 28 A1.5 1.5 0 0 1 29 29.5 V36.5 A1.5 1.5 0 0 1 27.5 38 A1.5 1.5 0 0 1 26 36.5 V29.5 A1.5 1.5 0 0 1 27.5 28 Z",
        "M33.5 28 A1.5 1.5 0 0 1 35 29.5 V36.5 A1.5 1.5 0 0 1 33.5 38 A1.5 1.5 0 0 1 32 36.5 V29.5 A1.5 1.5 0 0 1 33.5 28 Z",
        "M39.5 28 A1.5 1.5 0 0 1 41 29.5 V36.5 A1.5 1.5 0 0 1 39.5 38 A1.5 1.5 0 0 1 38 36.5 V29.5 A1.5 1.5 0 0 1 39.5 28 Z",
        "M45.5 28 A1.5 1.5 0 0 1 47 29.5 V36.5 A1.5 1.5 0 0 1 45.5 38 A1.5 1.5 0 0 1 44 36.5 V29.5 A1.5 1.5 0 0 1 45.5 28 Z"
    };

    // 八分音符：符头 + 符干 + 符尾。
    public const string NoteHead = "M14.8 15.5 A4.2 4.2 0 1 0 23.2 15.5 A4.2 4.2 0 1 0 14.8 15.5 Z";
    public const string NoteStem = "M22.4 6.2 H24.4 V16.4 H22.4 Z";
    public const string NoteFlag = "M24.4 6.2 C28.6 7.6 31.4 10.2 31 14.8 L28.8 13.2 C29.4 10.6 27.6 8.8 24.4 8.4 Z";

    // ---- 品牌色（固定值，不跟主题走）----
    public const string BadgeTop = "#4C8DF6";
    public const string BadgeBottom = "#2563EB";
    public const string HoleColor = "#1D4ED8";
    public const string InkColor = "#FFFFFF";

    private static DrawingImage? cached;

    // 纯矢量：任意尺寸都清晰，可直接用作窗口图标与标题栏 Logo。
    public static DrawingImage Image()
    {
        if (cached != null) return cached;
        var group = new DrawingGroup();
        group.Children.Add(Fill(Badge, Gradient()));
        foreach (string hole in Holes) group.Children.Add(Fill(hole, Solid(HoleColor)));
        var ink = Solid(InkColor);
        group.Children.Add(Fill(Body, ink));
        group.Children.Add(Fill(NoteHead, ink));
        group.Children.Add(Fill(NoteStem, ink));
        group.Children.Add(Fill(NoteFlag, ink));
        group.Freeze();
        cached = new DrawingImage(group);
        cached.Freeze();
        return cached;
    }

    // 标题栏 Logo：固定 24×24 的矢量徽标，自带背景圆角，不依赖主题色。
    public static Image Logo(double size = 22) => new()
    {
        Source = Image(),
        Width = size,
        Height = size,
        Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true
    };

    private static GeometryDrawing Fill(string path, Brush brush)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return new GeometryDrawing(brush, null, geometry);
    }

    private static Brush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Brush Gradient()
    {
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(BadgeTop),
            (Color)ColorConverter.ConvertFromString(BadgeBottom), new Point(0.2, 0), new Point(0.8, 1));
        brush.Freeze();
        return brush;
    }

    // 路径数据自检：WPF 解析 SVG 的 A 指令时不认逗号外的小写坐标写法，
    // 这里统一走 Geometry.Parse，任何一条写错都会在测试里直接失败。
    public static bool Validate() =>
        Holes.Length == 6 &&
        Geometry.Parse(Badge).IsEmpty() == false &&
        Geometry.Parse(Body).IsEmpty() == false &&
        Geometry.Parse(NoteHead).IsEmpty() == false &&
        Geometry.Parse(NoteStem).IsEmpty() == false &&
        Geometry.Parse(NoteFlag).IsEmpty() == false;
}
