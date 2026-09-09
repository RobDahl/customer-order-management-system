using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Common;
using Coms.Domain.Products;
using Xunit;

namespace Coms.Data.Tests.Repositories
{
    [Collection(DatabaseCollection.Name)]
    public class ProductRepositoryTests
    {
        private readonly DatabaseFixture _db;

        public ProductRepositoryTests(DatabaseFixture db)
        {
            _db = db;
        }

        [Fact]
        public async Task GetBySku_IsCaseInsensitiveOnInput()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                Product? product = await new ProductRepository(session).GetBySkuAsync(" fas-0001 ");

                Assert.NotNull(product);
                Assert.Equal("FAS-0001", product!.Sku);
                Assert.Equal(1, product.Id);
            }
        }

        [Fact]
        public async Task Search_LowStockOnly_ReturnsOnlyActiveAtOrBelowReorder()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new ProductFilter { LowStockOnly = true };

                PagedResult<Product> page = await new ProductRepository(session).SearchAsync(filter, PagedRequest.FirstPage(500));

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, p => Assert.True(p.IsLowStock));
            }
        }

        [Fact]
        public async Task Search_ByCategoryAndSort_Works()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                var filter = new ProductFilter { Category = "Fasteners" };
                var paging = new PagedRequest { PageSize = 100, SortBy = "price", SortDescending = true };

                PagedResult<Product> page = await new ProductRepository(session).SearchAsync(filter, paging);

                Assert.True(page.TotalCount > 0);
                Assert.All(page.Items, p => Assert.Equal("Fasteners", p.Category));
                for (int i = 1; i < page.Items.Count; i++)
                {
                    Assert.True(page.Items[i - 1].UnitPrice >= page.Items[i].UnitPrice);
                }
            }
        }

        [Fact]
        public async Task Lookup_ByPrefix_RespectsMaxAndActiveOnly()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<ProductLookup> rows = await new ProductRepository(session).LookupAsync("FAS", 5, activeOnly: true);

                Assert.True(rows.Count <= 5);
                Assert.True(rows.Count > 0);
                Assert.All(rows, r => Assert.StartsWith("FAS", r.Sku));
                Assert.All(rows, r => Assert.True(r.IsActive));
            }
        }

        [Fact]
        public async Task GetCategories_ReturnsSeededEight()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = _db.OpenSession())
            {
                IReadOnlyList<string> categories = await new ProductRepository(session).GetCategoriesAsync();

                Assert.Equal(8, categories.Count);
                Assert.Contains("Fasteners", categories);
            }
        }

        [Fact]
        public async Task Insert_ThenUpdate_RoundTrips()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new ProductRepository(session);
                var product = new Product
                {
                    Sku = "TST-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
                    Name = "Test Widget",
                    Category = "Testing",
                    UnitOfMeasure = "EA",
                    UnitPrice = 12.34m,
                    CostPrice = 6.78m,
                    QuantityOnHand = 10,
                    ReorderLevel = 2
                };

                await repository.InsertAsync(product, "tester");
                Assert.True(product.Id > 150);
                Assert.Equal(8, product.RowVersion.Length);

                product.UnitPrice = 15.00m;
                Assert.True(await repository.UpdateAsync(product, "tester"));

                Product? reloaded = await repository.GetByIdAsync(product.Id);
                Assert.Equal(15.00m, reloaded!.UnitPrice);
                Assert.Equal("tester", reloaded.UpdatedBy);
            }
        }

        [Fact]
        public async Task Update_StaleRowVersion_ReturnsFalse()
        {
            if (!_db.IsAvailable) { return; }

            using (DbSession session = await _db.BeginSessionAsync())
            {
                var repository = new ProductRepository(session);
                Product product = (await repository.GetByIdAsync(1))!;
                product.RowVersion = new byte[8];

                Assert.False(await repository.UpdateAsync(product, "tester"));
            }
        }
    }
}
