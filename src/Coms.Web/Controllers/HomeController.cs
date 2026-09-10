using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Coms.Application.Orders;
using Coms.Application.Reports;
using Coms.Web.Identity;
using Coms.Web.Models;
using Coms.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coms.Web.Controllers
{
    [Authorize(Policy = Policies.CanView)]
    public class HomeController : ComsController
    {
        private readonly IReportService _reports;
        private readonly IOrderService _orders;

        public HomeController(IReportService reports, IOrderService orders)
        {
            _reports = reports;
            _orders = orders;
        }

        public async Task<IActionResult> Index()
        {
            DateTime today = DateTime.UtcNow.Date;
            var model = new DashboardModel
            {
                Counts = await _reports.GetDashboardCountsAsync(),
                RecentOrders = await _orders.GetRecentAsync(10),
                OverdueInvoices = (await _reports.GetInvoiceAgingAsync(null)).Where(r => r.DaysOverdue > 0).Take(10).ToList(),
                LowStock = (await _reports.GetLowStockAsync()).Take(10).ToList()
            };

            var sales = await _reports.GetSalesByMonthAsync(ReportPeriod.LastTwelveMonths(today));
            if (sales.IsSuccess)
            {
                model.Sales = sales.Value;
            }

            return View(model);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };
            return View(model);
        }
    }
}
