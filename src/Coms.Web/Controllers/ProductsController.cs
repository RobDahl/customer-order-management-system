using System.Threading.Tasks;
using Coms.Application.Csv;
using Coms.Application.Products;
using Coms.Domain.Common;
using Coms.Domain.Products;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.CanView)]
    public class ProductsController : ComsController
    {
        private readonly IProductService _products;
        private readonly ICsvExportService _export;

        public ProductsController(IProductService products, ICsvExportService export)
        {
            _products = products;
            _export = export;
        }

        [HttpGet]
        public async Task<IActionResult> Index(ProductListModel query)
        {
            query.Categories = await _products.GetCategoriesAsync();
            query.Results = await _products.SearchAsync(query.ToFilter(), query.ToPagedRequest());
            return View(query);
        }

        [HttpGet]
        public async Task<IActionResult> Export(ProductListModel query)
        {
            return await CsvFileAsync("products", w => _export.WriteProductsAsync(w, query.ToFilter()));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            Product? product = await _products.GetAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Create()
        {
            return View(new ProductFormModel { Categories = await _products.GetCategoriesAsync() });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormModel model)
        {
            Result<Product> result = await _products.CreateAsync(model.Product);
            if (result.IsFailure)
            {
                AddErrors(result, nameof(ProductFormModel.Product));
                model.Categories = await _products.GetCategoriesAsync();
                return View(model);
            }

            Flash("Product " + result.Value.Sku + " created.");
            return RedirectToAction(nameof(Details), new { id = result.Value.Id });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> Edit(int id)
        {
            Product? product = await _products.GetAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(new ProductFormModel { Product = product, Categories = await _products.GetCategoriesAsync() });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductFormModel model)
        {
            model.Product.Id = id;

            Result<Product> result = await _products.UpdateAsync(model.Product);
            if (result.IsFailure)
            {
                AddErrors(result, nameof(ProductFormModel.Product));
                model.Categories = await _products.GetCategoriesAsync();
                return View(model);
            }

            Flash("Product " + result.Value.Sku + " saved.");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive(SetActiveModel model)
        {
            Result<Product> result = await _products.SetActiveAsync(model.Id, model.IsActive, ParseRowVersion(model.RowVersion));
            if (result.IsFailure)
            {
                FlashError(result.Message);
            }
            else
            {
                Flash("Product " + result.Value.Sku + (result.Value.IsActive ? " activated." : " deactivated."));
            }

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpGet]
        [Authorize(Policy = Policies.CanEdit)]
        public async Task<IActionResult> AdjustStock(int id)
        {
            Product? product = await _products.GetAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            return View(ToAdjustment(product));
        }

        [HttpPost]
        [Authorize(Policy = Policies.CanEdit)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(StockAdjustmentModel model)
        {
            Result<Product> result = await _products.AdjustStockAsync(model.Id, model.NewQuantity, model.Reason, ParseRowVersion(model.RowVersion));
            if (result.IsFailure)
            {
                AddErrors(result);

                Product? product = await _products.GetAsync(model.Id);
                if (product == null)
                {
                    return NotFound();
                }

                StockAdjustmentModel refreshed = ToAdjustment(product);
                refreshed.NewQuantity = model.NewQuantity;
                refreshed.Reason = model.Reason;
                return View(refreshed);
            }

            Flash("Stock for " + result.Value.Sku + " set to " + result.Value.QuantityOnHand.ToString("0.###") + ".");
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        private static StockAdjustmentModel ToAdjustment(Product product)
        {
            return new StockAdjustmentModel
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                UnitOfMeasure = product.UnitOfMeasure,
                CurrentQuantity = product.QuantityOnHand,
                NewQuantity = product.QuantityOnHand,
                RowVersion = System.Convert.ToBase64String(product.RowVersion)
            };
        }
    }
}
