using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HarpKit.IconGen;

// 把 UiIcon 的矢量徽标渲染成多尺寸 .ico（PNG 负载，Windows Vista 及以上原生支持）。
// 用法：dotnet run --project .\Tools\IconGen\IconGen.csproj [输出路径]
internal static class IconGenProgram
{
    // Windows 资源管理器/任务栏常用尺寸。
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    [STAThread]
    private static int Main(string[] arguments)
    {
        string output = arguments.Length > 0 && arguments[0].Length != 0
            ? Path.GetFullPath(arguments[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", "Assets", "Icon", "harpkit.ico"));
        if (!HarmonicaPlayer.UiIcon.Validate())
        {
            Console.Error.WriteLine("FAIL 图标几何数据无效（UiIcon 的 path 无法解析）。");
            return 1;
        }
        var rendered = new List<(int Size, byte[] Png)>();
        foreach (int size in Sizes) rendered.Add((size, Render(size)));
        WriteIco(output, rendered);
        Console.WriteLine($"OK 已生成 {output}（{rendered.Count} 个尺寸：{string.Join('/', Sizes.Select(s => s.ToString(CultureInfo.InvariantCulture)))}）");
        return 0;
    }

    private static byte[] Render(int size)
    {
        var visual = new DrawingVisual();
        using (DrawingContext context = visual.RenderOpen())
            context.DrawImage(HarmonicaPlayer.UiIcon.Image(), new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        return buffer.ToArray();
    }

    // ICO 容器：6 字节文件头 + 每个图像 16 字节目录项 + 依次排列的 PNG 数据。
    private static void WriteIco(string path, List<(int Size, byte[] Png)> images)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);
        int offset = 6 + images.Count * 16;
        foreach (var (size, png) in images)
        {
            // 256 及以上在 ICO 目录项里用 0 表示。
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images) writer.Write(png);
        writer.Flush();
    }
}
