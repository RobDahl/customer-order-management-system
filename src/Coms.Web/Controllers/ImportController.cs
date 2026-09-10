using System.IO;
using System.Text;
using System.Threading.Tasks;
using Coms.Application.Csv;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    /// <summary>
    /// CSV import. "Check file" runs the importer as a dry run and shows what
    /// would happen, row by row; "Import" runs it for real. Either way a
    /// file with any error changes nothing.
    /// </summary>
    [Authorize(Policy = Policies.CanEdit)]
    public class ImportController : ComsController
    {
        private const long MaxFileBytes = 5 * 1024 * 1024;

        private readonly ICsvImportService _import;

        public ImportController(ICsvImportService import)
        {
            _import = import;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Upload(string kind = "customers")
        {
            return View(new ImportModel { Kind = Normalise(kind) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileBytes)]
        public async Task<IActionResult> Upload(ImportModel model, string action)
        {
            model.Kind = Normalise(model.Kind);

            if (model.File == null || model.File.Length == 0)
            {
                ModelState.AddModelError(nameof(ImportModel.File), "Choose a CSV file to import.");
                return View(model);
            }

            var options = new CsvImportOptions { UpdateExisting = model.UpdateExisting, DryRun = action != "import" };

            using (Stream stream = model.File.OpenReadStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                model.Result = model.Kind == "products"
                    ? await _import.ImportProductsAsync(reader, options)
                    : await _import.ImportCustomersAsync(reader, options);
            }

            if (model.Result.Committed)
            {
                Flash(model.Result.Summary);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Template(string kind = "customers")
        {
            string header = Normalise(kind) == "products" ? _import.ProductTemplate : _import.CustomerTemplate;
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(header + "\r\n");
            return File(bytes, "text/csv; charset=utf-8", Normalise(kind) + "-template.csv");
        }

        private static string Normalise(string? kind)
        {
            return kind == "products" ? "products" : "customers";
        }
    }
}
