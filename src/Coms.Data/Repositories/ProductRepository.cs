using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Domain.Common;
using Coms.Domain.Products;
using Dapper;

namespace Coms.Data.Repositories
{
    public sealed class ProductRepository : RepositoryBase, IProductRepository
    {
        private const string SelectColumns = @"
            p.Id, p.Sku, p.Name, p.Description, p.Category, p.UnitOfMeasure,
            p.UnitPrice, p.CostPrice, p.QuantityOnHand, p.ReorderLevel, p.IsActive,
            p.CreatedAtUtc, p.CreatedBy, p.UpdatedAtUtc, p.UpdatedBy, p.RowVersion";

        private static readonly IReadOnlyDictionary<string, string> SortColumns = SortMap(
            ("sku", "p.Sku"),
            ("name", "p.Name"),
            ("category", "p.Category"),
            ("price", "p.UnitPrice"),
            ("cost", "p.CostPrice"),
            ("stock", "p.QuantityOnHand"),
            ("reorder", "p.ReorderLevel"),
            ("active", "p.IsActive"),
            ("updated", "p.UpdatedAtUtc"));

        public ProductRepository(IDbSession session)
            : base(session)
        {
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<Product>(
                Text("SELECT " + SelectColumns + " FROM dbo.Products p WHERE p.Id = @Id;", new { Id = id }, cancellationToken)).ConfigureAwait(false);
        }

        public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<Product>(
                Text("SELECT " + SelectColumns + " FROM dbo.Products p WHERE p.Sku = @Sku;", new { Sku = sku.Trim().ToUpperInvariant() }, cancellationToken)).ConfigureAwait(false);
        }

        public Task<PagedResult<Product>> SearchAsync(ProductFilter filter, PagedRequest paging, CancellationToken cancellationToken = default)
        {
            var where = new List<string>();
            var parameters = new DynamicParameters();

            string? search = StartsWith(filter.Search);
            if (search != null)
            {
                where.Add("(p.Sku LIKE @Search OR p.Name LIKE @Search)");
                parameters.Add("@Search", search);
            }

            if (!string.IsNullOrWhiteSpace(filter.Category))
            {
                where.Add("p.Category = @Category");
                parameters.Add("@Category", filter.Category!.Trim());
            }

            if (filter.IsActive.HasValue)
            {
                where.Add("p.IsActive = @IsActive");
                parameters.Add("@IsActive", filter.IsActive.Value);
            }

            if (filter.LowStockOnly)
            {
                where.Add("p.IsActive = 1 AND p.QuantityOnHand <= p.ReorderLevel");
            }

            string fromAndWhere = "FROM dbo.Products p" + (where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where));
            string orderBy = ResolveSort(paging, SortColumns, "p.Sku ASC, p.Id", "p.Id");

            return QueryPagedAsync<Product>(SelectColumns, fromAndWhere, orderBy, parameters, paging, cancellationToken);
        }

        public async Task<IReadOnlyList<ProductLookup>> LookupAsync(string search, int maxResults, bool activeOnly, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT TOP (@Max) p.Id, p.Sku, p.Name, p.UnitOfMeasure, p.UnitPrice, p.CostPrice, p.QuantityOnHand, p.IsActive
                  FROM dbo.Products p
                 WHERE (@Search IS NULL OR p.Sku LIKE @Search OR p.Name LIKE @Search)"
                + (activeOnly ? " AND p.IsActive = 1" : string.Empty) +
                " ORDER BY p.Sku;";

            var parameters = new { Max = Math.Max(1, maxResults), Search = StartsWith(search) };

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<ProductLookup>(Text(sql, parameters, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            return (await connection.QueryAsync<string>(
                Text("SELECT DISTINCT Category FROM dbo.Products ORDER BY Category;", null, cancellationToken)).ConfigureAwait(false)).ToList();
        }

        public async Task InsertAsync(Product product, string userName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT dbo.Products
                    (Sku, Name, Description, Category, UnitOfMeasure, UnitPrice, CostPrice, QuantityOnHand, ReorderLevel, IsActive,
                     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
                OUTPUT INSERTED.Id, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.RowVersion
                VALUES
                    (@Sku, @Name, @Description, @Category, @UnitOfMeasure, @UnitPrice, @CostPrice, @QuantityOnHand, @ReorderLevel, @IsActive,
                     SYSUTCDATETIME(), @UserName, SYSUTCDATETIME(), @UserName);";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            InsertedRow row = await connection.QuerySingleAsync<InsertedRow>(Text(sql, ToParameters(product, userName), cancellationToken)).ConfigureAwait(false);

            product.Id = row.Id;
            product.CreatedAtUtc = row.CreatedAtUtc;
            product.CreatedBy = userName;
            product.UpdatedAtUtc = row.UpdatedAtUtc;
            product.UpdatedBy = userName;
            product.RowVersion = row.RowVersion;
        }

        public async Task<bool> UpdateAsync(Product product, string userName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE dbo.Products
                   SET Sku = @Sku, Name = @Name, Description = @Description, Category = @Category, UnitOfMeasure = @UnitOfMeasure,
                       UnitPrice = @UnitPrice, CostPrice = @CostPrice, QuantityOnHand = @QuantityOnHand, ReorderLevel = @ReorderLevel,
                       IsActive = @IsActive, UpdatedAtUtc = SYSUTCDATETIME(), UpdatedBy = @UserName
                OUTPUT INSERTED.UpdatedAtUtc, INSERTED.RowVersion
                 WHERE Id = @Id AND RowVersion = @RowVersion;";

            IDbConnection connection = await ConnectionAsync(cancellationToken).ConfigureAwait(false);
            UpdatedRow? row = await connection.QuerySingleOrDefaultAsync<UpdatedRow>(Text(sql, ToParameters(product, userName), cancellationToken)).ConfigureAwait(false);

            if (row == null)
            {
                return false;
            }

            product.UpdatedAtUtc = row.UpdatedAtUtc;
            product.UpdatedBy = userName;
            product.RowVersion = row.RowVersion;
            return true;
        }

        private static object ToParameters(Product p, string userName)
        {
            return new
            {
                p.Id,
                p.RowVersion,
                p.Sku,
                p.Name,
                p.Description,
                p.Category,
                p.UnitOfMeasure,
                p.UnitPrice,
                p.CostPrice,
                p.QuantityOnHand,
                p.ReorderLevel,
                p.IsActive,
                UserName = userName
            };
        }

        private sealed class InsertedRow
        {
            public int Id { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public DateTime UpdatedAtUtc { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }

        private sealed class UpdatedRow
        {
            public DateTime UpdatedAtUtc { get; set; }
            public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        }
    }
}
