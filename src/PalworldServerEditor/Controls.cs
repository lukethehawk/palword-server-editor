using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PalworldServerEditor
{
    static class Theme
    {
        public static float Scale = 1f;

        public static readonly Color Sidebar = Color.FromArgb(0x1B, 0x22, 0x2D);
        public static readonly Color SidebarHover = Color.FromArgb(0x25, 0x2E, 0x3B);
        public static readonly Color SidebarActive = Color.FromArgb(0x2C, 0x37, 0x47);
        public static readonly Color SidebarText = Color.FromArgb(0xC8, 0xD0, 0xDB);
        public static readonly Color SidebarMuted = Color.FromArgb(0x7E, 0x89, 0x99);
        public static readonly Color SearchBg = Color.FromArgb(0x2A, 0x33, 0x41);
        public static readonly Color Accent = Color.FromArgb(0x2F, 0x7D, 0xE1);
        public static readonly Color AccentHover = Color.FromArgb(0x25, 0x6B, 0xC7);
        public static readonly Color AccentPressed = Color.FromArgb(0x1E, 0x5A, 0xA8);
        public static readonly Color Modified = Color.FromArgb(0xF0, 0x9A, 0x0E);
        public static readonly Color Background = Color.FromArgb(0xF3, 0xF5, 0xF8);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(0xE1, 0xE5, 0xEB);
        public static readonly Color Hover = Color.FromArgb(0xEE, 0xF1, 0xF5);
        public static readonly Color Pressed = Color.FromArgb(0xE2, 0xE7, 0xEE);
        public static readonly Color Text = Color.FromArgb(0x1F, 0x29, 0x37);
        public static readonly Color Muted = Color.FromArgb(0x6B, 0x72, 0x80);
        public static readonly Color ToggleOff = Color.FromArgb(0xC3, 0xC9, 0xD2);
        public static readonly Color WarnBg = Color.FromArgb(0xFF, 0xF6, 0xDD);
        public static readonly Color WarnBorder = Color.FromArgb(0xF1, 0xD1, 0x7A);
        public static readonly Color WarnText = Color.FromArgb(0x6E, 0x4E, 0x00);
        public static readonly Color Error = Color.FromArgb(0xC2, 0x41, 0x0C);

        public static Font Ui, UiSemibold, Small, Section, AppTitle, Icons;
        public static bool HasIconFont;

        public static void Init(float dpi)
        {
            Scale = dpi / 96f;
            Ui = new Font("Segoe UI", 9.75f);
            UiSemibold = new Font("Segoe UI Semibold", 9.75f);
            Small = new Font("Segoe UI", 8.5f);
            Section = new Font("Segoe UI Semibold", 13f);
            AppTitle = new Font("Segoe UI Semibold", 13f);
            using (var fonts = new InstalledFontCollection())
                HasIconFont = fonts.Families.Any(f => f.Name == "Segoe MDL2 Assets");
            Icons = HasIconFont ? new Font("Segoe MDL2 Assets", 10f) : new Font("Segoe UI Symbol", 11f);
        }

        public static int S(int px) => (int)Math.Round(px * Scale);

        public static string UndoGlyph => HasIconFont ? "" : "↺";
        public static string SearchGlyph => HasIconFont ? "" : "⌕";

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Grey hint text shown inside an empty TextBox.</summary>
        public static void SetCueBanner(TextBox box, string text)
        {
            void apply()
            {
                try { SendMessage(box.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, text); }
                catch (Exception) { /* not available outside Windows */ }
            }
            if (box.IsHandleCreated)
                apply();
            else
                box.HandleCreated += (s, e) => apply();
        }
    }

    /// <summary>Vertical stack: children are placed one under the other at full width, in insertion order.</summary>
    class StackPanel : Panel
    {
        readonly HashSet<Control> hidden = new HashSet<Control>();

        public void SetShown(Control c, bool shown)
        {
            if (shown ? !hidden.Remove(c) : !hidden.Add(c))
                return;
            c.Visible = shown;
        }

        public bool IsShown(Control c) => !hidden.Contains(c);

        protected override void OnLayout(LayoutEventArgs e)
        {
            int y = Padding.Top;
            int width = Math.Max(0, ClientSize.Width - Padding.Horizontal);
            foreach (Control c in Controls)
            {
                if (hidden.Contains(c))
                    continue;
                y += c.Margin.Top;
                c.SetBounds(Padding.Left + c.Margin.Left, y, Math.Max(0, width - c.Margin.Horizontal), c.Height);
                y += c.Height + c.Margin.Bottom;
            }
            int height = y + Padding.Bottom;
            if (Height != height)
                Height = height;
        }
    }

    /// <summary>The scrolling settings area. Mouse wheel scrolling is handled here for the whole page.</summary>
    sealed class ScrollPanel : Panel
    {
        public ScrollPanel()
        {
            AutoScroll = true;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            WheelScroll(e.Delta);
            if (e is HandledMouseEventArgs h)
                h.Handled = true;
        }

        public void WheelScroll(int delta)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0)
                lines = 3;
            int step = Theme.S(20) * lines * delta / 120;
            int y = -AutoScrollPosition.Y - step;
            AutoScrollPosition = new Point(0, Math.Max(0, y));
        }

        public static void ForwardWheel(Control c, int delta)
        {
            for (Control p = c.Parent; p != null; p = p.Parent)
            {
                if (p is ScrollPanel sp)
                {
                    sp.WheelScroll(delta);
                    return;
                }
            }
        }
    }

    /// <summary>NumericUpDown that does not change value when the page is scrolled over it and accepts both '.' and ','.</summary>
    sealed class SafeNumeric : NumericUpDown
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs h)
                h.Handled = true;
            ScrollPanel.ForwardWheel(this, e.Delta);
        }

        protected override void OnTextBoxKeyPress(object source, KeyPressEventArgs e)
        {
            if (e.KeyChar == '.' || e.KeyChar == ',')
            {
                string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                if (DecimalPlaces == 0)
                {
                    e.Handled = true;
                    return;
                }
                if (sep.Length == 1)
                    e.KeyChar = sep[0];
            }
            base.OnTextBoxKeyPress(source, e);
        }
    }

    /// <summary>Drop-down list that ignores the mouse wheel unless it is open.</summary>
    sealed class SafeCombo : ComboBox
    {
        public SafeCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (DroppedDown)
            {
                base.OnMouseWheel(e);
                return;
            }
            if (e is HandledMouseEventArgs h)
                h.Handled = true;
            ScrollPanel.ForwardWheel(this, e.Delta);
        }
    }

    /// <summary>On/off switch (a CheckBox drawn as a toggle).</summary>
    sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            Size = new Size(Theme.S(90), Theme.S(28));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float th = Theme.S(20), tw = Theme.S(40);
            var track = new RectangleF(Theme.S(1), (Height - th) / 2f, tw, th);
            Color fill = Checked ? Theme.Accent : Theme.ToggleOff;
            if (!Enabled)
                fill = Color.FromArgb(120, fill);
            using (GraphicsPath path = Theme.RoundRect(track, th / 2f))
            using (var brush = new SolidBrush(fill))
                g.FillPath(brush, path);

            float knob = th - Theme.S(6);
            float kx = Checked ? track.Right - Theme.S(3) - knob : track.Left + Theme.S(3);
            g.FillEllipse(Brushes.White, kx, track.Top + Theme.S(3), knob, knob);

            var textRect = new Rectangle((int)track.Right + Theme.S(10), 0, Math.Max(0, Width - (int)track.Right - Theme.S(10)), Height);
            TextRenderer.DrawText(g, Checked ? "Sì" : "No", Font, textRect, Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            if (Focused && ShowFocusCues)
            {
                var focus = Rectangle.Round(track);
                focus.Inflate(Theme.S(2), Theme.S(2));
                ControlPaint.DrawFocusRectangle(g, focus);
            }
        }
    }

    sealed class FlatButton : Button
    {
        bool primary;

        public FlatButton(string text, bool primary = false)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = primary ? Theme.UiSemibold : Theme.Ui;
            Height = Theme.S(34);
            Width = TextRenderer.MeasureText(text, Font).Width + Theme.S(30);
            Primary = primary;
        }

        public bool Primary
        {
            get => primary;
            set
            {
                primary = value;
                BackColor = value ? Theme.Accent : Theme.Card;
                ForeColor = value ? Color.White : Theme.Text;
                FlatAppearance.BorderColor = value ? Theme.Accent : Theme.Border;
                FlatAppearance.MouseOverBackColor = value ? Theme.AccentHover : Theme.Hover;
                FlatAppearance.MouseDownBackColor = value ? Theme.AccentPressed : Theme.Pressed;
            }
        }

        // No dotted focus rectangle on the flat buttons.
        protected override bool ShowFocusCues => false;
    }

    /// <summary>Section entry in the sidebar, with a badge counting unsaved changes.</summary>
    sealed class NavItem : Control
    {
        bool selected, hover;
        int badge;

        public NavItem(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Height = Theme.S(38);
            Font = Theme.Ui;
        }

        public bool Selected
        {
            get => selected;
            set { if (selected != value) { selected = value; Invalidate(); } }
        }

        public int Badge
        {
            get => badge;
            set { if (badge != value) { badge = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(selected ? Theme.SidebarActive : hover ? Theme.SidebarHover : Theme.Sidebar);
            if (selected)
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, 0, 0, Theme.S(3), Height);

            int right = Width - Theme.S(14);
            if (badge > 0)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                string s = badge.ToString(CultureInfo.CurrentCulture);
                Size size = TextRenderer.MeasureText(s, Theme.Small);
                int h = Theme.S(18);
                int w = Math.Max(h, size.Width + Theme.S(6));
                var pill = new Rectangle(right - w, (Height - h) / 2, w, h);
                using (GraphicsPath path = Theme.RoundRect(pill, h / 2f))
                using (var b = new SolidBrush(Theme.Modified))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, s, Theme.Small, pill, Theme.Sidebar,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                right = pill.Left - Theme.S(6);
            }
            var textRect = new Rectangle(Theme.S(20), 0, Math.Max(0, right - Theme.S(20)), Height);
            TextRenderer.DrawText(g, Text, selected ? Theme.UiSemibold : Theme.Ui, textRect,
                selected ? Color.White : Theme.SidebarText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>Section title above each card.</summary>
    sealed class SectionHeader : Control
    {
        readonly string subtitle;

        public SectionHeader(string title, string subtitle)
        {
            Text = title;
            this.subtitle = subtitle;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.Section.Height + Theme.Small.Height + Theme.S(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Background);
            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            int titleH = Theme.Section.Height;
            TextRenderer.DrawText(g, Text, Theme.Section, new Rectangle(0, 0, Width, titleH), Theme.Text, flags);
            TextRenderer.DrawText(g, subtitle, Theme.Small, new Rectangle(Theme.S(1), titleH + Theme.S(1), Width, Theme.Small.Height), Theme.Muted, flags);
        }
    }
}
