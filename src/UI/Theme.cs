using System.Drawing.Drawing2D;

namespace ChabadOfficePager.UI;

/// <summary>Shared colors, fonts, and the programmatically drawn tray icons.</summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb(32, 34, 40);
    public static readonly Color Surface = Color.FromArgb(45, 48, 56);
    public static readonly Color SurfaceHover = Color.FromArgb(62, 108, 216);
    public static readonly Color Text = Color.FromArgb(240, 242, 246);
    public static readonly Color TextMuted = Color.FromArgb(150, 156, 168);
    public static readonly Color Accent = Color.FromArgb(62, 108, 216);
    public static readonly Color Alert = Color.FromArgb(214, 62, 62);

    public static readonly Font TitleFont = new("Segoe UI Semibold", 11f);
    public static readonly Font ButtonFont = new("Segoe UI Semibold", 12f);
    public static readonly Font SmallFont = new("Segoe UI", 9f);
    public static readonly Font AlertFont = new("Segoe UI Semibold", 20f);

    private static Icon? _idleIcon;
    private static Icon? _alertIcon;

    /// <summary>Normal tray icon: a simple bell.</summary>
    public static Icon IdleIcon => _idleIcon ??= BuildBellIcon(Color.FromArgb(230, 233, 240));

    /// <summary>Tray icon while an unread page is waiting.</summary>
    public static Icon AlertIcon => _alertIcon ??= BuildBellIcon(Color.FromArgb(235, 80, 80));

    private static Icon BuildBellIcon(Color color)
    {
        // Drawn at 32x32 so it stays crisp at the sizes Windows asks for.
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(color);

            // Bell body: a dome with a flared skirt.
            using var body = new GraphicsPath();
            body.AddArc(7, 5, 18, 20, 180, 180);
            body.AddLine(25, 15, 27, 22);
            body.AddLine(27, 22, 5, 22);
            body.AddLine(5, 22, 7, 15);
            body.CloseFigure();
            g.FillPath(brush, body);

            // Handle on top and clapper below.
            g.FillEllipse(brush, 14, 2, 4, 4);
            g.FillEllipse(brush, 13, 23, 6, 5);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
