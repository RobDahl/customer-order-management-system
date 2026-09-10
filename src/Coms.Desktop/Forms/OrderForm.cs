using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Orders;
using Coms.Desktop.Infrastructure;
using Coms.Desktop.Printing;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Coms.Domain.Products;

namespace Coms.Desktop.Forms
{
    /// <summary>
    /// Order entry and workflow. Header fields at the top, an editable lines
    /// grid in the middle with live totals, status history on a second tab,
    /// and the workflow buttons enabled according to the state machine.
    /// </summary>
    internal sealed class OrderForm : Form
    {
        private readonly Scoped _scoped;
        private readonly ComsOptions _options;
        private Order _order;
        private Customer _customer;

        private readonly Label _numberLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Margin = new Padding(3, 6, 3, 3) };
        private readonly Label _statusLabel = new Label { AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
        private readonly TextBox _customerBox = Ui.MakeTextBox(280);
        private readonly Button _customerButton = Ui.MakeButton("...", null, 30);
        private readonly DateTimePicker _orderDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110 };
        private readonly DateTimePicker _requiredDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, ShowCheckBox = true, Checked = false };
        private readonly TextBox _reference = Ui.MakeTextBox(200, 50);
        private readonly TextBox _notes = new TextBox { Multiline = true, Width = 320, Height = 48, ScrollBars = ScrollBars.Vertical };
        private readonly TextBox _shipTo = new TextBox { Multiline = true, Width = 320, Height = 48, ReadOnly = true, BackColor = SystemColors.Control };

        private readonly BindingList<LineRow> _lines = new BindingList<LineRow>();
        private readonly DataGridView _linesGrid = new DataGridView();
        private readonly DataGridView _historyGrid = new DataGridView();
        private readonly Label _totalsLabel = new Label { AutoSize = true, Font = new Font("Consolas", 10F), Margin = new Padding(6) };

        private readonly Button _addLine;
        private readonly Button _removeLine;
        private readonly Button _save;
        private readonly Button _submit;
        private readonly Button _approve;
        private readonly Button _fulfil;
        private readonly Button _cancelOrder;
        private readonly Button _invoice;
        private readonly Button _printConfirmation;
        private readonly Button _printPickList;

        public OrderForm(Scoped scoped, ComsOptions options, Order order)
        {
            _scoped = scoped;
            _options = options;
            _order = order ?? new Order { TaxRate = options.TaxRate };

            Text = _order.IsNew ? "New order" : "Order " + _order.OrderNumber;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(900, 600);

            /* header */
            var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(8) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            var customerPanel = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0) };
            customerPanel.Controls.Add(_customerBox);
            customerPanel.Controls.Add(_customerButton);
            _customerBox.ReadOnly = true;
            _customerBox.BackColor = SystemColors.Control;
            _customerButton.Click += async (s, e) => await PickCustomerAsync();

            AddRow(header, 0, "Order", _numberLabel, "Status", _statusLabel);
            AddRow(header, 1, "Customer *", customerPanel, "Order date *", _orderDate);
            AddRow(header, 2, "Reference", _reference, "Required", _requiredDate);
            AddRow(header, 3, "Notes", _notes, "Ship to", _shipTo);

            /* lines and history */
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var linesTab = new TabPage("Lines");
            var historyTab = new TabPage("Status history");
            tabs.TabPages.Add(linesTab);
            tabs.TabPages.Add(historyTab);

            GridHelper.Configure(_linesGrid, readOnly: false);
            GridHelper.AddColumn(_linesGrid, "SKU", nameof(LineRow.Sku), 110);
            GridHelper.AddColumn(_linesGrid, "Description", nameof(LineRow.Description), 330, readOnly: false);
            GridHelper.AddColumn(_linesGrid, "Quantity", nameof(LineRow.Quantity), 90, alignRight: true, format: "0.###", readOnly: false);
            GridHelper.AddColumn(_linesGrid, "Unit price", nameof(LineRow.UnitPrice), 100, alignRight: true, format: "N2", readOnly: false);
            GridHelper.AddColumn(_linesGrid, "Disc %", nameof(LineRow.DiscountPercent), 70, alignRight: true, format: "0.##", readOnly: false);
            GridHelper.AddColumn(_linesGrid, "Line total", nameof(LineRow.LineTotal), 110, alignRight: true, format: "N2");
            _linesGrid.DataSource = _lines;
            _linesGrid.CellEndEdit += (s, e) => Recalculate();
            _linesGrid.DataError += (s, e) => { e.ThrowException = false; Ui.Warn(this, "Enter a number."); };

            var lineButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(3) };
            _addLine = Ui.MakeButton("Add line...", async (s, e) => await AddLineAsync(), 90);
            _removeLine = Ui.MakeButton("Remove line", (s, e) => RemoveLine(), 90);
            lineButtons.Controls.Add(_addLine);
            lineButtons.Controls.Add(_removeLine);
            lineButtons.Controls.Add(_totalsLabel);

            linesTab.Controls.Add(_linesGrid);
            linesTab.Controls.Add(lineButtons);

            GridHelper.Configure(_historyGrid);
            GridHelper.AddColumn(_historyGrid, "When (UTC)", nameof(OrderStatusChange.ChangedAtUtc), 130, format: "yyyy-MM-dd HH:mm");
            GridHelper.AddColumn(_historyGrid, "From", nameof(OrderStatusChange.FromStatus), 90);
            GridHelper.AddColumn(_historyGrid, "To", nameof(OrderStatusChange.ToStatus), 90);
            GridHelper.AddColumn(_historyGrid, "By", nameof(OrderStatusChange.ChangedBy), 120);
            GridHelper.AddColumn(_historyGrid, "Comment", nameof(OrderStatusChange.Comment), 400);
            historyTab.Controls.Add(_historyGrid);

            /* action buttons */
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Padding = new Padding(8) };
            _save = Ui.MakeButton("Save", async (s, e) => await SaveAsync());
            _submit = Ui.MakeButton("Submit", async (s, e) => await TransitionAsync(OrderStatus.Submitted));
            _approve = Ui.MakeButton("Approve", async (s, e) => await TransitionAsync(OrderStatus.Approved));
            _fulfil = Ui.MakeButton("Fulfil", async (s, e) => await TransitionAsync(OrderStatus.Fulfilled));
            _cancelOrder = Ui.MakeButton("Cancel order", async (s, e) => await TransitionAsync(OrderStatus.Cancelled), 100);
            _invoice = Ui.MakeButton("Create invoice", async (s, e) => await CreateInvoiceAsync(), 110);
            _printConfirmation = Ui.MakeButton("Print confirmation", (s, e) => Print(false), 130);
            _printPickList = Ui.MakeButton("Print pick list", (s, e) => Print(true), 110);
            Button close = Ui.MakeButton("Close", (s, e) => Close());
            buttons.Controls.AddRange(new Control[] { _save, _submit, _approve, _fulfil, _cancelOrder, _invoice, _printConfirmation, _printPickList, close });
            CancelButton = close;

            Controls.Add(tabs);
            Controls.Add(header);
            Controls.Add(buttons);

            Load += async (s, e) => await PopulateAsync();
        }

        private async Task PopulateAsync()
        {
            try
            {
                _customer = _order.CustomerId > 0
                    ? await _scoped.RunAsync<ICustomerService, Customer>(s => s.GetAsync(_order.CustomerId))
                    : null;
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }

            Text = _order.IsNew ? "New order" : "Order " + _order.OrderNumber;
            _numberLabel.Text = _order.IsNew ? "(assigned on save)" : _order.OrderNumber;
            _statusLabel.Text = _order.IsNew ? "Draft (unsaved)" : _order.Status.ToString();
            _customerBox.Text = _customer == null ? string.Empty : _customer.CustomerNumber + "  " + _customer.Name;
            _orderDate.Value = _order.OrderDate == default(DateTime) ? DateTime.Today : _order.OrderDate;
            _requiredDate.Checked = _order.RequiredDate.HasValue;
            if (_order.RequiredDate.HasValue) { _requiredDate.Value = _order.RequiredDate.Value; }
            _reference.Text = _order.CustomerReference;
            _notes.Text = _order.Notes;
            _shipTo.Text = _order.ShipTo?.ToString() ?? _customer?.EffectiveShippingAddress.ToString() ?? string.Empty;

            _lines.Clear();
            foreach (OrderLine line in _order.Lines)
            {
                _lines.Add(LineRow.From(line));
            }

            _historyGrid.DataSource = new List<OrderStatusChange>(_order.History);

            Recalculate();
            ApplyStatus();
        }

        private void ApplyStatus()
        {
            bool editable = _order.IsNew || _order.CanEdit;

            _customerButton.Enabled = editable;
            _orderDate.Enabled = editable;
            _requiredDate.Enabled = editable;
            _reference.ReadOnly = !editable;
            _notes.ReadOnly = !editable;
            _linesGrid.ReadOnly = !editable;
            _addLine.Enabled = editable;
            _removeLine.Enabled = editable;
            _save.Enabled = editable;

            _submit.Enabled = !_order.IsNew && _order.CanTransitionTo(OrderStatus.Submitted);
            _approve.Enabled = !_order.IsNew && _order.CanTransitionTo(OrderStatus.Approved);
            _fulfil.Enabled = !_order.IsNew && _order.CanTransitionTo(OrderStatus.Fulfilled);
            _cancelOrder.Enabled = !_order.IsNew && _order.CanTransitionTo(OrderStatus.Cancelled);
            _invoice.Enabled = !_order.IsNew && _order.Status == OrderStatus.Fulfilled;
            _printConfirmation.Enabled = !_order.IsNew;
            _printPickList.Enabled = !_order.IsNew;
        }

        private void Recalculate()
        {
            // Same arithmetic as the server: build a domain order from the rows.
            var order = new Order { TaxRate = _order.TaxRate };
            foreach (LineRow row in _lines)
            {
                order.Lines.Add(new OrderLine { Quantity = row.Quantity, UnitPrice = row.UnitPrice, DiscountPercent = row.DiscountPercent });
            }

            order.Recalculate();
            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].LineTotal = order.Lines[i].LineTotal;
            }

            _linesGrid.Refresh();
            _totalsLabel.Text = "Subtotal " + Ui.Money(order.Subtotal) + "    Tax " + Ui.Money(order.TaxAmount) + "    TOTAL " + Ui.Money(order.Total);
        }

        private async Task PickCustomerAsync()
        {
            using (var lookup = new CustomerLookupForm(_scoped))
            {
                if (lookup.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _customer = lookup.Selected;
                _customerBox.Text = _customer.CustomerNumber + "  " + _customer.Name;
                _shipTo.Text = _customer.EffectiveShippingAddress.ToString();
            }

            await Task.CompletedTask;
        }

        private async Task AddLineAsync()
        {
            using (var lookup = new ProductLookupForm(_scoped))
            {
                if (lookup.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ProductLookup product = lookup.Selected;
                _lines.Add(new LineRow
                {
                    ProductId = product.Id,
                    Sku = product.Sku,
                    Description = product.Name,
                    Quantity = 1,
                    UnitPrice = product.UnitPrice,
                    DiscountPercent = 0
                });
                Recalculate();
                _linesGrid.CurrentCell = _linesGrid.Rows[_linesGrid.Rows.Count - 1].Cells[nameof(LineRow.Quantity)];
            }

            await Task.CompletedTask;
        }

        private void RemoveLine()
        {
            LineRow row = GridHelper.SelectedItem<LineRow>(_linesGrid);
            if (row != null)
            {
                _lines.Remove(row);
                Recalculate();
            }
        }

        private OrderInput CollectInput()
        {
            var input = new OrderInput
            {
                CustomerId = _customer?.Id ?? 0,
                OrderDate = _orderDate.Value.Date,
                RequiredDate = _requiredDate.Checked ? _requiredDate.Value.Date : (DateTime?)null,
                CustomerReference = _reference.Text,
                Notes = _notes.Text
            };

            foreach (LineRow row in _lines)
            {
                input.Lines.Add(new OrderLineInput
                {
                    ProductId = row.ProductId,
                    Quantity = row.Quantity,
                    UnitPrice = row.UnitPrice,
                    DiscountPercent = row.DiscountPercent,
                    Description = row.Description
                });
            }

            return input;
        }

        private async Task SaveAsync()
        {
            _linesGrid.EndEdit();
            OrderInput input = CollectInput();

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Order> result = _order.IsNew
                        ? await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.CreateDraftAsync(input))
                        : await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.UpdateAsync(_order.Id, _order.RowVersion, input));

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            await ReloadAsync();
                        }

                        return;
                    }

                    _order = result.Value;
                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private async Task TransitionAsync(OrderStatus target)
        {
            string verb = target == OrderStatus.Submitted ? "Submit" : target == OrderStatus.Approved ? "Approve" : target == OrderStatus.Fulfilled ? "Fulfil" : "Cancel";
            string prompt = target == OrderStatus.Fulfilled
                ? "Fulfilling deducts each line's quantity from stock. Comment (optional):"
                : target == OrderStatus.Cancelled
                    ? "A cancelled order cannot be reopened. Reason:"
                    : "Comment (optional):";

            string comment = CommentForm.Ask(this, verb + " order " + _order.OrderNumber, prompt);
            if (comment == null)
            {
                return;
            }

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Order> result;
                    switch (target)
                    {
                        case OrderStatus.Submitted:
                            result = await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.SubmitAsync(_order.Id, _order.RowVersion, comment));
                            break;
                        case OrderStatus.Approved:
                            result = await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.ApproveAsync(_order.Id, _order.RowVersion, comment));
                            break;
                        case OrderStatus.Fulfilled:
                            result = await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.FulfilAsync(_order.Id, _order.RowVersion, comment));
                            break;
                        default:
                            result = await _scoped.RunAsync<IOrderService, Result<Order>>(s => s.CancelAsync(_order.Id, _order.RowVersion, comment));
                            break;
                    }

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            await ReloadAsync();
                        }

                        return;
                    }

                    _order = result.Value;
                    await PopulateAsync();
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private async Task CreateInvoiceAsync()
        {
            if (!Ui.Confirm(this, "Issue an invoice for order " + _order.OrderNumber + " dated today?"))
            {
                return;
            }

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Invoice> result = await _scoped.RunAsync<IInvoiceService, Result<Invoice>>(
                        s => s.CreateFromOrderAsync(_order.Id, _order.RowVersion, DateTime.Today));

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        await ReloadAsync();
                        return;
                    }

                    Ui.Info(this, "Invoice " + result.Value.InvoiceNumber + " issued.");
                    await ReloadAsync();

                    using (var form = new InvoiceForm(_scoped, result.Value))
                    {
                        form.ShowDialog(this);
                    }

                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private void Print(bool pickList)
        {
            try
            {
                List<string> lines = pickList ? Documents.PickList(_order, _customer) : Documents.OrderConfirmation(_order, _customer);
                new TextDocumentPrinter((pickList ? "Pick list " : "Order confirmation ") + _order.OrderNumber, lines).Preview(this);
            }
            catch (Exception ex)
            {
                Ui.Error(this, ex);
            }
        }

        private async Task ReloadAsync()
        {
            if (_order.IsNew)
            {
                return;
            }

            Order fresh = await _scoped.RunAsync<IOrderService, Order>(s => s.GetAsync(_order.Id));
            if (fresh != null)
            {
                _order = fresh;
            }

            await PopulateAsync();
        }

        private static void AddRow(TableLayoutPanel table, int row, string label1, Control control1, string label2, Control control2)
        {
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Ui.MakeLabel(label1), 0, row);
            control1.Margin = new Padding(3);
            table.Controls.Add(control1, 1, row);
            table.Controls.Add(Ui.MakeLabel(label2), 2, row);
            control2.Margin = new Padding(3);
            table.Controls.Add(control2, 3, row);
        }

        /// <summary>One editable grid row. Plain properties so the grid can bind and edit them.</summary>
        public sealed class LineRow
        {
            public int ProductId { get; set; }
            public string Sku { get; set; }
            public string Description { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPercent { get; set; }
            public decimal LineTotal { get; set; }

            public static LineRow From(OrderLine line)
            {
                return new LineRow
                {
                    ProductId = line.ProductId,
                    Sku = line.Sku,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountPercent = line.DiscountPercent,
                    LineTotal = line.LineTotal
                };
            }
        }
    }
}
