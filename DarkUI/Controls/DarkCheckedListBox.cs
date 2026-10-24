using DarkUI.Config;
using DarkUI.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace DarkUI.Controls
{
    public class DarkCheckedListBox : CheckedListBox
    {
        public DarkCheckedListBox()
        {
            BackColor = Colors.LightBackground;
            ForeColor = Colors.LightText;
            Padding = new Padding(2, 2, 2, 2);
            BorderStyle = BorderStyle.FixedSingle;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = DpiHelper.Scale(this, 18);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public DarkCheckedListBox(IContainer container) : this()
        {
            container.Add(this);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ItemHeight = DpiHelper.Scale(this, 18);
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
            BackColor = Colors.LightBackground;
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
            get => Colors.LightBackground;
            set => base.BackColor = Colors.LightBackground;
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
            // "Draw no items" paint (empty area below the last item).
            if (e.Index < 0 || e.Index >= Items.Count) return;

            Rectangle bounds = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height);

            // Background
            var odd = e.Index % 2 != 0;
            var bgColor = !odd ? Colors.HeaderBackground : Colors.GreyBackground;

            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                bgColor = Focused ? Colors.BlueSelection : Colors.GreySelection;

            using (var b = new SolidBrush(bgColor))
                e.Graphics.FillRectangle(b, bounds);

            // Check box glyph. CheckedListBox draws the glyph inside its own
            // OnDrawItem, so an override that does not chain to base must draw
            // the glyph itself or no check boxes would ever appear.
            Size glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, CheckBoxState.UncheckedNormal);
            int cy = bounds.Y + Math.Max((bounds.Height - glyphSize.Height) / 2, 0);
            var glyphRect = new Rectangle(bounds.X + 2, cy, glyphSize.Width, glyphSize.Height);

            if (Application.RenderWithVisualStyles)
            {
                CheckBoxState cbState;
                if (!Enabled)
                {
                    cbState = GetItemCheckState(e.Index) switch
                    {
                        CheckState.Checked => CheckBoxState.CheckedDisabled,
                        CheckState.Indeterminate => CheckBoxState.MixedDisabled,
                        _ => CheckBoxState.UncheckedDisabled,
                    };
                }
                else
                {
                    cbState = GetItemCheckState(e.Index) switch
                    {
                        CheckState.Checked => CheckBoxState.CheckedNormal,
                        CheckState.Indeterminate => CheckBoxState.MixedNormal,
                        _ => CheckBoxState.UncheckedNormal,
                    };
                }
                CheckBoxRenderer.DrawCheckBox(e.Graphics, glyphRect.Location, cbState);
            }
            else
            {
                ButtonState state = ButtonState.Flat;
                if (GetItemCheckState(e.Index) != CheckState.Unchecked)
                    state |= ButtonState.Checked;
                if (!Enabled)
                    state |= ButtonState.Inactive;
                ControlPaint.DrawCheckBox(e.Graphics, glyphRect, state);
            }

            // Text after the glyph
            var textBounds = new Rectangle(
                glyphRect.Right + 3,
                bounds.Y,
                bounds.Right - glyphRect.Right - 3,
                bounds.Height);

            var isFocusedSelection = (e.State & DrawItemState.Selected) == DrawItemState.Selected && Focused;
            using (var brush = new SolidBrush(Enabled ? (isFocusedSelection ? Colors.SelectionText : Colors.LightText) : Colors.DisabledText))
                e.Graphics.DrawString(Items[e.Index].ToString(), e.Font, brush, textBounds, StringFormat.GenericDefault);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }
    }
}
