using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Customers;
using Coms.Desktop.Forms;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Customers;

namespace Coms.Desktop.Screens
{
    internal sealed class CustomerListScreen : ListScreen<Customer>
    {
        private readonly Scoped _scoped;
        private readonly TextBox _search;
        private readonly ComboBox _status;

        public CustomerListScreen(Scoped scoped)
            : base("Customers", canCreate: true)
        {
            _scoped = scoped;
            _search = AddFilter("Search", Ui.MakeTextBox(200));
            _status = AddFilter("Status", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 });
            _status.Items.AddRange(new object[] { "All", CustomerStatus.Active, CustomerStatus.OnHold, CustomerStatus.Inactive });
            _status.SelectedIndex = 0;
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
            AddFilterButtons();
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "Number", nameof(Customer.CustomerNumber), 100);
            GridHelper.AddColumn(grid, "Name", nameof(Customer.Name), 220);
            GridHelper.AddColumn(grid, "Contact", nameof(Customer.ContactName), 140);
            GridHelper.AddColumn(grid, "Email", nameof(Customer.Email), 220);
            GridHelper.AddColumn(grid, "Phone", nameof(Customer.Phone), 110);
            GridHelper.AddColumn(grid, "Terms", nameof(Customer.PaymentTermsDays), 55, alignRight: true);
            GridHelper.AddColumn(grid, "Credit limit", nameof(Customer.CreditLimit), 95, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Status", nameof(Customer.Status), 80);
            GridHelper.AddColumn(grid, "Updated", nameof(Customer.UpdatedAtUtc), 110, format: "yyyy-MM-dd HH:mm");
        }

        protected override Task<PagedResult<Customer>> LoadPageAsync(PagedRequest paging)
        {
            var filter = new CustomerFilter
            {
                Search = _search.Text,
                Status = _status.SelectedItem is CustomerStatus status ? status : (CustomerStatus?)null
            };
            paging.SortBy = "name";
            return _scoped.RunAsync<ICustomerService, PagedResult<Customer>>(s => s.SearchAsync(filter, paging));
        }

        protected override void ClearFilters()
        {
            _search.Text = string.Empty;
            _status.SelectedIndex = 0;
        }

        protected override void CreateNew()
        {
            Edit(new Customer());
        }

        protected override void Open(Customer item)
        {
            Edit(item);
        }

        private async void Edit(Customer customer)
        {
            try
            {
                Customer fresh = customer.IsNew ? customer : await _scoped.RunAsync<ICustomerService, Customer>(s => s.GetAsync(customer.Id));
                if (fresh == null)
                {
                    Ui.Warn(this, "The customer no longer exists.");
                    await ReloadAsync();
                    return;
                }

                using (var form = new CustomerForm(_scoped, fresh))
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        await ReloadAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }
    }
}
