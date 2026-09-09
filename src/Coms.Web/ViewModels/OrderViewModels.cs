using System;
using System.Collections.Generic;
using Coms.Application.Orders;
using Coms.Domain.Common;
using Coms.Domain.Customers;
using Coms.Domain.Invoices;
using Coms.Domain.Notes;
using Coms.Domain.Orders;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Coms.Web.ViewModels
{
    public sealed class OrderListModel : ListQuery
    {
        public string? Search { get; set; }

        public OrderStatus? Status { get; set; }

        public int? CustomerId { get; set; }

        public DateTime? From { get; set; }

        public DateTime? To { get; set; }

        public string? CustomerName { get; set; }

        public PagedResult<OrderSummary> Results { get; set; } = PagedResult<OrderSummary>.Empty(new PagedRequest());

        public OrderFilter ToFilter()
        {
            return new OrderFilter { Search = Search, Status = Status, CustomerId = CustomerId, FromDate = From, ToDate = To };
        }
    }

    public sealed class OrderDetailsModel
    {
        public Order Order { get; set; } = new Order();

        public Customer? Customer { get; set; }

        public Invoice? Invoice { get; set; }

        public IReadOnlyList<Note> Notes { get; set; } = new List<Note>();

        public IReadOnlyList<OrderStatus> AllowedTransitions { get; set; } = new List<OrderStatus>();
    }

    /// <summary>
    /// The order entry form. Lines are added and removed by posting the
    /// form back with an action name, so the screen works without script.
    /// </summary>
    public sealed class OrderFormModel
    {
        public int Id { get; set; }

        public string? OrderNumber { get; set; }

        public string? RowVersion { get; set; }

        public OrderInput Input { get; set; } = new OrderInput();

        /// <summary>Totals computed from the current lines, when they are valid.</summary>
        public Order? Preview { get; set; }

        public IReadOnlyList<SelectListItem> Customers { get; set; } = new List<SelectListItem>();

        public IReadOnlyList<SelectListItem> Products { get; set; } = new List<SelectListItem>();

        public bool IsNew => Id == 0;
    }

    public sealed class TransitionModel
    {
        public int Id { get; set; }

        public OrderStatus To { get; set; }

        public string? Comment { get; set; }

        public string? RowVersion { get; set; }

        public Order? Order { get; set; }

        public string Verb
        {
            get
            {
                switch (To)
                {
                    case OrderStatus.Submitted: return "Submit";
                    case OrderStatus.Approved: return "Approve";
                    case OrderStatus.Fulfilled: return "Fulfil";
                    case OrderStatus.Cancelled: return "Cancel";
                    default: return To.ToString();
                }
            }
        }
    }

    public sealed class OrderPrintModel
    {
        public Order Order { get; set; } = new Order();

        public Customer? Customer { get; set; }

        /// <summary>"confirmation" or "picklist".</summary>
        public string Kind { get; set; } = "confirmation";
    }
}
