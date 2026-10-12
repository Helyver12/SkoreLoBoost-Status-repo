using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Deceive;

/// SkoreLoBoost visual identity: dark background with gold accents.
internal static class Theme
{
    internal static readonly Color Bg = Color.FromArgb(7, 7, 10);
    internal static readonly Color Panel = Color.FromArgb(20, 20, 26);
    internal static readonly Color PanelHover = Color.FromArgb(36, 34, 24);
    internal static readonly Color Gold = Color.FromArgb(255, 177, 0);
    internal static readonly Color GoldLight = Color.FromArgb(255, 211, 77);
    internal static readonly Color GoldDim = Color.FromArgb(110, 84, 12);
    internal static readonly Color Text = Color.FromArgb(235, 235, 240);
    internal static readonly Color Muted = Color.FromArgb(150, 150, 162);

    /// Loads the embedded SkoreLoBoost logo, or null if it is missing.
    internal static Image? LoadLogo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("skore-logo.png", StringComparison.OrdinalIgnoreCase));
            if (name is null)
                return null;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
                return null;

            // Image.FromStream needs the stream to stay open, so copy it to memory first.
            var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            return Image.FromStream(memory);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// Asks Windows 10/11 for a dark title bar. Silently does nothing on older systems.
    internal static void UseDarkTitleBar(Form form)
    {
        try
        {
            var enabled = 1;
            // Attribute 20 on recent Windows builds, 19 on older Windows 10 builds.
            if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
        }
        catch
        {
            // Cosmetic only.
        }
    }

    /// Flat dark button with a gold border that lights up on hover.
    internal static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? Gold : GoldDim;
        button.FlatAppearance.MouseOverBackColor = PanelHover;
        button.FlatAppearance.MouseDownBackColor = PanelHover;
        button.BackColor = primary ? Gold : Panel;
        button.ForeColor = primary ? Color.Black : Text;
        button.MouseEnter += (_, _) => button.FlatAppearance.BorderColor = Gold;
        button.MouseLeave += (_, _) => button.FlatAppearance.BorderColor = primary ? Gold : GoldDim;
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Panel;
    public override Color ImageMarginGradientBegin => Theme.Panel;
    public override Color ImageMarginGradientMiddle => Theme.Panel;
    public override Color ImageMarginGradientEnd => Theme.Panel;
    public override Color MenuBorder => Theme.GoldDim;
    public override Color MenuItemBorder => Theme.Gold;
    public override Color MenuItemSelected => Theme.PanelHover;
    public override Color MenuItemSelectedGradientBegin => Theme.PanelHover;
    public override Color MenuItemSelectedGradientEnd => Theme.PanelHover;
    public override Color MenuItemPressedGradientBegin => Theme.PanelHover;
    public override Color MenuItemPressedGradientMiddle => Theme.PanelHover;
    public override Color MenuItemPressedGradientEnd => Theme.PanelHover;
    public override Color SeparatorDark => Color.FromArgb(60, 60, 70);
    public override Color SeparatorLight => Color.FromArgb(60, 60, 70);
    public override Color CheckBackground => Theme.PanelHover;
    public override Color CheckSelectedBackground => Theme.PanelHover;
    public override Color CheckPressedBackground => Theme.PanelHover;
}

/// Dark renderer for the tray menu. An item tagged "brand" is drawn in gold.
internal class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColorTable()) => RoundedEdges = false;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Tag as string == "brand" ? Theme.Gold : Theme.Text;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Gold;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var rect = e.ImageRectangle;
        var previous = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(Theme.Gold, 2f))
        {
            e.Graphics.DrawLines(pen, new[]
            {
                new Point(rect.Left + 4, rect.Top + rect.Height / 2),
                new Point(rect.Left + rect.Width / 2 - 1, rect.Bottom - 5),
                new Point(rect.Right - 4, rect.Top + 5)
            });
        }

        e.Graphics.SmoothingMode = previous;
    }
}
