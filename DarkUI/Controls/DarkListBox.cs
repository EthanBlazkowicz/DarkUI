using DarkUI.Config;
using DarkUI.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    public class DarkListBox : ListBox
    {
        public DarkListBox()
        {
            BackColor = Colors.GreyBackground;
            ForeColor = Colors.LightText;
            Padding = new Padding(2, 2, 2, 2);
            BorderStyle = BorderStyle.FixedSingle;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = DpiHelper.Scale(this, 18);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public DarkListBox(IContainer container) : this()
        {
            container.Add(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) ApplyThemeColors();
            // The native listbox border is system-colored (blue when
            // focused) — paint it with the theme border instead.
            NativeFocusBorder.Apply(Handle);
            ItemHeight = DpiHelper.Scale(this, 18);
            BeginInvoke(() => ItemHeight = DpiHelper.Scale(this, 18));
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ItemHeight = DpiHelper.Scale(this, 18);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        // The native control paints the empty area below the last item
        // with BackColor — keep it themed so it matches the item rows.
        private void ApplyThemeColors()
        {
            BackColor = Colors.GreyBackground;
            ForeColor = Colors.LightText;
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
            get => Colors.GreyBackground;
            set => base.BackColor = Colors.GreyBackground;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color ForeColor
        {
            get => Colors.LightText;
            set => base.ForeColor = Colors.LightText;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            int index = e.Index >= 0 ? e.Index : 0;
            if (index > Items.Count - 1) return;

            Rectangle bounds = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);

            // Background
            var odd = e.Index % 2 != 0;
            var bgColor = !odd ? Colors.HeaderBackground : Colors.GreyBackground;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                bgColor = Focused ? Colors.BlueSelection : Colors.GreySelection;

            using (var b = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(b, bounds);

            var formatE = new ListControlConvertEventArgs(null, typeof(string), Items[e.Index]);
            OnFormat(formatE);
            string text = formatE.Value?.ToString() ?? Items[e.Index].ToString();
            var isFocusedSelection = (e.State & DrawItemState.Selected) == DrawItemState.Selected && Focused;
            using (var brush = new SolidBrush(Enabled ? (isFocusedSelection ? Colors.SelectionText : Colors.LightText) : Colors.DisabledText))
                e.Graphics.DrawString(text, e.Font, brush, bounds, StringFormat.GenericDefault);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }
}
