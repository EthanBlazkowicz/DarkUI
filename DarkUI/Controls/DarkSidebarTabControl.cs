using DarkUI.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using Microsoft.DotNet.DesignTools.Editors;

namespace DarkUI.Controls
{
    /// <summary>
    /// A tab control whose menu is a vertical sidebar on the left, like
    /// the TestApp's own navigation. Pages are SidebarPages (a themed
    /// Panel); switching pages swaps the visible content. Designer-aware:
    /// the Add Page / Remove Page verbs and the Pages collection editor
    /// work like a TabControl's TabPages.
    /// </summary>
    [Designer(typeof(DarkSidebarTabControlDesigner))]
    [DefaultEvent(nameof(SelectedIndexChanged))]
    public class DarkSidebarTabControl : UserControl
    {
        private readonly SidebarNavigationListBox _sidebar = new SidebarNavigationListBox();
        private readonly Panel _sidebarDivider = new Panel();
        private readonly SidebarPageCollection _pages;
        private int _current = -1;
        private int _sidebarWidth = 180;
        private bool _allowSidebarResize = true;
        private bool _resizingSidebar;
        private int _resizeStartScreenX;
        private int _resizeStartWidth;

        public DarkSidebarTabControl()
        {
            _sidebar.Dock = DockStyle.None;
            _sidebar.BorderStyle = BorderStyle.None;
            _sidebar.BackColor = Colors.GreyBackground;
            _sidebar.ForeColor = Colors.LightText;
            _sidebar.SelectedIndexChanged += (s, e) =>
            {
                SyncSelection();
            };
            // The resize target lives inside the list's right edge. Keeping
            // this as one painted surface prevents a second HWND from
            // retaining the previous item's highlight during a tab switch.
            _sidebar.MouseDown += SidebarMouseDown;
            _sidebar.MouseMove += SidebarMouseMove;
            _sidebar.MouseUp += SidebarMouseUp;

            _sidebarDivider.BackColor = Colors.DarkBorder;
            _sidebarDivider.Enabled = false;

            base.BackColor = Colors.GreyBackground;
            base.Controls.Add(_sidebar);
            base.Controls.Add(_sidebarDivider);

            _pages = new SidebarPageCollection(this);

            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        // A fresh instance must show sidebar + page area together: the
        // UserControl default (150×150) is narrower than the 180px sidebar,
        // which leaves the Dock=Fill page host with zero width — the control
        // looks like "sidebar only". The designer uses DefaultSize for new
        // instances and skips serializing Size until the user resizes.
        protected override Size DefaultSize => new Size(640, 400);

        #region Properties

        [DefaultValue(180)]
        [Category("Behavior")]
        [Description("Width of the sidebar menu on the left.")]
        public int SidebarWidth
        {
            get => _sidebarWidth;
            set
            {
                if (value < 60) value = 60;
                if (_sidebarWidth == value) return;
                _sidebarWidth = value;
                PerformLayout();
                Invalidate();
            }
        }

        [DefaultValue(true)]
        [Category("Behavior")]
        [Description("Determines whether the sidebar divider can be dragged while the application is running.")]
        public bool AllowSidebarResize
        {
            get => _allowSidebarResize;
            set
            {
                if (_allowSidebarResize == value) return;
                _allowSidebarResize = value;
                _sidebar.Cursor = Cursors.Default;

                if (!value && _resizingSidebar)
                {
                    _resizingSidebar = false;
                    _sidebar.Capture = false;
                    SidebarResizeCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [DefaultValue(-1)]
        [Category("Behavior")]
        [Description("Index of the currently selected page; -1 when none.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _current;
            set
            {
                if (value < -1 || value >= _pages.Count) value = -1;
                if (value == _current) return;
                if (value == -1)
                {
                    SetCurrent(-1);
                    _sidebar.SelectedIndex = -1;
                    return;
                }
                _sidebar.SelectedIndex = value;
                SetCurrent(value);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SidebarPage SelectedPage
        {
            get => _current >= 0 && _current < _pages.Count ? _pages[_current] : null;
            set
            {
                if (value == null) SelectedIndex = -1;
                else SelectedIndex = _pages.IndexOf(value);
            }
        }

        [Category("Pages")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(SidebarPageCollectionEditor), typeof(UITypeEditor))]
        public SidebarPageCollection Pages => _pages;

        // Pages are serialized exclusively through Pages. Exposing the
        // inherited Controls collection to the serializer would allow it to
        // emit a second, conflicting parenting path for the same pages.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ControlCollection Controls => base.Controls;

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the control from following
        // ThemeManager. ForeColor is also clamped so children on the pages
        // inherit LightText instead of the system text color.
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

        #endregion

        #region Events

        public event EventHandler SelectedIndexChanged;

        internal event EventHandler SidebarResizeStarted;
        internal event EventHandler SidebarResizeCompleted;

        #endregion

        #region Internals (called by SidebarPageCollection)

        internal int GetSidebarIndexAtScreen(Point screenPoint)
            => _sidebar.IndexFromPoint(_sidebar.PointToClient(screenPoint));

        internal void AttachPage(SidebarPage page)
        {
            ApplyPageColors(page);
            page.SidebarOwner = this;
            page.Dock = DockStyle.None;
            page.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            page.Visible = false;
            page.TextChanged += PageLabelChanged;
            base.Controls.Add(page);
            // The sidebar must remain ahead of each Dock=Fill page both for
            // docking layout and for native design-time hit testing.
            _sidebar.BringToFront();

            _sidebar.Items.Add(GetLabel(page));
            if (_pages.Count == 1)
            {
                // The first page becomes the initial navigation target.
                _sidebar.SelectedIndex = 0;
                SetCurrent(0);
            }
        }

        internal void DetachPage(SidebarPage page)
        {
            page.TextChanged -= PageLabelChanged;
            page.SidebarOwner = null;
            base.Controls.Remove(page);
        }

        internal void RemoveSidebarItem(int index)
        {
            if (index >= 0 && index < _sidebar.Items.Count)
                _sidebar.Items.RemoveAt(index);
        }

        internal void ClearSidebarItems() => _sidebar.Items.Clear();

        // Mirrors the page list 1:1 after an Insert reordered things.
        internal void RebuildSidebar()
        {
            int keep = _current;
            _sidebar.Items.Clear();
            foreach (SidebarPage page in _pages)
                _sidebar.Items.Add(GetLabel(page));
            if (keep >= 0 && keep < _sidebar.Items.Count)
                _sidebar.SelectedIndex = keep;
        }

        #endregion

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);

            // _sidebarWidth is stored in 96-DPI design units (the designer
            // serializes e.g. 140) — layout uses the DPI-scaled value.
            int sidebarWidth = (int)Math.Round(_sidebarWidth * DeviceDpi / 96f);

            var pageBounds = new Rectangle(
                sidebarWidth,
                0,
                Math.Max(0, ClientSize.Width - sidebarWidth),
                ClientSize.Height);

            _sidebar.Bounds = new Rectangle(
            0,
            0,
            Math.Max(0, sidebarWidth - 1),
            ClientSize.Height);

            _sidebarDivider.Bounds = new Rectangle(_sidebar.Width, 0, 1, ClientSize.Height);

            if (_pages != null)
            {
                foreach (SidebarPage page in _pages)
                    page.Bounds = pageBounds;
            }
        }

        // ── Anchor margin baseline ─────────────────────────────────────
        // The owning form's DPI scale pass resizes page children directly
        // and refreshes anchor margins only for the page that is visible
        // at the time — children on hidden pages keep stale margins and
        // come out mispositioned/overlapping when their page is shown.
        // Snapshot each child's margins once (post-scale) and re-apply
        // them when a page is selected.

        private readonly Dictionary<Control, Rectangle> _anchorMargins = new();
        private bool _anchorMarginsCaptured;

        private void CaptureAnchorMargins()
        {
            if (_anchorMarginsCaptured || DesignMode || _pages == null) return;
            _anchorMarginsCaptured = true;
            foreach (SidebarPage page in _pages)
            {
                int pageWidth = Math.Max(1, page.ClientSize.Width);
                int pageHeight = Math.Max(1, page.ClientSize.Height);
                foreach (Control c in page.Controls)
                    _anchorMargins[c] = new Rectangle(
                        c.Left, c.Top,
                        Math.Max(0, pageWidth - c.Right),
                        Math.Max(0, pageHeight - c.Bottom));
            }
        }

        private void ApplyAnchorMargins(SidebarPage page)
        {
            if (!_anchorMarginsCaptured || page is null) return;
            int pageWidth = Math.Max(1, page.ClientSize.Width);
            int pageHeight = Math.Max(1, page.ClientSize.Height);
            foreach (Control c in page.Controls)
            {
                // Docked and AutoSize controls manage their own bounds.
                if (c.Dock != DockStyle.None || c.AutoSize) continue;
                if (!_anchorMargins.TryGetValue(c, out var m))
                {
                    _anchorMargins[c] = new Rectangle(
                        c.Left, c.Top,
                        Math.Max(0, pageWidth - c.Right),
                        Math.Max(0, pageHeight - c.Bottom));
                    continue;
                }
                int w = pageWidth - m.Left - m.Right;
                int h = pageHeight - m.Top - m.Bottom;
                if (w > 0 && h > 0)
                    c.Bounds = new Rectangle(m.Left, m.Top, w, h);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyDpiMetrics();
            // The child's DPI context settles shortly after handle creation.
            BeginInvoke(ApplyDpiMetrics);
            // Defer until the owning form's DPI scale pass has fully landed,
            // then snapshot every page child's anchor margins and re-apply
            // them — hidden pages would otherwise come out mispositioned.
            BeginInvoke(() =>
            {
                if (Disposing || !IsHandleCreated) return;
                CaptureAnchorMargins();
                if (SelectedPage != null) ApplyAnchorMargins(SelectedPage);
            });
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyDpiMetrics();
        }

        // Item heights and other fixed metrics follow the real DPI; a layout
        // pass picks up the scaled sidebar width afterwards.
        private void ApplyDpiMetrics()
        {
            _sidebar.ItemHeight = (int)Math.Round(22 * DeviceDpi / 96f);
            PerformLayout();
        }

        #region Selection

        private void SidebarMouseDown(object sender, MouseEventArgs e)
        {
            if (!_allowSidebarResize || e.Button != MouseButtons.Left ||
                e.X < _sidebar.ClientSize.Width - SidebarResizeGripWidth)
                return;

            _resizingSidebar = true;
            _resizeStartScreenX = MousePosition.X;
            _resizeStartWidth = (int)Math.Round(_sidebarWidth * DeviceDpi / 96f);
            _sidebar.Capture = true;
            SidebarResizeStarted?.Invoke(this, EventArgs.Empty);
        }

        private void SidebarMouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizingSidebar)
            {
                _sidebar.Cursor = _allowSidebarResize &&
                    e.X >= _sidebar.ClientSize.Width - SidebarResizeGripWidth
                    ? Cursors.SizeWE
                    : Cursors.Default;
                return;
            }

            int scale = DeviceDpi;
            int minimum = (int)Math.Round(60f * scale / 96f);
            int maximum = Math.Max(minimum, ClientSize.Width - minimum);
            int physical = Math.Clamp(
                _resizeStartWidth + MousePosition.X - _resizeStartScreenX,
                minimum,
                maximum);
            SidebarWidth = (int)Math.Round(physical * 96f / scale);
        }

        private void SidebarMouseUp(object sender, MouseEventArgs e)
        {
            if (!_resizingSidebar)
                return;

            _resizingSidebar = false;
            _sidebar.Capture = false;
            SidebarResizeCompleted?.Invoke(this, EventArgs.Empty);
        }

        private const int SidebarResizeGripWidth = 6;

        private void SyncSelection()
        {
            int idx = _sidebar.SelectedIndex;
            if (idx >= _pages.Count) idx = -1; // stale after a removal

            // Tab-style navigation must never hide the active page merely
            // because the menu temporarily reports no selected item.
            if (idx == -1 && _current >= 0 && _current < _pages.Count)
                return;

            SetCurrent(idx);
        }

        internal void SetCurrent(int idx)
        {
            if (idx == _current) return;
            _current = idx;
            ShowPageAt(idx);
            CaptureAnchorMargins();
            var page = idx >= 0 && idx < _pages.Count ? _pages[idx] : null;
            if (page != null) ApplyAnchorMargins(page);
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }

        // All pages remain direct children of this control and stay Dock=Fill.
        // This mirrors the ownership shape expected by ParentControlDesigner:
        // each sited SidebarPage owns its own editable design surface.
        private void ShowPageAt(int idx)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                page.Visible = i == idx;
                if (i == idx)
                {
                    page.BringToFront();
                    _sidebar.BringToFront();
                    _sidebarDivider.BringToFront();
                }
            }
        }

        #endregion

        #region Theme

        protected override void Dispose(bool disposing)
        {
            if (disposing) ThemeManager.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            base.BackColor = Colors.GreyBackground;
            _sidebar.BackColor = Colors.GreyBackground;
            _sidebar.ForeColor = Colors.LightText;
            _sidebarDivider.BackColor = Colors.DarkBorder;
            foreach (SidebarPage page in _pages)
                ApplyPageColors(page);
            _sidebar.Invalidate();
            Invalidate(true);
        }

        private static void ApplyPageColors(SidebarPage page)
        {
            page.BackColor = Colors.GreyBackground;
        }

        #endregion

        #region Sidebar label sync

        // Sidebar label: page Text, falling back to the component Name so a
        // page whose designer Text was never set still shows a label (users
        // commonly set Name in the designer and expect it to show).
        private static string GetLabel(SidebarPage page)
            => string.IsNullOrEmpty(page.Text) ? page.Name : page.Text;

        private void PageLabelChanged(object sender, EventArgs e)
        {
            if (sender is not SidebarPage page) return;
            int idx = _pages.IndexOf(page);
            if (idx >= 0 && idx < _sidebar.Items.Count)
                _sidebar.Items[idx] = GetLabel(page);
        }

        #endregion
    }

    /// <summary>
    /// Small, purpose-built navigation list. Drawing the single sidebar
    /// column here avoids the shared DarkListView custom-draw pipeline and
    /// guarantees that both labels and selection colors follow the theme.
    /// </summary>
    internal sealed class SidebarNavigationListBox : ListBox
    {
        public SidebarNavigationListBox()
        {
            // Buffer owner-drawn item transitions so the old and new
            // selection paint as one visual update instead of flashing.
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
            UpdateStyles();
            DrawMode = DrawMode.OwnerDrawFixed;
            IntegralHeight = false;
            ItemHeight = 22;
            SelectionMode = SelectionMode.One;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color backColor = selected ? Colors.BlueSelection : Colors.GreyBackground;
            Color textColor = !Enabled ? Colors.DisabledText : selected ? Colors.SelectionText : Colors.LightText;

            using (var brush = new SolidBrush(backColor))
                e.Graphics.FillRectangle(brush, e.Bounds);

            var textBounds = new Rectangle(
                e.Bounds.X + 8,
                e.Bounds.Y,
                Math.Max(0, e.Bounds.Width - 16),
                e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                Items[e.Index]?.ToString() ?? string.Empty,
                Font,
                textBounds,
                textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Do not draw ListBox's dotted focus rectangle. Apart from not
            // fitting the DarkUI sidebar appearance, it is painted after the
            // selection fill and leaves stray pixels at an item's right edge
            // while the selected row changes.
        }

    }

    [ToolboxItem(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal sealed class SidebarResizeHandle : Control
    {
        internal SidebarNavigationListBox NavigationList { get; set; }
        private int _selectedIndex = -1;

        internal int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value)
                    return;

                _selectedIndex = value;
                Invalidate();
                // The grip is a separate child control. Update it now so it
                // cannot retain the previous row while the list redraws.
                if (IsHandleCreated)
                    Update();
            }
        }

        public SidebarResizeHandle()
        {
            Cursor = Cursors.SizeWE;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var background = new SolidBrush(Colors.GreyBackground))
                e.Graphics.FillRectangle(background, ClientRectangle);

            // The grip overlaps the last few pixels of the navigation list
            // to provide a comfortable resize target. Continue the selected
            // row through that overlap so it does not look clipped.
            if (NavigationList != null && _selectedIndex >= 0 &&
                _selectedIndex < NavigationList.Items.Count)
            {
                Rectangle selectedBounds = NavigationList.GetItemRectangle(_selectedIndex);
                using (var selection = new SolidBrush(Colors.BlueSelection))
                    e.Graphics.FillRectangle(
                        selection,
                        new Rectangle(0, selectedBounds.Y, Width, selectedBounds.Height));
            }

            // Keep the selected item continuous across the full sidebar.
            // The divider is drawn above and below it only, rather than
            // leaving a dark one-pixel seam at the menu item's right edge.
            using (var divider = new Pen(Colors.DarkBorder))
            {
                Rectangle selectedBounds = NavigationList != null && _selectedIndex >= 0 &&
                    _selectedIndex < NavigationList.Items.Count
                    ? NavigationList.GetItemRectangle(_selectedIndex)
                    : Rectangle.Empty;

                if (selectedBounds.IsEmpty)
                {
                    e.Graphics.DrawLine(divider, Width - 1, 0, Width - 1, Height);
                }
                else
                {
                    if (selectedBounds.Top > 0)
                        e.Graphics.DrawLine(divider, Width - 1, 0, Width - 1, selectedBounds.Top - 1);
                    if (selectedBounds.Bottom < Height)
                        e.Graphics.DrawLine(divider, Width - 1, selectedBounds.Bottom, Width - 1, Height);
                }
            }
        }
    }

    /// <summary>
    /// The page type of a DarkSidebarTabControl — a themed Panel whose Text
    /// becomes the sidebar entry. A Panel subclass because TabPage refuses
    /// any parent that isn't a TabControl.
    /// </summary>
    [Designer(typeof(DarkSidebarPageDesigner))]
    [ToolboxItem(false)]
    public class SidebarPage : Panel
    {
        // Set by DarkSidebarTabControl.AttachPage. The designer uses it to
        // reach the owning control without relying on the Parent property
        // while a collection change is in progress.
        // Internal → never serialized.
        internal DarkSidebarTabControl SidebarOwner { get; set; }

        public SidebarPage()
        {
            Padding = Padding.Empty;
        }

        public SidebarPage(string text) : this() => Text = text;

        // Panel hides Text in the Properties window because a normal panel
        // has no visible caption. SidebarPage uses it as the sidebar menu
        // title, so expose it again and allow normal multi-word labels.
        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [Category("Appearance")]
        [DefaultValue("")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [Description("Text displayed for this page in the sidebar menu.")]
        public override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        // ── Designer freeze guard ─────────────────────────────────────
        // Theme-owned colors must not be serialized by the designer — a
        // frozen value in Designer.cs would stop the page from following
        // ThemeManager.
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Color BackColor
        {
            get => Colors.GreyBackground;
            set => base.BackColor = Colors.GreyBackground;
        }
    }

    /// <summary>
    /// The Pages collection of a DarkSidebarTabControl. Keeps the sidebar
    /// menu items in sync 1:1 with the page list.
    /// </summary>
    public class SidebarPageCollection : IList
    {
        private readonly DarkSidebarTabControl _owner;
        private readonly List<SidebarPage> _list = new List<SidebarPage>();

        internal SidebarPageCollection(DarkSidebarTabControl owner) => _owner = owner;

        public int Count => _list.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => _list;

        public SidebarPage this[int index]
        {
            get => _list[index];
            set { RemoveAt(index); Insert(index, value); }
        }

        object IList.this[int index]
        {
            get => this[index];
            set => this[index] = (SidebarPage)value;
        }

        public int Add(SidebarPage page)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            int existing = _list.IndexOf(page);
            if (existing >= 0) return existing;
            _list.Add(page);
            _owner.AttachPage(page);
            return _list.Count - 1;
        }

        int IList.Add(object value) => Add((SidebarPage)value);

        public void AddRange(SidebarPage[] pages)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            foreach (var page in pages) Add(page);
        }

        public void Insert(int index, SidebarPage page)
        {
            if (index < 0 || index > _list.Count) throw new ArgumentOutOfRangeException(nameof(index));
            _list.Insert(index, page);
            _owner.AttachPage(page);
            _owner.RebuildSidebar();
        }

        void IList.Insert(int index, object value) => Insert(index, (SidebarPage)value);

        public void Remove(SidebarPage page) => RemoveAt(_list.IndexOf(page));

        void IList.Remove(object value) => Remove(value as SidebarPage);

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _list.Count) return;
            var page = _list[index];
            _list.RemoveAt(index);
            _owner.DetachPage(page);
            _owner.RemoveSidebarItem(index);
            if (_list.Count == 0)
                _owner.SetCurrent(-1);
            else if (_owner.SelectedIndex >= _list.Count)
                _owner.SelectedIndex = _list.Count - 1;
        }

        public void Clear()
        {
            foreach (var page in _list)
                _owner.DetachPage(page);
            _list.Clear();
            _owner.ClearSidebarItems();
            _owner.SetCurrent(-1);
        }

        public bool Contains(SidebarPage page) => _list.Contains(page);

        bool IList.Contains(object value) => Contains(value as SidebarPage);

        public int IndexOf(SidebarPage page) => _list.IndexOf(page);

        int IList.IndexOf(object value) => IndexOf(value as SidebarPage);

        public void CopyTo(Array array, int index) => ((IList)_list).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _list.GetEnumerator();
    }

    /// <summary>
    /// Collection editor for the Pages property — opens the standard
    /// collection dialog and creates pre-themed pages, like the
    /// TabPages editor on a TabControl.
    /// </summary>
    public class SidebarPageCollectionEditor : Microsoft.DotNet.DesignTools.Editors.CollectionEditor
    {
        public SidebarPageCollectionEditor(IServiceProvider serviceProvider, Type type)
            : base(serviceProvider, type) { }

        protected override Type CreateCollectionItemType() => typeof(SidebarPage);

        protected override object CreateInstance(Type itemType)
        {
            // In the designer, create through the designer host so the page
            // gets a site + name and serializes exactly like verb-created
            // pages (raw `new` here would leave the page unsited, breaking
            // child-control serialization after save/reopen).
            if (Context.GetService(typeof(IDesignerHost)) is IDesignerHost host)
            {
                int n = 1;
                while (host.Container.Components["tabPage" + n] != null) n++;
                var page = (SidebarPage)host.CreateComponent(typeof(SidebarPage), "tabPage" + n);
                page.Text = $"Page {n}";
                return page;
            }

            int num = 1;
            if (Context.Instance is DarkSidebarTabControl owner)
                num = owner.Pages.Count + 1;
            return new SidebarPage { Text = $"Page {num}" };
        }
    }
}
