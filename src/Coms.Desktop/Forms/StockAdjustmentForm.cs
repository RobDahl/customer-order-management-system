using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Products;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Desktop.Forms
{
    internal sealed class StockAdjustmentForm : Form
    {
        private readonly Scoped _scoped;
        private readonly Product _product;
        private readonly TextBox _quantity = Ui.MakeTextBox(100);
        private readonly TextBox _reason = Ui.MakeTextBox(300, 200);

        public StockAdjustmentForm(Scoped scoped, Product product)
        {
            _scoped = scoped;
            _product = product;

            Text = "Adjust stock " + product.Sku;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 190);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            AddRow(table, "Product", new Label { Text = product.Sku + "  " + product.Name, AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            AddRow(table, "Current quantity", new Label { Text = Ui.Quantity(product.QuantityOnHand) + " " + product.UnitOfMeasure, AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
            AddRow(table, "Counted quantity *", _quantity);
            AddRow(table, "Reason *", _reason);
            _quantity.Text = Ui.Quantity(product.QuantityOnHand);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("Save", async (s, e) => await SaveAsync());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(table);
            Controls.Add(buttons);
            Load += (s, e) => { _quantity.Focus(); _quantity.SelectAll(); };
        }

        private async Task SaveAsync()
        {
            decimal quantity;
            if (!decimal.TryParse(_quantity.Text, out quantity))
            {
                Ui.Warn(this, "Counted quantity must be a number.");
                _quantity.Focus();
                return;
            }

            try
            {
                using (Ui.Busy(this))
                {
                    Result<Product> result = await _scoped.RunAsync<IProductService, Result<Product>>(
                        s => s.AdjustStockAsync(_product.Id, quantity, _reason.Text, _product.RowVersion));

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            DialogResult = DialogResult.Cancel;
                        }

                        return;
                    }

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
