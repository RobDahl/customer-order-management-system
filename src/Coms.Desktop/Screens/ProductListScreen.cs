using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Coms.Application.Products;
using Coms.Desktop.Forms;
using Coms.Desktop.Infrastructure;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Desktop.Screens
{
    internal sealed class ProductListScreen : ListScreen<Product>
    {
        private readonly Scoped _scoped;
        private readonly TextBox _search;
        private readonly ComboBox _category;
        private readonly ComboBox _active;
        private readonly CheckBox _lowStock;
        private bool _categoriesLoaded;

        public ProductListScreen(Scoped scoped)
            : base("Products", canCreate: true)
        {
            _scoped = scoped;
            _search = AddFilter("Search", Ui.MakeTextBox(180));
            _category = AddFilter("Category", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 });
            _active = AddFilter("Show", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 });
            _active.Items.AddRange(new object[] { "Active", "Inactive", "All" });
            _active.SelectedIndex = 0;
            _lowStock = AddFilter(string.Empty, new CheckBox { Text = "Low stock only", AutoSize = true, Margin = new System.Windows.Forms.Padding(3, 6, 9, 3) });
            _search.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
            AddFilterButtons();

            var adjust = Ui.MakeButton("Adjust stock", (s, e) => AdjustStock(), 100);
            Controls[0].Controls[2].Controls.Add(adjust);
        }

        protected override void DefineColumns(DataGridView grid)
        {
            GridHelper.AddColumn(grid, "SKU", nameof(Product.Sku), 100);
            GridHelper.AddColumn(grid, "Name", nameof(Product.Name), 240);
            GridHelper.AddColumn(grid, "Category", nameof(Product.Category), 110);
            GridHelper.AddColumn(grid, "Unit", nameof(Product.UnitOfMeasure), 50);
            GridHelper.AddColumn(grid, "Price", nameof(Product.UnitPrice), 90, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "Cost", nameof(Product.CostPrice), 90, alignRight: true, format: "N2");
            GridHelper.AddColumn(grid, "On hand", nameof(Product.QuantityOnHand), 80, alignRight: true, format: "0.###");
            GridHelper.AddColumn(grid, "Reorder", nameof(Product.ReorderLevel), 80, alignRight: true, format: "0.###");
            GridHelper.AddCheckColumn(grid, "Active", nameof(Product.IsActive), 55);
            GridHelper.AddCheckColumn(grid, "Low", nameof(Product.IsLowStock), 45);
        }

        protected override async Task<PagedResult<Product>> LoadPageAsync(PagedRequest paging)
        {
            if (!_categoriesLoaded)
            {
                IReadOnlyList<string> categories = await _scoped.RunAsync<IProductService, IReadOnlyList<string>>(s => s.GetCategoriesAsync());
                _category.Items.Clear();
                _category.Items.Add("All");
                _category.Items.AddRange(categories.Cast<object>().ToArray());
                _category.SelectedIndex = 0;
                _categoriesLoaded = true;
            }

            var filter = new ProductFilter
            {
                Search = _search.Text,
                Category = _category.SelectedIndex > 0 ? (string)_category.SelectedItem : null,
                IsActive = _active.SelectedIndex == 0 ? true : _active.SelectedIndex == 1 ? false : (bool?)null,
                LowStockOnly = _lowStock.Checked
            };
            paging.SortBy = "sku";
            return await _scoped.RunAsync<IProductService, PagedResult<Product>>(s => s.SearchAsync(filter, paging));
        }

        protected override void ClearFilters()
        {
            _search.Text = string.Empty;
            _category.SelectedIndex = 0;
            _active.SelectedIndex = 0;
            _lowStock.Checked = false;
        }

        protected override void CreateNew()
        {
            Edit(new Product());
        }

        protected override void Open(Product item)
        {
            Edit(item);
        }

        private async void Edit(Product product)
        {
            try
            {
                Product fresh = product.IsNew ? product : await _scoped.RunAsync<IProductService, Product>(s => s.GetAsync(product.Id));
                if (fresh == null)
                {
                    Ui.Warn(this, "The product no longer exists.");
                    await ReloadAsync();
                    return;
                }

                string[] categories = (await _scoped.RunAsync<IProductService, IReadOnlyList<string>>(s => s.GetCategoriesAsync())).ToArray();
                using (var form = new ProductForm(_scoped, fresh, categories))
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

        private async void AdjustStock()
        {
            Product selected = SelectedItem;
            if (selected == null)
            {
                Ui.Info(this, "Select a product first.");
                return;
            }

            try
            {
                Product fresh = await _scoped.RunAsync<IProductService, Product>(s => s.GetAsync(selected.Id));
                if (fresh == null)
                {
                    await ReloadAsync();
                    return;
                }

                using (var form = new StockAdjustmentForm(_scoped, fresh))
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
