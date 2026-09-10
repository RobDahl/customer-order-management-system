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
    internal sealed class ProductForm : Form
    {
        private readonly Scoped _scoped;
        private Product _product;

        private readonly TextBox _sku = Ui.MakeTextBox(160, 40);
        private readonly TextBox _name = Ui.MakeTextBox(320, 200);
        private readonly TextBox _description = new TextBox { Multiline = true, Height = 60, Width = 320, ScrollBars = ScrollBars.Vertical, MaxLength = 1000 };
        private readonly ComboBox _category = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 200, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
        private readonly TextBox _unit = Ui.MakeTextBox(60, 10);
        private readonly TextBox _unitPrice = Ui.MakeTextBox(100);
        private readonly TextBox _costPrice = Ui.MakeTextBox(100);
        private readonly TextBox _quantity = Ui.MakeTextBox(100);
        private readonly TextBox _reorder = Ui.MakeTextBox(100);
        private readonly CheckBox _active = new CheckBox { Text = "Active", AutoSize = true };

        public ProductForm(Scoped scoped, Product product, string[] categories)
        {
            _scoped = scoped;
            _product = product ?? new Product();

            Text = _product.IsNew ? "New product" : "Product " + _product.Sku;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 420);

            _category.Items.AddRange(categories);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            AddRow(table, "SKU *", _sku);
            AddRow(table, "Name *", _name);
            AddRow(table, "Description", _description);
            AddRow(table, "Category *", _category);
            AddRow(table, "Unit of measure *", _unit);
            AddRow(table, "Unit price *", _unitPrice);
            AddRow(table, "Cost price", _costPrice);
            AddRow(table, _product.IsNew ? "Opening quantity" : "Quantity on hand", _quantity);
            AddRow(table, "Reorder level", _reorder);
            AddRow(table, string.Empty, _active);

            if (!_product.IsNew)
            {
                _quantity.ReadOnly = true;
                _quantity.BackColor = SystemColors.Control;
            }

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(8) };
            Button cancel = Ui.MakeButton("Cancel", (s, e) => DialogResult = DialogResult.Cancel);
            Button ok = Ui.MakeButton("Save", async (s, e) => await SaveAsync());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(table);
            Controls.Add(buttons);
            Load += (s, e) => Populate();
        }

        public Product Result => _product;

        private void Populate()
        {
            _sku.Text = _product.Sku;
            _name.Text = _product.Name;
            _description.Text = _product.Description;
            _category.Text = _product.Category;
            _unit.Text = _product.UnitOfMeasure;
            _unitPrice.Text = _product.UnitPrice.ToString("0.00");
            _costPrice.Text = _product.CostPrice.ToString("0.00");
            _quantity.Text = _product.QuantityOnHand.ToString("0.###");
            _reorder.Text = _product.ReorderLevel.ToString("0.###");
            _active.Checked = _product.IsActive;
            _sku.Focus();
        }

        private bool Collect()
        {
            _product.Sku = _sku.Text;
            _product.Name = _name.Text;
            _product.Description = _description.Text;
            _product.Category = _category.Text;
            _product.UnitOfMeasure = _unit.Text;
            _product.IsActive = _active.Checked;

            decimal value;
            if (!decimal.TryParse(_unitPrice.Text, out value)) { Ui.Warn(this, "Unit price must be a number."); _unitPrice.Focus(); return false; }
            _product.UnitPrice = value;
            if (!decimal.TryParse(_costPrice.Text, out value)) { Ui.Warn(this, "Cost price must be a number."); _costPrice.Focus(); return false; }
            _product.CostPrice = value;
            if (_product.IsNew)
            {
                if (!decimal.TryParse(_quantity.Text, out value)) { Ui.Warn(this, "Opening quantity must be a number."); _quantity.Focus(); return false; }
                _product.QuantityOnHand = value;
            }

            if (!decimal.TryParse(_reorder.Text, out value)) { Ui.Warn(this, "Reorder level must be a number."); _reorder.Focus(); return false; }
            _product.ReorderLevel = value;
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
                    Result<Product> result = _product.IsNew
                        ? await _scoped.RunAsync<IProductService, Result<Product>>(s => s.CreateAsync(_product))
                        : await _scoped.RunAsync<IProductService, Result<Product>>(s => s.UpdateAsync(_product));

                    if (result.IsFailure)
                    {
                        Ui.ShowFailure(this, result);
                        if (result.Code == ErrorCode.Conflict)
                        {
                            Product fresh = await _scoped.RunAsync<IProductService, Product>(s => s.GetAsync(_product.Id));
                            if (fresh != null)
                            {
                                _product = fresh;
                                Populate();
                            }
                        }

                        return;
                    }

                    _product = result.Value;
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
