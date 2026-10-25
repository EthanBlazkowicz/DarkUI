using DarkUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// A removable tag chip — rounded pill with the item text and an × that
    /// raises <see cref="RemoveClicked"/>. Meant to live inside a
    /// DarkChipsPanel (e.g. showing selected filters from a
    /// DarkCheckedComboBox / DarkCheckedListBox).
    /// Colors are read from Colors.* at paint time, so the chip follows
    /// live theme switches without subscribing to ThemeManager.
    /// </summary>
    public class DarkChip : Control
    {
        private const int ChipHeight = 22;
        private const int CloseZoneWidth = 18;
        private const int TextLeftPad = 9;
        private const int RightPad = 6;

        // The constants above are 96-DPI design units; chips are created at
        // runtime (post form scaling), so scale them here.
        private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);

        private bool _overClose;

        /// <summary>Raised when the × is clicked.</summary>
        public event EventHandler RemoveClicked;

        public DarkChip()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            // A tag chip is not a focus target — this also avoids any
            // focus-related outline visuals.
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;

            Height = Scale(ChipHeight);
            Margin = new Padding(Scale(2));
            // OPAQUE BackColor: a transparent BackColor makes WinForms take
            // the transparent-child path, which repaints every chip through
            // the panel whenever ANY chip is added or removed — the sibling
            // chips flash. The corners still get the container's real
            // background from OnPaintBackground's parent walk (see below),
            // so the pill blends in regardless of this value.
            BackColor = Colors.GreyBackground;
            Cursor = Cursors.Default;

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the control from
        // following ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor
        {
            get => Colors.GreyBackground;
            set => base.BackColor = Colors.GreyBackground;
        }

        public DarkChip(IContainer container) : this()
        {
            container.Add(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

        private Rectangle CloseZone => new Rectangle(Width - Scale(CloseZoneWidth), 0, Scale(CloseZoneWidth), Height);

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            // Pill auto-sizes to its text: text + left pad + × zone + right pad.
            var t = TextRenderer.MeasureText(Text, Font);
            Width = t.Width + Scale(TextLeftPad + CloseZoneWidth + RightPad);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = CloseZone.Contains(e.Location);
            if (over != _overClose)
            {
                _overClose = over;
                Cursor = over ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_overClose)
            {
                _overClose = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && CloseZone.Contains(e.Location))
                OnRemoveClicked(EventArgs.Empty);
        }

        protected virtual void OnRemoveClicked(EventArgs e)
            => RemoveClicked?.Invoke(this, e);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw ONLY the pill — the corners stay transparent so the
            // hosting panel's own pixels show through. No opaque fill of
            // the whole client rect, so no rectangular edge can appear even
            // if background colors differ by a shade.
            // (Inset by 1px so the anti-aliased path edge stays inside the
            // drawable pixel area — a path exactly on the outer bounds
            // produces a faint halo along the control's edges.)
            var pill = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedPath(pill, pill.Height / 2))
            using (var b = new SolidBrush(!Enabled ? Colors.DarkGreySelection
                : _overClose ? Colors.LightBackground : Colors.MediumBackground))
                g.FillPath(b, path);

            // Text
            var textRect = new Rectangle(Scale(TextLeftPad), 0, Width - Scale(TextLeftPad + CloseZoneWidth), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? Colors.LightText : Colors.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // × glyph (U+00D7 — guaranteed in Segoe UI; "✕" U+2715 renders
            // as a missing-glyph box in the default charset)
            TextRenderer.DrawText(g, "×", Font, CloseZone,
                Enabled && _overClose ? Colors.LightText : Colors.DisabledText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // The pill's corners must blend into whatever container hosts the
        // chip. Fill the background with the effective (opaque) parent
        // background color, read at paint time — so it tracks the current
        // theme and never depends on Colors.GreyBackground matching the
        // parent (a fixed color would show as dark wedges on light themes).
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color bg = Colors.GreyBackground; // fallback if no opaque parent
            for (Control p = Parent; p != null; p = p.Parent)
            {
                if (p.BackColor.A == 255)
                {
                    bg = p.BackColor;
                    break;
                }
            }

            using (var b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, e.ClipRectangle);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
