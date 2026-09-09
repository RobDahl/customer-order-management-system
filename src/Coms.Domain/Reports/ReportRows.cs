using System;
using Coms.Domain.Invoices;

namespace Coms.Domain.Reports
{
    /* Row shapes returned by the rpt schema. Property names match the
       column names of the corresponding view or procedure. */

    public sealed class DashboardCounts
    {
        public int DraftOrders { get; set; }
        public int SubmittedOrders { get; set; }
        public int ApprovedOrders { get; set; }
        public int FulfilledOrders { get; set; }
        public int OrdersToday { get; set; }
        public decimal OrdersTodayValue { get; set; }
        public int OpenInvoices { get; set; }
        public decimal OpenInvoiceBalance { get; set; }
        public int OverdueInvoices { get; set; }
        public decimal OverdueBalance { get; set; }
        public int LowStockProducts { get; set; }
        public int ActiveCustomers { get; set; }
        public int OnHoldCustomers { get; set; }
        public DateTime AsOfDate { get; set; }
    }

    public sealed class SalesByMonthRow
    {
        public DateTime MonthStart { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public sealed class TopCustomerRow
    {
        public long Rank { get; set; }
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Total { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal SharePercent { get; set; }
        public DateTime LastOrderDate { get; set; }
    }

    public sealed class ArAgingRow
    {
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int PaymentTermsDays { get; set; }
        public int InvoiceCount { get; set; }
        public decimal NotYetDue { get; set; }
        public decimal Days1To30 { get; set; }
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Over90 { get; set; }
        public decimal TotalOutstanding { get; set; }
        public DateTime OldestDueDate { get; set; }
    }

    public sealed class ProductSalesRow
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UnitOfMeasure { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int OrderCount { get; set; }
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal MarginPercent { get; set; }
    }

    public sealed class LowStockRow
    {
        public int ProductId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal PendingDemand { get; set; }
        public decimal ProjectedOnHand { get; set; }
        public decimal Shortfall { get; set; }
    }

    public sealed class InvoiceAgingRow
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime IssuedDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Total { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance { get; set; }
        public InvoiceStatus Status { get; set; }
        public int DaysOverdue { get; set; }
        public string AgeBucket { get; set; } = string.Empty;
    }
}
