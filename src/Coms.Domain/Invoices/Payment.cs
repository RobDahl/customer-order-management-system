using System;
using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Domain.Invoices
{
    public class Payment : AuditedEntity
    {
        public int InvoiceId { get; set; }

        public DateTime PaidDate { get; set; }

        public decimal Amount { get; set; }

        public PaymentMethod Method { get; set; }

        public string? Reference { get; set; }

        public string? Notes { get; set; }

        public void Normalize()
        {
            Reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference!.Trim();
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes!.Trim();
            PaidDate = PaidDate.Date;
        }

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (InvoiceId <= 0)
            {
                errors.Add(new ValidationError(nameof(InvoiceId), "An invoice is required."));
            }

            if (Amount <= 0)
            {
                errors.Add(new ValidationError(nameof(Amount), "Amount must be greater than zero."));
            }

            if (PaidDate == default)
            {
                errors.Add(new ValidationError(nameof(PaidDate), "Paid date is required."));
            }

            if (Reference != null && Reference.Length > 50)
            {
                errors.Add(new ValidationError(nameof(Reference), "Reference must be 50 characters or fewer."));
            }

            if (Notes != null && Notes.Length > 500)
            {
                errors.Add(new ValidationError(nameof(Notes), "Notes must be 500 characters or fewer."));
            }

            return errors;
        }
    }
}
