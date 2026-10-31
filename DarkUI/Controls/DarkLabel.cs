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
        private double _lastAppliedDesignHeight = -1;
        private bool _hasApplied;
        private bool _fitQueued;

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
            if (DesignMode) return;
            ApplyThemeColors();
            // Every fit is deferred: before the owning form's DPI scale pass
            // lands, the width is still a design-unit value while the
            // point-based font already renders scaled, so WordBreak measuring
            // against it produces garbage (several-times-too-tall) heights.
            // The pass itself also raises OnFontChanged mid-scale (font
            // scaled, bounds not yet) — a direct fit there would measure at
            // mixed scaled/unscaled geometry too.
            QueueFit();
        }

        // Designer label heights are 96-DPI design units; the scaled font on
        // a high-DPI display is taller and would clip descenders. Measure the
        // actual wrapped text at the label's current (scaled) width and fit
        // the height to it, anchored at the designed top. Growing/shrinking
        // downward (never moving Top) keeps labels that start below a
        // section header below it: the previous bottom-anchored growth
        // marched Top upward and, once it clamped at 0, parked the label
        // over the header it was drawn beside.
        //
        // The FIRST fit is exact (the bounds it sees are mechanical DPI
        // scaling, not consumer intent, and consumers tune their layouts
        // around the fitted result). Afterwards the consumer wins: the fit
        // grows when measured text would clip, shrinks only a height this
        // routine applied itself (recovering an early over-measure), and
        // never touches a height changed by consumer code. Applied heights
        // are tracked in DESIGN units (device px × 96/DeviceDpi) so the
        // framework's own DPI scaling of the height is still recognized as
        // ours instead of being mistaken for a consumer change.
        /// <summary>
        /// Coalesces every fit request into one deferred call, so fits always
        /// run with settled (post-scale) geometry — direct fits from
        /// OnFontChanged/OnDpiChangedAfterParent would measure mid-scale
        /// mixed scaled/unscaled values.
        /// </summary>
        private void QueueFit()
        {
            if (_fitQueued || !IsHandleCreated || IsDisposed) return;
            _fitQueued = true;
            BeginInvoke(() =>
            {
                _fitQueued = false;
                if (!IsDisposed && IsHandleCreated)
                    ApplyDpiHeight();
            });
        }

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

            if (!_hasApplied)
            {
                // First fit — exact, recoverable afterwards.
                _hasApplied = true;
                _lastAppliedDesignHeight = min * 96.0 / DeviceDpi;
                if (Height != min)
                    Height = min;
                return;
            }

            double currentDesignHeight = Height * 96.0 / DeviceDpi;
            bool ours = _lastAppliedDesignHeight >= 0 &&
                        Math.Abs(currentDesignHeight - _lastAppliedDesignHeight) < 0.5;

            if (min > Height || (ours && Height != min))
            {
                _lastAppliedDesignHeight = min * 96.0 / DeviceDpi;
                if (Height != min)
                    Height = min;
            }
            else if (!ours)
            {
                // Consumer-owned height — record it as untouchable.
                _lastAppliedDesignHeight = -1;
            }
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            QueueFit();
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
            QueueFit();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ResizeLabel();
            QueueFit();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ResizeLabel();
        }

        #endregion
    }
}
