using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coms.Application.Csv;
using Coms.Application.Reports;
using Coms.Domain.Common;
using Coms.Domain.Reports;
using Coms.Web.Identity;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    /// <summary>
    /// Each report takes its parameters from the query string, renders a
    /// table, and with format=csv returns the same rows as a download.
    /// </summary>
    [Authorize(Policy = Policies.CanView)]
    public class ReportsController : ComsController
    {
        private const string CsvFormat = "csv";

        private readonly IReportService _reports;
        private readonly ICsvExportService _export;

        public ReportsController(IReportService reports, ICsvExportService export)
        {
            _reports = reports;
            _export = export;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SalesByMonth(SalesByMonthModel model, string? format)
        {
            model.ApplyDefaults();
            Result<IReadOnlyList<SalesByMonthRow>> result = await _reports.GetSalesByMonthAsync(model.Period);
            if (result.IsFailure)
            {
                AddErrors(result);
                return View(model);
            }

            model.Rows = result.Value;
            return format == CsvFormat
                ? await CsvFileAsync("sales-by-month", w => _export.WriteRecordsAsync(w, model.Rows))
                : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TopCustomers(TopCustomersModel model, string? format)
        {
            model.ApplyDefaults();
            Result<IReadOnlyList<TopCustomerRow>> result = await _reports.GetTopCustomersAsync(model.Period, model.Top);
            if (result.IsFailure)
            {
                AddErrors(result);
                return View(model);
            }

            model.Rows = result.Value;
            return format == CsvFormat
                ? await CsvFileAsync("top-customers", w => _export.WriteRecordsAsync(w, model.Rows))
                : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ProductSales(ProductSalesModel model, string? format)
        {
            model.ApplyDefaults();
            Result<IReadOnlyList<ProductSalesRow>> result = await _reports.GetProductSalesAsync(model.Period);
            if (result.IsFailure)
            {
                AddErrors(result);
                return View(model);
            }

            model.Rows = result.Value;
            return format == CsvFormat
                ? await CsvFileAsync("product-sales", w => _export.WriteRecordsAsync(w, model.Rows))
                : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ArAging(ArAgingModel model, string? format)
        {
            model.AsOf ??= DateTime.UtcNow.Date;
            model.Rows = await _reports.GetArAgingAsync(model.AsOf);
            return format == CsvFormat
                ? await CsvFileAsync("receivables-aging", w => _export.WriteRecordsAsync(w, model.Rows))
                : View(model);
        }

        [HttpGet]
        public async Task<IActionResult> LowStock(string? format)
        {
            var model = new LowStockModel { Rows = await _reports.GetLowStockAsync() };
            return format == CsvFormat
                ? await CsvFileAsync("low-stock", w => _export.WriteRecordsAsync(w, model.Rows))
                : View(model);
        }
    }
}
