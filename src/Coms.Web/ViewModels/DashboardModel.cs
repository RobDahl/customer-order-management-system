using System.Collections.Generic;
using System.Linq;
using Coms.Domain.Orders;
using Coms.Domain.Reports;

namespace Coms.Web.ViewModels
{
    public sealed class DashboardModel
    {
        public DashboardCounts Counts { get; set; } = new DashboardCounts();

        public IReadOnlyList<OrderSummary> RecentOrders { get; set; } = new List<OrderSummary>();

        public IReadOnlyList<InvoiceAgingRow> OverdueInvoices { get; set; } = new List<InvoiceAgingRow>();

        public IReadOnlyList<LowStockRow> LowStock { get; set; } = new List<LowStockRow>();

        public IReadOnlyList<SalesByMonthRow> Sales { get; set; } = new List<SalesByMonthRow>();

        public decimal SalesMax => Sales.Count == 0 ? 0 : Sales.Max(s => s.Total);

        /// <summary>Bar width for a month as a percentage of the best month.</summary>
        public int BarPercent(SalesByMonthRow row)
        {
            return SalesMax == 0 ? 0 : (int)(row.Total * 100 / SalesMax);
        }
    }
}
