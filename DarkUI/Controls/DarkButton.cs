using DarkUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    [ToolboxBitmap(typeof(Button))]
    [DefaultEvent("Click")]
    public class DarkButton : Button
    {
        #region Field Region

        private bool _useGenericBackColor = true;
        private DarkButtonStyle _style = DarkButtonStyle.Normal;
        private DarkControlState _buttonState = DarkControlState.Normal;

        private bool _isDefault;
        private bool _spacePressed;

        // Design units (5 = half of the 96-DPI Consts.Padding): the owning
        // form's DPI scaling scales Control.Padding itself — pre-scaling via
        // Consts would double-scale and shrink the text area to nothing.
        private const int _padding = 5;
        private int _imagePadding = 5; // Consts.Padding / 2

        #endregion

        #region Designer Property Region

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public new string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;
                Invalidate();
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool Enabled
        {
            get { return base.Enabled; }
            set
            {
                base.Enabled = value;
                Invalidate();
            }
        }

        [Localizable(true)]
        [ReadOnly(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Padding Padding
        {
            get { return base.Padding; }
            set { base.Padding = value; }
        }

        [Category("Appearance")]
        [Description("Determines if system BackColor should be used or not.")]
        [DefaultValue(true)]
        public bool BackColorUseGeneric
        {
            get { return _useGenericBackColor; }
            set
            {
                _useGenericBackColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Determines the style of the button.")]
        [DefaultValue(DarkButtonStyle.Normal)]
        public DarkButtonStyle ButtonStyle
        {
            get { return _style; }
            set
            {
                _style = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Determines the amount of padding between the image and text.")]
        [DefaultValue(5)]
        public int ImagePadding
        {
            get { return _imagePadding; }
            set
            {
                _imagePadding = value;
                Invalidate();
            }
        }

        #endregion

        #region Code Property Region

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool AutoEllipsis
        {
            get { return false; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DarkControlState ButtonState
        {
            get { return _buttonState; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ContentAlignment ImageAlign
        {
            get { return base.ImageAlign; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool FlatAppearance
        {
            get { return false; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new FlatStyle FlatStyle
        {
            get { return base.FlatStyle; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ContentAlignment TextAlign
        {
            get { return base.TextAlign; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool UseCompatibleTextRendering
        {
            get { return false; }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool UseVisualStyleBackColor
        {
            get { return false; }
        }

        #endregion

        #region Constructor Region

        public DarkButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            base.UseVisualStyleBackColor = false;
            base.UseCompatibleTextRendering = false;

            SetButtonState(DarkControlState.Normal);
            Padding = new Padding(_padding);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

        #endregion

        #region Method Region

        private void SetButtonState(DarkControlState buttonState)
        {
            if (_buttonState != buttonState)
            {
                _buttonState = buttonState;
                Invalidate();
            }
        }

        #endregion

        #region Event Handler Region

        protected override void OnCreateControl()
        {
            base.OnCreateControl();

            var form = FindForm();
            if (form != null)
            {
                if (form.AcceptButton == this)
                    _isDefault = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_spacePressed)
                return;

            if (e.Button == MouseButtons.Left)
            {
                if (ClientRectangle.Contains(e.Location))
                    SetButtonState(DarkControlState.Pressed);
                else
                    SetButtonState(DarkControlState.Hover);
            }
            else
            {
                SetButtonState(DarkControlState.Hover);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!ClientRectangle.Contains(e.Location))
                return;

            SetButtonState(DarkControlState.Pressed);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_spacePressed)
                return;

            SetButtonState(DarkControlState.Normal);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_spacePressed)
                return;

            SetButtonState(DarkControlState.Normal);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            if (_spacePressed)
                return;

            var location = Cursor.Position;

            if (!ClientRectangle.Contains(location))
                SetButtonState(DarkControlState.Normal);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);

            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);

            _spacePressed = false;

            var location = Cursor.Position;

            if (!ClientRectangle.Contains(location))
                SetButtonState(DarkControlState.Normal);
            else
                SetButtonState(DarkControlState.Hover);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Space)
            {
                _spacePressed = true;
                SetButtonState(DarkControlState.Pressed);
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.KeyCode != Keys.Space)
                return;

            _spacePressed = false;

            var location = Cursor.Position;

            if (!ClientRectangle.Contains(location))
                SetButtonState(DarkControlState.Normal);
            else
                SetButtonState(DarkControlState.Hover);
        }

        public override void NotifyDefault(bool value)
        {
            base.NotifyDefault(value);

            if (!DesignMode)
                return;

            _isDefault = value;
            Invalidate();
        }

        #endregion

        #region Paint Region

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);

            var textColor = Colors.LightText;
            var borderColor = Colors.GreySelection;
            var fillColor = _useGenericBackColor ? (_isDefault ? Colors.DarkBlueBackground : Colors.LightBackground) : BackColor;
            var hoverColor = _useGenericBackColor ? (_isDefault ? Colors.BlueBackground : Colors.LighterBackground) : ControlPaint.Light(BackColor);
            bool aeroGlass = AeroGlassRenderer.IsActive;
            bool xpLuna = WindowsXpLunaRenderer.IsActive;
            bool windows98 = Windows98Renderer.IsActive;

            if (Enabled)
            {
                switch (ButtonStyle)
                {
                    case DarkButtonStyle.Normal:
                        if (Focused && TabStop)
                            borderColor = Colors.BlueHighlight;

                        switch (ButtonState)
                        {
                            case DarkControlState.Hover:
                                fillColor = hoverColor;
                                break;
                            case DarkControlState.Pressed:
                                fillColor = Colors.DarkBackground;
                                break;
                        }
                        break;
                    case DarkButtonStyle.Flat:
                        switch (ButtonState)
                        {
                            case DarkControlState.Normal:
                                fillColor = Colors.GreyBackground;
                                break;
                            case DarkControlState.Hover:
                                fillColor = Colors.MediumBackground;
                                break;
                            case DarkControlState.Pressed:
                                fillColor = Colors.DarkBackground;
                                break;
                        }
                        break;
                }
            }
            else
            {
                textColor = Colors.DisabledText;
                fillColor = Colors.DarkGreySelection;
            }

            if (aeroGlass)
            {
                AeroGlassRenderer.DrawSurface(g, rect, fillColor, borderColor, 7,
                    ButtonState == DarkControlState.Pressed,
                    Focused || ButtonState == DarkControlState.Hover || _isDefault);
            }
            else if (xpLuna)
            {
                WindowsXpLunaRenderer.DrawSurface(g, rect, fillColor, borderColor,
                    ButtonState == DarkControlState.Pressed,
                    Focused || ButtonState == DarkControlState.Hover || _isDefault);
            }
            else if (windows98)
            {
                Windows98Renderer.DrawSurface(g, rect, fillColor, ButtonState == DarkControlState.Pressed,
                    Focused || ButtonState == DarkControlState.Hover || _isDefault);
            }
            else
            {
                using var b = new SolidBrush(fillColor);
                g.FillRectangle(b, rect);
            }

            if (!aeroGlass && !xpLuna && !windows98 && ThemeManager.Active.DepthStyle != ThemeDepthStyle.Flat && Enabled)
            {
                bool pressed = ButtonState == DarkControlState.Pressed;
                int strength = ThemeManager.Active.DepthStyle switch
                {
                    ThemeDepthStyle.Soft3D or ThemeDepthStyle.Neumorphic3D or
                    ThemeDepthStyle.Clay3D or ThemeDepthStyle.Paper3D => 16,
                    ThemeDepthStyle.Glass3D => 26,
                    ThemeDepthStyle.Industrial3D or ThemeDepthStyle.Terminal3D => 32,
                    ThemeDepthStyle.RetroWindows3D or ThemeDepthStyle.Console3D or
                    ThemeDepthStyle.Crystal3D or ThemeDepthStyle.SciFi3D => 50,
                    _ => 42
                };
                Color Shift(Color color, int amount) => Color.FromArgb(
                    Math.Clamp(color.R + amount, 0, 255), Math.Clamp(color.G + amount, 0, 255), Math.Clamp(color.B + amount, 0, 255));
                Color topLeft = Shift(fillColor, pressed ? -strength : strength);
                Color bottomRight = Shift(fillColor, pressed ? strength : -strength);

                using (var topPen = new Pen(topLeft))
                using (var bottomPen = new Pen(bottomRight))
                {
                    g.DrawLine(topPen, 1, 1, Math.Max(1, rect.Right - 2), 1);
                    g.DrawLine(topPen, 1, 1, 1, Math.Max(1, rect.Bottom - 2));
                    g.DrawLine(bottomPen, 1, Math.Max(1, rect.Bottom - 2), Math.Max(1, rect.Right - 2), Math.Max(1, rect.Bottom - 2));
                    g.DrawLine(bottomPen, Math.Max(1, rect.Right - 2), 1, Math.Max(1, rect.Right - 2), Math.Max(1, rect.Bottom - 2));
                }
            }

            if (!aeroGlass && !xpLuna && !windows98 && ButtonStyle == DarkButtonStyle.Normal)
            {
                using (var p = new Pen(borderColor, 1))
                {
                    var modRect = new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);

                    g.DrawRectangle(p, modRect);
                }
            }

            var textOffsetX = 0;
            var textOffsetY = 0;

            if (Image != null)
            {
                var stringSize = g.MeasureString(Text, Font, rect.Size);

                var x = ClientSize.Width / 2 - Image.Size.Width / 2;
                var y = ClientSize.Height / 2 - Image.Size.Height / 2;

                switch (TextImageRelation)
                {
                    case TextImageRelation.ImageAboveText:
                        textOffsetY = Image.Size.Height / 2 + ImagePadding / 2;
                        y = y - ((int)(stringSize.Height / 2) + ImagePadding / 2);
                        break;
                    case TextImageRelation.TextAboveImage:
                        textOffsetY = (Image.Size.Height / 2 + ImagePadding / 2) * -1;
                        y = y + (int)(stringSize.Height / 2) + ImagePadding / 2;
                        break;
                    case TextImageRelation.ImageBeforeText:
                        textOffsetX = Image.Size.Width + ImagePadding / 2;
                        x = ImagePadding;
                        break;
                    case TextImageRelation.TextBeforeImage:
                        x = x + (int)stringSize.Width;
                        break;
                }

                //g.DrawImage(Image, x, y);
                g.DrawImage(Image, new Rectangle(x, y, Image.Width, Image.Height));
            }

            using (var b = new SolidBrush(textColor))
            {
                // Vertical centering must span the full client height:
                // subtracting the scaled padding shrank the text rect below
                // the scaled font's line box (a 46 px button held a 26 px
                // rect for a 32 px line at 200%), shaving ascenders and
                // descenders — 'Expand All' lost its 'p'. The horizontal
                // inset still keeps wide text off the borders.
                var modRect = new Rectangle(rect.Left + textOffsetX + Padding.Left,
                                            rect.Top + textOffsetY,
                                            rect.Width - Padding.Horizontal,
                                            rect.Height);

                if (Image != null)
                {
                    TextRenderer.DrawText(g, Text, Font, modRect, textColor,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
                else
                {
                    TextRenderer.DrawText(g, Text, Font, modRect, textColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        #endregion
    }
}
