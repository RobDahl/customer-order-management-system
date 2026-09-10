using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Customers;
using Coms.Application.Products;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Products;

namespace Coms.Desktop.Forms
{
    /// <summary>Search box plus grid; double-click or OK returns the selected row.</summary>
    internal abstract class LookupForm<T> : Form where T : class
    {
        private readonly TextBox _search = Ui.MakeTextBox(260);
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _hint = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(6, 6, 3, 3) };

        protected LookupForm(string title)
        {
            Text = title;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(720, 460);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
            top.Controls.Add(Ui.MakeLabel("Search"));
            top.Controls.Add(_search);
            top.Controls.Add(Ui.MakeButton("Find", async (s, e) => await SearchAsync(), 70));
            top.Controls.Add(_hint);

            GridHelper.Configure(_grid);
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) { Accept(); } };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; Accept(); } };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("OK", (s, e) => Accept());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            CancelButton = cancel;

            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(); } };

            Controls.Add(_grid);
            Controls.Add(top);
            Controls.Add(buttons);

            Load += async (s, e) => { DefineColumns(_grid); await SearchAsync(); _search.Focus(); };
        }

        public T Selected { get; private set; }

        protected abstract void DefineColumns(DataGridView grid);

        protected abstract Task<IReadOnlyList<T>> FindAsync(string search);

        private async Task SearchAsync()
        {
            try
            {
                using (Ui.Busy(this))
                {
                    IReadOnlyList<T> rows = await FindAsync(_search.Text);
                    _grid.DataSource = new List<T>(rows);
                    _hint.Text = rows.Count == 0 ? "No matches" : rows.Count + " shown";
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private void Accept()
        {
            T item = GridHelper.SelectedItem<T>(_grid);
            if (item == null)
            {
                return;
            }

            Selected = item;
            DialogResult = DialogResult.OK;
        }
    }

    internal sealed class CustomerLookupForm : LookupForm<Customer>
    {
        private readonly Scoped _scoped;

        public CustomerLookupForm(Scoped scoped)
            : base("Find customer")
        {
            _scoped = scoped;
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "Number", nameof(Customer.CustomerNumber), 100);
            GridHelper.AddColumn(grid, "Name", nameof(Customer.Name), 260);
            GridHelper.AddColumn(grid, "Contact", nameof(Customer.ContactName), 140);
            GridHelper.AddColumn(grid, "Terms", nameof(Customer.PaymentTermsDays), 55, alignRight: true);
            GridHelper.AddColumn(grid, "Status", nameof(Customer.Status), 80);
        }

        protected override async Task<IReadOnlyList<Customer>> FindAsync(string search)
        {
            var filter = new CustomerFilter { Search = search, Status = CustomerStatus.Active };
            var paging = new PagedRequest { PageSize = 200, SortBy = "name" };
            PagedResult<Customer> page = await _scoped.RunAsync<ICustomerService, PagedResult<Customer>>(s => s.SearchAsync(filter, paging));
            return page.Items;
        }
    }

    internal sealed class ProductLookupForm : LookupForm<ProductLookup>
    {
        private readonly Scoped _scoped;

        public ProductLookupForm(Scoped scoped)
            : base("Find product")
        {
            _scoped = scoped;
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "SKU", nameof(ProductLookup.Sku), 100);
            GridHelper.AddColumn(grid, "Name", nameof(ProductLookup.Name), 300);
            GridHelper.AddColumn(grid, "Unit", nameof(ProductLookup.UnitOfMeasure), 50);
            GridHelper.AddColumn(grid, "Price", nameof(ProductLookup.UnitPrice), 90, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "On hand", nameof(ProductLookup.QuantityOnHand), 80, alignRight: true, format: "0.###");
        }

        protected override Task<IReadOnlyList<ProductLookup>> FindAsync(string search)
        {
            return _scoped.RunAsync<IProductService, IReadOnlyList<ProductLookup>>(s => s.LookupAsync(search, 200));
        }
    }

    /// <summary>A one-line prompt for a comment or reason.</summary>
    internal sealed class CommentForm : Form
    {
        private readonly TextBox _text = new TextBox { Width = 380, MaxLength = 500 };

        public CommentForm(string title, string prompt)
        {
            Text = title;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 120);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(10) };
            layout.Controls.Add(new Label { Text = prompt, AutoSize = true, Margin = new Padding(3, 3, 3, 6) });
            layout.Controls.Add(_text);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("OK", (s, e) => DialogResult = DialogResult.OK);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(layout);
            Controls.Add(buttons);
        }

        public string Comment => string.IsNullOrWhiteSpace(_text.Text) ? null : _text.Text.Trim();

        public static string Ask(IWin32Window owner, string title, string prompt)
        {
            using (var form = new CommentForm(title, prompt))
            {
                return form.ShowDialog(owner) == DialogResult.OK ? (form.Comment ?? string.Empty) : null;
            }
        }
    }
}
