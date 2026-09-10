using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application;
using Coms.Application.Orders;
using Coms.Desktop.Forms;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Orders;

namespace Coms.Desktop.Screens
{
    internal sealed class OrderListScreen : ListScreen<OrderSummary>
    {
        private readonly Scoped _scoped;
        private readonly ComsOptions _options;
        private readonly TextBox _search;
        private readonly ComboBox _status;
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;

        public OrderListScreen(Scoped scoped, ComsOptions options)
            : base("Orders", canCreate: true)
        {
            _scoped = scoped;
            _options = options;
            _search = AddFilter("Search", Ui.MakeTextBox(180));
            _status = AddFilter("Status", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            _status.Items.Add("All");
            foreach (OrderStatus status in Enum.GetValues(typeof(OrderStatus)))
            {
                _status.Items.Add(status);
            }

            _status.SelectedIndex = 0;
            _from = AddFilter("From", new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 110 });
            _to = AddFilter("To", new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 110 });
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
            AddFilterButtons();
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "Order", nameof(OrderSummary.OrderNumber), 120);
            GridHelper.AddColumn(grid, "Date", nameof(OrderSummary.OrderDate), 85, format: "yyyy-MM-dd");
            GridHelper.AddColumn(grid, "Required", nameof(OrderSummary.RequiredDate), 85, format: "yyyy-MM-dd");
            GridHelper.AddColumn(grid, "Customer", nameof(OrderSummary.CustomerName), 220);
            GridHelper.AddColumn(grid, "Reference", nameof(OrderSummary.CustomerReference), 100);
            GridHelper.AddColumn(grid, "Status", nameof(OrderSummary.Status), 85);
            GridHelper.AddColumn(grid, "Lines", nameof(OrderSummary.LineCount), 50, alignRight: true);
            GridHelper.AddColumn(grid, "Total", nameof(OrderSummary.Total), 100, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Invoice", nameof(OrderSummary.InvoiceNumber), 120);
            GridHelper.AddColumn(grid, "Updated", nameof(OrderSummary.UpdatedAtUtc), 110, format: "yyyy-MM-dd HH:mm");
        }

        protected override Task<PagedResult<OrderSummary>> LoadPageAsync(PagedRequest paging)
        {
            var filter = new OrderFilter
            {
                Search = _search.Text,
                Status = _status.SelectedItem is OrderStatus status ? status : (OrderStatus?)null,
                FromDate = _from.Checked ? _from.Value.Date : (DateTime?)null,
                ToDate = _to.Checked ? _to.Value.Date : (DateTime?)null
            };
            return _scoped.RunAsync<IOrderService, PagedResult<OrderSummary>>(s => s.SearchAsync(filter, paging));
        }

        protected override void ClearFilters()
        {
            _search.Text = string.Empty;
            _status.SelectedIndex = 0;
            _from.Checked = false;
            _to.Checked = false;
        }

        protected override void CreateNew()
        {
            CreateNewOrder();
        }

        protected override void Open(OrderSummary item)
        {
            OpenOrder(item.OrderId);
        }

        public async void CreateNewOrder()
        {
            try
            {
                using (var form = new OrderForm(_scoped, _options, null))
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

        private async void OpenOrder(int orderId)
        {
            try
            {
                Order order = await _scoped.RunAsync<IOrderService, Order>(s => s.GetAsync(orderId));
                if (order == null)
                {
                    Ui.Warn(this, "The order no longer exists.");
                    await ReloadAsync();
                    return;
                }

                using (var form = new OrderForm(_scoped, _options, order))
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
