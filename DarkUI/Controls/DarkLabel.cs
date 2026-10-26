using DarkUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    public class DarkLabel : Label
    {
        #region Field Region

        private bool _autoUpdateHeight;
        private bool _isGrowing;

        #endregion

        #region Property Region

        [Category("Layout")]
        [Description("Enables automatic height sizing based on the contents of the label.")]
        [DefaultValue(false)]
        public bool AutoUpdateHeight
        {
            get { return _autoUpdateHeight; }
            set
            {
                _autoUpdateHeight = value;

                if (_autoUpdateHeight)
                {
                    AutoSize = false;
                    ResizeLabel();
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool AutoSize
        {
            get { return base.AutoSize; }
            set
            {
                base.AutoSize = value;

                if (AutoSize)
                    AutoUpdateHeight = false;
            }
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the control from
        // following ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color ForeColor
        {
            get => Enabled ? Colors.LightText : Colors.DisabledText;
            set => base.ForeColor = Enabled ? Colors.LightText : Colors.DisabledText;
        }

        #endregion

        #region Constructor Region

        public DarkLabel()
        {
            // Center the text vertically: designer heights are tight against
            // the scaled font, and rows expect the label text to align with
            // the vertical center of the control beside it (combo, checkbox).
            TextAlign = ContentAlignment.MiddleLeft;
            ForeColor = Colors.LightText;
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) ApplyThemeColors();
            ApplyDpiHeight();
            // Re-apply once the owning form's DPI scale pass has landed.
            BeginInvoke(ApplyDpiHeight);
        }

        // Designer label heights are 96-DPI design units; the scaled font on
        // a high-DPI display is taller and would clip descenders. Measure the
        // actual wrapped text at the label's current (scaled) width and grow
        // the label upward, so its bottom edge — which sits next to the
        // associated control — stays put. Idempotent: the measured height for
        // a given width/DPI is stable, so repeated calls cannot creep.
        private void ApplyDpiHeight()
        {
            if (_autoUpdateHeight || AutoSize || _isGrowing || !IsHandleCreated) return;
            // A tiny width means the layout has not settled yet — measuring
            // wrapped text against it produces garbage heights.
            if (Width < Font.Height * 2) return;
            int min = Font.Height + Padding.Vertical;
            if (Text.Length > 0)
            {
                var sz = TextRenderer.MeasureText(Text, Font,
                    new Size(Width, int.MaxValue), TextFormatFlags.WordBreak);
                min = Math.Max(min, sz.Height + Padding.Vertical + 2);
            }
            if (Height >= min) return;
            int grown = min - Height;
            Top = Math.Max(0, Top - grown);
            Height = min;
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyDpiHeight();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        private void ApplyThemeColors()
        {
            ForeColor = Enabled ? Colors.LightText : Colors.DisabledText;
            Invalidate(true);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            ApplyThemeColors();
        }

        #endregion

        #region Method Region

        private void ResizeLabel()
        {
            if (!_autoUpdateHeight || _isGrowing)
                return;

            try
            {
                _isGrowing = true;
                var sz = new Size(Width, int.MaxValue);
                sz = TextRenderer.MeasureText(Text, Font, sz, TextFormatFlags.WordBreak);
                Height = sz.Height + Padding.Vertical;
            }
            finally
            {
                _isGrowing = false;
            }
        }

        #endregion

        #region Event Handler Region

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ResizeLabel();
            ApplyDpiHeight();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ResizeLabel();
            ApplyDpiHeight();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ResizeLabel();
        }

        #endregion
    }
}
