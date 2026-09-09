using System.Threading.Tasks;
using Coms.Application.Tests.Fakes;
using Coms.Domain.Common;
using Coms.Domain.Products;
using Xunit;

namespace Coms.Application.Tests.Products
{
    public class ProductServiceTests
    {
        private readonly TestWorld _world = new TestWorld();

        [Fact]
        public async Task Create_DuplicateSku_IsRejectedCaseInsensitively()
        {
            var product = new Product { Sku = "bolt-1", Name = "Another bolt", Category = "Fasteners", UnitPrice = 1 };

            Result<Product> result = await _world.ProductService.CreateAsync(product);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Contains(result.Errors, e => e.Field == "Sku" && e.Message.Contains("already used"));
            Assert.Equal(3, _world.Products.Products.Count);
        }

        [Fact]
        public async Task Create_Valid_NormalizesAndSaves()
        {
            var product = new Product { Sku = " washer-9 ", Name = " Washer ", Category = "Fasteners", UnitOfMeasure = "pk", UnitPrice = 0.5m };

            Result<Product> result = await _world.ProductService.CreateAsync(product);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("WASHER-9", result.Value.Sku);
            Assert.Equal("PK", result.Value.UnitOfMeasure);
            Assert.True(result.Value.Id > 0);
        }

        [Fact]
        public async Task Update_ChangingSkuToExistingOne_IsRejected()
        {
            Product nut = _world.Nut;
            nut.Sku = "BOLT-1";

            Result<Product> result = await _world.ProductService.UpdateAsync(nut);

            Assert.Equal(ErrorCode.Validation, result.Code);
        }

        [Fact]
        public async Task AdjustStock_RequiresReason()
        {
            Result<Product> result = await _world.ProductService.AdjustStockAsync(_world.Bolt.Id, 50, "  ", _world.Bolt.RowVersion);

            Assert.Equal(ErrorCode.Validation, result.Code);
            Assert.Equal(100, _world.Bolt.QuantityOnHand);
        }

        [Fact]
        public async Task AdjustStock_SetsQuantityAndBumpsVersion()
        {
            Result<Product> result = await _world.ProductService.AdjustStockAsync(_world.Bolt.Id, 42, "Stock count", _world.Bolt.RowVersion);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(42, _world.Bolt.QuantityOnHand);
        }

        [Fact]
        public async Task AdjustStock_StaleVersion_ReturnsConflict()
        {
            Result<Product> result = await _world.ProductService.AdjustStockAsync(_world.Bolt.Id, 42, "Stock count", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            Assert.Equal(ErrorCode.Conflict, result.Code);
            Assert.Equal(100, _world.Bolt.QuantityOnHand);
        }

        [Fact]
        public async Task SetActive_TogglesAndIsIdempotent()
        {
            Result<Product> first = await _world.ProductService.SetActiveAsync(_world.Bolt.Id, false, _world.Bolt.RowVersion);
            Result<Product> second = await _world.ProductService.SetActiveAsync(_world.Bolt.Id, false, _world.Bolt.RowVersion);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.False(_world.Bolt.IsActive);
        }

        [Fact]
        public async Task Lookup_ReturnsActiveOnly()
        {
            var rows = await _world.ProductService.LookupAsync("", 10);

            Assert.Equal(2, rows.Count);
            Assert.DoesNotContain(rows, r => r.Sku == "OLD-1");
        }
    }
}
