using System;
using System.Collections.Generic;
using Coms.Application.Reports;
using Coms.Domain.Reports;

namespace Coms.Web.ViewModels
{
    /// <summary>Date range parameters shared by the period reports.</summary>
    public abstract class PeriodReportModel
    {
        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public ReportPeriod Period
        {
            get
            {
                ReportPeriod fallback = ReportPeriod.LastTwelveMonths(DateTime.UtcNow.Date);
                return new ReportPeriod(From ?? fallback.FromDate, To ?? fallback.ToDate);
            }
        }

        public void ApplyDefaults()
        {
            ReportPeriod period = Period;
            From = period.FromDate;
            To = period.ToDate;
        }
    }

    public sealed class SalesByMonthModel : PeriodReportModel
    {
        public IReadOnlyList<SalesByMonthRow> Rows { get; set; } = new List<SalesByMonthRow>();
    }

    public sealed class TopCustomersModel : PeriodReportModel
    {
        public int Top { get; set; } = ReportService.DefaultTop;

        public IReadOnlyList<TopCustomerRow> Rows { get; set; } = new List<TopCustomerRow>();
    }

    public sealed class ProductSalesModel : PeriodReportModel
    {
        public IReadOnlyList<ProductSalesRow> Rows { get; set; } = new List<ProductSalesRow>();
    }

    public sealed class ArAgingModel
    {
        public DateTime? AsOf { get; set; }

        public IReadOnlyList<ArAgingRow> Rows { get; set; } = new List<ArAgingRow>();
    }

    public sealed class LowStockModel
    {
        public IReadOnlyList<LowStockRow> Rows { get; set; } = new List<LowStockRow>();
    }
}
