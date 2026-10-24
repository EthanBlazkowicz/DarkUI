using DarkUI.Config;
using DarkUI.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    public class DarkTreeView : UserControl
    {
        private const int B = 1;
        private readonly InnerTree _tree = new InnerTree();
        private readonly DarkScrollBar _vScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Vertical };
        private readonly DarkScrollBar _hScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Horizontal };
        private readonly Timer _deferTimer = new Timer { Interval = 500 };
        private readonly List<TreeNode> _visibleNodes = new();
        private int _scrollSize => Consts.ScrollBarSize;
        private bool _updateLayout;
        private bool _layoutPending;

        private class InnerTree : TreeView
        {
            [DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool show);
            const int SB_BOTH = 3;
            const int WM_VSCROLL = 0x115;
            const int WM_HSCROLL = 0x114;
            const int TVM_INSERTITEMW = 0x1132;
            const int TVM_DELETEITEM = 0x1101;

            public event Action ScrollStateChanged;
            public event Action ItemsChanged;

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (!IsHandleCreated) return;
                ShowScrollBar(Handle, SB_BOTH, false);
                if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL)
                    ScrollStateChanged?.Invoke();
                else if (m.Msg == TVM_INSERTITEMW || m.Msg == TVM_DELETEITEM)
                    ItemsChanged?.Invoke();
            }
        }

        [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO lpsi);
        [DllImport("user32.dll")] static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        const int SB_VERT = 1;
        const int SB_HORZ = 0;
        const int SIF_ALL = 0x1 | 0x2 | 0x4;
        const int WM_HSCROLL = 0x114;
        const int SB_THUMBPOSITION = 4;
        [StructLayout(LayoutKind.Sequential)] struct SCROLLINFO { public int cbSize, fMask, nMin, nMax, nPage, nPos, nTrackPos; }

        public TreeNodeCollection Nodes => _tree.Nodes;
        public TreeNode SelectedNode { get => _tree.SelectedNode; set => _tree.SelectedNode = value; }
        public ImageList ImageList { get => _tree.ImageList; set => _tree.ImageList = value; }
        public string PathSeparator { get => _tree.PathSeparator; set => _tree.PathSeparator = value; }
        public bool ShowPlusMinus { get => _tree.ShowPlusMinus; set => _tree.ShowPlusMinus = value; }
        public bool ShowLines { get => _tree.ShowLines; set => _tree.ShowLines = value; }
        public bool ShowRootLines { get => _tree.ShowRootLines; set => _tree.ShowRootLines = value; }
        public bool HotTracking { get => _tree.HotTracking; set => _tree.HotTracking = value; }
        public bool FullRowSelect { get => _tree.FullRowSelect; set => _tree.FullRowSelect = value; }
        public bool Scrollable { get => _tree.Scrollable; set => _tree.Scrollable = value; }
        public bool LabelEdit { get => _tree.LabelEdit; set => _tree.LabelEdit = value; }
        public bool CheckBoxes { get => _tree.CheckBoxes; set => _tree.CheckBoxes = value; }
        public int Indent { get => _tree.Indent; set => _tree.Indent = value; }
        public int ItemHeight { get => _tree.ItemHeight; set => _tree.ItemHeight = value; }
        public bool Sorted { get => _tree.Sorted; set => _tree.Sorted = value; }
        public System.Collections.IComparer TreeViewNodeSorter { get => _tree.TreeViewNodeSorter; set => _tree.TreeViewNodeSorter = value; }
        public TreeNode TopNode
        {
            get => _tree.TopNode;
            set
            {
                _tree.TopNode = value;
                // Sync DarkScrollBar to the new position
                if (!_updateLayout && value != null)
                {
                    int idx = _visibleNodes.IndexOf(value);
                    if (idx >= 0 && _vScrollBar.Visible)
                        _vScrollBar.Value = idx;
                }
            }
        }
        public new ContextMenuStrip ContextMenuStrip { get => _tree.ContextMenuStrip; set => _tree.ContextMenuStrip = value; }
        public bool UseCompatibleStateImageBehavior { get; set; }

        public event TreeViewEventHandler AfterSelect;
        public event TreeNodeMouseClickEventHandler NodeMouseClick;
        public new event MouseEventHandler MouseClick;
        public new event EventHandler DoubleClick;
        public event TreeViewCancelEventHandler BeforeExpand;
        public event TreeViewCancelEventHandler BeforeCollapse;
        public event TreeViewEventHandler AfterExpand;
        public event TreeViewEventHandler AfterCollapse;
        public event TreeViewEventHandler AfterCheck;
        public event TreeNodeMouseClickEventHandler NodeMouseDoubleClick;
        public event ItemDragEventHandler ItemDrag;
        public new event KeyEventHandler KeyDown;
        public new event KeyPressEventHandler KeyPress;
        public new event KeyEventHandler KeyUp;

        public DarkTreeView()
        {
            base.BackColor = Colors.LightBorder;
            ThemeManager.ThemeChanged += OnThemeChanged;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = Colors.GreyBackground;
            _tree.ForeColor = Colors.LightText;
            _tree.LineColor = Colors.GreySelection;
            _tree.HideSelection = false;
            // Native drawing (DrawMode.Normal) — OwnerDrawText proved unreliable:
            // the native TreeView's text rendering can't be cleanly suppressed,
            // producing doubled text and offset highlights. The native control draws
            // text + selection correctly; BackColor/ForeColor follow the theme.
            _tree.DrawMode = TreeViewDrawMode.Normal;
            _tree.Font = new Font("Segoe UI", 10F);
            _tree.ItemHeight = DpiHelper.Scale(this, 24);
            _tree.ShowPlusMinus = true;

            _tree.AfterSelect += (s, e) => AfterSelect?.Invoke(this, e);
            _tree.NodeMouseClick += (s, e) => NodeMouseClick?.Invoke(this, e);
            _tree.MouseClick += (s, e) => MouseClick?.Invoke(this, e);
            _tree.DoubleClick += (s, e) => DoubleClick?.Invoke(this, e);
            _tree.BeforeExpand += (s, e) => BeforeExpand?.Invoke(this, e);
            _tree.BeforeCollapse += (s, e) => BeforeCollapse?.Invoke(this, e);
            _tree.AfterExpand += (s, e) => { AfterExpand?.Invoke(this, e); UpdateScrollBarLayout(); _deferTimer.Stop(); _deferTimer.Start(); };
            _tree.AfterCollapse += (s, e) => { AfterCollapse?.Invoke(this, e); UpdateScrollBarLayout(); _deferTimer.Stop(); _deferTimer.Start(); };
            _tree.AfterCheck += (s, e) => AfterCheck?.Invoke(this, e);
            _tree.NodeMouseDoubleClick += (s, e) => NodeMouseDoubleClick?.Invoke(this, e);
            _tree.ItemDrag += (s, e) => ItemDrag?.Invoke(this, e);
            _tree.KeyDown += (s, e) => KeyDown?.Invoke(this, e);
            _tree.KeyPress += (s, e) => KeyPress?.Invoke(this, e);
            _tree.KeyUp += (s, e) => KeyUp?.Invoke(this, e);

            _tree.ScrollStateChanged += () =>
            {
                if (!_updateLayout && IsHandleCreated)
                    UpdateScrollBarLayout();
            };

            _tree.ItemsChanged += () => ScheduleLayout();

            _deferTimer.Tick += (s, e) =>
            {
                _deferTimer.Stop();
                if (!Disposing && IsHandleCreated) UpdateScrollBarLayout();
            };

            _tree.MouseWheel += (s, e) =>
            {
                if (ModifierKeys.HasFlag(Keys.Shift))
                {
                    if (_hScrollBar.Visible)
                        _hScrollBar.Value = Math.Max(0, Math.Min(_hScrollBar.Maximum,
                            _hScrollBar.Value - Math.Sign(e.Delta)));
                }
                else
                {
                    if (_vScrollBar.Visible)
                        _vScrollBar.Value = Math.Max(0, Math.Min(_vScrollBar.Maximum,
                            _vScrollBar.Value - Math.Sign(e.Delta)));
                }
            };

            _vScrollBar.BackColor = Colors.MediumBackground;
            _vScrollBar.Minimum = 0; _vScrollBar.Maximum = 100;
            _vScrollBar.ValueChanged += (s, e) =>
            {
                if (!_tree.IsHandleCreated || _updateLayout) return;
                _updateLayout = true;
                var node = GetNthVisible(e.Value);
                if (node != null) _tree.TopNode = node;

                SCROLLINFO hsi = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL };
                GetScrollInfo(_tree.Handle, SB_HORZ, ref hsi);
                if (hsi.nPos != _hScrollBar.Value)
                    SendMessage(_tree.Handle, WM_HSCROLL,
                        (IntPtr)((_hScrollBar.Value << 16) | SB_THUMBPOSITION), IntPtr.Zero);

                _updateLayout = false;
            };

            _hScrollBar.BackColor = Colors.MediumBackground;
            _hScrollBar.Minimum = 0; _hScrollBar.Maximum = 100;
            _hScrollBar.ValueChanged += (s, e) =>
            {
                if (!_tree.IsHandleCreated || _updateLayout) return;
                _updateLayout = true;
                SendMessage(_tree.Handle, WM_HSCROLL,
                    (IntPtr)((e.Value << 16) | SB_THUMBPOSITION), IntPtr.Zero);
                _updateLayout = false;
            };

            Controls.Add(_vScrollBar);
            Controls.Add(_hScrollBar);
            Controls.Add(_tree);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _deferTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        private void ApplyThemeColors()
        {
            base.BackColor = Colors.LightBorder;
            _tree.BackColor = Colors.GreyBackground;
            _tree.ForeColor = Colors.LightText;
            _tree.LineColor = Colors.GreySelection;
            _vScrollBar.BackColor = Colors.MediumBackground;
            _hScrollBar.BackColor = Colors.MediumBackground;
            Invalidate(true);
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // The wrapper's BackColor is the themed BORDER — clamp it so a
        // serialized value in Designer.cs can never pin it to a stale
        // color or stop it following ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new Color BackColor
        {
            get => Colors.LightBorder;
            set => base.BackColor = Colors.LightBorder;
        }

        public void ExpandAll()
        {
            _tree.ExpandAll();
            UpdateScrollBarLayout();
            _deferTimer.Stop();
            _deferTimer.Start();
            // Scroll to bottom once layout settles
            BeginInvoke(() =>
            {
                if (!Disposing && IsHandleCreated && _vScrollBar.Visible)
                    _vScrollBar.Value = _vScrollBar.Maximum;
            });
        }
        public void CollapseAll() { _tree.CollapseAll(); UpdateScrollBarLayout(); _deferTimer.Stop(); _deferTimer.Start(); }
        public new void BeginUpdate() => _tree.BeginUpdate();
        public new void EndUpdate()
        {
            _tree.EndUpdate();
            ScheduleLayout();
        }
        public TreeNode GetNodeAt(Point pt)
        {
            // pt is relative to DarkTreeView. Convert to inner _tree client coords.
            return _tree.GetNodeAt(_tree.PointToClient(this.PointToScreen(pt)));
        }

        public new void Focus() => _tree.Focus();
        public void RefreshLayout()
        {
            if (!Disposing && IsHandleCreated) UpdateScrollBarLayout();
        }

        private void ScheduleLayout()
        {
            if (_layoutPending || !IsHandleCreated) return;
            _layoutPending = true;
            BeginInvoke(() =>
            {
                _layoutPending = false;
                if (!Disposing && IsHandleCreated) UpdateScrollBarLayout();
            });
        }

        // ── NM_CUSTOMDRAW: theme the native selection via colors only ──
        // DrawMode stays Normal — the native TreeView draws text, images, glyphs,
        // indent, geometry. We only supply clrText/clrTextBk per node so selection
        // follows Colors.BlueSelection. No DrawNode, no manual drawing.
        //
        // PROTOTYPE SWITCH — PART 5 of the design doc:
        //   true  = locally mask CDIS_SELECTED during the draw notification
        //           (needed if visual styles override clrTextBk for selected nodes)
        //   false = keep CDIS_SELECTED, rely on clrTextBk being honored.
        // Flip after visual testing; do not ship unverified.
        private const bool MaskSelectedStateForDraw = true;

        protected override void WndProc(ref Message m)
        {
            // WM_NOTIFY from the inner native TreeView reaches this wrapper (its parent).
            // Custom-draw runs even when disabled so the disabled tree keeps
            // the themed palette (native disabled painting is system gray).
            if (m.Msg == NativeCustomDraw.WM_NOTIFY && IsHandleCreated
                && NativeCustomDraw.IsCustomDraw(m.LParam, _tree.Handle))
            {
                if (HandleTreeCustomDraw(ref m))
                    return; // m.Result set — do not pass to base
            }
            base.WndProc(ref m);
        }

        private bool HandleTreeCustomDraw(ref Message m)
        {
            var tvcd = Marshal.PtrToStructure<NMTVCUSTOMDRAW>(m.LParam);

            switch (tvcd.nmcd.dwDrawStage)
            {
                case NativeCustomDraw.CDDS_PREPAINT:
                    m.Result = (IntPtr)NativeCustomDraw.CDRF_NOTIFYITEMDRAW;
                    return true;

                case NativeCustomDraw.CDDS_ITEMPREPAINT:
                    {
                        bool sel = (tvcd.nmcd.uItemState & NativeCustomDraw.CDIS_SELECTED) != 0;

                        tvcd.clrTextBk = NativeCustomDraw.ToColorRef(sel ? Colors.BlueSelection : _tree.BackColor);
                        tvcd.clrText = NativeCustomDraw.ToColorRef(sel ? Colors.SelectionText
                            : Enabled ? _tree.ForeColor : Colors.DisabledText);

                        if (sel && MaskSelectedStateForDraw)
                            tvcd.nmcd.uItemState &= ~NativeCustomDraw.CDIS_SELECTED;

                        Marshal.StructureToPtr(tvcd, m.LParam, false);
                        m.Result = (IntPtr)NativeCustomDraw.CDRF_DODEFAULT;
                        return true;
                    }
            }
            return false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode) ApplyThemeColors();
            _tree.HandleCreated += (s, ev) => BeginInvoke(UpdateScrollBarLayout);
            _tree.ItemHeight = DpiHelper.Scale(this, 24);
            BeginInvoke(() => _tree.ItemHeight = DpiHelper.Scale(this, 24));
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            _tree.ItemHeight = DpiHelper.Scale(this, 24);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            // Re-assert dark colors when toggled (defensive — a disabled TreeView
            // normally keeps its BackColor, but this guarantees it).
            _tree.BackColor = Colors.GreyBackground;
            _tree.ForeColor = Colors.LightText;
            _tree.Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (ClientSize.Width > 50 && ClientSize.Height > 50)
            {
                UpdateScrollBarLayout();
                _deferTimer.Stop();
                _deferTimer.Start();
            }
        }

        private void UpdateScrollBarLayout()
        {
            if (_updateLayout || !_tree.IsHandleCreated) return;
            try
            {
                _updateLayout = true;

                int cw = ClientSize.Width - B * 2;
                int ch = ClientSize.Height - B * 2;
                if (cw < 20 || ch < 20) return;

                RebuildVisibleCache();
                for (int pass = 0; pass < 2; pass++)
                {
                    SCROLLINFO vsi = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL };
                    GetScrollInfo(_tree.Handle, SB_VERT, ref vsi);
                    bool vVis = _visibleNodes.Count > vsi.nPage && vsi.nPage > 0;

                    SCROLLINFO hsi = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL };
                    GetScrollInfo(_tree.Handle, SB_HORZ, ref hsi);
                    bool hVis = hsi.nMax > hsi.nPage && hsi.nPage > 0;

                    int vbw = vVis ? _scrollSize : 0;
                    int hbh = hVis ? _scrollSize : 0;

                    if (vVis)
                    {
                        _vScrollBar.ViewSize = vsi.nPage;
                        _vScrollBar.Maximum = _visibleNodes.Count;
                        if (_vScrollBar.Value != vsi.nPos) _vScrollBar.Value = vsi.nPos;
                        _vScrollBar.Bounds = new Rectangle(B + cw - _scrollSize, B, _scrollSize, ch - hbh);
                    }
                    _vScrollBar.Visible = vVis;

                    if (hVis)
                    {
                        _hScrollBar.ViewSize = hsi.nPage;
                        _hScrollBar.Maximum = hsi.nMax;
                        if (_hScrollBar.Value != hsi.nPos) _hScrollBar.Value = hsi.nPos;
                        _hScrollBar.Bounds = new Rectangle(B, B + ch - _scrollSize, cw - vbw, _scrollSize);
                    }
                    _hScrollBar.Visible = hVis;

                    _tree.Bounds = new Rectangle(B, B, cw - vbw, ch - hbh);
                }

                _tree.SendToBack();
                _vScrollBar.BringToFront();
                _hScrollBar.BringToFront();

                if (_vScrollBar.Visible || _hScrollBar.Visible) _tree.Invalidate();

                if (_vScrollBar.Visible)
                {
                    var node = GetNthVisible(_vScrollBar.Value);
                    if (node != null) _tree.TopNode = node;
                }
                if (_hScrollBar.Visible)
                {
                    SCROLLINFO curHsi = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL };
                    GetScrollInfo(_tree.Handle, SB_HORZ, ref curHsi);
                    if (curHsi.nPos != _hScrollBar.Value)
                        SendMessage(_tree.Handle, WM_HSCROLL,
                            (IntPtr)((_hScrollBar.Value << 16) | SB_THUMBPOSITION), IntPtr.Zero);
                }
                else
                {
                    SCROLLINFO curHsi = new SCROLLINFO { cbSize = Marshal.SizeOf<SCROLLINFO>(), fMask = SIF_ALL };
                    GetScrollInfo(_tree.Handle, SB_HORZ, ref curHsi);
                    if (curHsi.nPos != 0)
                        SendMessage(_tree.Handle, WM_HSCROLL,
                            (IntPtr)SB_THUMBPOSITION, IntPtr.Zero);
                }
            }
            finally { _updateLayout = false; }
        }

        private void RebuildVisibleCache()
        {
            _visibleNodes.Clear();
            Flatten(_tree.Nodes);
            void Flatten(TreeNodeCollection nodes)
            {
                foreach (TreeNode n in nodes)
                {
                    _visibleNodes.Add(n);
                    if (n.IsExpanded) Flatten(n.Nodes);
                }
            }
        }

        private TreeNode GetNthVisible(int index)
        {
            return index >= 0 && index < _visibleNodes.Count ? _visibleNodes[index] : null;
        }
    }
}
