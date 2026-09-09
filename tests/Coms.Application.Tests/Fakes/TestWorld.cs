using System;
using System.Collections.Generic;
using Coms.Application.Csv;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Notes;
using Coms.Application.Orders;
using Coms.Application.Products;
using Coms.Application.Reports;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coms.Application.Tests.Fakes
{
    /// <summary>
    /// All fakes wired to all services, with a small seed: one active
    /// customer with a credit limit, one on-hold customer, three products
    /// (one inactive).
    /// </summary>
    internal sealed class TestWorld
    {
        public TestWorld()
        {
            Customers = new FakeCustomerRepository();
            Products = new FakeProductRepository();
            Orders = new FakeOrderRepository(Products, Customers);
            Invoices = new FakeInvoiceRepository(Orders, Customers);
            Notes = new FakeNoteRepository();
            Reports = new FakeReportRepository();
            UnitOfWork = new FakeUnitOfWork();
            CurrentUser = new FakeCurrentUser("tester");
            Options = new ComsOptions { TaxRate = 0.10m };

            ActiveCustomer = Customers.Add(new Customer
            {
                Name = "Acme Ltd",
                Email = "orders@acme.example",
                BillingAddress = new Address { Line1 = "1 High St", City = "Town", Country = "UK" },
                ShippingAddress = new Address { Line1 = "Dock 4", City = "Port", Country = "UK" },
                PaymentTermsDays = 30,
                CreditLimit = 1000m,
                Status = CustomerStatus.Active
            });

            OnHoldCustomer = Customers.Add(new Customer
            {
                Name = "Slowpay Inc",
                BillingAddress = new Address { Line1 = "2 Low St", City = "Town", Country = "UK" },
                Status = CustomerStatus.OnHold
            });

            Bolt = Products.Add(new Product { Sku = "BOLT-1", Name = "Bolt", Category = "Fasteners", UnitPrice = 10m, CostPrice = 4m, QuantityOnHand = 100, ReorderLevel = 10 });
            Nut = Products.Add(new Product { Sku = "NUT-1", Name = "Nut", Category = "Fasteners", UnitPrice = 2.5m, CostPrice = 1m, QuantityOnHand = 5, ReorderLevel = 10 });
            Retired = Products.Add(new Product { Sku = "OLD-1", Name = "Retired", Category = "Misc", UnitPrice = 1m, CostPrice = 1m, QuantityOnHand = 0, IsActive = false });

            CustomerService = new CustomerService(Customers, CurrentUser);
            ProductService = new ProductService(Products, CurrentUser, NullLogger<ProductService>.Instance);
            OrderService = new OrderService(Orders, Customers, Products, CurrentUser, Options, NullLogger<OrderService>.Instance);
            InvoiceService = new InvoiceService(Invoices, Invoices, Orders, CurrentUser, NullLogger<InvoiceService>.Instance);
            NoteService = new NoteService(Notes, Customers, Orders, Invoices, CurrentUser);
            ReportService = new ReportService(Reports);
            CsvImport = new CsvImportService(Customers, Products, UnitOfWork, CurrentUser, Options, NullLogger<CsvImportService>.Instance);
            CsvExport = new CsvExportService(Customers, Products, Orders, Invoices);
        }

        public FakeCustomerRepository Customers { get; }
        public FakeProductRepository Products { get; }
        public FakeOrderRepository Orders { get; }
        public FakeInvoiceRepository Invoices { get; }
        public FakeNoteRepository Notes { get; }
        public FakeReportRepository Reports { get; }
        public FakeUnitOfWork UnitOfWork { get; }
        public FakeCurrentUser CurrentUser { get; }
        public ComsOptions Options { get; }

        public Customer ActiveCustomer { get; }
        public Customer OnHoldCustomer { get; }
        public Product Bolt { get; }
        public Product Nut { get; }
        public Product Retired { get; }

        public CustomerService CustomerService { get; }
        public ProductService ProductService { get; }
        public OrderService OrderService { get; }
        public InvoiceService InvoiceService { get; }
        public NoteService NoteService { get; }
        public ReportService ReportService { get; }
        public CsvImportService CsvImport { get; }
        public CsvExportService CsvExport { get; }

        public OrderInput NewInput(int? customerId = null, params (Product Product, decimal Quantity)[] lines)
        {
            var input = new OrderInput
            {
                CustomerId = customerId ?? ActiveCustomer.Id,
                OrderDate = new DateTime(2026, 9, 9),
                RequiredDate = new DateTime(2026, 9, 16),
                CustomerReference = "PO-1"
            };

            if (lines.Length == 0)
            {
                lines = new[] { (Bolt, 2m), (Nut, 4m) };
            }

            foreach ((Product product, decimal quantity) in lines)
            {
                input.Lines.Add(new OrderLineInput { ProductId = product.Id, Quantity = quantity });
            }

            return input;
        }

        /// <summary>Creates an order directly in the fake at the given status, bypassing the service.</summary>
        public Order SeedOrder(OrderStatus status, int? customerId = null, params (Product Product, decimal Quantity)[] lines)
        {
            if (lines.Length == 0)
            {
                lines = new[] { (Bolt, 2m), (Nut, 4m) };
            }

            var order = new Order
            {
                CustomerId = customerId ?? ActiveCustomer.Id,
                Status = status,
                OrderDate = new DateTime(2026, 9, 1),
                TaxRate = Options.TaxRate,
                ShipTo = ActiveCustomer.EffectiveShippingAddress.Copy()
            };

            foreach ((Product product, decimal quantity) in lines)
            {
                order.Lines.Add(new OrderLine
                {
                    ProductId = product.Id,
                    Sku = product.Sku,
                    Description = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.UnitPrice,
                    UnitCost = product.CostPrice
                });
            }

            order.Recalculate();
            return Orders.Add(order);
        }

        public void SetBalance(int customerId, decimal creditLimit, decimal outstanding, decimal committed)
        {
            Customers.Balances[customerId] = new CustomerBalance
            {
                CustomerId = customerId,
                CreditLimit = creditLimit,
                OutstandingBalance = outstanding,
                CommittedTotal = committed,
                Exposure = outstanding + committed,
                CreditAvailable = creditLimit - outstanding - committed
            };
        }

        public static IReadOnlyList<ValidationError> ErrorsOf(Result result)
        {
            return result.Errors;
        }
    }
}
