using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Orders;
using Coms.Domain.Products;

namespace Coms.Application.Csv
{
    public interface ICsvExportService
    {
        /// <summary>Every customer matching the filter, in the import file layout.</summary>
        Task<int> WriteCustomersAsync(TextWriter writer, CustomerFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Every product matching the filter, in the import file layout.</summary>
        Task<int> WriteProductsAsync(TextWriter writer, ProductFilter filter, CancellationToken cancellationToken = default);

        Task<int> WriteOrdersAsync(TextWriter writer, OrderFilter filter, CancellationToken cancellationToken = default);

        /// <summary>One row per line of one order, with the header fields repeated.</summary>
        Task<int> WriteOrderLinesAsync(TextWriter writer, int orderId, CancellationToken cancellationToken = default);

        Task<int> WriteInvoicesAsync(TextWriter writer, InvoiceFilter filter, CancellationToken cancellationToken = default);

        /// <summary>Any list of flat objects, e.g. report rows, with property names as headers.</summary>
        Task<int> WriteRecordsAsync<T>(TextWriter writer, IEnumerable<T> records, CancellationToken cancellationToken = default);
    }
}
