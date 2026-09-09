using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Coms.Application.Products
{
    public sealed class ProductService : IProductService
    {
        private readonly IProductRepository _products;
        private readonly ICurrentUser _currentUser;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IProductRepository products, ICurrentUser currentUser, ILogger<ProductService> logger)
        {
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            return _products.GetByIdAsync(id, cancellationToken);
        }

        public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            return _products.GetBySkuAsync(sku, cancellationToken);
        }

        public Task<PagedResult<Product>> SearchAsync(ProductFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            return _products.SearchAsync(filter, paging, cancellationToken);
        }

        public Task<IReadOnlyList<ProductLookup>> LookupAsync(string search, int maxResults, CancellationToken cancellationToken = default)
        {
            return _products.LookupAsync(search, maxResults, activeOnly: true, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return _products.GetCategoriesAsync(cancellationToken);
        }

        public async Task<Result<Product>> CreateAsync(Product product, CancellationToken cancellationToken = default)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            product.Normalize();
            Result<Product> validation = await ValidateAsync(product, cancellationToken).ConfigureAwait(false);
            if (validation.IsFailure)
            {
                return validation;
            }

            await _products.InsertAsync(product, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            return Result<Product>.Success(product);
        }

        public async Task<Result<Product>> UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            if (product.IsNew)
            {
                return Result<Product>.Invalid(nameof(Product.Id), "The product has not been saved yet.");
            }

            product.Normalize();
            Result<Product> validation = await ValidateAsync(product, cancellationToken).ConfigureAwait(false);
            if (validation.IsFailure)
            {
                return validation;
            }

            bool updated = await _products.UpdateAsync(product, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
            return updated ? Result<Product>.Success(product) : Result<Product>.Conflict();
        }

        public async Task<Result<Product>> AdjustStockAsync(int id, decimal newQuantity, string reason, byte[] rowVersion, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Result<Product>.Invalid("Reason", "A reason for the stock adjustment is required.");
            }

            Product? product = await _products.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (product == null)
            {
                return Result<Product>.NotFound("Product");
            }

            if (!RowVersions.Match(product.RowVersion, rowVersion))
            {
                return Result<Product>.Conflict();
            }

            decimal previous = product.QuantityOnHand;
            product.QuantityOnHand = newQuantity;

            Result<Product> result = await UpdateAsync(product, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Stock adjusted for {Sku} from {Previous} to {New} by {User}: {Reason}",
                    product.Sku, previous, newQuantity, _currentUser.UserName, reason.Trim());
            }

            return result;
        }

        public async Task<Result<Product>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default)
        {
            Product? product = await _products.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (product == null)
            {
                return Result<Product>.NotFound("Product");
            }

            if (!RowVersions.Match(product.RowVersion, rowVersion))
            {
                return Result<Product>.Conflict();
            }

            if (product.IsActive == isActive)
            {
                return Result<Product>.Success(product);
            }

            product.IsActive = isActive;
            return await UpdateAsync(product, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result<Product>> ValidateAsync(Product product, CancellationToken cancellationToken)
        {
            var errors = new List<ValidationError>(product.Validate());

            if (errors.Count == 0)
            {
                Product? existing = await _products.GetBySkuAsync(product.Sku, cancellationToken).ConfigureAwait(false);
                if (existing != null && existing.Id != product.Id)
                {
                    errors.Add(new ValidationError(nameof(Product.Sku), "SKU '" + product.Sku + "' is already used by " + existing.Name + "."));
                }
            }

            return errors.Count == 0 ? Result<Product>.Success(product) : Result<Product>.Invalid(errors);
        }
    }
}
