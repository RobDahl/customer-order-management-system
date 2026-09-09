using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Application.Tests.Fakes
{
    internal sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new List<Product>();

        public Product Add(Product product)
        {
            product.Id = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
            product.RowVersion = Versions.Next();
            Products.Add(product);
            return product;
        }

        public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
        }

        public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Products.FirstOrDefault(p => string.Equals(p.Sku, sku.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public Task<PagedResult<Product>> SearchAsync(ProductFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            IEnumerable<Product> query = Products;

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                query = query.Where(p => p.Sku.StartsWith(filter.Search!, StringComparison.OrdinalIgnoreCase)
                                      || p.Name.StartsWith(filter.Search!, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }

            if (filter.LowStockOnly)
            {
                query = query.Where(p => p.IsLowStock);
            }

            List<Product> all = query.OrderBy(p => p.Sku).ToList();
            List<Product> page = all.Skip(paging.Offset).Take(paging.PageSize).ToList();
            return Task.FromResult(new PagedResult<Product>(page, all.Count, paging.Page, paging.PageSize));
        }

        public Task<IReadOnlyList<ProductLookup>> LookupAsync(string search, int maxResults, bool activeOnly, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProductLookup> rows = Products
                .Where(p => !activeOnly || p.IsActive)
                .Where(p => string.IsNullOrEmpty(search) || p.Sku.StartsWith(search, StringComparison.OrdinalIgnoreCase) || p.Name.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Sku)
                .Take(maxResults)
                .Select(p => new ProductLookup
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Name = p.Name,
                    UnitOfMeasure = p.UnitOfMeasure,
                    UnitPrice = p.UnitPrice,
                    CostPrice = p.CostPrice,
                    QuantityOnHand = p.QuantityOnHand,
                    IsActive = p.IsActive
                })
                .ToList();

            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> categories = Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
            return Task.FromResult(categories);
        }

        public Task InsertAsync(Product product, string userName, CancellationToken cancellationToken = default)
        {
            Add(product);
            product.CreatedBy = product.UpdatedBy = userName;
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Product product, string userName, CancellationToken cancellationToken = default)
        {
            Product? stored = Products.FirstOrDefault(p => p.Id == product.Id);
            if (stored == null || !Versions.Match(stored.RowVersion, product.RowVersion))
            {
                return Task.FromResult(false);
            }

            if (!ReferenceEquals(stored, product))
            {
                Products.Remove(stored);
                Products.Add(product);
            }

            product.RowVersion = Versions.Next();
            product.UpdatedBy = userName;
            return Task.FromResult(true);
        }
    }
}
