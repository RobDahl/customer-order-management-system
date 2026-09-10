using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;

namespace Coms.Desktop.Screens
{
    /// <summary>
    /// The one list layout every module uses: a filter bar on top, a grid
    /// in the middle, a pager and record count at the bottom, and the
    /// Refresh / New / Open / Export buttons. Subclasses add their filter
    /// controls, define columns and load a page.
    /// </summary>
    internal abstract class ListScreen<T> : UserControl, IActivatable, IStatusSource where T : class
    {
        private readonly FlowLayoutPanel _filters;
        private readonly DataGridView _grid;
        private readonly Label _countLabel;
        private readonly Button _firstButton;
        private readonly Button _previousButton;
        private readonly Button _nextButton;
        private readonly Button _lastButton;
        private readonly Button _newButton;
        private readonly Button _openButton;
        private readonly Button _exportButton;

        private PagedResult<T> _page = PagedResult<T>.Empty(new PagedRequest());
        private int _pageNumber = 1;

        protected ListScreen(string title, bool canCreate)
        {
            Title = title;
            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 9F);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(3) };

            _grid = new DataGridView();
            GridHelper.Configure(_grid);
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) { OpenSelected(); } };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; OpenSelected(); } };

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(3) };
            _countLabel = new Label { AutoSize = true, Margin = new Padding(3, 8, 12, 3), Width = 220 };
            _firstButton = Ui.MakeButton("|<", (s, e) => GoTo(1), 36);
            _previousButton = Ui.MakeButton("<", (s, e) => GoTo(_pageNumber - 1), 36);
            _nextButton = Ui.MakeButton(">", (s, e) => GoTo(_pageNumber + 1), 36);
            _lastButton = Ui.MakeButton(">|", (s, e) => GoTo(_page.TotalPages), 36);
            var spacer = new Label { Width = 24 };
            var refreshButton = Ui.MakeButton("Refresh", async (s, e) => await ReloadAsync());
            _newButton = Ui.MakeButton("New", (s, e) => CreateNew());
            _openButton = Ui.MakeButton("Open", (s, e) => OpenSelected());
            _exportButton = Ui.MakeButton("Export CSV", (s, e) => GridCsvExporter.ExportWithDialog(this, _grid, ExportFileName));

            _newButton.Visible = canCreate;
            bottom.Controls.AddRange(new Control[] { _countLabel, _firstButton, _previousButton, _nextButton, _lastButton, spacer, refreshButton, _newButton, _openButton, _exportButton });

            layout.Controls.Add(_filters, 0, 0);
            layout.Controls.Add(_grid, 0, 1);
            layout.Controls.Add(bottom, 0, 2);
            Controls.Add(layout);
        }

        /// <summary>Raised after a page loads, so the main window can show the count in its status bar.</summary>
        public event EventHandler<string> StatusChanged;

        public string Title { get; }

        protected DataGridView Grid => _grid;

        protected virtual string ExportFileName => Title.ToLowerInvariant().Replace(' ', '-');

        protected virtual int PageSize => 50;

        protected T SelectedItem => GridHelper.SelectedItem<T>(_grid);

        /// <summary>Adds a labelled filter control to the filter bar.</summary>
        protected TControl AddFilter<TControl>(string label, TControl control) where TControl : Control
        {
            _filters.Controls.Add(Ui.MakeLabel(label));
            control.Margin = new Padding(3, 3, 9, 3);
            _filters.Controls.Add(control);
            return control;
        }

        protected void AddFilterButtons()
        {
            _filters.Controls.Add(Ui.MakeButton("Search", async (s, e) => await ReloadAsync(), 70));
            _filters.Controls.Add(Ui.MakeButton("Clear", async (s, e) => { ClearFilters(); await ReloadAsync(); }, 60));
        }

        /// <summary>Called once to define grid columns.</summary>
        protected abstract void DefineColumns(DataGridView grid);

        /// <summary>Loads one page using the current filter values.</summary>
        protected abstract Task<PagedResult<T>> LoadPageAsync(PagedRequest paging);

        protected virtual void ClearFilters()
        {
        }

        protected virtual void CreateNew()
        {
        }

        protected virtual void Open(T item)
        {
        }

        public async Task ActivateAsync()
        {
            if (_grid.Columns.Count == 0)
            {
                DefineColumns(_grid);
            }

            await ReloadAsync();
        }

        public async Task ReloadAsync()
        {
            await GoToAsync(_pageNumber);
        }

        private async void GoTo(int page)
        {
            await GoToAsync(page);
        }

        private async Task GoToAsync(int page)
        {
            try
            {
                using (Ui.Busy(this))
                {
                    var paging = new PagedRequest { Page = Math.Max(1, page), PageSize = PageSize };
                    _page = await LoadPageAsync(paging);
                    _pageNumber = _page.Page;

                    _grid.DataSource = new List<T>(_page.Items);
                    _countLabel.Text = _page.TotalCount == 0
                        ? "No records"
                        : "Showing " + _page.FirstItemNumber + "-" + _page.LastItemNumber + " of " + _page.TotalCount.ToString("N0") + "  (page " + _page.Page + " of " + _page.TotalPages + ")";

                    _firstButton.Enabled = _previousButton.Enabled = _page.HasPrevious;
                    _nextButton.Enabled = _lastButton.Enabled = _page.HasNext;
                    _openButton.Enabled = _exportButton.Enabled = _page.Items.Count > 0;

                    StatusChanged?.Invoke(this, _page.TotalCount.ToString("N0") + " record(s)");
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private void OpenSelected()
        {
            T item = SelectedItem;
            if (item != null)
            {
                Open(item);
            }
        }
    }
}
