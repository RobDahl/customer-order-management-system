using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Products;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace Coms.Application.Csv
{
    public sealed class CsvImportService : ICsvImportService
    {
        private readonly ICustomerRepository _customers;
        private readonly IProductRepository _products;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly ComsOptions _options;
        private readonly ILogger<CsvImportService> _logger;

        public CsvImportService(
            ICustomerRepository customers,
            IProductRepository products,
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            ComsOptions options,
            ILogger<CsvImportService> logger)
        {
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string CustomerTemplate => string.Join(",", CustomerCsvRow.Headers);

        public string ProductTemplate => string.Join(",", ProductCsvRow.Headers);

        public async Task<CsvImportResult> ImportCustomersAsync(TextReader reader, CsvImportOptions options, CancellationToken cancellationToken = default)
        {
            var result = new CsvImportResult("customer") { DryRun = options.DryRun };
            var inserts = new List<Customer>();
            var updates = new List<Customer>();
            var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var csv = new CsvReader(reader, CsvFormat.Reader()))
            {
                if (!ReadHeader(csv, CustomerCsvRow.RequiredHeaders, result))
                {
                    return result;
                }

                while (csv.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.TotalRows++;
                    int rowNumber = csv.Parser.Row;

                    if (result.TotalRows > _options.MaxImportRows)
                    {
                        result.Errors.Add(new CsvRowError(0, string.Empty, "The file has more than " + _options.MaxImportRows + " rows."));
                        break;
                    }

                    var rowErrors = new List<CsvRowError>();
                    var row = new CsvRowReader(csv, rowNumber, rowErrors);

                    string? number = row.Text(nameof(CustomerCsvRow.CustomerNumber));
                    Customer? existing = null;

                    if (number != null)
                    {
                        if (!seenNumbers.Add(number))
                        {
                            rowErrors.Add(new CsvRowError(rowNumber, nameof(CustomerCsvRow.CustomerNumber), "Customer number appears more than once in the file."));
                        }

                        existing = await _customers.GetByNumberAsync(number, cancellationToken).ConfigureAwait(false);
                        if (existing == null)
                        {
                            rowErrors.Add(new CsvRowError(rowNumber, nameof(CustomerCsvRow.CustomerNumber), "Customer " + number + " does not exist. Leave the number blank to create a new customer."));
                        }
                        else if (!options.UpdateExisting)
                        {
                            rowErrors.Add(new CsvRowError(rowNumber, nameof(CustomerCsvRow.CustomerNumber), "Customer " + number + " already exists. Enable 'update existing' to overwrite it."));
                        }
                    }

                    /* Always build a fresh object: the existing one must stay
                       untouched until the whole file has passed validation. */
                    Customer customer = existing == null ? new Customer() : CloneForUpdate(existing);
                    ApplyCustomerRow(row, customer);
                    customer.Normalize();

                    foreach (ValidationError error in customer.Validate())
                    {
                        rowErrors.Add(new CsvRowError(rowNumber, error.Field, error.Message));
                    }

                    if (rowErrors.Count > 0)
                    {
                        result.Errors.AddRange(rowErrors);
                        continue;
                    }

                    if (existing != null)
                    {
                        updates.Add(customer);
                    }
                    else
                    {
                        inserts.Add(customer);
                    }
                }
            }

            result.ToInsert = inserts.Count;
            result.ToUpdate = updates.Count;

            if (result.HasErrors || options.DryRun)
            {
                return result;
            }

            await CommitAsync(result, async () =>
            {
                foreach (Customer customer in inserts)
                {
                    await _customers.InsertAsync(customer, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
                }

                foreach (Customer customer in updates)
                {
                    if (!await _customers.UpdateAsync(customer, _currentUser.UserName, cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("Customer " + customer.CustomerNumber + " was changed by another user during the import.");
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        public async Task<CsvImportResult> ImportProductsAsync(TextReader reader, CsvImportOptions options, CancellationToken cancellationToken = default)
        {
            var result = new CsvImportResult("product") { DryRun = options.DryRun };
            var inserts = new List<Product>();
            var updates = new List<Product>();
            var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var csv = new CsvReader(reader, CsvFormat.Reader()))
            {
                if (!ReadHeader(csv, ProductCsvRow.RequiredHeaders, result))
                {
                    return result;
                }

                while (csv.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.TotalRows++;
                    int rowNumber = csv.Parser.Row;

                    if (result.TotalRows > _options.MaxImportRows)
                    {
                        result.Errors.Add(new CsvRowError(0, string.Empty, "The file has more than " + _options.MaxImportRows + " rows."));
                        break;
                    }

                    var rowErrors = new List<CsvRowError>();
                    var row = new CsvRowReader(csv, rowNumber, rowErrors);

                    string sku = row.Required(nameof(ProductCsvRow.Sku)).ToUpperInvariant();
                    Product? existing = null;

                    if (sku.Length > 0)
                    {
                        if (!seenSkus.Add(sku))
                        {
                            rowErrors.Add(new CsvRowError(rowNumber, nameof(ProductCsvRow.Sku), "SKU appears more than once in the file."));
                        }

                        existing = await _products.GetBySkuAsync(sku, cancellationToken).ConfigureAwait(false);
                        if (existing != null && !options.UpdateExisting)
                        {
                            rowErrors.Add(new CsvRowError(rowNumber, nameof(ProductCsvRow.Sku), "SKU " + sku + " already exists. Enable 'update existing' to overwrite it."));
                        }
                    }

                    Product product = existing == null ? new Product() : CloneForUpdate(existing);
                    ApplyProductRow(row, product, existing == null);
                    product.Normalize();

                    foreach (ValidationError error in product.Validate())
                    {
                        rowErrors.Add(new CsvRowError(rowNumber, error.Field, error.Message));
                    }

                    if (rowErrors.Count > 0)
                    {
                        result.Errors.AddRange(rowErrors);
                        continue;
                    }

                    if (existing != null)
                    {
                        updates.Add(product);
                    }
                    else
                    {
                        inserts.Add(product);
                    }
                }
            }

            result.ToInsert = inserts.Count;
            result.ToUpdate = updates.Count;

            if (result.HasErrors || options.DryRun)
            {
                return result;
            }

            await CommitAsync(result, async () =>
            {
                foreach (Product product in inserts)
                {
                    await _products.InsertAsync(product, _currentUser.UserName, cancellationToken).ConfigureAwait(false);
                }

                foreach (Product product in updates)
                {
                    if (!await _products.UpdateAsync(product, _currentUser.UserName, cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException("Product " + product.Sku + " was changed by another user during the import.");
                    }
                }
            }, cancellationToken).ConfigureAwait(false);

            return result;
        }

        /* ------------------------------------------------------------------ */

        private static bool ReadHeader(CsvReader csv, string[] required, CsvImportResult result)
        {
            if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord == null || csv.HeaderRecord.Length == 0)
            {
                result.Errors.Add(new CsvRowError(0, string.Empty, "The file is empty or has no header row."));
                return false;
            }

            var present = new HashSet<string>(csv.HeaderRecord.Select(h => h.Trim()), StringComparer.OrdinalIgnoreCase);
            string[] missing = required.Where(r => !present.Contains(r)).ToArray();
            if (missing.Length > 0)
            {
                result.Errors.Add(new CsvRowError(0, string.Empty, "Missing required column(s): " + string.Join(", ", missing) + "."));
                return false;
            }

            return true;
        }

        private static Customer CloneForUpdate(Customer source)
        {
            return new Customer
            {
                Id = source.Id,
                CustomerNumber = source.CustomerNumber,
                RowVersion = source.RowVersion,
                CreatedAtUtc = source.CreatedAtUtc,
                CreatedBy = source.CreatedBy,
                UpdatedAtUtc = source.UpdatedAtUtc,
                UpdatedBy = source.UpdatedBy,
                PaymentTermsDays = source.PaymentTermsDays,
                Status = source.Status
            };
        }

        private static Product CloneForUpdate(Product source)
        {
            return new Product
            {
                Id = source.Id,
                RowVersion = source.RowVersion,
                CreatedAtUtc = source.CreatedAtUtc,
                CreatedBy = source.CreatedBy,
                UpdatedAtUtc = source.UpdatedAtUtc,
                UpdatedBy = source.UpdatedBy,
                UnitOfMeasure = source.UnitOfMeasure,
                UnitPrice = source.UnitPrice,
                CostPrice = source.CostPrice,
                QuantityOnHand = source.QuantityOnHand,
                ReorderLevel = source.ReorderLevel,
                IsActive = source.IsActive
            };
        }

        private static void ApplyCustomerRow(CsvRowReader row, Customer customer)
        {
            customer.Name = row.Required(nameof(CustomerCsvRow.Name));
            customer.ContactName = row.Text(nameof(CustomerCsvRow.ContactName));
            customer.Email = row.Text(nameof(CustomerCsvRow.Email));
            customer.Phone = row.Text(nameof(CustomerCsvRow.Phone));

            customer.BillingAddress = new Address
            {
                Line1 = row.Required(nameof(CustomerCsvRow.BillingLine1)),
                Line2 = row.Text(nameof(CustomerCsvRow.BillingLine2)),
                City = row.Required(nameof(CustomerCsvRow.BillingCity)),
                Region = row.Text(nameof(CustomerCsvRow.BillingRegion)),
                PostalCode = row.Text(nameof(CustomerCsvRow.BillingPostalCode)),
                Country = row.Required(nameof(CustomerCsvRow.BillingCountry))
            };

            customer.ShippingAddress = new Address
            {
                Line1 = row.Text(nameof(CustomerCsvRow.ShippingLine1)) ?? string.Empty,
                Line2 = row.Text(nameof(CustomerCsvRow.ShippingLine2)),
                City = row.Text(nameof(CustomerCsvRow.ShippingCity)) ?? string.Empty,
                Region = row.Text(nameof(CustomerCsvRow.ShippingRegion)),
                PostalCode = row.Text(nameof(CustomerCsvRow.ShippingPostalCode)),
                Country = row.Text(nameof(CustomerCsvRow.ShippingCountry)) ?? string.Empty
            };

            customer.PaymentTermsDays = row.Int(nameof(CustomerCsvRow.PaymentTermsDays)) ?? (customer.IsNew ? Customer.DefaultPaymentTermsDays : customer.PaymentTermsDays);
            customer.CreditLimit = row.Text(nameof(CustomerCsvRow.CreditLimit)) == null ? null : row.Decimal(nameof(CustomerCsvRow.CreditLimit));
            customer.Status = row.Enum<CustomerStatus>(nameof(CustomerCsvRow.Status)) ?? customer.Status;
            customer.Notes = row.Text(nameof(CustomerCsvRow.Notes));
        }

        private static void ApplyProductRow(CsvRowReader row, Product product, bool isNew)
        {
            product.Sku = row.Required(nameof(ProductCsvRow.Sku));
            product.Name = row.Required(nameof(ProductCsvRow.Name));
            product.Description = row.Text(nameof(ProductCsvRow.Description));
            product.Category = row.Required(nameof(ProductCsvRow.Category));
            product.UnitOfMeasure = row.Text(nameof(ProductCsvRow.UnitOfMeasure)) ?? (isNew ? Product.DefaultUnitOfMeasure : product.UnitOfMeasure);
            product.UnitPrice = row.Decimal(nameof(ProductCsvRow.UnitPrice)) ?? (isNew ? 0 : product.UnitPrice);
            product.CostPrice = row.Decimal(nameof(ProductCsvRow.CostPrice)) ?? (isNew ? 0 : product.CostPrice);
            product.QuantityOnHand = row.Decimal(nameof(ProductCsvRow.QuantityOnHand)) ?? (isNew ? 0 : product.QuantityOnHand);
            product.ReorderLevel = row.Decimal(nameof(ProductCsvRow.ReorderLevel)) ?? (isNew ? 0 : product.ReorderLevel);
            product.IsActive = row.Bool(nameof(ProductCsvRow.IsActive)) ?? (isNew || product.IsActive);
        }

        private async Task CommitAsync(CsvImportResult result, Func<Task> write, CancellationToken cancellationToken)
        {
            bool ownsTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsTransaction)
            {
                await _unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await write().ConfigureAwait(false);

                if (ownsTransaction)
                {
                    await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                result.Committed = true;
                _logger.LogInformation("CSV import of {Entity}: {Inserted} inserted, {Updated} updated by {User}", result.EntityName, result.ToInsert, result.ToUpdate, _currentUser.UserName);
            }
            catch
            {
                if (ownsTransaction)
                {
                    await _unitOfWork.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }

                throw;
            }
        }
    }
}
