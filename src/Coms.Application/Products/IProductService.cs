using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Products;

namespace Coms.Application.Products
{
    public interface IProductService
    {
        Task<Product?> GetAsync(int id, CancellationToken cancellationToken = default);

        Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);

        Task<PagedResult<Product>> SearchAsync(ProductFilter filter, PagedRequest paging, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ProductLookup>> LookupAsync(string search, int maxResults, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

        Task<Result<Product>> CreateAsync(Product product, CancellationToken cancellationToken = default);

        Task<Result<Product>> UpdateAsync(Product product, CancellationToken cancellationToken = default);

        /// <summary>Sets the on-hand quantity to a counted value with a reason for the log.</summary>
        Task<Result<Product>> AdjustStockAsync(int id, decimal newQuantity, string reason, byte[] rowVersion, CancellationToken cancellationToken = default);

        Task<Result<Product>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default);
    }
}
