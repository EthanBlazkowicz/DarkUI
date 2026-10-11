using DarkUI.Config;
using DarkUI.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// A combobox whose dropdown items have checkboxes — multi-select.
    /// Clicking an item toggles its checkbox and keeps the dropdown open;
    /// clicking the arrow / outside / Escape closes it. The closed state
    /// shows the checked items as comma-separated text.
    /// Note: like DarkCheckedListBox, ItemHeight is fixed (18) and does not
    /// scale with DPI.
    /// </summary>
    public class DarkCheckedComboBox : ComboBox
    {
        #region Native Interop

        private const int CB_GETCOMBOBOXINFO = 0x0164;
        private const int CB_INSERTSTRING = 0x014A;
        private const int CB_DELETESTRING = 0x0144;
        private const int CB_RESETCONTENT = 0x014B;
        private const int LB_ITEMFROMPOINT = 0x01A9;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_NCPAINT = 0x0085;
        private const int VK_SPACE = 0x20;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_CLIENTEDGE = 0x0200;
        private const uint RDW_FRAME = 0x0400;
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo;
            public IntPtr hwndItem;
            public IntPtr hwndList;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetComboBoxInfo(IntPtr hWnd, ref COMBOBOXINFO pcbi);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
            UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam,
            IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

        #endregion Native Interop

        #region Fields

        // Checked state — index-keyed so duplicates and value types work, and
        // kept aligned with the native list via CB_INSERTSTRING/DELETESTRING/
        // RESETCONTENT interception in WndProc.
        private readonly HashSet<int> _checkedIndices = new HashSet<int>();
        private bool _droppedDown;

        // The dropdown list is a native popup window that handles its own
        // mouse messages — the combo's WndProc never sees item clicks. To
        // toggle checkboxes without closing, the list window is subclassed
        // each time the dropdown opens (the list is recreated per session).
        private readonly SUBCLASSPROC _listSubclassProc;
        private IntPtr _subclassedList;

        // Theme colors — re-read from Colors.* on every ThemeChanged.
        private Color _borderColor = Colors.GreySelection;
        private Color _buttonColor = Colors.LightBackground;
        private Color _boxBorder = Colors.LightBorder;
        private Color _boxFill = Colors.BlueSelection;
        private Color _checkedRow = Colors.LightBackground;
        private Color _uncheckedRow = Colors.GreyBackground;
        private readonly Padding _textPadding = new Padding(2);

        #endregion Fields

        #region Constructor

        public DarkCheckedComboBox()
        {
            _listSubclassProc = ListSubclassProc;
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 18;
            FlatStyle = FlatStyle.Flat;
            DropDownStyle = ComboBoxStyle.DropDownList;

            BackColor = Colors.LightBackground;
            ForeColor = Colors.LightText;
            ThemeManager.ThemeChanged += OnThemeChanged;

            CheckedItems = new CheckedItemsCollection(this);
            CheckedIndices = new CheckedIndicesCollection(this);
        }

        public DarkCheckedComboBox(IContainer container) : this()
        {
            container.Add(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) ApplyThemeColors();
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
            ForeColor = Colors.LightText;
            _borderColor = Colors.GreySelection;
            _buttonColor = Colors.LightBackground;
            _boxBorder = Colors.LightBorder;
            _boxFill = Colors.BlueSelection;
            _checkedRow = Colors.LightBackground;
            _uncheckedRow = Colors.GreyBackground;
            Invalidate(true);
        }

        #endregion Constructor

        #region Hidden Properties

        [DefaultValue(DrawMode.OwnerDrawFixed)]
        public new DrawMode DrawMode
        {
            get { return base.DrawMode; }
            set { base.DrawMode = value; }
        }

        [DefaultValue(FlatStyle.Flat)]
        public new FlatStyle FlatStyle
        {
            get { return base.FlatStyle; }
            set { base.FlatStyle = value; }
        }

        [DefaultValue(ComboBoxStyle.DropDownList)]
        public new ComboBoxStyle DropDownStyle
        {
            get { return base.DropDownStyle; }
            set { base.DropDownStyle = value; }
        }

        // Checked state is index-keyed, which breaks under CBS_SORT (insert
        // indices don't reflect final positions) — sorting is always off.
        [DefaultValue(false)]
        public new bool Sorted
        {
            get { return base.Sorted; }
            set { base.Sorted = false; }
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the control from
        // following ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [ReadOnly(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public sealed override Color ForeColor
        {
            get => Colors.LightText;
            set
            {
                base.ForeColor = Colors.LightText;
                Invalidate();
            }
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [ReadOnly(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public sealed override Color BackColor
        {
            get => Colors.LightBackground;
            set
            {
                base.BackColor = Colors.LightBackground;
                Invalidate();
            }
        }

        #endregion Hidden Properties

        #region Checked State API

        /// <summary>Sets whether the item at the given index is checked.</summary>
        public void SetItemChecked(int index, bool isChecked)
        {
            if (index < 0 || index >= Items.Count) return;

            bool changed = isChecked ? _checkedIndices.Add(index) : _checkedIndices.Remove(index);
            if (!changed) return;

            Invalidate();
            InvalidateList();
            OnCheckedItemsChanged(EventArgs.Empty);
        }

        /// <summary>Gets whether the item at the given index is checked.</summary>
        public bool GetItemChecked(int index)
            => index >= 0 && index < Items.Count && _checkedIndices.Contains(index);

        /// <summary>Checks or unchecks every item in one operation (single event).</summary>
        public void SetAllItemsChecked(bool isChecked)
        {
            if (isChecked)
            {
                if (_checkedIndices.Count == Items.Count) return;
                for (int i = 0; i < Items.Count; i++) _checkedIndices.Add(i);
            }
            else
            {
                if (_checkedIndices.Count == 0) return;
                _checkedIndices.Clear();
            }

            Invalidate();
            InvalidateList();
            OnCheckedItemsChanged(EventArgs.Empty);
        }

        /// <summary>The checked items, in item order.</summary>
        public CheckedItemsCollection CheckedItems { get; }

        /// <summary>The indices of the checked items, ascending.</summary>
        public CheckedIndicesCollection CheckedIndices { get; }

        /// <summary>Raised whenever the set of checked items changes.</summary>
        public event EventHandler CheckedItemsChanged;

        protected virtual void OnCheckedItemsChanged(EventArgs e)
            => CheckedItemsChanged?.Invoke(this, e);

        #endregion Checked State API

        #region Dropdown State

        protected override void OnDropDown(EventArgs e)
        {
            base.OnDropDown(e);
            _droppedDown = true;
            ApplyListSubclass();
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            base.OnDropDownClosed(e);
            _droppedDown = false;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            // Selection is hover/highlight state only — the closed box never
            // paints it as the selected item.
            Invalidate();
            base.OnSelectedIndexChanged(e);
        }

        #endregion Dropdown State

        #region Message Handling

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_KEYDOWN && _droppedDown)
            {
                // Fallback for builds where key messages go to the combo
                // rather than the list window (the list subclass handles
                // the normal case).
                if ((int)m.WParam == VK_SPACE && SelectedIndex >= 0)
                {
                    SetItemChecked(SelectedIndex, !GetItemChecked(SelectedIndex));
                    m.Result = (IntPtr)1;
                    return;
                }
            }
            else if (m.Msg == CB_INSERTSTRING)
            {
                // Items.Insert — shift checked indices >= wParam up by one.
                base.WndProc(ref m);
                ShiftIndicesAfter((int)m.WParam, +1);
                return;
            }
            else if (m.Msg == CB_DELETESTRING)
            {
                // Items.RemoveAt — drop the removed index, shift the rest down.
                base.WndProc(ref m);
                ShiftIndicesAfter((int)m.WParam, -1);
                return;
            }
            else if (m.Msg == CB_RESETCONTENT)
            {
                // Items.Clear.
                base.WndProc(ref m);
                _checkedIndices.Clear();
                return;
            }
            // CB_ADDSTRING (Items.Add) needs no handling — appending never
            // shifts existing indices, which also makes handle-recreation
            // repopulation state-safe.

            base.WndProc(ref m);
        }

        private void ApplyListSubclass()
        {
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (!GetComboBoxInfo(Handle, ref info) || info.hwndList == IntPtr.Zero) return;

            if (info.hwndList == _subclassedList) return;
            if (_subclassedList != IntPtr.Zero)
                RemoveWindowSubclass(_subclassedList, _listSubclassProc, (UIntPtr)1);

            if (SetWindowSubclass(info.hwndList, _listSubclassProc, (UIntPtr)1, UIntPtr.Zero))
            {
                _subclassedList = info.hwndList;
                // Force the non-client area to repaint so the themed border
                // replaces the native focus outline immediately.
                RedrawWindow(info.hwndList, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW);
            }
        }

        // Runs in the dropdown list's window proc while it's open. The list
        // is a native popup: clicks on items would commit the selection and
        // close the dropdown — here they toggle the checkbox instead.
        // lParam is in list-client coordinates (the message is delivered to
        // the list itself), so the raw value works with LB_ITEMFROMPOINT.
        private IntPtr ListSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
            UIntPtr uIdSubclass, UIntPtr dwRefData)
        {
            switch (uMsg)
            {
                case WM_NCPAINT:
                    {
                        // The native list paints its own blue focus outline around
                        // the open dropdown — replace it with the theme border.
                        IntPtr hdc = GetWindowDC(hWnd);
                        try
                        {
                            var rc = new RECT();
                            GetWindowRect(hWnd, ref rc);
                            int w = rc.Right - rc.Left;
                            int h = rc.Bottom - rc.Top;
                            int thickness = (GetWindowLong(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_CLIENTEDGE) != 0 ? 2 : 1;
                            using (var g = Graphics.FromHdc(hdc))
                            using (var pen = new Pen(_borderColor, thickness))
                                g.DrawRectangle(pen, thickness / 2f, thickness / 2f, w - thickness, h - thickness);
                        }
                        finally
                        {
                            ReleaseDC(hWnd, hdc);
                        }
                        return (IntPtr)1;
                    }
                case WM_LBUTTONDOWN:
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    {
                        // Leave the vertical scrollbar alone.
                        var rc = new RECT();
                        GetClientRect(hWnd, ref rc);
                        int x = unchecked((short)((uint)(int)lParam & 0xFFFF));
                        if (x >= rc.Right - SystemInformation.VerticalScrollBarWidth)
                            break;

                        long result = Native.SendMessage(hWnd, (uint)LB_ITEMFROMPOINT, IntPtr.Zero, lParam).ToInt64();
                        uint index = (uint)result & 0xFFFF;
                        uint outside = (uint)(result >> 16) & 0xFFFF;
                        if (outside == 0 && index != 0xFFFF && index < Items.Count)
                        {
                            if (uMsg == WM_LBUTTONDOWN)
                                SetItemChecked((int)index, !GetItemChecked((int)index));
                            return (IntPtr)1; // swallow — no selection commit, dropdown stays open
                        }
                        // Dead area below the items → native close.
                        break;
                    }
                case WM_KEYDOWN:
                    if ((int)wParam == VK_SPACE && SelectedIndex >= 0)
                    {
                        SetItemChecked(SelectedIndex, !GetItemChecked(SelectedIndex));
                        return (IntPtr)1;
                    }
                    break;
            }
            return DefSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
        }

        private void InvalidateList()
        {
            // Control.Invalidate() only repaints the closed box — the open
            // list is a separate native popup that must be invalidated on its
            // own window for fresh WM_DRAWITEMs.
            if (!_droppedDown) return;
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf<COMBOBOXINFO>() };
            if (GetComboBoxInfo(Handle, ref info) && info.hwndList != IntPtr.Zero)
                InvalidateRect(info.hwndList, IntPtr.Zero, true);
        }

        private void ShiftIndicesAfter(int fromIndex, int delta)
        {
            var shifted = new HashSet<int>();
            foreach (int i in _checkedIndices)
            {
                if (delta < 0 && i == fromIndex) continue; // removed item
                shifted.Add(i >= fromIndex ? i + delta : i);
            }
            _checkedIndices.Clear();
            foreach (int i in shifted) _checkedIndices.Add(i);
        }

        private string GetCheckedSummaryText()
        {
            var parts = new List<string>(_checkedIndices.Count);
            foreach (int i in _checkedIndices.OrderBy(x => x))
            {
                if (i < 0 || i >= Items.Count) continue;
                var formatE = new ListControlConvertEventArgs(null, typeof(string), Items[i]);
                OnFormat(formatE);
                parts.Add(formatE.Value?.ToString() ?? Items[i].ToString());
            }
            return string.Join(", ", parts);
        }

        #endregion Message Handling

        #region Drawing

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (Items.Count <= e.Index || e.Index <= -1) return;

            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool checkedItem = _checkedIndices.Contains(e.Index);

            Color bg = sel ? Colors.BlueSelection
                     : checkedItem ? _checkedRow
                     : _uncheckedRow;
            Color fg = !Enabled ? Colors.DisabledText : (sel ? Colors.SelectionText : Colors.LightText);

            using (var b = new SolidBrush(bg))
                e.Graphics.FillRectangle(b, e.Bounds);

            // Checkbox glyph — drawn manually (no CheckBoxRenderer in the
            // library), vertically centered in the 18px row.
            const int boxSize = 12;
            int boxX = e.Bounds.X + 3;
            int boxY = e.Bounds.Y + (e.Bounds.Height - boxSize) / 2;
            var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            using (var pen = new Pen(_boxBorder))
                e.Graphics.DrawRectangle(pen, boxRect);

            if (checkedItem)
            {
                using (var fill = new SolidBrush(_boxFill))
                    e.Graphics.FillRectangle(fill, boxRect);
                using (var font = new Font("Segoe UI Symbol", 9f))
                using (var brush = new SolidBrush(Enabled ? (sel ? Colors.SelectionText : Colors.LightText) : Colors.DisabledText))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString("✓", font, brush, boxRect, sf);
                }
            }

            var formatE = new ListControlConvertEventArgs(null, typeof(string), Items[e.Index]);
            OnFormat(formatE);
            string text = formatE.Value?.ToString() ?? Items[e.Index].ToString();
            var textRect = new Rectangle(
                boxRect.Right + 5, e.Bounds.Y,
                e.Bounds.Right - (boxRect.Right + 5) - 2, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, Font, textRect, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var buttonRect = new Rectangle(
                ClientRectangle.Width - SystemInformation.VerticalScrollBarWidth, 0,
                SystemInformation.VerticalScrollBarWidth, ClientRectangle.Height);
            var buttonIconRect = new Rectangle(
                buttonRect.Left + (buttonRect.Width - DarkComboBox.DefaultButtonIcon.Width) / 2,
                buttonRect.Top + (buttonRect.Height / 2 - DarkComboBox.DefaultButtonIcon.Height / 2),
                DarkComboBox.DefaultButtonIcon.Width, DarkComboBox.DefaultButtonIcon.Height);
            var textRect = new Rectangle(
                1 + _textPadding.Left, 1 + _textPadding.Top,
                ClientRectangle.Width - (2 + buttonRect.Width + _textPadding.Horizontal),
                ClientRectangle.Height - (2 + _textPadding.Vertical));

            // Dropdown button + border
            using (var buttonBrush = new SolidBrush(_buttonColor))
                e.Graphics.FillRectangle(buttonBrush, buttonRect);
            e.Graphics.DrawImage(DarkComboBox.DefaultButtonIcon, buttonIconRect);
            ControlPaint.DrawBorder(e.Graphics, buttonRect, _borderColor, ButtonBorderStyle.Solid);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, _borderColor, ButtonBorderStyle.Solid);

            // Closed state — comma-separated checked items, ellipsized.
            using (var backBrush = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(backBrush, textRect);
            TextRenderer.DrawText(e.Graphics, GetCheckedSummaryText(), Font, textRect,
                Enabled ? ForeColor : Colors.DisabledText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }

        #endregion Drawing

        #region Collections

        /// <summary>Read-only collection of checked item indices, ascending.</summary>
        public sealed class CheckedIndicesCollection : IList
        {
            private readonly DarkCheckedComboBox _owner;
            internal CheckedIndicesCollection(DarkCheckedComboBox owner) => _owner = owner;

            private int[] Snapshot() => _owner._checkedIndices.OrderBy(x => x).ToArray();

            public int Count => _owner._checkedIndices.Count;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool IsSynchronized => false;
            public object SyncRoot => this;

            public object this[int index]
            {
                get => Snapshot()[index];
                set => throw new NotSupportedException();
            }

            public bool Contains(object value)
                => value is int i && _owner.GetItemChecked(i);

            public int IndexOf(object value)
            {
                if (!(value is int i) || !_owner._checkedIndices.Contains(i)) return -1;
                return _owner._checkedIndices.OrderBy(x => x).ToList().IndexOf(i);
            }

            public void CopyTo(Array array, int index)
            {
                var snapshot = Snapshot();
                for (int i = 0; i < snapshot.Length; i++)
                    array.SetValue(snapshot[i], index + i);
            }

            public IEnumerator GetEnumerator() => Snapshot().GetEnumerator();

            public int Add(object value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, object value) => throw new NotSupportedException();
            public void Remove(object value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }

        /// <summary>Read-only collection of checked items, in item order.</summary>
        public sealed class CheckedItemsCollection : IList
        {
            private readonly DarkCheckedComboBox _owner;
            internal CheckedItemsCollection(DarkCheckedComboBox owner) => _owner = owner;

            private int[] Snapshot() => _owner._checkedIndices.OrderBy(x => x).ToArray();

            public int Count => _owner._checkedIndices.Count;
            public bool IsReadOnly => true;
            public bool IsFixedSize => true;
            public bool IsSynchronized => false;
            public object SyncRoot => this;

            public object this[int index]
            {
                get => _owner.Items[Snapshot()[index]];
                set => throw new NotSupportedException();
            }

            public bool Contains(object value)
            {
                foreach (int i in _owner._checkedIndices)
                {
                    if (Equals(_owner.Items[i], value)) return true;
                }
                return false;
            }

            public int IndexOf(object value)
            {
                int position = 0;
                foreach (int i in _owner._checkedIndices.OrderBy(x => x))
                {
                    if (Equals(_owner.Items[i], value)) return position;
                    position++;
                }
                return -1;
            }

            public void CopyTo(Array array, int index)
            {
                foreach (int i in Snapshot())
                    array.SetValue(_owner.Items[i], index++);
            }

            public IEnumerator GetEnumerator()
            {
                foreach (int i in Snapshot())
                    yield return _owner.Items[i];
            }

            public int Add(object value) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, object value) => throw new NotSupportedException();
            public void Remove(object value) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }

        #endregion Collections
    }
}
