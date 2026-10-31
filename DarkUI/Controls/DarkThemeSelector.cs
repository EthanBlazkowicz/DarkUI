using DarkUI.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// A compact reusable Category + Theme picker. Drop it into any DarkUI
    /// container to let the user switch the application's active theme.
    /// </summary>
    [DefaultEvent(nameof(SelectedThemeChanged))]
    [DefaultProperty(nameof(ShowLabels))]
    public class DarkThemeSelector : UserControl
    {
        private readonly DarkLabel _categoryLabel;
        private readonly DarkComboBox _categoryCombo;
        private readonly DarkLabel _themeLabel;
        private readonly DarkComboBox _themeCombo;
        private IReadOnlyList<Theme> _visibleThemes = Array.Empty<Theme>();
        private bool _updating;
        private bool _showLabels = true;

        public event EventHandler SelectedThemeChanged;

        [DefaultValue(true)]
        [Category("Appearance")]
        [Description("Shows the Category and Theme labels.")]
        public bool ShowLabels
        {
            get => _showLabels;
            set
            {
                if (_showLabels == value) return;
                _showLabels = value;
                LayoutChildren();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedCategory => _categoryCombo.SelectedItem as string ?? ThemeManager.AllThemesCategory;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Theme SelectedTheme => _themeCombo.SelectedIndex >= 0 && _themeCombo.SelectedIndex < _visibleThemes.Count
            ? _visibleThemes[_themeCombo.SelectedIndex]
            : null;

        protected override Size DefaultSize => new Size(430, 28);

        public DarkThemeSelector()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            base.BackColor = Colors.GreyBackground;
            Padding = new Padding(0);

            _categoryLabel = new DarkLabel { Text = "Category", AutoSize = true };
            _categoryCombo = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(140, 25)
            };
            _themeLabel = new DarkLabel { Text = "Theme", AutoSize = true };
            _themeCombo = new DarkComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(190, 25)
            };

            _categoryCombo.Items.AddRange(ThemeManager.ThemeCategories.Cast<object>().ToArray());
            _categoryCombo.SelectedIndexChanged += (_, _) => PopulateThemes(applyFirstTheme: true);
            _themeCombo.SelectedIndexChanged += (_, _) => ApplySelectedTheme();

            Controls.Add(_categoryLabel);
            Controls.Add(_categoryCombo);
            Controls.Add(_themeLabel);
            Controls.Add(_themeCombo);

            ThemeManager.ThemeChanged += OnThemeChanged;
            SelectActiveTheme();
            LayoutChildren();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutChildren();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            base.BackColor = Colors.GreyBackground;
            SelectActiveTheme();
            Invalidate(true);
        }

        private int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96f);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DesignMode) return;
            // Re-layout once the owning form's DPI scale pass has landed —
            // it resizes this control without raising OnResize.
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(LayoutChildren);
        }

        private void LayoutChildren()
        {
            if (_themeCombo == null) return;

            _categoryLabel.Visible = _showLabels;
            _themeLabel.Visible = _showLabels;

            int comboY = Math.Max(0, (Height - _categoryCombo.Height) / 2);
            int labelY = Math.Max(0, (Height - _categoryLabel.Height) / 2);
            int left = Padding.Left;

            if (_showLabels)
            {
                _categoryLabel.Location = new Point(left, labelY);
                left = _categoryLabel.Right + Scale(6);
            }

            // The width clamp bounds are 96-DPI design units — scale them or
            // the category combo renders half-width on scaled displays and
            // the theme combo absorbs the difference (clipping its text).
            int categoryWidth = Math.Clamp((int)Math.Round(Width / 3f), Scale(100), Scale(160));
            _categoryCombo.Bounds = new Rectangle(left, comboY, categoryWidth, _categoryCombo.Height);
            left = _categoryCombo.Right + Scale(12);

            if (_showLabels)
            {
                _themeLabel.Location = new Point(left, labelY);
                left = _themeLabel.Right + Scale(6);
            }

            _themeCombo.Bounds = new Rectangle(left, comboY,
                Math.Max(Scale(90), Width - Padding.Right - left), _themeCombo.Height);
        }

        private void PopulateThemes(bool applyFirstTheme = false)
        {
            if (_updating) return;

            _updating = true;
            try
            {
                _visibleThemes = ThemeManager.GetThemes(SelectedCategory);
                _themeCombo.Items.Clear();
                _themeCombo.Items.AddRange(_visibleThemes.Select(theme => (object)theme.Name).ToArray());
                _themeCombo.SelectedIndex = _visibleThemes.Count > 0 ? 0 : -1;
            }
            finally
            {
                _updating = false;
            }

            // Selecting a category is also a complete theme choice: the first
            // theme shown for that category becomes active immediately. This
            // keeps save/close workflows from retaining the previous theme
            // merely because the user did not click the second combo box.
            if (applyFirstTheme)
                ApplySelectedTheme();
        }

        private void ApplySelectedTheme()
        {
            if (_updating || SelectedTheme == null) return;
            ThemeManager.Apply(SelectedTheme);
            SelectedThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SelectActiveTheme()
        {
            if (_categoryCombo == null) return;

            _updating = true;
            try
            {
                string category = ThemeManager.GetThemeCategory(ThemeManager.Active);
                int categoryIndex = _categoryCombo.Items.IndexOf(category);
                _categoryCombo.SelectedIndex = categoryIndex >= 0 ? categoryIndex : 0;
            }
            finally
            {
                _updating = false;
            }

            PopulateThemes();
            int themeIndex = _visibleThemes
                .Select((theme, index) => new { theme, index })
                .Where(item => ReferenceEquals(item.theme, ThemeManager.Active))
                .Select(item => item.index)
                .DefaultIfEmpty(-1)
                .First();
            if (themeIndex >= 0)
                _themeCombo.SelectedIndex = themeIndex;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color BackColor
        {
            get => Colors.GreyBackground;
            set => base.BackColor = Colors.GreyBackground;
        }
    }
}
