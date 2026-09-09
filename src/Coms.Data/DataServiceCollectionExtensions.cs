using System;
using Coms.Data.Connections;
using Coms.Data.Repositories;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Coms.Domain.Products;
using Coms.Domain.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Coms.Data
{
    public static class DataServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the connection factory, a scoped session (one connection
        /// and optional transaction per request or desktop action) and every
        /// repository against its domain interface.
        /// </summary>
        public static IServiceCollection AddComsData(this IServiceCollection services, string connectionString)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

            services.AddScoped<DbSession>();
            services.AddScoped<IDbSession>(provider => provider.GetRequiredService<DbSession>());
            services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DbSession>());

            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<INoteRepository, NoteRepository>();
            services.AddScoped<IReportRepository, ReportRepository>();

            return services;
        }
    }
}
