using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace StayAwake;

/// <summary>
/// Generates app.ico at runtime if missing (first run from source / publish prep).
/// Also used by tools/create-icon if invoked separately.
/// </summary>
public static class IconGenerator
{
    public static void EnsureAppIcon(string path)
    {
        if (File.Exists(path))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(46, 125, 79));
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(Color.White);
            var size = g.MeasureString("A", font);
            g.DrawString("A", font, textBrush, (32 - size.Width) / 2f, (32 - size.Height) / 2f - 1f);
        }
        SaveAsIcon(bmp, path);
    }

    public static void SaveAsIcon(Bitmap bmp, string path)
    {
        // Simple single-image ICO writer
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        var png = ms.ToArray();

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((short)0);      // reserved
        bw.Write((short)1);      // type icon
        bw.Write((short)1);      // count
        bw.Write((byte)bmp.Width);
        bw.Write((byte)bmp.Height);
        bw.Write((byte)0);       // colors
        bw.Write((byte)0);       // reserved
        bw.Write((short)1);      // planes
        bw.Write((short)32);     // bit count
        bw.Write(png.Length);
        bw.Write(22);            // offset
        bw.Write(png);
    }
}
