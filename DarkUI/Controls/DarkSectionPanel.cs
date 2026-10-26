using DarkUI.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    [Designer(typeof(DarkSectionPanelDesigner))]
    public class DarkSectionPanel : Panel
    {
        internal const int BorderThickness = 1;
        internal const int DefaultHeaderHeight = 25;

        private string _sectionHeader;
        private Padding _contentPadding = Padding.Empty;

        [Category("Layout")]
        [Description("The space between the section border and its child controls.")]
        [DefaultValue(typeof(Padding), "0, 0, 0, 0")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public new Padding Padding
        {
            get { return _contentPadding; }
            set
            {
                if (_contentPadding == value)
                    return;

                _contentPadding = value;
                PerformLayout();
                Invalidate();
            }
        }

        [Category("Layout")]
        [Description("The space between this section panel and neighbouring controls when its parent layout supports margins.")]
        [DefaultValue(typeof(Padding), "12, 12, 12, 12")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public new Padding Margin
        {
            get { return base.Margin; }
            set { base.Margin = value; }
        }

        [Category("Appearance")]
        [Description("The section header text associated with this control.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public string SectionHeader
        {
            get { return _sectionHeader; }
            set
            {
                if (_sectionHeader == value)
                    return;

                _sectionHeader = value;
                PerformLayout();
                Invalidate();
            }
        }

        [Browsable(false)]
        public Rectangle ContentBounds => GetContentBounds();

        internal int HeaderHeight =>
            string.IsNullOrEmpty(_sectionHeader) ? 0 : (int)Math.Round(DefaultHeaderHeight * DeviceDpi / 96f);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Partial invalidation leaves stale pixels from the pre-scale
            // layout on screen (stray header-text fragments).
            Invalidate(true);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            // Dock-fill children must fill the content area below the header
            // bar, not the whole client — base layout ignores the painted
            // header, so place them explicitly.
            foreach (Control c in Controls)
                if (c.Dock == DockStyle.Fill)
                    c.Bounds = GetContentBounds();
        }

        public DarkSectionPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            base.Margin = new Padding(6);
            // base.Padding stays empty: Padding feeds the anchored-child
            // layout's display rect, and a header-sized top term re-bases
            // designer-positioned children into the header bar at high DPI.
            // The header is painted; Dock-fill children are fitted to the
            // content area in OnLayout via GetContentBounds().
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

        internal Rectangle GetContentBounds()
        {
            // Content padding is authored in 96-DPI design units; scale it
            // like every other metric.
            double scale = DeviceDpi / 96.0;
            int S(int v) => (int)Math.Round(v * scale);
            var left = BorderThickness + S(_contentPadding.Left);
            var top = (HeaderHeight > 0 ? HeaderHeight : BorderThickness) + S(_contentPadding.Top);
            var right = Math.Max(left, ClientSize.Width - BorderThickness - S(_contentPadding.Right));
            var bottom = Math.Max(top, ClientSize.Height - BorderThickness - S(_contentPadding.Bottom));
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ContentBounds.Contains(e.Location) && Controls.Count > 0)
                Controls[0].Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = ClientRectangle;

            if (AeroGlassRenderer.IsActive)
                AeroGlassRenderer.DrawPanelBackground(g, rect);
            else if (WindowsXpLunaRenderer.IsActive)
                WindowsXpLunaRenderer.DrawBackground(g, rect);
            else if (Windows98Renderer.IsActive)
            {
                using var b = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(b, rect);
            }
            else
            {
                using var b = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(b, rect);
            }

            if (HeaderHeight > 0)
            {
                var bgColor = ContainsFocus ? Colors.BlueBackground : Colors.HeaderBackground;
                var darkColor = ContainsFocus ? Colors.DarkBlueBorder : Colors.DarkBorder;
                var lightColor = ContainsFocus ? Colors.LightBlueBorder : Colors.LightBorder;

                var headerRect = new Rectangle(0, 0, rect.Width, HeaderHeight);
                if (AeroGlassRenderer.IsActive)
                    AeroGlassRenderer.DrawSurface(g, headerRect, bgColor, darkColor, 7, false, ContainsFocus);
                else if (WindowsXpLunaRenderer.IsActive)
                    WindowsXpLunaRenderer.DrawSurface(g, headerRect, bgColor, darkColor, false, ContainsFocus);
                else if (Windows98Renderer.IsActive)
                    Windows98Renderer.DrawSurface(g, headerRect, bgColor, false, ContainsFocus);
                else
                {
                    using var b = new SolidBrush(bgColor);
                    g.FillRectangle(b, headerRect);
                }

                if (!AeroGlassRenderer.IsActive && !WindowsXpLunaRenderer.IsActive && !Windows98Renderer.IsActive)
                {
                    using var p = new Pen(darkColor);
                    g.DrawLine(p, rect.Left, 0, rect.Right, 0);
                    g.DrawLine(p, rect.Left, HeaderHeight - 1, rect.Right, HeaderHeight - 1);
                }

                if (!AeroGlassRenderer.IsActive && !WindowsXpLunaRenderer.IsActive && !Windows98Renderer.IsActive)
                {
                    using var p = new Pen(lightColor);
                    g.DrawLine(p, rect.Left, 1, rect.Right, 1);
                }

                // Focus uses the theme accent as its header fill. Some bright
                // 3D accents (for example Console 10) make LightText blend
                // into that fill, so choose the stronger neutral foreground
                // only for the focused header.
                var headerTextColor = !Enabled
                    ? Colors.DisabledText
                    : ContainsFocus ? GetFocusedHeaderTextColor(bgColor) : Colors.LightText;

                using (var b = new SolidBrush(headerTextColor))
                using (var format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap,
                    Trimming = StringTrimming.EllipsisCharacter
                })
                {
                    var textRect = new Rectangle(3, 0, Math.Max(0, rect.Width - 7), HeaderHeight);
                    g.DrawString(SectionHeader, Font, b, textRect, format);
                }
            }

            if (!AeroGlassRenderer.IsActive && !WindowsXpLunaRenderer.IsActive && !Windows98Renderer.IsActive)
            {
                using var p = new Pen(Colors.DarkBorder, BorderThickness);
                var modRect = new Rectangle(rect.Left, rect.Top, Math.Max(0, rect.Width - 1), Math.Max(0, rect.Height - 1));
                g.DrawRectangle(p, modRect);
            }
            else if (AeroGlassRenderer.IsActive)
                AeroGlassRenderer.DrawSurface(g, rect, Color.FromArgb(0, Colors.GreyBackground), Colors.LightBorder, 8, false);
            else if (WindowsXpLunaRenderer.IsActive)
                WindowsXpLunaRenderer.DrawSurface(g, rect, Colors.GreyBackground, Colors.DarkBlueBorder);
            else
                Windows98Renderer.DrawSurface(g, rect, Colors.GreyBackground);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Absorb event.
        }

        private static Color GetFocusedHeaderTextColor(Color background)
        {
            return ContrastRatio(Color.Black, background) >= ContrastRatio(Color.White, background)
                ? Color.Black
                : Color.White;
        }

        private static double ContrastRatio(Color first, Color second)
        {
            double firstLuminance = RelativeLuminance(first);
            double secondLuminance = RelativeLuminance(second);
            return (Math.Max(firstLuminance, secondLuminance) + 0.05d)
                   / (Math.Min(firstLuminance, secondLuminance) + 0.05d);
        }

        private static double RelativeLuminance(Color color)
        {
            static double Linear(byte channel)
            {
                double value = channel / 255d;
                return value <= 0.04045d
                    ? value / 12.92d
                    : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
            }

            return (0.2126d * Linear(color.R))
                   + (0.7152d * Linear(color.G))
                   + (0.0722d * Linear(color.B));
        }
    }
}
