using DarkUI.Config;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// Scroll source/sink for a control that is themed with <see cref="DarkScrollBarHost"/>.
    /// Values are in whatever unit the native control reports (items, lines, chars...).
    /// </summary>
    internal interface IDarkScrollContent
    {
        bool VerticalScrollable { get; }
        int VerticalValue { get; }
        int VerticalMaximum { get; }
        int VerticalViewport { get; }
        void ScrollVerticalTo(int value);

        bool HorizontalScrollable { get; }
        int HorizontalValue { get; }
        int HorizontalMaximum { get; }
        int HorizontalViewport { get; }
        void ScrollHorizontalTo(int value);
    }

    /// <summary>
    /// Adds two themed <see cref="DarkScrollBar"/>s to a native scrollable control,
    /// hides the native scrollbars and keeps the bars in sync. Requires the native
    /// control to reclaim its client area after <c>ShowScrollBar(false)</c>
    /// (ListBox, CheckedListBox, multiline TextBox all do; RichEdit does not).
    /// </summary>
    internal sealed class DarkScrollBarHost : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool show);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after,
            int x, int y, int cx, int cy, uint flags);

        private const int SB_BOTH = 3;
        private const int GWL_STYLE = -16;
        private const int WS_CLIPCHILDREN = 0x02000000;
        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
            SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

        private readonly Control _owner;
        private readonly IDarkScrollContent _content;
        private readonly DarkScrollBar _vScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Vertical };
        private readonly DarkScrollBar _hScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Horizontal };

        private bool _updating;
        private bool _syncing;

        public DarkScrollBarHost(Control owner, IDarkScrollContent content)
        {
            _owner = owner;
            _content = content;

            _vScrollBar.ValueChanged += (s, e) => OnBarScrolled(false, e.Value);
            _hScrollBar.ValueChanged += (s, e) => OnBarScrolled(true, e.Value);

            owner.Controls.Add(_vScrollBar);
            owner.Controls.Add(_hScrollBar);

            ApplyTheme();
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        public void Dispose()
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _vScrollBar.Dispose();
            _hScrollBar.Dispose();
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyTheme();

        private void ApplyTheme()
        {
            _vScrollBar.BackColor = Colors.MediumBackground;
            _hScrollBar.BackColor = Colors.MediumBackground;
            _owner.Invalidate();
        }

        private void OnBarScrolled(bool horizontal, int value)
        {
            if (_syncing || _updating || !_owner.IsHandleCreated)
                return;

            if (horizontal)
                _content.ScrollHorizontalTo(value);
            else
                _content.ScrollVerticalTo(value);

            Sync();
        }

        /// <summary>Re-applies clipping and refreshes both bars.</summary>
        public void Update()
        {
            if (_updating || !_owner.IsHandleCreated)
                return;

            _updating = true;
            try
            {
                ApplyChildClipping();
                HideNativeScrollBars();

                bool vVisible = _content.VerticalScrollable;
                bool hVisible = _content.HorizontalScrollable;

                int vw = SystemInformation.VerticalScrollBarWidth;
                int hh = SystemInformation.HorizontalScrollBarHeight;
                var client = _owner.ClientRectangle;

                if (vVisible)
                    _vScrollBar.Bounds = new Rectangle(client.Right - vw, client.Top, vw, client.Height);
                _vScrollBar.Visible = vVisible;

                if (hVisible)
                    _hScrollBar.Bounds = new Rectangle(client.Left, client.Bottom - hh,
                        Math.Max(0, client.Width - (vVisible ? vw : 0)), hh);
                _hScrollBar.Visible = hVisible;

                if (vVisible)
                    _vScrollBar.BringToFront();
                if (hVisible)
                    _hScrollBar.BringToFront();

                Sync();
            }
            finally
            {
                _updating = false;
            }
        }

        public void HideNativeScrollBars()
        {
            if (!_owner.IsHandleCreated)
                return;
            ShowScrollBar(_owner.Handle, SB_BOTH, false);
            ShowScrollBar(_owner.Handle, SB_BOTH, false);
        }

        private void ApplyChildClipping()
        {
            if (!_owner.IsHandleCreated)
                return;
            var style = GetWindowLong(_owner.Handle, GWL_STYLE);
            if ((style & WS_CLIPCHILDREN) != 0)
                return;
            SetWindowLong(_owner.Handle, GWL_STYLE, style | WS_CLIPCHILDREN);
            SetWindowPos(_owner.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        private void Sync()
        {
            _syncing = true;
            try
            {
                SetBar(_vScrollBar, _content.VerticalValue, _content.VerticalMaximum, _content.VerticalViewport);
                SetBar(_hScrollBar, _content.HorizontalValue, _content.HorizontalMaximum, _content.HorizontalViewport);
            }
            finally
            {
                _syncing = false;
            }
        }

        private static void SetBar(DarkScrollBar bar, int value, int maximum, int viewport)
        {
            int max = Math.Max(maximum, viewport + 1);
            if (bar.Minimum != 0)
                bar.Minimum = 0;
            if (bar.Maximum != max)
                bar.Maximum = max;
            if (bar.ViewSize != viewport)
                bar.ViewSize = viewport;
            if (bar.Value != value)
                bar.Value = value;
        }
    }
}
