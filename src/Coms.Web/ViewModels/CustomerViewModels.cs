using System.Collections.Generic;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;

namespace Coms.Web.ViewModels
{
    public sealed class CustomerListModel : ListQuery
    {
        public string? Search { get; set; }

        public CustomerStatus? Status { get; set; }

        public PagedResult<Customer> Results { get; set; } = PagedResult<Customer>.Empty(new PagedRequest());

        public CustomerFilter ToFilter()
        {
            return new CustomerFilter { Search = Search, Status = Status };
        }
    }

    public sealed class CustomerDetailsModel
    {
        public Customer Customer { get; set; } = new Customer();

        public CustomerBalance? Balance { get; set; }

        public IReadOnlyList<OrderSummary> RecentOrders { get; set; } = new List<OrderSummary>();

        public IReadOnlyList<InvoiceSummary> RecentInvoices { get; set; } = new List<InvoiceSummary>();

        public IReadOnlyList<Note> Notes { get; set; } = new List<Note>();
    }

    public sealed class ChangeStatusModel
    {
        public int Id { get; set; }

        public CustomerStatus Status { get; set; }

        public string? RowVersion { get; set; }
    }

    public sealed class AddNoteModel
    {
        public NoteEntityType EntityType { get; set; }

        public int EntityId { get; set; }

        public string Body { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
