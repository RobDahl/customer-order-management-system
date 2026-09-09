using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Domain.Customers
{
    public class Customer : AuditedEntity
    {
        public const int DefaultPaymentTermsDays = 30;

        /// <summary>Assigned by the database on insert (CUST-000123).</summary>
        public string CustomerNumber { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? ContactName { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public Address BillingAddress { get; set; } = new Address();

        /// <summary>Null means "same as billing".</summary>
        public Address? ShippingAddress { get; set; }

        public int PaymentTermsDays { get; set; } = DefaultPaymentTermsDays;

        public decimal? CreditLimit { get; set; }

        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        public string? Notes { get; set; }

        public bool CanOrder => Status == CustomerStatus.Active;

        /// <summary>Address goods ship to: the shipping address when set, otherwise billing.</summary>
        public Address EffectiveShippingAddress => ShippingAddress ?? BillingAddress;

        /// <summary>Trims text and turns an all-blank shipping address into null. Tolerates nulls from model binding.</summary>
        public void Normalize()
        {
            Name = (Name ?? string.Empty).Trim();
            BillingAddress ??= new Address();
            BillingAddress.Normalize();
            ShippingAddress?.Normalize();
            ContactName = TrimToNull(ContactName);
            Email = TrimToNull(Email);
            Phone = TrimToNull(Phone);
            Notes = TrimToNull(Notes);

            if (ShippingAddress != null && ShippingAddress.IsEmpty)
            {
                ShippingAddress = null;
            }
        }

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add(new ValidationError(nameof(Name), "Name is required."));
            }
            else if (Name.Length > 200)
            {
                errors.Add(new ValidationError(nameof(Name), "Name must be 200 characters or fewer."));
            }

            if (ContactName != null && ContactName.Length > 100)
            {
                errors.Add(new ValidationError(nameof(ContactName), "Contact name must be 100 characters or fewer."));
            }

            if (Email != null)
            {
                if (Email.Length > 254)
                {
                    errors.Add(new ValidationError(nameof(Email), "Email must be 254 characters or fewer."));
                }
                else if (!LooksLikeEmail(Email))
                {
                    errors.Add(new ValidationError(nameof(Email), "Email address is not valid."));
                }
            }

            if (Phone != null && Phone.Length > 30)
            {
                errors.Add(new ValidationError(nameof(Phone), "Phone must be 30 characters or fewer."));
            }

            BillingAddress.Validate(nameof(BillingAddress), errors);

            if (ShippingAddress != null && !ShippingAddress.IsEmpty)
            {
                ShippingAddress.Validate(nameof(ShippingAddress), errors);
            }

            if (PaymentTermsDays < 0 || PaymentTermsDays > 365)
            {
                errors.Add(new ValidationError(nameof(PaymentTermsDays), "Payment terms must be between 0 and 365 days."));
            }

            if (CreditLimit.HasValue && CreditLimit.Value < 0)
            {
                errors.Add(new ValidationError(nameof(CreditLimit), "Credit limit cannot be negative."));
            }

            return errors;
        }

        private static bool LooksLikeEmail(string value)
        {
            int at = value.IndexOf('@');
            return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0 && !value.Contains(" ");
        }

        private static string? TrimToNull(string? value)
        {
            if (value == null)
            {
                return null;
            }

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
