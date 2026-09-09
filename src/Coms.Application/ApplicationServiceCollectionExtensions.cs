using System;
using Coms.Application.Csv;
using Coms.Application.Customers;
using Coms.Application.Invoices;
using Coms.Application.Notes;
using Coms.Application.Orders;
using Coms.Application.Products;
using Coms.Application.Reports;
using Microsoft.Extensions.DependencyInjection;

namespace Coms.Application
{
    public static class ApplicationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the application services. The host must also register
        /// the repositories (see AddComsData) and an <see cref="ICurrentUser"/>.
        /// </summary>
        public static IServiceCollection AddComsApplication(this IServiceCollection services, ComsOptions options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddSingleton(options ?? new ComsOptions());

            services.AddScoped<ICustomerService, CustomerService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IInvoiceService, InvoiceService>();
            services.AddScoped<INoteService, NoteService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<ICsvImportService, CsvImportService>();
            services.AddScoped<ICsvExportService, CsvExportService>();

            return services;
        }
    }
}
