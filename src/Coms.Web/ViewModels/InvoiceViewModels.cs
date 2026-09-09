using System;
using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;

namespace Coms.Web.ViewModels
{
    public sealed class InvoiceListModel : ListQuery
    {
        public string? Search { get; set; }

        public InvoiceStatus? Status { get; set; }

        public int? CustomerId { get; set; }

        public bool Overdue { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public PagedResult<InvoiceSummary> Results { get; set; } = PagedResult<InvoiceSummary>.Empty(new PagedRequest());

        public InvoiceFilter ToFilter()
        {
            return new InvoiceFilter { Search = Search, Status = Status, CustomerId = CustomerId, OverdueOnly = Overdue, FromDate = From, ToDate = To };
        }
    }

    public sealed class InvoiceDetailsModel
    {
        public Invoice Invoice { get; set; } = new Invoice();

        public Order? Order { get; set; }

        public Customer? Customer { get; set; }

        public IReadOnlyList<Note> Notes { get; set; } = new List<Note>();
    }

    public sealed class CreateInvoiceModel
    {
        public int OrderId { get; set; }

        public DateTime IssuedDate { get; set; } = DateTime.UtcNow.Date;

        public string? RowVersion { get; set; }

        public Order? Order { get; set; }

        public Customer? Customer { get; set; }
    }

    public sealed class PaymentModel
    {
        public int InvoiceId { get; set; }

        public decimal Amount { get; set; }

        public DateTime PaidDate { get; set; } = DateTime.UtcNow.Date;

        public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;

        public string? Reference { get; set; }

        public string? Notes { get; set; }

        public string? RowVersion { get; set; }

        public Invoice? Invoice { get; set; }
    }

    public sealed class VoidInvoiceModel
    {
        public int Id { get; set; }

        public string? Comment { get; set; }

        public string? RowVersion { get; set; }

        public Invoice? Invoice { get; set; }
    }

    public sealed class InvoicePrintModel
    {
        public Invoice Invoice { get; set; } = new Invoice();

        public Order? Order { get; set; }

        public Customer? Customer { get; set; }
    }

    public sealed class StatementModel
    {
        public Customer Customer { get; set; } = new Customer();

        public CustomerBalance? Balance { get; set; }

        public IReadOnlyList<InvoiceSummary> Invoices { get; set; } = new List<InvoiceSummary>();

        public IReadOnlyList<Payment> Payments { get; set; } = new List<Payment>();
    }
}
