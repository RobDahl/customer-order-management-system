using Coms.Domain.Products;
using Xunit;

namespace Coms.Domain.Tests.Products
{
    public class ProductValidationTests
    {
        [Fact]
        public void Normalize_UppercasesSkuAndUnit_TrimsText()
        {
            var product = new Product { Sku = " abc-01 ", Name = " Bolt ", Category = " Fasteners ", UnitOfMeasure = "ea", Description = "  " };

            product.Normalize();

            Assert.Equal("ABC-01", product.Sku);
            Assert.Equal("Bolt", product.Name);
            Assert.Equal("Fasteners", product.Category);
            Assert.Equal("EA", product.UnitOfMeasure);
            Assert.Null(product.Description);
        }

        [Fact]
        public void Validate_ValidProduct_HasNoErrors()
        {
            Assert.Empty(ValidProduct().Validate());
        }

        [Fact]
        public void Validate_SkuWithSpace_Fails()
        {
            Product product = ValidProduct();
            product.Sku = "AB C";

            Assert.Contains(product.Validate(), e => e.Field == nameof(Product.Sku));
        }

        [Fact]
        public void Validate_NegativePrices_Fail()
        {
            Product product = ValidProduct();
            product.UnitPrice = -1;
            product.CostPrice = -1;
            product.ReorderLevel = -1;

            var errors = product.Validate();

            Assert.Contains(errors, e => e.Field == nameof(Product.UnitPrice));
            Assert.Contains(errors, e => e.Field == nameof(Product.CostPrice));
            Assert.Contains(errors, e => e.Field == nameof(Product.ReorderLevel));
        }

        [Fact]
        public void IsLowStock_OnlyForActiveAtOrBelowReorder()
        {
            Assert.True(new Product { IsActive = true, QuantityOnHand = 5, ReorderLevel = 5 }.IsLowStock);
            Assert.False(new Product { IsActive = true, QuantityOnHand = 6, ReorderLevel = 5 }.IsLowStock);
            Assert.False(new Product { IsActive = false, QuantityOnHand = 0, ReorderLevel = 5 }.IsLowStock);
        }

        [Fact]
        public void UnitMargin_IsPriceMinusCostRounded()
        {
            Assert.Equal(2.36m, new Product { UnitPrice = 10.005m, CostPrice = 7.65m }.UnitMargin);   // 2.355 rounds away from zero
        }

        private static Product ValidProduct()
        {
            return new Product { Sku = "ABC-01", Name = "Bolt", Category = "Fasteners", UnitOfMeasure = "EA", UnitPrice = 1, CostPrice = 0.5m };
        }
    }
}
