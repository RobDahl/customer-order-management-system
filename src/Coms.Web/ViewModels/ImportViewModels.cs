using Coms.Application.Csv;
using Microsoft.AspNetCore.Http;

namespace Coms.Web.ViewModels
{
    public sealed class ImportModel
    {
        /// <summary>"customers" or "products".</summary>
        public string Kind { get; set; } = "customers";

        public IFormFile? File { get; set; }

        public bool UpdateExisting { get; set; }

        public CsvImportResult? Result { get; set; }

        public string Title => Kind == "products" ? "Import products" : "Import customers";

        public string[] RequiredColumns => Kind == "products" ? ProductCsvRow.RequiredHeaders : CustomerCsvRow.RequiredHeaders;

        public string[] AllColumns => Kind == "products" ? ProductCsvRow.Headers : CustomerCsvRow.Headers;
    }
}
