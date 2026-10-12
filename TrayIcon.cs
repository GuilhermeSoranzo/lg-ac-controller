using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using LGClimaTray.Services;

namespace LGClimaTray;

/// <summary>Draws the tray icon at runtime: a snowflake on a rounded tile colored by state.</summary>
internal static class TrayIcon
{
    private const int Size = 32;

    public static Icon Create(AirConditionerState state)
    {
        var (top, bottom) = state.IsOn ? ModeColors(state.Mode) : (Color.FromArgb(120, 134, 132), Color.FromArgb(78, 90, 89));

        using var bitmap = new Bitmap(Size, Size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            var tile = new RectangleF(1, 1, Size - 2, Size - 2);
            using (var path = RoundedRect(tile, 8))
            using (var fill = new LinearGradientBrush(tile, top, bottom, LinearGradientMode.Vertical))
                g.FillPath(fill, path);

            DrawSnowflake(g, Size / 2f, Size / 2f, 11f, Color.White);
        }

        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    public static string Tooltip(AirConditionerState state)
    {
        if (!state.IsOn) return "LG Clima — Desligado";
        var mode = state.Mode switch
        {
            ClimateMode.Frio => "Frio",
            ClimateMode.Quente => "Quente",
            ClimateMode.Automatico => "Automático",
            ClimateMode.Ventilacao => "Ventilação",
            ClimateMode.Desumidificar => "Desumidificar",
            _ => "—"
        };
        return $"LG Clima — Ligado · {mode} · {state.TargetTemperature}°C";
    }

    private static (Color Top, Color Bottom) ModeColors(ClimateMode mode) => mode switch
    {
        ClimateMode.Frio => (Color.FromArgb(86, 200, 245), Color.FromArgb(28, 110, 200)),
        ClimateMode.Quente => (Color.FromArgb(255, 176, 92), Color.FromArgb(226, 92, 60)),
        ClimateMode.Ventilacao => (Color.FromArgb(140, 170, 255), Color.FromArgb(84, 104, 220)),
        ClimateMode.Desumidificar => (Color.FromArgb(120, 220, 230), Color.FromArgb(40, 150, 170)),
        _ => (Color.FromArgb(155, 230, 208), Color.FromArgb(42, 160, 130)),
    };

    private static void DrawSnowflake(Graphics g, float cx, float cy, float radius, Color color)
    {
        using var pen = new Pen(color, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (var i = 0; i < 6; i++)
        {
            var angle = i * Math.PI / 3 - Math.PI / 2;
            var tip = Point(cx, cy, radius, angle);
            g.DrawLine(pen, cx, cy, tip.X, tip.Y);

            // Small "V" branch on each arm
            var branchBase = Point(cx, cy, radius * 0.55f, angle);
            var left = Point(branchBase.X, branchBase.Y, radius * 0.38f, angle - Math.PI / 4);
            var right = Point(branchBase.X, branchBase.Y, radius * 0.38f, angle + Math.PI / 4);
            g.DrawLine(pen, branchBase, left);
            g.DrawLine(pen, branchBase, right);
        }
    }

    private static PointF Point(float x, float y, float length, double angle) =>
        new(x + length * (float)Math.Cos(angle), y + length * (float)Math.Sin(angle));

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
