using DarkUI.Config;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace DarkUI.Controls
{
    /// <summary>
    /// Optional presentation overrides for an item cell in <see cref="DarkGroupedListView"/>.
    /// This lets consumers add domain-specific status colors or images without
    /// coupling the reusable control to their data model.
    /// </summary>
    public sealed class DarkGroupedListViewCellPresentation
    {
        public string Text { get; init; }
        public Image Image { get; init; }
        public Color? ForeColor { get; init; }
        public Color? BackColor { get; init; }
        public Color? SelectionForeColor { get; init; }
        public Color? SelectionBackColor { get; init; }
        public DataGridViewImageCellLayout ImageLayout { get; init; } = DataGridViewImageCellLayout.Zoom;
    }

    public class DarkGroupedListView : UserControl
    {
        private readonly DataGridView _base = new DataGridView();
        private readonly DarkScrollBar _vScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Vertical };
        private readonly DarkScrollBar _hScrollBar = new DarkScrollBar { ScrollOrientation = DarkScrollOrientation.Horizontal };
        private static readonly PropertyInfo _doubleBuffer = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly List<int> _groupRows = new();
        private (string name, int width)[] _cachedColumns;
        private bool _updating;
        private bool _suppressSync;
        private bool _updateLayout;
        private int _sortCol = -1;
        private bool _sortAsc = true;
        private int _scrollSize => DpiHelper.Scale(this, Consts.ScrollBarSize);

        public event EventHandler SelectedItemChanged;
        public event Action<int, string, MouseEventArgs> GroupHeaderClicked;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedFilePath { get; private set; }

        private ContextMenuStrip _contextMenu;
        private ContextMenuStrip _hookedMenu;
        private int _restoreRow = -1;   // pending restore: current cell to return to after menu closes
        private int _restoreCol = -1;

        public DarkGroupedListView()
        {
            BackColor = Colors.LightBorder;
            ThemeManager.ThemeChanged += OnThemeChanged;

            _base.Name = "baseView";
            _base.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _base.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _base.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            _base.RowHeadersVisible = false;
            _base.AllowUserToResizeRows = false;
            _base.BorderStyle = BorderStyle.None;
            _base.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _base.ScrollBars = ScrollBars.None;
            _base.EnableHeadersVisualStyles = false;
            _doubleBuffer.SetValue(_base, true, null);
            _base.BackgroundColor = Colors.GreyBackground;
            _base.GridColor = Colors.GreyBackground;
            _base.CellBorderStyle = DataGridViewCellBorderStyle.None;
            _base.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.DarkBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.DarkBackground,
                SelectionForeColor = Colors.LightText,
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };
            _base.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.GreyBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.BlueSelection,
                SelectionForeColor = Colors.SelectionText
            };
            _base.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.GreyBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.BlueSelection,
                SelectionForeColor = Colors.SelectionText
            };
            _base.ReadOnly = true;
            _base.AllowUserToAddRows = false;
            _base.AllowUserToDeleteRows = false;
            _base.MultiSelect = false;
            _base.RowTemplate.Height = 22;

            _base.RowPostPaint += (s, e) =>
            {
                if (e.RowIndex >= 0 && _groupRows.Contains(e.RowIndex))
                {
                    var r = _base.GetRowDisplayRectangle(e.RowIndex, false);
                    using var hiPen = new Pen(Colors.LightBorder); // theme-relative top line
                    e.Graphics.DrawLine(hiPen, r.Left, r.Top, r.Right - 1, r.Top);
                    using var shPen = new Pen(Colors.DarkBorder);
                    e.Graphics.DrawLine(shPen, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
            };

            _base.RowsAdded += (s, e) => UpdateScrollBarLayout();
            _base.RowsRemoved += (s, e) => UpdateScrollBarLayout();
            _base.ColumnWidthChanged += (s, e) => UpdateScrollBarLayout();
            _base.Scroll += (s, e) =>
            {
                if (_updateLayout) return;
                if (_hScrollBar.Value != _base.HorizontalScrollingOffset)
                    _hScrollBar.Value = _base.HorizontalScrollingOffset;
                if (_vScrollBar.Value != _base.FirstDisplayedScrollingRowIndex)
                    _vScrollBar.Value = _base.FirstDisplayedScrollingRowIndex;
            };

            // Sort within groups by rebuilding rows.
            // DataGridView.Sort() rearranges ALL rows including group headers,
            // which destroys the grouped structure.
            _base.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Left || _base.Rows.Count == 0) return;
                if (e.ColumnIndex < 0 || _groupRows.Count == 0) return;

                // ── Extract group data ──────────────────────────
                var groups = new List<(string headerText, bool collapsed, List<DataGridViewRow> items)>();
                for (int g = 0; g < _groupRows.Count; g++)
                {
                    int hdr = _groupRows[g];
                    string txt = _base.Rows[hdr].Cells[0].Value?.ToString() ?? "";
                    bool collapsed = txt.StartsWith("▶");
                    var items = new List<DataGridViewRow>();
                    int end = g + 1 < _groupRows.Count ? _groupRows[g + 1] : _base.Rows.Count;
                    for (int r = hdr + 1; r < end; r++)
                        items.Add(_base.Rows[r]);
                    groups.Add((txt, collapsed, items));
                }

                // ── Sort items within each group ────────────────
                int colIdx = e.ColumnIndex;
                var dir = _sortCol == colIdx && _sortAsc
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
                _sortCol = colIdx;
                _sortAsc = dir == ListSortDirection.Ascending;

                foreach (var (_, _, items) in groups)
                {
                    string colName = colIdx >= 0 && colIdx < _base.Columns.Count
                        ? _base.Columns[colIdx].Name : "";
                    items.Sort((a, b) =>
                    {
                        var va = a.Cells[colIdx].Value;
                        var vb = b.Cells[colIdx].Value;
                        int cmp;
                        if (colName == "Size")
                            cmp = CompareSizeStrings(va?.ToString(), vb?.ToString());
                        else
                            cmp = Comparer<object>.Default.Compare(va, vb);
                        return _sortAsc ? cmp : -cmp;
                    });
                }

                // ── Rebuild all rows in grouped order ───────────
                _updating = true;
                _base.Rows.Clear();
                _groupRows.Clear();
                foreach (var (headerText, collapsed, items) in groups)
                {
                    int hdrIdx = _base.Rows.Add();
                    _groupRows.Add(hdrIdx);
                    var hdr = _base.Rows[hdrIdx];
                    hdr.Height = (int)Math.Round(26 * DeviceDpi / 96f);
                    hdr.DefaultCellStyle.BackColor = Colors.MediumBackground;
                    hdr.DefaultCellStyle.ForeColor = Colors.LightText;
                    hdr.DefaultCellStyle.SelectionBackColor = Colors.MediumBackground;
                    hdr.DefaultCellStyle.SelectionForeColor = Colors.LightText;
                    hdr.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
                    hdr.Cells[0].Value = collapsed
                        ? headerText.Replace("▼", "▶")
                        : headerText.Replace("▶", "▼");

                    foreach (var item in items)
                    {
                        int r = _base.Rows.Add();
                        _base.Rows[r].Tag = item.Tag;
                        for (int c = 0; c < _base.Columns.Count && c < item.Cells.Count; c++)
                        {
                            var source = item.Cells[c];
                            if (source is DataGridViewImageCell imageCell)
                            {
                                _base.Rows[r].Cells[c] = new DataGridViewImageCell
                                {
                                    Value = imageCell.Value,
                                    ImageLayout = imageCell.ImageLayout
                                };
                            }
                            else
                            {
                                _base.Rows[r].Cells[c].Value = source.Value;
                            }

                            if (source.HasStyle)
                                _base.Rows[r].Cells[c].Style = new DataGridViewCellStyle(source.Style);
                        }
                        _base.Rows[r].Visible = !collapsed;
                    }
                }
                _updating = false;
                UpdateScrollBarLayout();
            };

            _base.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;

                if (_groupRows.Contains(e.RowIndex))
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        ToggleGroup(e.RowIndex);
                        // Auto-select the group's top PKG so the highlight + app sync follow.
                        // Only when expanded — selecting a hidden row would throw.
                        if (!IsGroupCollapsed(e.RowIndex))
                        {
                            for (int r = e.RowIndex + 1; r < _base.Rows.Count && !_groupRows.Contains(r); r++)
                            {
                                if (_base.Rows[r].Tag != null)
                                {
                                    _base.CurrentCell = _base.Rows[r].Cells[0]; // fires sync (intended)
                                    _base.Rows[r].Selected = true;
                                    break;
                                }
                            }
                        }
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        // The DGV already parked the current cell on this header row during
                        // MouseDown — that drives the IsGroupActive highlight while the menu
                        // is open. The cell/selection is restored when the menu closes
                        // (see ContextMenu_Closed / MouseDown). No explicit selection here.
                        string raw = _base.Rows[e.RowIndex].Cells[0].Value?.ToString() ?? "";
                        string groupName = raw;
                        if (groupName.StartsWith("▼ ") || groupName.StartsWith("▶ "))
                            groupName = groupName.Substring(2);
                        int paren = groupName.LastIndexOf("  (");
                        if (paren > 0) groupName = groupName.Substring(0, paren);
                        GroupHeaderClicked?.Invoke(e.RowIndex, groupName, e);
                    }
                    return;
                }

                if (e.Button == MouseButtons.Right)
                {
                    // Target the menu at the CLICKED row so the label has a real target
                    // (like the DGV), but WITHOUT raising SelectedItemChanged — no PKG
                    // read, no sync. The current cell/selection is restored separately.
                    var rtag = _base.Rows[e.RowIndex].Tag;
                    if (rtag != null)
                    {
                        var t = rtag.GetType();
                        var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                        SelectedFilePath = p?.GetValue(rtag)?.ToString() ?? rtag.ToString();
                    }
                    if (_contextMenu != null)
                        _contextMenu.Show(Cursor.Position);
                    return;
                }

                var tag = _base.Rows[e.RowIndex].Tag;
                if (tag != null)
                {
                    var t = tag.GetType();
                    var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                    SelectedFilePath = p?.GetValue(tag)?.ToString() ?? tag.ToString();
                }
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            };

            // WinForms DGV moves CurrentCell on ANY click (including right-click), which would
            // fire CurrentCellChanged → group highlight + app sync (PKG re-read). Capture the
            // pre-click cell here (the event fires before DGV's internal selection logic) and
            // suppress the sync during the click's cell move.
            //   • item right-click → restore immediately (view-only click)
            //   • header right-click → keep the parked highlight for the menu's lifetime,
            //     restore when the menu closes (ContextMenu_Closed)
            _base.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var hit = _base.HitTest(e.X, e.Y);
                if (hit.Type != DataGridViewHitTestType.Cell || hit.RowIndex < 0) return;
                bool onHeader = _groupRows.Contains(hit.RowIndex);

                int keepRow = _base.CurrentCellAddress.Y;
                int keepCol = _base.CurrentCellAddress.X;
                _suppressSync = true;
                _base.BeginInvoke((System.Windows.Forms.MethodInvoker)(() =>
                {
                    _suppressSync = false;
                    if (keepRow < 0 || keepRow >= _base.Rows.Count || _base.Columns.Count == 0) return;
                    int col = Math.Max(0, keepCol);
                    if (onHeader)
                    {
                        _restoreRow = keepRow;   // release when the menu closes
                        _restoreCol = col;
                    }
                    else
                    {
                        _suppressSync = true;
                        _base.CurrentCell = _base.Rows[keepRow].Cells[col];
                        _base.Rows[keepRow].Selected = true;
                        _suppressSync = false;
                    }
                }));
            };

            // Keyboard navigation (arrow keys) changes the current row without a mouse click —
            // sync SelectedFilePath and notify listeners so the main grid follows the highlight.
            _base.CurrentCellChanged += (s, e) =>
            {
                if (_updating || _suppressSync) return;
                _base.Invalidate(); // repaint group-header highlight for the new current row
                var row = _base.CurrentRow;
                if (row == null || row.Tag == null) return;
                var tag = row.Tag;
                var t = tag.GetType();
                var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                string path = p?.GetValue(tag)?.ToString() ?? tag.ToString();
                if (string.Equals(SelectedFilePath, path, StringComparison.OrdinalIgnoreCase)) return;
                SelectedFilePath = path;
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            };

            // Keyboard navigation never lands on a group header row.
            // Covers Up/Down/Home/End/PageUp/PageDown. The jump happens before
            // CurrentCellChanged sync so the synced path is always an item row.
            _base.KeyDown += (s, e) =>
            {
                if (_base.RowCount == 0 || _base.Columns.Count == 0) return;
                int row = _base.CurrentRow?.Index ?? 0;
                int col = Math.Max(0, Math.Min(_base.CurrentCell?.ColumnIndex ?? 0, _base.Columns.Count - 1));

                int target = -1;
                switch (e.KeyCode)
                {
                    case Keys.Down:
                        target = NextVisibleRowExpanding(row, 1);
                        break;
                    case Keys.Up:
                        target = NextVisibleRowExpanding(row, -1);
                        break;
                    case Keys.Home:
                        target = NextNonGroupRow(-1, 1);
                        break;
                    case Keys.End:
                        target = NextNonGroupRow(_base.RowCount, -1);
                        break;
                    case Keys.PageDown:
                        {
                            int page = Math.Max(1, _base.DisplayedRowCount(false) - 1);
                            target = NextNonGroupRow(row, page);
                            break;
                        }
                    case Keys.PageUp:
                        {
                            int page = Math.Max(1, _base.DisplayedRowCount(false) - 1);
                            target = NextNonGroupRow(row, -page);
                            break;
                        }
                }

                if (target < 0) return;               // nothing found — let DGV behave
                if (target == row) { e.Handled = true; return; }  // stay put, never land on a header
                if (target < _base.Rows.Count && col < _base.Rows[target].Cells.Count)
                {
                    _base.CurrentCell = _base.Rows[target].Cells[col];
                    _base.Rows[target].Selected = true;
                    e.Handled = true;
                }
            };

            // A group header renders highlighted while any of its items is the
            // current row (or the header itself is current, e.g. after right-click).
            _base.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (!Enabled)
                {
                    // Disabled: keep the dark body + dimmed text — the DGV would
                    // otherwise repaint with system colors (white/gray wash).
                    e.CellStyle.BackColor = Colors.GreyBackground;
                    e.CellStyle.SelectionBackColor = Colors.GreyBackground;
                    e.CellStyle.ForeColor = Colors.DisabledText;
                    e.CellStyle.SelectionForeColor = Colors.DisabledText;
                    return;
                }
                if (!_groupRows.Contains(e.RowIndex)) return;
                bool active = IsGroupActive(e.RowIndex);
                e.CellStyle.BackColor = active ? Colors.GreySelection : Colors.MediumBackground;
                e.CellStyle.SelectionBackColor = active ? Colors.GreySelection : Colors.MediumBackground;
                e.CellStyle.ForeColor = Colors.LightText;
                e.CellStyle.SelectionForeColor = Colors.LightText;
            };

            _base.MouseWheel += (s, e) =>
            {
                if (_vScrollBar.Visible)
                    _vScrollBar.Value = Math.Max(0, Math.Min(_vScrollBar.Maximum,
                        _vScrollBar.Value - Math.Sign(e.Delta)));
            };

            // ── Vertical scrollbar ────────────────────────────
            _vScrollBar.BackColor = Colors.MediumBackground;
            _vScrollBar.Minimum = 0; _vScrollBar.Maximum = 0;
            _vScrollBar.ValueChanged += (s, e) =>
            {
                if (_updateLayout || _base.RowCount == 0) return;
                _updateLayout = true;
                int target = Math.Max(0, Math.Min(_base.RowCount - 1, e.Value));
                while (target < _base.RowCount && !_base.Rows[target].Visible) target++;
                if (target < _base.RowCount)
                    _base.FirstDisplayedScrollingRowIndex = target;
                _updateLayout = false;
            };

            // ── Horizontal scrollbar ──────────────────────────
            _hScrollBar.BackColor = Colors.MediumBackground;
            _hScrollBar.Minimum = 0; _hScrollBar.Maximum = 0;
            _hScrollBar.ValueChanged += (s, e) =>
            {
                if (_updateLayout) return;
                _updateLayout = true;
                _base.HorizontalScrollingOffset = Math.Max(0, Math.Min(
                    Math.Max(TotalColumnsWidth - 1, 0), e.Value));
                _updateLayout = false;
            };

            Controls.Add(_base);
            Controls.Add(_vScrollBar);
            Controls.Add(_hScrollBar);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _base?.Dispose();
                _hScrollBar?.Dispose();
                _vScrollBar?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DesignMode) return;
            ApplyThemeColors();
            ApplyDpiRowMetrics();
            // Re-apply once the owning form's DPI scale pass has landed.
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(ApplyDpiRowMetrics);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            ApplyDpiRowMetrics();
        }

        // Row and group-header heights are 96-DPI design units; scale them
        // or rows render half-height on scaled displays. Rows added before
        // the handle existed (a hidden tab page populates lazily) captured
        // the unscaled template, so re-apply to every existing row too.
        // Recomputation is absolute, so repeated calls stay idempotent.
        private void ApplyDpiRowMetrics()
        {
            int itemHeight = (int)Math.Round(22 * DeviceDpi / 96f);
            int headerHeight = (int)Math.Round(26 * DeviceDpi / 96f);
            bool changed = _base.RowTemplate.Height != itemHeight;
            _base.RowTemplate.Height = itemHeight;
            foreach (DataGridViewRow row in _base.Rows)
            {
                int desired = _groupRows.Contains(row.Index) ? headerHeight : itemHeight;
                if (row.Height == desired)
                    continue;
                row.Height = desired;
                changed = true;
            }
            if (changed)
                UpdateScrollBarLayout();
        }

        private void OnThemeChanged(object sender, EventArgs e) => ApplyThemeColors();

        private void ApplyThemeColors()
        {
            BackColor = Colors.LightBorder;
            _base.BackgroundColor = Colors.GreyBackground;
            _base.GridColor = Colors.GreyBackground;
            // Assign FRESH style objects rather than mutating — DataGridView's
            // DefaultCellStyle property mutation was observed to not stick
            // (the style kept the initial theme's colors after a theme change).
            // The setter path notifies the grid and invalidates reliably.
            _base.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.GreyBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.BlueSelection,
                SelectionForeColor = Colors.SelectionText,
            };
            _base.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.GreyBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.BlueSelection,
                SelectionForeColor = Colors.SelectionText,
            };
            // Column headers: same approach — fresh object with selection colors
            // (they were frozen to the initial theme's charcoal before).
            _base.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Colors.DarkBackground,
                ForeColor = Colors.LightText,
                SelectionBackColor = Colors.DarkBackground,
                SelectionForeColor = Colors.LightText,
                Alignment = DataGridViewContentAlignment.MiddleCenter,
            };
            _vScrollBar.BackColor = Colors.MediumBackground;
            _hScrollBar.BackColor = Colors.MediumBackground;

            // Group header rows carry their own DefaultCellStyle set at row-creation
            // time (SetGroups / sort rebuild). Refresh them so they never hold stale
            // theme colors. Font (Bold) is theme-independent — leave it.
            foreach (int hdrIdx in _groupRows)
            {
                if (hdrIdx < 0 || hdrIdx >= _base.Rows.Count) continue;
                var hdr = _base.Rows[hdrIdx];
                hdr.DefaultCellStyle.BackColor = Colors.MediumBackground;
                hdr.DefaultCellStyle.ForeColor = Colors.LightText;
                hdr.DefaultCellStyle.SelectionBackColor = Colors.MediumBackground;
                hdr.DefaultCellStyle.SelectionForeColor = Colors.LightText;
            }

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

        // ── Public API ──────────────────────────────────────────

        public ContextMenuStrip ContextMenuStrip
        {
            get => _contextMenu;
            set
            {
                _contextMenu = value; /* do NOT set _base.ContextMenuStrip — it suppresses CellMouseClick */
                // Hook Closed so the group-header highlight can be released once the
                // (shared) menu closes. The app shows this same menu for group headers.
                if (value != null && !ReferenceEquals(value, _hookedMenu))
                {
                    if (_hookedMenu != null)
                        _hookedMenu.Closed -= ContextMenu_Closed;
                    value.Closed += ContextMenu_Closed;
                    _hookedMenu = value;
                }
            }
        }

        private void ContextMenu_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (_restoreRow < 0) return;
            int row = _restoreRow, col = _restoreCol;
            _restoreRow = -1; _restoreCol = -1;
            if (row >= 0 && row < _base.Rows.Count && _base.Columns.Count > 0)
            {
                _suppressSync = true;
                _base.CurrentCell = _base.Rows[row].Cells[Math.Max(0, col)];
                _base.Rows[row].Selected = true;
                _suppressSync = false;
            }
        }

        public bool MultiSelect
        {
            get => _base.MultiSelect;
            set => _base.MultiSelect = value;
        }

        public void DefineColumns(params (string name, int width)[] cols)
        {
            _cachedColumns = cols;
            _base.Columns.Clear();
            for (int i = 0; i < cols.Length; i++)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = cols[i].name,
                    HeaderText = cols[i].name,
                    // Match DarkDataGridView: columns fill the available width,
                    // and resizing one column redistributes the remaining space.
                    // FillWeight preserves the caller's intended initial proportions.
                    MinimumWidth = 24,
                    FillWeight = Math.Max(1, cols[i].width),
                    SortMode = DataGridViewColumnSortMode.Programmatic,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                };
                if (i > 0) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _base.Columns.Add(col);
            }
        }

        public void SetGroups<T>(IEnumerable<T> items, Func<T, string> groupBy,
            Func<T, string[]> columnsFunc, Func<T, Image[]> imagesFunc = null,
            Func<T, string, DarkGroupedListViewCellPresentation> presentationFunc = null)
        {
            _updating = true;
            _base.Rows.Clear();
            _groupRows.Clear();

            // Define columns from cache if not already defined
            if (_cachedColumns != null && _base.Columns.Count == 0)
                DefineColumns(_cachedColumns);
            if (_base.Columns.Count == 0)
            {
                _updating = false;
                UpdateScrollBarLayout();
                return;
            }

            if (items == null)
            {
                _updating = false;
                UpdateScrollBarLayout();
                return;
            }

            var grouped = items.GroupBy(groupBy).OrderBy(g => g.Key);
            foreach (var grp in grouped)
            {
                int hdrIdx = _base.Rows.Add();
                _groupRows.Add(hdrIdx);
                var hdr = _base.Rows[hdrIdx];
                hdr.Height = (int)Math.Round(26 * DeviceDpi / 96f);
                hdr.DefaultCellStyle.BackColor = Colors.MediumBackground;
                hdr.DefaultCellStyle.ForeColor = Colors.LightText;
                hdr.DefaultCellStyle.SelectionBackColor = Colors.MediumBackground;
                hdr.DefaultCellStyle.SelectionForeColor = Colors.LightText;
                hdr.DefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
                hdr.Cells[0].Value = $"▼ {grp.Key}  ({grp.Count()})";

                foreach (var item in grp)
                {
                    var cols = columnsFunc(item);
                    var imgs = imagesFunc?.Invoke(item);
                    int r = _base.Rows.Add();
                    _base.Rows[r].Tag = item;
                    for (int c = 0; c < cols.Length && c < _base.Columns.Count; c++)
                    {
                        var presentation = presentationFunc?.Invoke(item, _base.Columns[c].Name);
                        Image image = presentation?.Image ?? (imgs != null && c < imgs.Length ? imgs[c] : null);
                        if (image != null)
                        {
                            _base.Rows[r].Cells[c] = new DataGridViewImageCell
                            {
                                Value = image,
                                ImageLayout = presentation?.ImageLayout ?? DataGridViewImageCellLayout.Normal
                            };
                        }
                        else
                        {
                            _base.Rows[r].Cells[c].Value = presentation?.Text ?? cols[c];
                        }

                        ApplyCellPresentation(_base.Rows[r].Cells[c], presentation);
                    }
                }
            }
            _updating = false;
            UpdateScrollBarLayout();
        }

        private static void ApplyCellPresentation(DataGridViewCell cell, DarkGroupedListViewCellPresentation presentation)
        {
            if (presentation == null) return;
            if (presentation.ForeColor.HasValue) cell.Style.ForeColor = presentation.ForeColor.Value;
            if (presentation.BackColor.HasValue) cell.Style.BackColor = presentation.BackColor.Value;
            if (presentation.SelectionForeColor.HasValue) cell.Style.SelectionForeColor = presentation.SelectionForeColor.Value;
            if (presentation.SelectionBackColor.HasValue) cell.Style.SelectionBackColor = presentation.SelectionBackColor.Value;
        }

        public void Clear() { _base.Rows.Clear(); _groupRows.Clear(); UpdateScrollBarLayout(); }
        public void ExpandAll() { SetAllGroups(false); }
        public void CollapseAll() { SetAllGroups(true); }

        /// <summary>
        /// Updates the value of a single cell in an item row (not a group header)
        /// identified by the file path stored in the row's Tag.
        /// </summary>
        public void UpdateCellForPath(string filePath, int columnIndex, string newValue)
        {
            for (int r = 0; r < _base.Rows.Count; r++)
            {
                var row = _base.Rows[r];
                if (row.Tag == null) continue;
                var t = row.Tag.GetType();
                var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                string path = p?.GetValue(row.Tag)?.ToString();
                if (path == null) path = row.Tag.ToString();
                if (!string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (columnIndex >= 0 && columnIndex < row.Cells.Count)
                    row.Cells[columnIndex].Value = newValue;
                return;
            }
        }

        /// <summary>
        /// Returns the group-header row index that contains the given file path,
        /// or -1 if not found.
        /// </summary>
        public int FindGroupForPath(string filePath)
        {
            for (int r = 0; r < _base.Rows.Count; r++)
            {
                var row = _base.Rows[r];
                if (row.Tag == null) continue;
                var t = row.Tag.GetType();
                var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                string path = p?.GetValue(row.Tag)?.ToString();
                if (string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    for (int h = r - 1; h >= 0; h--)
                        if (_groupRows.Contains(h))
                            return h;
                }
            }
            return -1;
        }

        private void SetAllGroups(bool collapse)
        {
            foreach (int hdrIdx in _groupRows)
            {
                string txt = _base.Rows[hdrIdx].Cells[0].Value?.ToString() ?? "";
                bool currentlyExpanded = txt.StartsWith("▼");
                if (collapse == currentlyExpanded)
                    ToggleGroup(hdrIdx);
            }
        }

        public List<T> GetGroupItems<T>(int headerRowIndex) where T : class
        {
            var items = new List<T>();
            for (int r = headerRowIndex + 1;
                 r < _base.Rows.Count && !_groupRows.Contains(r); r++)
            {
                if (_base.Rows[r].Tag is T item)
                    items.Add(item);
            }
            return items;
        }

        public List<string> GetGroupFilePaths(int headerRowIndex)
        {
            var paths = new List<string>();
            for (int r = headerRowIndex + 1;
                 r < _base.Rows.Count && !_groupRows.Contains(r); r++)
            {
                var tag = _base.Rows[r].Tag;
                if (tag != null)
                {
                    var t = tag.GetType();
                    var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                    var path = p?.GetValue(tag)?.ToString();
                    if (!string.IsNullOrEmpty(path))
                        paths.Add(path);
                }
            }
            return paths;
        }

        public List<string> GetSelectedFilePaths()
        {
            var paths = new List<string>();
            foreach (DataGridViewRow row in _base.SelectedRows)
            {
                if (_groupRows.Contains(row.Index)) continue;
                var tag = row.Tag;
                if (tag != null)
                {
                    var t = tag.GetType();
                    var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                    var path = p?.GetValue(tag)?.ToString();
                    if (!string.IsNullOrEmpty(path))
                        paths.Add(path);
                }
            }
            return paths;
        }

        /// <summary>
        /// Selects the item row whose tag path matches (DGV → GLV sync), expanding
        /// the containing group first when it is collapsed. Returns true when a row
        /// was selected; never raises SelectedItemChanged — the caller is already
        /// the source of the selection, so this cannot loop back.
        /// </summary>
        public bool SelectFilePath(string filePath)
        {
            if (_updating || _suppressSync || string.IsNullOrEmpty(filePath)) return false;

            for (int r = 0; r < _base.Rows.Count; r++)
            {
                var row = _base.Rows[r];
                if (row.Tag == null) continue;

                var t = row.Tag.GetType();
                var p = t.GetProperty("Path") ?? t.GetProperty("FilePath");
                string path = p?.GetValue(row.Tag)?.ToString() ?? row.Tag.ToString();
                if (!string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase)) continue;

                // Expand a collapsed group so the target row becomes visible
                if (!row.Visible)
                {
                    int hdr = FindGroupHeader(r);
                    if (hdr >= 0 && IsGroupCollapsed(hdr))
                        ExpandGroup(hdr);
                    if (!row.Visible) return false; // still hidden — cannot select
                }

                if (_base.CurrentCellAddress.Y == r && row.Selected)
                    return false; // already current — nothing to do

                _suppressSync = true;
                try
                {
                    _base.CurrentCell = row.Cells[0];
                    row.Selected = true;
                }
                finally
                {
                    _suppressSync = false;
                }
                return true;
            }
            return false;
        }

        // ── Layout ───────────────────────────────────────────────

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            // Re-assert dark colors — a disabled DataGridView repaints with system colors.
            ApplyThemeColors();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBarLayout();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateScrollBarLayout();
        }

        private int TotalColumnsWidth =>
            _base.Columns.Cast<DataGridViewColumn>().Sum(c => c.Width);

        private void UpdateScrollBarLayout()
        {
            if (_updating || _updateLayout) return;
            try
            {
                _updateLayout = true;
                var size = ClientSize;
                const int B = 1;

                // Two-pass — scrollbar visibility changes the available space
                for (int pass = 0; pass < 2; pass++)
                {
                    // Vertical
                    int rowCount = _base.RowCount;
                    int visibleRowCount = CountVisibleRows();
                    bool vVis = visibleRowCount < rowCount && rowCount > 0;
                    int vbw = vVis ? _scrollSize : 0;

                    // Horizontal
                    int totalW = TotalColumnsWidth;
                    int availW = size.Width - B * 2 - vbw;
                    bool hVis = totalW > availW && availW > 0;
                    int hbh = hVis ? _scrollSize : 0;

                    var baseBounds = new Rectangle(B, B,
                        size.Width - B * 2 - vbw,
                        size.Height - B * 2 - hbh);

                    if (vVis)
                    {
                        _vScrollBar.ViewSize = visibleRowCount;
                        _vScrollBar.Maximum = Math.Max(visibleRowCount + 1, rowCount);
                        _vScrollBar.Bounds = new Rectangle(
                            size.Width - _scrollSize - B, B, _scrollSize, size.Height - B * 2 - hbh);
                    }
                    _vScrollBar.Visible = vVis;

                    if (hVis)
                    {
                        _hScrollBar.ViewSize = availW;
                        _hScrollBar.Maximum = Math.Max(availW + 1, totalW);
                        _hScrollBar.Bounds = new Rectangle(
                            B, size.Height - _scrollSize - B, size.Width - B * 2 - vbw, _scrollSize);
                    }
                    _hScrollBar.Visible = hVis;

                    _base.Bounds = baseBounds;
                }

                _base.SendToBack();
                _vScrollBar.BringToFront();
                _hScrollBar.BringToFront();
            }
            finally { _updateLayout = false; }
        }

        private int CountVisibleRows()
        {
            int count = 0;
            int remaining = ClientSize.Height - _base.ColumnHeadersHeight;
            if (remaining <= 0) return 0;
            foreach (DataGridViewRow row in _base.Rows)
            {
                if (!row.Visible) continue;
                remaining -= row.Height;
                if (remaining < 0) break;
                count++;
            }
            return count;
        }

        private static int CompareSizeStrings(string a, string b)
        {
            long Parse(string s)
            {
                if (string.IsNullOrEmpty(s)) return 0;
                var parts = s.Split(' ');
                if (parts.Length != 2) return 0;
                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val)) return 0;
                return parts[1].ToUpperInvariant() switch
                {
                    "B" or "BYTES" => (long)val,
                    "KB" => (long)(val * 1024),
                    "MB" => (long)(val * 1024 * 1024),
                    "GB" => (long)(val * 1024 * 1024 * 1024),
                    "TB" => (long)(val * 1024L * 1024 * 1024 * 1024),
                    _ => 0
                };
            }
            return Parse(a).CompareTo(Parse(b));
        }

        private int NextVisibleRowExpanding(int fromRow, int delta)
        {
            int dir = delta > 0 ? 1 : -1;
            int r = fromRow + delta;
            while (r >= 0 && r < _base.RowCount)
            {
                if (_groupRows.Contains(r))
                {
                    // A collapsed group directly ahead — expand it and enter its first item.
                    if (dir > 0 && IsGroupCollapsed(r))
                    {
                        ExpandGroup(r);
                        return r + 1;
                    }
                    r += dir; // header is never a landing spot
                    continue;
                }

                if (_base.Rows[r].Visible)
                    return r;

                // Invisible item = inside a collapsed group above (Up key).
                // Expand that group and land on its last item (nearest to current).
                if (dir < 0)
                {
                    int hdr = FindGroupHeader(r);
                    if (hdr >= 0)
                    {
                        ExpandGroup(hdr);
                        return LastItemOfGroup(hdr);
                    }
                }
                r += dir;
            }
            return fromRow;
        }

        private bool IsGroupCollapsed(int hdrRow)
        {
            string txt = _base.Rows[hdrRow].Cells[0].Value?.ToString() ?? "";
            return txt.StartsWith("▶");
        }

        private void ExpandGroup(int hdrRow)
        {
            if (IsGroupCollapsed(hdrRow)) ToggleGroup(hdrRow);
        }

        private int FindGroupHeader(int itemRow)
        {
            int owner = -1;
            foreach (int h in _groupRows)
            {
                if (h <= itemRow) owner = h;
                else break;
            }
            return owner;
        }

        private int LastItemOfGroup(int hdrRow)
        {
            int next = -1;
            foreach (int h in _groupRows)
            {
                if (h > hdrRow) { next = h; break; }
            }
            int last = next > 0 ? next - 1 : _base.RowCount - 1;
            return Math.Max(last, hdrRow + 1);
        }

        private int NextNonGroupRow(int fromRow, int delta)
        {
            int dir = delta > 0 ? 1 : -1;
            int r = fromRow + delta;
            while (r >= 0 && r < _base.RowCount)
            {
                if (!_groupRows.Contains(r) && _base.Rows[r].Visible)
                    return r;
                r += dir;
            }
            return fromRow;
        }

        private bool IsGroupActive(int hdrRow)
        {
            int cur = _base.CurrentCell?.RowIndex ?? -1;
            if (cur < 0) return false;
            int owner = -1;
            foreach (int h in _groupRows)
            {
                if (h <= cur) owner = h;
                else break;
            }
            return owner == hdrRow;
        }

        // ── Collapse/expand ─────────────────────────────────────

        private void ToggleGroup(int hdrRow)
        {
            string txt = _base.Rows[hdrRow].Cells[0].Value?.ToString() ?? "";
            bool expanded = txt.StartsWith("▼");
            for (int r = hdrRow + 1; r < _base.Rows.Count && !_groupRows.Contains(r); r++)
                _base.Rows[r].Visible = !expanded;
            _base.Rows[hdrRow].Cells[0].Value = expanded
                ? txt.Replace("▼", "▶") : txt.Replace("▶", "▼");
            UpdateScrollBarLayout();
        }
    }
}
