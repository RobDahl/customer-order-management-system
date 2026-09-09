using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using CsvHelper;

namespace Coms.Application.Csv
{
    public sealed class CsvExportService : ICsvExportService
    {
        private const int PageSize = PagedRequest.MaxPageSize;

        private readonly ICustomerRepository _customers;
        private readonly IProductRepository _products;
        private readonly IOrderRepository _orders;
        private readonly IInvoiceRepository _invoices;

        public CsvExportService(
            ICustomerRepository customers,
            IProductRepository products,
            IOrderRepository orders,
            IInvoiceRepository invoices)
        {
            _customers = customers ?? throw new ArgumentNullException(nameof(customers));
            _products = products ?? throw new ArgumentNullException(nameof(products));
            _orders = orders ?? throw new ArgumentNullException(nameof(orders));
            _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        }

        public Task<int> WriteCustomersAsync(TextWriter writer, CustomerFilter filter, CancellationToken cancellationToken = default)
        {
            return WritePagedAsync(
                writer,
                paging => _customers.SearchAsync(filter, paging, cancellationToken),
                ToRow,
                cancellationToken);
        }

        public Task<int> WriteProductsAsync(TextWriter writer, ProductFilter filter, CancellationToken cancellationToken = default)
        {
            return WritePagedAsync(
                writer,
                paging => _products.SearchAsync(filter, paging, cancellationToken),
                ToRow,
                cancellationToken);
        }

        public Task<int> WriteOrdersAsync(TextWriter writer, OrderFilter filter, CancellationToken cancellationToken = default)
        {
            return WritePagedAsync(
                writer,
                paging => _orders.SearchAsync(filter, paging, cancellationToken),
                ToRow,
                cancellationToken);
        }

        public async Task<int> WriteOrderLinesAsync(TextWriter writer, int orderId, CancellationToken cancellationToken = default)
        {
            Order? order = await _orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order == null)
            {
                return 0;
            }

            Customer? customer = await _customers.GetByIdAsync(order.CustomerId, cancellationToken).ConfigureAwait(false);
            var rows = new List<OrderLineCsvRow>(order.Lines.Count);

            foreach (OrderLine line in order.Lines)
            {
                rows.Add(new OrderLineCsvRow
                {
                    OrderNumber = order.OrderNumber,
                    Status = order.Status.ToString(),
                    OrderDate = CsvFormat.Date(order.OrderDate),
                    CustomerNumber = customer?.CustomerNumber,
                    CustomerName = customer?.Name,
                    LineNumber = line.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Sku = line.Sku,
                    Description = line.Description,
                    Quantity = CsvFormat.Quantity(line.Quantity),
                    UnitPrice = CsvFormat.Money(line.UnitPrice),
                    DiscountPercent = CsvFormat.Quantity(line.DiscountPercent),
                    LineTotal = CsvFormat.Money(line.LineTotal)
                });
            }

            return await WriteRecordsAsync(writer, rows, cancellationToken).ConfigureAwait(false);
        }

        public Task<int> WriteInvoicesAsync(TextWriter writer, InvoiceFilter filter, CancellationToken cancellationToken = default)
        {
            return WritePagedAsync(
                writer,
                paging => _invoices.SearchAsync(filter, paging, cancellationToken),
                ToRow,
                cancellationToken);
        }

        public async Task<int> WriteRecordsAsync<T>(TextWriter writer, IEnumerable<T> records, CancellationToken cancellationToken = default)
        {
            int count = 0;

            using (var csv = new CsvWriter(writer, CsvFormat.Writer(), leaveOpen: true))
            {
                csv.WriteHeader<T>();
                await csv.NextRecordAsync().ConfigureAwait(false);

                foreach (T record in records)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    csv.WriteRecord(record);
                    await csv.NextRecordAsync().ConfigureAwait(false);
                    count++;
                }

                await csv.FlushAsync().ConfigureAwait(false);
            }

            return count;
        }

        /* ------------------------------------------------------------------ */

        private static async Task<int> WritePagedAsync<TEntity, TRow>(
            TextWriter writer,
            Func<PagedRequest, Task<PagedResult<TEntity>>> fetchPage,
            Func<TEntity, TRow> toRow,
            CancellationToken cancellationToken)
        {
            int count = 0;

            using (var csv = new CsvWriter(writer, CsvFormat.Writer(), leaveOpen: true))
            {
                csv.WriteHeader<TRow>();
                await csv.NextRecordAsync().ConfigureAwait(false);

                var paging = new PagedRequest { Page = 1, PageSize = PageSize };
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PagedResult<TEntity> page = await fetchPage(paging).ConfigureAwait(false);

                    foreach (TEntity item in page.Items)
                    {
                        csv.WriteRecord(toRow(item));
                        await csv.NextRecordAsync().ConfigureAwait(false);
                        count++;
                    }

                    if (!page.HasNext)
                    {
                        break;
                    }

                    paging.Page++;
                }

                await csv.FlushAsync().ConfigureAwait(false);
            }

            return count;
        }

        private static CustomerCsvRow ToRow(Customer c)
        {
            Address? ship = c.ShippingAddress;
            return new CustomerCsvRow
            {
                CustomerNumber = c.CustomerNumber,
                Name = c.Name,
                ContactName = c.ContactName,
                Email = c.Email,
                Phone = c.Phone,
                BillingLine1 = c.BillingAddress.Line1,
                BillingLine2 = c.BillingAddress.Line2,
                BillingCity = c.BillingAddress.City,
                BillingRegion = c.BillingAddress.Region,
                BillingPostalCode = c.BillingAddress.PostalCode,
                BillingCountry = c.BillingAddress.Country,
                ShippingLine1 = ship?.Line1,
                ShippingLine2 = ship?.Line2,
                ShippingCity = ship?.City,
                ShippingRegion = ship?.Region,
                ShippingPostalCode = ship?.PostalCode,
                ShippingCountry = ship?.Country,
                PaymentTermsDays = c.PaymentTermsDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CreditLimit = c.CreditLimit.HasValue ? CsvFormat.Money(c.CreditLimit.Value) : string.Empty,
                Status = c.Status.ToString(),
                Notes = c.Notes
            };
        }

        private static ProductCsvRow ToRow(Product p)
        {
            return new ProductCsvRow
            {
                Sku = p.Sku,
                Name = p.Name,
                Description = p.Description,
                Category = p.Category,
                UnitOfMeasure = p.UnitOfMeasure,
                UnitPrice = CsvFormat.Money(p.UnitPrice),
                CostPrice = CsvFormat.Money(p.CostPrice),
                QuantityOnHand = CsvFormat.Quantity(p.QuantityOnHand),
                ReorderLevel = CsvFormat.Quantity(p.ReorderLevel),
                IsActive = p.IsActive ? "true" : "false"
            };
        }

        private static OrderCsvRow ToRow(OrderSummary s)
        {
            return new OrderCsvRow
            {
                OrderNumber = s.OrderNumber,
                Status = s.Status.ToString(),
                OrderDate = CsvFormat.Date(s.OrderDate),
                RequiredDate = CsvFormat.Date(s.RequiredDate),
                CustomerNumber = s.CustomerNumber,
                CustomerName = s.CustomerName,
                CustomerReference = s.CustomerReference,
                Lines = s.LineCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Subtotal = CsvFormat.Money(s.Subtotal),
                TaxAmount = CsvFormat.Money(s.TaxAmount),
                Total = CsvFormat.Money(s.Total),
                InvoiceNumber = s.InvoiceNumber,
                InvoiceStatus = s.InvoiceStatus?.ToString()
            };
        }

        private static InvoiceCsvRow ToRow(InvoiceSummary s)
        {
            return new InvoiceCsvRow
            {
                InvoiceNumber = s.InvoiceNumber,
                Status = s.Status.ToString(),
                OrderNumber = s.OrderNumber,
                CustomerNumber = s.CustomerNumber,
                CustomerName = s.CustomerName,
                IssuedDate = CsvFormat.Date(s.IssuedDate),
                DueDate = CsvFormat.Date(s.DueDate),
                Total = CsvFormat.Money(s.Total),
                AmountPaid = CsvFormat.Money(s.AmountPaid),
                Balance = CsvFormat.Money(s.Balance),
                DaysOverdue = s.DaysOverdue.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }
    }
}
