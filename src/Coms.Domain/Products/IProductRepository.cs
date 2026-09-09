using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;

namespace Coms.Domain.Products
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

        Task<PagedResult<Product>> SearchAsync(ProductFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        /// <summary>Up to <paramref name="maxResults"/> products whose SKU or name starts with <paramref name="search"/>, for pickers.</summary>
        Task<IReadOnlyList<ProductLookup>> LookupAsync(string search, int maxResults, bool activeOnly, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        Task InsertAsync(Product product, string userName, CancellationToken cancellationToken = default);

        /// <summary>Updates the product. Returns false when the RowVersion is stale.</summary>
        Task<bool> UpdateAsync(Product product, string userName, CancellationToken cancellationToken = default);
    }
}
