using DarkUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// A themed search box with a search icon on the left and a clear (✕)
    /// button on the right. The ✕ is painted and hit-tested by the control
    /// itself — no child label, so no transparent-repaint flashes.
    /// </summary>
    public class DarkSearchBox : UserControl
    {
        // The placeholder must be drawn INTO the textbox's own surface
        // (WM_PAINT overlay, like DarkTextBox): child controls paint OVER
        // their parent, so drawing it in the control's OnPaint leaves it
        // permanently hidden behind the opaque textbox.
        private sealed class PlaceholderTextBox : TextBox
        {
            public string OverlayText = "";

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F) return; // WM_PAINT — overlay after the native paint

                // Disabled: the native EDIT paints system gray text — draw it
                // with the theme's DisabledText instead.
                if (!Enabled && Text.Length > 0)
                {
                    using (var g = CreateGraphics())
                    {
                        var rect = new Rectangle(2, 0, Width - 2, Height);
                        TextRenderer.DrawText(g, Text, Font, rect, Colors.DisabledText,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    }
                }
                else if (!Focused && Text.Length == 0 && !string.IsNullOrEmpty(OverlayText))
                {
                    using (var g = CreateGraphics())
                    {
                        var rect = new Rectangle(2, 0, Width - 2, Height);
                        TextRenderer.DrawText(g, OverlayText, Font, rect, Colors.DisabledText,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    }
                }
            }
        }

        protected readonly TextBox _textBox = new PlaceholderTextBox();
        private readonly Label _iconLabel = new Label();
        private string _placeholder = "Search…";
        private bool _clearHovered;
        private bool _clearPressed;

        public event EventHandler SearchTextChanged;

        [Category("Appearance")]
        [DefaultValue("Search…")]
        public string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value ?? ""; ((PlaceholderTextBox)_textBox).OverlayText = _placeholder; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SearchText
        {
            get { return _textBox.Text; }
            set { _textBox.Text = value; }
        }

        // ── constructor ───────────────────────────────────────────────

        public DarkSearchBox()
        {
            BackColor = Colors.LightBackground;
            Height = 28;

            // Search icon: magnifying glass emoji. Opaque BackColor (a
            // transparent label shows the page color through) and INSET 1px
            // (see OnResize) so it never covers the box's themed border.
            _iconLabel.Text = "🔍";
            _iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            _iconLabel.BackColor = Colors.LightBackground;
            _iconLabel.ForeColor = Colors.DisabledText;
            _iconLabel.Cursor = Cursors.Default;
            _iconLabel.Size = new Size(24, 26);

            // Borderless TextBox blends into the control
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = new Font("Segoe UI", 9.5F);
            // Themed right-click edit menu (Undo/Cut/Copy/Paste/Delete/Select
            // All) — a bare TextBox would show the native Windows menu.
            _textBox.ContextMenuStrip = DarkEditMenu.Create(_textBox);
            ApplyTextBoxColors();

            // Sync the default placeholder into the textbox overlay (the
            // default is a field initializer, not the property setter).
            ((PlaceholderTextBox)_textBox).OverlayText = _placeholder;

            _textBox.TextChanged += (s, e) =>
            {
                Invalidate();
                SearchTextChanged?.Invoke(this, EventArgs.Empty);
            };
            _textBox.Enter += (s, e) => Invalidate();
            _textBox.Leave += (s, e) => Invalidate();

            ThemeManager.ThemeChanged += OnThemeChanged;

            Controls.Add(_textBox);
            Controls.Add(_iconLabel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DesignMode) return;
            ApplyThemeColors();
            // Re-layout once any pending form-level DPI scaling has landed.
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(LayoutChildren);
        }

        // Propagate Enabled to the inner edit so it paints its disabled
        // state (the field would otherwise stay bright inside a disabled box).
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _textBox.Enabled = Enabled;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        private void ApplyThemeColors()
        {
            BackColor = Colors.LightBackground;
            _iconLabel.BackColor = Colors.LightBackground;
            _iconLabel.ForeColor = Colors.DisabledText;
            ApplyTextBoxColors();
            Invalidate();
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the control from
        // following ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor
        {
            get => Colors.LightBackground;
            set => base.BackColor = Colors.LightBackground;
        }

        private void ApplyTextBoxColors()
        {
            _textBox.BackColor = Colors.LightBackground;
            _textBox.ForeColor = Colors.LightText;
        }

        // ── layout ────────────────────────────────────────────────────

        private const int IconW = 26;
        private const int ClearW = 24;
        private const int Pad = 2;

        private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);

        private Rectangle ClearButtonRect =>
            new Rectangle(Width - Scale(ClearW) + Scale(4), (Height - Scale(16)) / 2, Scale(16), Scale(16));

        // Positions the icon strip and the inner edit for the current size
        // and DPI. Re-run after every size/DPI change — the form-level DPI
        // scaling pass resizes this UserControl and its children directly
        // (via ScaleControl, which suppresses OnResize), so a layout computed
        // for the 96-DPI design size would otherwise stay stale (and doubled).
        private void LayoutChildren()
        {
            int iconW = Scale(IconW);
            int clearW = Scale(ClearW);
            // Inset 1px so the label never covers the box's themed border
            // (a child paints over its parent — the border segments at the
            // icon strip top/left/bottom would be erased).
            _iconLabel.Bounds = new Rectangle(1, 1, iconW - 2, Height - 2);
            // Box snug around the font line height, vertically centered —
            // the native EDIT mis-aligns text when given slack room, so give
            // it none: the text lands visually centered either way.
            int boxH = Math.Max(Scale(16), (int)Math.Ceiling(_textBox.Font.GetHeight()) + 1);
            int boxTop = Math.Max(0, (Height - boxH) / 2);
            _textBox.Bounds = new Rectangle(iconW, boxTop,
                Width - iconW - clearW - Scale(Pad), boxH);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            LayoutChildren();
        }

        // ── clear-button input ────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_textBox.Text.Length > 0 && ClearButtonRect.Contains(e.Location))
            {
                _clearPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_clearPressed) return;
            _clearPressed = false;
            Invalidate();
            if (ClearButtonRect.Contains(e.Location))
            {
                _textBox.Text = "";
                _textBox.Focus();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hover = _textBox.Text.Length > 0 && ClearButtonRect.Contains(e.Location);
            if (hover != _clearHovered)
            {
                _clearHovered = hover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_clearHovered)
            {
                _clearHovered = false;
                Invalidate();
            }
        }

        // ── focus ─────────────────────────────────────────────────────

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _textBox.Focus();
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        // ── paint ─────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var cr = ClientRectangle;
            bool focused = _textBox.Focused || Focused;

            // Background
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, cr);

            // Border — BlueHighlight when focused, DarkBorder otherwise
            using (var pen = new Pen(focused ? Colors.BlueHighlight : Colors.DarkBorder))
                g.DrawRectangle(pen, cr.Left, cr.Top, cr.Width - 1, cr.Height - 1);

            // Placeholder — shown when empty and unfocused
            DrawPlaceholder(g, focused);

            // Clear button — painted by us, never a child label
            if (_textBox.Text.Length > 0)
                DrawClearButton(g);
        }

        // The placeholder is drawn by the inner textbox's own WndProc overlay
        // (see PlaceholderTextBox) — drawing it here would sit behind the
        // opaque textbox and never be visible. Kept as an empty virtual for
        // variant subclasses that call it.
        protected void DrawPlaceholder(Graphics g, bool focused) { }

        // Drawing helpers — used by variant subclasses
        protected static void DrawSearchIcon(Graphics g, Color color)
        {
            int cx = 10, cy = (28 - 14) / 2, r = 5, d = r * 2;
            using (var pen = new Pen(color, 1.4f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            {
                g.DrawEllipse(pen, cx, cy, d, d);
                double angle = Math.PI / 4;
                int sx = cx + r + (int)(r * Math.Cos(angle));
                int sy = cy + r + (int)(r * Math.Sin(angle));
                g.DrawLine(pen, sx, sy, sx + 4, sy + 4);
            }
        }

        protected void DrawClearButton(Graphics g)
        {
            Color c = _clearPressed ? Colors.BlueSelection
                : _clearHovered ? Colors.LightText
                : Colors.DisabledText;

            // Center the × by its actual glyph extent: VerticalCenter alone
            // centers the LINE BOX, which leaves the glyph ~2px above the
            // visual center — the × would look off-center.
            var sz = TextRenderer.MeasureText("×", _textBox.Font, Size.Empty,
                TextFormatFlags.NoPadding);
            var rect = new Rectangle(ClearButtonRect.X,
                ClearButtonRect.Y + (ClearButtonRect.Height - sz.Height) / 2,
                ClearButtonRect.Width, sz.Height);
            TextRenderer.DrawText(g, "×", _textBox.Font, rect, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }
    }
}
