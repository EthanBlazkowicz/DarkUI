using DarkUI.Config;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    public class DarkTabControl : TabControl
    {
        private const int TCS_MULTILINE = 0x0200;
        private const int TCS_FIXEDWIDTH = 0x0008;
        private const int WM_ERASEBKGND = 0x0014;

        private int _hoveredTab = -1;
        private bool _multiline;
        private bool _autoStack;
        private bool _effectiveMultiline;
        private bool _normalizingPageDockOrder;

        /// <summary>
        /// When true, tabs that don't fit wrap onto additional rows instead
        /// of showing the ◄ ► scroll arrows. Only applies to Top/Bottom
        /// alignment (multiline is unsupported for Left/Right).
        /// </summary>
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool Multiline
        {
            get => _multiline;
            set
            {
                if (_multiline == value) return;
                _multiline = value;
                UpdateStacking();
            }
        }

        /// <summary>
        /// When true, the control switches automatically between a single
        /// side-by-side row and stacked rows: if the tabs don't fit in the
        /// available width they wrap onto additional rows; when they fit
        /// again they collapse back to a single row. An explicit
        /// Multiline = true always wins (tabs stay stacked).
        /// </summary>
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool AutoStack
        {
            get => _autoStack;
            set
            {
                if (_autoStack == value) return;
                _autoStack = value;
                UpdateStacking();
            }
        }

        // True when the tabs need more width than the control has — the
        // trigger for AutoStack to switch from one row to stacked rows.
        // Works without a handle (TabCount/ItemSize/ClientSize are all
        // available pre-creation), so CreateParams can use it.
        private bool TabsOverflow()
        {
            if (Alignment != TabAlignment.Top && Alignment != TabAlignment.Bottom) return false;
            if (TabCount == 0) return false;
            // Fixed-size tabs: each tab is ItemSize.Width wide (+ a couple
            // of px of edge padding), so the whole row needs that sum.
            int needed = ItemSize.Width * TabCount + 6;
            return needed > ClientSize.Width;
        }

        // Recomputes the effective layout and recreates the native control
        // only when the stacked/single-row state actually changed (handle
        // recreation is expensive and causes flicker — never do it on
        // every resize, only on a state transition).
        private void UpdateStacking()
        {
            bool effective = _multiline || (_autoStack && TabsOverflow());
            if (effective == _effectiveMultiline) return;
            _effectiveMultiline = effective;
            if (IsHandleCreated) RecreateHandle();
            else Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // Resolve the effective state here so the native control is
                // created with the right style from the very first handle —
                // no RecreateHandle needed for the initial layout.
                _effectiveMultiline = _multiline || (_autoStack && TabsOverflow());
                if (_effectiveMultiline &&
                    (Alignment == TabAlignment.Top || Alignment == TabAlignment.Bottom))
                {
                    // Multiline makes tabs wrap to extra rows; FixedWidth
                    // gives uniform tab sizes so rows wrap evenly.
                    cp.Style |= TCS_MULTILINE | TCS_FIXEDWIDTH;
                }
                return cp;
            }
        }

        public DarkTabControl()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);

            base.BackColor = Colors.GreyBackground;
            base.DrawMode = TabDrawMode.OwnerDrawFixed;
            base.SizeMode = TabSizeMode.Fixed;
            base.ItemSize = new Size(160, 28);
            Padding = new Point(0, 0);
            AllowDrop = true;

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        // Suppress the native background erase — everything is painted in
        // OnPaint. Without this the control can flash system-colored
        // background between state changes.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) ApplyThemeColors();
            AutoSizeTabs();
            // RecreateHandle is illegal while creation is still in progress
            // (OnHandleCreated runs inside CreateHandle) — defer the state
            // check until the message loop can run it safely. Needed only
            // when AutoSizeTabs changed the tab widths enough to flip the
            // fit state after creation.
            BeginInvoke(() => { if (!Disposing && IsHandleCreated) UpdateStacking(); });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        private void ApplyThemeColors()
        {
            foreach (TabPage tp in TabPages)
            {
                tp.BackColor = Colors.GreyBackground;
                tp.UseVisualStyleBackColor = false;
            }
            Invalidate(true);
        }

        #region Hidden Properties

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new TabDrawMode DrawMode
        {
            get => TabDrawMode.OwnerDrawFixed;
            set => base.DrawMode = TabDrawMode.OwnerDrawFixed;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new TabSizeMode SizeMode
        {
            get => TabSizeMode.Fixed;
            set => base.SizeMode = TabSizeMode.Fixed;
        }

        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor
        {
            get => Colors.GreyBackground;
            set => base.BackColor = Colors.GreyBackground;
        }

        #endregion

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            if (e.Control is TabPage page)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Colors.GreyBackground;
                page.TextChanged += (_, _) => { AutoSizeTabs(); UpdateStacking(); };
                page.ControlAdded += OnTabPageControlsChanged;
                page.ControlRemoved += OnTabPageControlsChanged;
                page.Layout += OnTabPageLayout;
                NormalizeDockedFillControl(page);
            }
            else if ((Site?.DesignMode == true || DesignMode) && SelectedTab != null)
            {
                // The out-of-process designer can route a toolbox drop to
                // this owner-drawn TabControl rather than its selected native
                // TabPage. Move it immediately to the visible page so it is
                // editable and serializes as tabPage.Controls.Add(...).
                SelectedTab.Controls.Add(e.Control);
            }
            AutoSizeTabs();
            UpdateStacking();
        }

        protected override void OnControlRemoved(ControlEventArgs e)
        {
            if (e.Control is TabPage page)
            {
                page.ControlAdded -= OnTabPageControlsChanged;
                page.ControlRemoved -= OnTabPageControlsChanged;
                page.Layout -= OnTabPageLayout;
            }
            base.OnControlRemoved(e);
            AutoSizeTabs();
            UpdateStacking();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            // The designer can apply Dock after it adds controls and defer the
            // TabPage's own layout. Normalize here as well, before WinForms
            // calculates the next page layout pass.
            foreach (TabPage page in TabPages)
                NormalizeDockedFillControl(page);

            base.OnLayout(levent);
        }

        private void OnTabPageControlsChanged(object sender, ControlEventArgs e)
        {
            NormalizeDockedFillControl(sender as TabPage);
        }

        private void OnTabPageLayout(object sender, LayoutEventArgs e)
        {
            NormalizeDockedFillControl(sender as TabPage);
        }

        private void NormalizeDockedFillControl(TabPage page)
        {
            if (page == null || _normalizingPageDockOrder)
                return;

            Control fillControl = null;
            foreach (Control control in page.Controls)
            {
                if (control.Dock != DockStyle.Fill)
                    continue;

                // Multiple Fill children deliberately overlap in WinForms.
                // Leave that explicit advanced layout alone.
                if (fillControl != null)
                    return;

                fillControl = control;
            }

            // WinForms lays out docked controls from back to front. Put the
            // sole Fill child at index zero so Top/Bottom/Left/Right siblings
            // reserve their space first, independent of designer add order.
            if (fillControl != null && page.Controls.GetChildIndex(fillControl) != 0)
            {
                try
                {
                    _normalizingPageDockOrder = true;
                    page.Controls.SetChildIndex(fillControl, 0);
                }
                finally
                {
                    _normalizingPageDockOrder = false;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateStacking();
        }

        private void AutoSizeTabs()
        {
            if (TabCount == 0 || !IsHandleCreated) return;

            // Scale every pixel constant by the real DPI ratio — the font
            // measures correctly at high DPI, but fixed paddings/heights
            // would leave tabs cramped and short on scaled displays.
            float dpi = DeviceDpi / 96f;
            int minW = (int)Math.Round(80 * dpi);  // minimum width
            int pad = (int)Math.Round(32 * dpi);   // 16px padding each side
            int height = (int)Math.Round(28 * dpi); // tab row height
            int maxW = minW;
            using (var g = CreateGraphics())
            {
                for (int i = 0; i < TabCount; i++)
                {
                    var sz = g.MeasureString(TabPages[i].Text, Font);
                    int w = (int)Math.Ceiling(sz.Width) + pad;
                    if (w > maxW) maxW = w;
                }
            }
            if (ItemSize.Width != maxW || ItemSize.Height != height)
                ItemSize = new Size(maxW, height);
            Invalidate();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            // ItemSize assignment can trigger RecreateHandle mid-DPI-walk;
            // defer it until the message loop so sibling children finish
            // their own before/after-parent processing first.
            BeginInvoke(() =>
            {
                if (!Disposing && IsHandleCreated) AutoSizeTabs();
            });
        }

        #region State

        // The native multiline tab control rotates rows so the selected row
        // is always adjacent to the page. DarkUI draws and hit-tests a stable
        // index-based grid instead, while the native control continues to
        // host the TabPages and maintain selection state.
        private Rectangle GetVisualTabRect(int index)
        {
            if (!_effectiveMultiline || Alignment != TabAlignment.Top)
                return GetTabRect(index);

            int tabWidth = Math.Max(1, ItemSize.Width);
            int tabHeight = Math.Max(1, ItemSize.Height);
            int availableWidth = Math.Max(1, ClientSize.Width - 6);
            int tabsPerRow = Math.Max(1, availableWidth / tabWidth);
            int row = index / tabsPerRow;
            int column = index % tabsPerRow;
            return new Rectangle(column * tabWidth, row * tabHeight, tabWidth, tabHeight);
        }

        private int GetVisualTabAt(Point location)
        {
            for (int i = 0; i < TabCount; i++)
            {
                if (GetVisualTabRect(i).Contains(location))
                    return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prev = _hoveredTab;
            _hoveredTab = GetVisualTabAt(e.Location);
            if (_hoveredTab != prev) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredTab = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int visualTab = GetVisualTabAt(e.Location);
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && visualTab >= 0 && SelectedIndex != visualTab)
                SelectedIndex = visualTab;
            if (visualTab >= 0) { Invalidate(); Update(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            // Synchronous repaint — an async invalidate can leave an
            // intermediate frame visible between the click and the new
            // selection state.
            Invalidate();
            Update();
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            NormalizeDockedFillControl(SelectedTab);
            base.OnSelectedIndexChanged(e);
            Invalidate();
            Update();
        }

        #endregion

        #region Paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var tabHeight = ItemSize.Height;
            bool aeroGlass = AeroGlassRenderer.IsActive;
            bool xpLuna = WindowsXpLunaRenderer.IsActive;
            bool windows98 = Windows98Renderer.IsActive;

            // Header spans every tab row — with Multiline the tabs wrap
            // onto multiple rows, so the header/body split is the bottom
            // of the lowest tab row, not just ItemSize.Height.
            int headerH = tabHeight;
            if (TabCount > 0 && IsHandleCreated)
            {
                for (int i = 0; i < TabCount; i++)
                    headerH = Math.Max(headerH, GetVisualTabRect(i).Bottom);
            }

            // --- Fill the entire tab header background ---
            var headerRect = new Rectangle(0, 0, Width, headerH);
            if (aeroGlass)
                AeroGlassRenderer.DrawPanelBackground(g, headerRect);
            else if (xpLuna)
                WindowsXpLunaRenderer.DrawBackground(g, headerRect);
            else if (windows98)
            {
                using var brush = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(brush, headerRect);
            }
            else
            {
                using var brush = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(brush, headerRect);
            }

            // --- Draw each tab ---
            for (int i = 0; i < TabCount; i++)
            {
                var tabRect = GetVisualTabRect(i);
                bool active = i == SelectedIndex;
                bool hovered = i == _hoveredTab && !active;

                Color fill, text, border;
                if (active)
                {
                    fill = Colors.LighterBackground;
                    text = Colors.LightText;
                    // Theme border, not a background color — LightestBackground
                    // painted a washed-out light-grey outline around the tab.
                    border = Colors.LightBorder;
                }
                else if (hovered)
                {
                    fill = Colors.LightBackground;
                    text = Colors.LightText;
                    border = Colors.GreyHighlight;
                }
                else
                {
                    fill = Colors.MediumBackground;
                    text = Colors.LightText;
                    border = Colors.LightBorder;
                }

                if (!Enabled) text = Colors.DisabledText;

                if (aeroGlass)
                    AeroGlassRenderer.DrawSurface(g, tabRect, fill, border, 7, false, active || hovered);
                else if (xpLuna)
                    WindowsXpLunaRenderer.DrawSurface(g, tabRect, fill, border, false, active || hovered);
                else if (windows98)
                    Windows98Renderer.DrawSurface(g, tabRect, fill, false, active || hovered);
                else
                {
                    using var brush = new SolidBrush(fill);
                    g.FillRectangle(brush, new Rectangle(tabRect.Left, tabRect.Top, tabRect.Width - 1, tabRect.Height - 1));
                    using var pen = new Pen(border);
                    g.DrawRectangle(pen, tabRect.Left, tabRect.Top, tabRect.Width - 1, tabRect.Height - 1);
                }

                // Tab text — inset scaled with DPI so the padding around the
                // label grows proportionally on scaled displays.
                int inset = (int)(8 * DeviceDpi / 96f);
                var textRect = new Rectangle(tabRect.Left + inset, tabRect.Top,
                    tabRect.Width - inset * 2, tabRect.Height);
                using (var brush = new SolidBrush(text))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString(TabPages[i].Text, Font, brush, textRect, sf);
                }
            }

            // --- Content area background ---
            var bodyRect = new Rectangle(0, headerH, Width, Height - headerH);
            if (aeroGlass)
                AeroGlassRenderer.DrawPanelBackground(g, bodyRect);
            else if (xpLuna)
                WindowsXpLunaRenderer.DrawBackground(g, bodyRect);
            else if (windows98)
            {
                using var brush = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(brush, bodyRect);
            }
            else
            {
                using var brush = new SolidBrush(Colors.GreyBackground);
                g.FillRectangle(brush, bodyRect);
            }

            // Content border — matches DataGridView outline color
            if (aeroGlass)
                AeroGlassRenderer.DrawSurface(g, bodyRect, Color.FromArgb(0, Colors.GreyBackground), Colors.LightBorder, 8);
            else if (xpLuna)
                WindowsXpLunaRenderer.DrawSurface(g, bodyRect, Colors.GreyBackground, Colors.DarkBlueBorder);
            else if (windows98)
                Windows98Renderer.DrawSurface(g, bodyRect, Colors.GreyBackground);
            else
            {
                using var pen = new Pen(Colors.LightBorder);
                g.DrawRectangle(pen, bodyRect.Left, bodyRect.Top, bodyRect.Width - 1, bodyRect.Height - 1);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Absorbed — handled in OnPaint
        }

        #endregion

    }
}
