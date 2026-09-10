using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Customers;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Customers;

namespace Coms.Desktop.Forms
{
    /// <summary>Create or edit a customer. Labels left, controls right, OK/Cancel bottom right.</summary>
    internal sealed class CustomerForm : Form
    {
        private readonly Scoped _scoped;
        private Customer _customer;

        private readonly TextBox _number = Ui.MakeTextBox(120);
        private readonly TextBox _name = Ui.MakeTextBox(320, 200);
        private readonly TextBox _contact = Ui.MakeTextBox(320, 100);
        private readonly TextBox _email = Ui.MakeTextBox(320, 254);
        private readonly TextBox _phone = Ui.MakeTextBox(160, 30);
        private readonly NumericUpDown _terms = new NumericUpDown { Minimum = 0, Maximum = 365, Width = 80 };
        private readonly TextBox _creditLimit = Ui.MakeTextBox(120);
        private readonly ComboBox _status = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        private readonly TextBox _notes = new TextBox { Multiline = true, Height = 60, Width = 320, ScrollBars = ScrollBars.Vertical };
        private readonly AddressFields _billing = new AddressFields();
        private readonly AddressFields _shipping = new AddressFields();

        public CustomerForm(Scoped scoped, Customer customer)
        {
            _scoped = scoped;
            _customer = customer ?? new Customer();

            Text = _customer.IsNew ? "New customer" : "Customer " + _customer.CustomerNumber;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 560);

            _status.Items.AddRange(new object[] { CustomerStatus.Active, CustomerStatus.OnHold, CustomerStatus.Inactive });
            _number.ReadOnly = true;

            var left = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            AddRow(left, "Number", _number);
            AddRow(left, "Name *", _name);
            AddRow(left, "Contact", _contact);
            AddRow(left, "Email", _email);
            AddRow(left, "Phone", _phone);
            AddRow(left, "Terms (days) *", _terms);
            AddRow(left, "Credit limit", _creditLimit);
            AddRow(left, "Status *", _status);
            AddRow(left, "Notes", _notes);

            var right = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
            var billingGroup = new GroupBox { Text = "Billing address", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(6) };
            billingGroup.Controls.Add(_billing.Panel);
            var shippingGroup = new GroupBox { Text = "Shipping address (blank = same as billing)", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(6) };
            shippingGroup.Controls.Add(_shipping.Panel);
            right.Controls.Add(billingGroup);
            right.Controls.Add(shippingGroup);

            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            columns.Controls.Add(left, 0, 0);
            columns.Controls.Add(right, 1, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("Save", async (s, e) => await SaveAsync());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(columns);
            Controls.Add(buttons);

            Load += (s, e) => Populate();
        }

        /// <summary>The saved customer, when the dialog returned OK.</summary>
        public Customer Result => _customer;

        private void Populate()
        {
            _number.Text = _customer.IsNew ? "(assigned on save)" : _customer.CustomerNumber;
            _name.Text = _customer.Name;
            _contact.Text = _customer.ContactName;
            _email.Text = _customer.Email;
            _phone.Text = _customer.Phone;
            _terms.Value = Math.Max(0, Math.Min(365, _customer.PaymentTermsDays));
            _creditLimit.Text = _customer.CreditLimit.HasValue ? _customer.CreditLimit.Value.ToString("0.00") : string.Empty;
            _status.SelectedItem = _customer.Status;
            _notes.Text = _customer.Notes;
            _billing.Set(_customer.BillingAddress);
            _shipping.Set(_customer.ShippingAddress);
            _name.Focus();
        }

        private bool Collect()
        {
            _customer.Name = _name.Text;
            _customer.ContactName = _contact.Text;
            _customer.Email = _email.Text;
            _customer.Phone = _phone.Text;
            _customer.PaymentTermsDays = (int)_terms.Value;
            _customer.Status = (CustomerStatus)_status.SelectedItem;
            _customer.Notes = _notes.Text;
            _customer.BillingAddress = _billing.Get();
            _customer.ShippingAddress = _shipping.Get();

            if (string.IsNullOrWhiteSpace(_creditLimit.Text))
            {
                _customer.CreditLimit = null;
            }
            else
            {
                decimal limit;
                if (!decimal.TryParse(_creditLimit.Text, out limit))
                {
                    Ui.Warn(this, "Credit limit must be a number or blank.");
                    _creditLimit.Focus();
                    return false;
                }

                _customer.CreditLimit = limit;
            }

            return true;
        }

        private async Task SaveAsync()
        {
            if (!Collect())
            {
                return;
            }

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Customer> result = _customer.IsNew
                        ? await _scoped.RunAsync<ICustomerService, Result<Customer>>(s => s.CreateAsync(_customer))
                        : await _scoped.RunAsync<ICustomerService, Result<Customer>>(s => s.UpdateAsync(_customer));

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            Customer fresh = await _scoped.RunAsync<ICustomerService, Customer>(s => s.GetAsync(_customer.Id));
                            if (fresh != null)
                            {
                                _customer = fresh;
                                Populate();
                            }
                        }

                        return;
                    }

                    _customer = result.Value;
                    DialogResult = DialogResult.OK;
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Ui.MakeLabel(label), 0, row);
            control.Margin = new Padding(3);
            table.Controls.Add(control, 1, row);
        }

        /// <summary>Six address boxes laid out in a small table; shared by billing and shipping.</summary>
        private sealed class AddressFields
        {
            private readonly TextBox _line1 = Ui.MakeTextBox(220, 100);
            private readonly TextBox _line2 = Ui.MakeTextBox(220, 100);
            private readonly TextBox _city = Ui.MakeTextBox(160, 80);
            private readonly TextBox _region = Ui.MakeTextBox(100, 80);
            private readonly TextBox _postalCode = Ui.MakeTextBox(100, 20);
            private readonly TextBox _country = Ui.MakeTextBox(160, 60);

            public AddressFields()
            {
                Panel = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
                Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
                AddRow(Panel, "Line 1", _line1);
                AddRow(Panel, "Line 2", _line2);
                AddRow(Panel, "City", _city);
                AddRow(Panel, "Region", _region);
                AddRow(Panel, "Postal code", _postalCode);
                AddRow(Panel, "Country", _country);
            }

            public TableLayoutPanel Panel { get; }

            public void Set(Address address)
            {
                _line1.Text = address?.Line1;
                _line2.Text = address?.Line2;
                _city.Text = address?.City;
                _region.Text = address?.Region;
                _postalCode.Text = address?.PostalCode;
                _country.Text = address?.Country;
            }

            public Address Get()
            {
                return new Address
                {
                    Line1 = _line1.Text,
                    Line2 = _line2.Text,
                    City = _city.Text,
                    Region = _region.Text,
                    PostalCode = _postalCode.Text,
                    Country = _country.Text
                };
            }
        }
    }
}
