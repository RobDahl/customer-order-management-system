using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Coms.Application.Csv
{
    public interface ICsvImportService
    {
        /// <summary>Header line for a customer import file.</summary>
        string CustomerTemplate { get; }

        /// <summary>Header line for a product import file.</summary>
        string ProductTemplate { get; }

        /// <summary>
        /// Reads every row, validates all of them, and only then writes them
        /// in one transaction. A file with any error imports nothing.
        /// </summary>
        Task<CsvImportResult> ImportCustomersAsync(TextReader reader, CsvImportOptions options, CancellationToken cancellationToken = default);

        Task<CsvImportResult> ImportProductsAsync(TextReader reader, CsvImportOptions options, CancellationToken cancellationToken = default);
    }
}
