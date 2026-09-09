using System;
using System.Collections.Generic;
using System.Linq;
using Coms.Domain.Common;

namespace Coms.Domain.Orders
{
    public class Order : AuditedEntity
    {
        /// <summary>Assigned by the database on insert (ORD-2026-000042).</summary>
        public string OrderNumber { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Draft;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow.Date;

        public DateTime? RequiredDate { get; set; }

        public string? CustomerReference { get; set; }

        /// <summary>Snapshot of where the goods go; null until set from the customer.</summary>
        public Address? ShipTo { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TaxRate { get; set; }

        public decimal TaxAmount { get; set; }

        public decimal Total { get; set; }

        public string? Notes { get; set; }

        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();

        public List<OrderStatusChange> History { get; set; } = new List<OrderStatusChange>();

        public bool CanEdit => OrderStatusMachine.AllowsEditing(Status);

        public bool IsTerminal => OrderStatusMachine.IsTerminal(Status);

        public bool CanTransitionTo(OrderStatus target)
        {
            return OrderStatusMachine.CanTransition(Status, target);
        }

        /// <summary>Renumbers lines 1..n and recomputes every stored total.</summary>
        public void Recalculate()
        {
            int number = 1;
            decimal subtotal = 0;

            foreach (OrderLine line in Lines)
            {
                line.LineNumber = number++;
                line.Recalculate();
                subtotal += line.LineTotal;
            }

            Subtotal = Money.Round(subtotal);
            TaxAmount = Money.Round(Subtotal * TaxRate);
            Total = Subtotal + TaxAmount;
        }

        public void Normalize()
        {
            CustomerReference = string.IsNullOrWhiteSpace(CustomerReference) ? null : CustomerReference!.Trim();
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim();
            OrderDate = OrderDate.Date;
            RequiredDate = RequiredDate?.Date;

            ShipTo?.Normalize();

            foreach (OrderLine line in Lines)
            {
                line.Description = (line.Description ?? string.Empty).Trim();
                line.Sku = (line.Sku ?? string.Empty).Trim().ToUpperInvariant();
            }
        }

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (CustomerId <= 0)
            {
                errors.Add(new ValidationError(nameof(CustomerId), "A customer is required."));
            }

            if (RequiredDate.HasValue && RequiredDate.Value.Date < OrderDate.Date)
            {
                errors.Add(new ValidationError(nameof(RequiredDate), "Required date cannot be before the order date."));
            }

            if (CustomerReference != null && CustomerReference.Length > 50)
            {
                errors.Add(new ValidationError(nameof(CustomerReference), "Customer reference must be 50 characters or fewer."));
            }

            if (TaxRate < 0 || TaxRate >= 1)
            {
                errors.Add(new ValidationError(nameof(TaxRate), "Tax rate must be between 0 and 1 (e.g. 0.08 for 8%)."));
            }

            if (ShipTo != null && !ShipTo.IsEmpty)
            {
                ShipTo.Validate(nameof(ShipTo), errors);
            }

            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Validate("Lines[" + i + "].", errors);
            }

            if (Lines.Select(l => l.LineNumber).Distinct().Count() != Lines.Count)
            {
                errors.Add(new ValidationError(nameof(Lines), "Line numbers must be unique."));
            }

            return errors;
        }
    }
}
