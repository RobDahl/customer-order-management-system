using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Orders;
using Coms.Desktop.Infrastructure;
using Coms.Desktop.Printing;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;

namespace Coms.Desktop.Forms
{
    /// <summary>Invoice detail with payments; record payment, void, print.</summary>
    internal sealed class InvoiceForm : Form
    {
        private readonly Scoped _scoped;
        private Invoice _invoice;
        private Order _order;
        private Customer _customer;

        private readonly Label _summary = new Label { AutoSize = true, Font = new Font("Consolas", 10F), Margin = new Padding(8) };
        private readonly DataGridView _payments = new DataGridView();
        private readonly Button _pay;
        private readonly Button _void;

        public InvoiceForm(Scoped scoped, Invoice invoice)
        {
            _scoped = scoped;
            _invoice = invoice;

            Text = "Invoice " + invoice.InvoiceNumber;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 520);

            GridHelper.Configure(_payments);
            GridHelper.AddColumn(_payments, "Date", nameof(Payment.PaidDate), 90, format: "yyyy-MM-dd");
            GridHelper.AddColumn(_payments, "Method", nameof(Payment.Method), 100);
            GridHelper.AddColumn(_payments, "Reference", nameof(Payment.Reference), 160);
            GridHelper.AddColumn(_payments, "Amount", nameof(Payment.Amount), 100, alignRight: true, format: "N2");
            GridHelper.AddColumn(_payments, "Recorded by", nameof(Payment.CreatedBy), 120);
            GridHelper.AddColumn(_payments, "Notes", nameof(Payment.Notes), 160);

            var group = new GroupBox { Text = "Payments", Dock = DockStyle.Fill, Padding = new Padding(6) };
            group.Controls.Add(_payments);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
            _pay = Ui.MakeButton("Record payment...", async (s, e) => await RecordPaymentAsync(), 120);
            _void = Ui.MakeButton("Void invoice", async (s, e) => await VoidAsync(), 100);
            Button print = Ui.MakeButton("Print invoice", (s, e) => Print(), 100);
            Button close = Ui.MakeButton("Close", (s, e) => Close());
            buttons.Controls.AddRange(new Control[] { _pay, _void, print, close });
            CancelButton = close;

            Controls.Add(group);
            Controls.Add(_summary);
            Controls.Add(buttons);
            _summary.Dock = DockStyle.Top;

            Load += async (s, e) => await PopulateAsync();
        }

        private async Task PopulateAsync()
        {
            try
            {
                _order = await _scoped.RunAsync<IOrderService, Order>(s => s.GetAsync(_invoice.OrderId));
                _customer = await _scoped.RunAsync<ICustomerService, Customer>(s => s.GetAsync(_invoice.CustomerId));
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }

            DateTime today = DateTime.Today;
            string overdue = _invoice.IsOverdue(today) ? "   OVERDUE " + _invoice.DaysOverdue(today) + " days" : string.Empty;

            _summary.Text =
                "Invoice   " + _invoice.InvoiceNumber + "      Status  " + _invoice.Status + overdue + Environment.NewLine +
                "Order     " + (_order?.OrderNumber ?? string.Empty) + "      Customer  " + (_customer == null ? string.Empty : _customer.CustomerNumber + " " + _customer.Name) + Environment.NewLine +
                "Issued    " + _invoice.IssuedDate.ToString("yyyy-MM-dd") + "           Due     " + _invoice.DueDate.ToString("yyyy-MM-dd") + Environment.NewLine +
                "Subtotal  " + Ui.Money(_invoice.Subtotal).PadLeft(14) + "   Tax  " + Ui.Money(_invoice.TaxAmount).PadLeft(12) + "   Total  " + Ui.Money(_invoice.Total).PadLeft(14) + Environment.NewLine +
                "Paid      " + Ui.Money(_invoice.AmountPaid).PadLeft(14) + "                       BALANCE " + Ui.Money(_invoice.Balance).PadLeft(14);

            _payments.DataSource = new List<Payment>(_invoice.Payments);
            _pay.Enabled = _invoice.IsOpen;
            _void.Enabled = _invoice.Status != InvoiceStatus.Void && _invoice.AmountPaid == 0;
        }

        private async Task RecordPaymentAsync()
        {
            using (var form = new PaymentForm(_scoped, _invoice))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _invoice = form.Result;
                }
            }

            await ReloadAsync();
        }

        private async Task VoidAsync()
        {
            string reason = CommentForm.Ask(this, "Void invoice " + _invoice.InvoiceNumber, "The order returns to Fulfilled and can be re-invoiced. Reason:");
            if (reason == null)
            {
                return;
            }

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Invoice> result = await _scoped.RunAsync<IInvoiceService, Result<Invoice>>(s => s.VoidAsync(_invoice.Id, _invoice.RowVersion, reason));
                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                    }
                    else
                    {
                        _invoice = result.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }

            await ReloadAsync();
        }

        private void Print()
        {
            try
            {
                new TextDocumentPrinter("Invoice " + _invoice.InvoiceNumber, Documents.Invoice(_invoice, _order, _customer)).Preview(this);
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private async Task ReloadAsync()
        {
            try
            {
                Invoice fresh = await _scoped.RunAsync<IInvoiceService, Invoice>(s => s.GetAsync(_invoice.Id));
                if (fresh != null)
                {
                    _invoice = fresh;
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }

            await PopulateAsync();
        }
    }

    internal sealed class PaymentForm : Form
    {
        private readonly Scoped _scoped;
        private readonly Invoice _invoice;
        private readonly TextBox _amount = Ui.MakeTextBox(120);
        private readonly DateTimePicker _date = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly ComboBox _method = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        private readonly TextBox _reference = Ui.MakeTextBox(220, 50);
        private readonly TextBox _notes = Ui.MakeTextBox(300, 500);

        public PaymentForm(Scoped scoped, Invoice invoice)
        {
            _scoped = scoped;
            _invoice = invoice;
            Result = invoice;

            Text = "Record payment " + invoice.InvoiceNumber;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 230);

            foreach (PaymentMethod method in Enum.GetValues(typeof(PaymentMethod)))
            {
                _method.Items.Add(method);
            }

            _method.SelectedItem = PaymentMethod.BankTransfer;
            _amount.Text = invoice.Balance.ToString("0.00");
            _date.Value = DateTime.Today;

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            AddRow(table, "Balance", new Label { Text = Ui.Money(invoice.Balance), AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            AddRow(table, "Amount *", _amount);
            AddRow(table, "Paid date *", _date);
            AddRow(table, "Method *", _method);
            AddRow(table, "Reference", _reference);
            AddRow(table, "Notes", _notes);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("Record", async (s, e) => await SaveAsync());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(table);
            Controls.Add(buttons);
            Load += (s, e) => { _amount.Focus(); _amount.SelectAll(); };
        }

        public Invoice Result { get; private set; }

        private async Task SaveAsync()
        {
            decimal amount;
            if (!decimal.TryParse(_amount.Text, out amount))
            {
                Ui.Warn(this, "Amount must be a number.");
                _amount.Focus();
                return;
            }

            var payment = new Payment
            {
                InvoiceId = _invoice.Id,
                Amount = amount,
                PaidDate = _date.Value.Date,
                Method = (PaymentMethod)_method.SelectedItem,
                Reference = _reference.Text,
                Notes = _notes.Text
            };

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Invoice> result = await _scoped.RunAsync<IInvoiceService, Result<Invoice>>(s => s.ApplyPaymentAsync(payment, _invoice.RowVersion));
                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            DialogResult = DialogResult.Cancel;
                        }

                        return;
                    }

                    Result = result.Value;
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
    }
}
