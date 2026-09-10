using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Invoices;
using Coms.Desktop.Forms;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Invoices;

namespace Coms.Desktop.Screens
{
    internal sealed class InvoiceListScreen : ListScreen<InvoiceSummary>
    {
        private readonly Scoped _scoped;
        private readonly TextBox _search;
        private readonly ComboBox _status;
        private readonly CheckBox _overdue;

        public InvoiceListScreen(Scoped scoped)
            : base("Invoices", canCreate: false)
        {
            _scoped = scoped;
            _search = AddFilter("Search", Ui.MakeTextBox(180));
            _status = AddFilter("Status", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 });
            _status.Items.Add("All");
            foreach (InvoiceStatus status in Enum.GetValues(typeof(InvoiceStatus)))
            {
                _status.Items.Add(status);
            }

            _status.SelectedIndex = 0;
            _overdue = AddFilter(string.Empty, new CheckBox { Text = "Overdue only", AutoSize = true, Margin = new Padding(3, 6, 9, 3) });
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
            AddFilterButtons();
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "Invoice", nameof(InvoiceSummary.InvoiceNumber), 120);
            GridHelper.AddColumn(grid, "Order", nameof(InvoiceSummary.OrderNumber), 120);
            GridHelper.AddColumn(grid, "Customer", nameof(InvoiceSummary.CustomerName), 220);
            GridHelper.AddColumn(grid, "Issued", nameof(InvoiceSummary.IssuedDate), 85, format: "yyyy-MM-dd");
            GridHelper.AddColumn(grid, "Due", nameof(InvoiceSummary.DueDate), 85, format: "yyyy-MM-dd");
            GridHelper.AddColumn(grid, "Status", nameof(InvoiceSummary.Status), 95);
            GridHelper.AddColumn(grid, "Total", nameof(InvoiceSummary.Total), 100, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Paid", nameof(InvoiceSummary.AmountPaid), 100, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Balance", nameof(InvoiceSummary.Balance), 100, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Overdue", nameof(InvoiceSummary.DaysOverdue), 65, alignRight: true);
        }

        protected override Task<PagedResult<InvoiceSummary>> LoadPageAsync(PagedRequest paging)
        {
            var filter = new InvoiceFilter
            {
                Search = _search.Text,
                Status = _status.SelectedItem is InvoiceStatus status ? status : (InvoiceStatus?)null,
                OverdueOnly = _overdue.Checked
            };
            return _scoped.RunAsync<IInvoiceService, PagedResult<InvoiceSummary>>(s => s.SearchAsync(filter, paging));
        }

        protected override void ClearFilters()
        {
            _search.Text = string.Empty;
            _status.SelectedIndex = 0;
            _overdue.Checked = false;
        }

        protected override async void Open(InvoiceSummary item)
        {
            try
            {
                Invoice invoice = await _scoped.RunAsync<IInvoiceService, Invoice>(s => s.GetAsync(item.InvoiceId));
                if (invoice == null)
                {
                    await ReloadAsync();
                    return;
                }

                using (var form = new InvoiceForm(_scoped, invoice))
                {
                    form.ShowDialog(this);
                }

                await ReloadAsync();
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }
    }
}
