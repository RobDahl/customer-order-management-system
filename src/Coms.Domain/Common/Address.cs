using System.Collections.Generic;

namespace Coms.Domain.Common
{
    /// <summary>
    /// Postal address used for customer billing and shipping and copied
    /// onto orders as a ship-to snapshot.
    /// </summary>
    public sealed class Address
    {
        public string Line1 { get; set; } = string.Empty;

        public string? Line2 { get; set; }

        public string City { get; set; } = string.Empty;

        public string? Region { get; set; }

        public string? PostalCode { get; set; }

        public string Country { get; set; } = string.Empty;

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Line1)
            && string.IsNullOrWhiteSpace(Line2)
            && string.IsNullOrWhiteSpace(City)
            && string.IsNullOrWhiteSpace(Region)
            && string.IsNullOrWhiteSpace(PostalCode)
            && string.IsNullOrWhiteSpace(Country);

        /// <summary>Trims every part and replaces nulls from model binding with empty strings.</summary>
        public void Normalize()
        {
            Line1 = (Line1 ?? string.Empty).Trim();
            Line2 = TrimToNull(Line2);
            City = (City ?? string.Empty).Trim();
            Region = TrimToNull(Region);
            PostalCode = TrimToNull(PostalCode);
            Country = (Country ?? string.Empty).Trim();
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

        public Address Copy()
        {
            return new Address
            {
                Line1 = Line1,
                Line2 = Line2,
                City = City,
                Region = Region,
                PostalCode = PostalCode,
                Country = Country
            };
        }

        /// <summary>Validates a required address. <paramref name="prefix"/> names the owning field, e.g. "BillingAddress".</summary>
        public void Validate(string prefix, ICollection<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(Line1))
            {
                errors.Add(new ValidationError(prefix + ".Line1", "Address line 1 is required."));
            }
            else if (Line1.Length > 100)
            {
                errors.Add(new ValidationError(prefix + ".Line1", "Address line 1 must be 100 characters or fewer."));
            }

            if (Line2 != null && Line2.Length > 100)
            {
                errors.Add(new ValidationError(prefix + ".Line2", "Address line 2 must be 100 characters or fewer."));
            }

            if (string.IsNullOrWhiteSpace(City))
            {
                errors.Add(new ValidationError(prefix + ".City", "City is required."));
            }
            else if (City.Length > 80)
            {
                errors.Add(new ValidationError(prefix + ".City", "City must be 80 characters or fewer."));
            }

            if (Region != null && Region.Length > 80)
            {
                errors.Add(new ValidationError(prefix + ".Region", "Region must be 80 characters or fewer."));
            }

            if (PostalCode != null && PostalCode.Length > 20)
            {
                errors.Add(new ValidationError(prefix + ".PostalCode", "Postal code must be 20 characters or fewer."));
            }

            if (string.IsNullOrWhiteSpace(Country))
            {
                errors.Add(new ValidationError(prefix + ".Country", "Country is required."));
            }
            else if (Country.Length > 60)
            {
                errors.Add(new ValidationError(prefix + ".Country", "Country must be 60 characters or fewer."));
            }
        }

        public override string ToString()
        {
            var parts = new List<string>();
            AddIfPresent(parts, Line1);
            AddIfPresent(parts, Line2);
            AddIfPresent(parts, City);
            AddIfPresent(parts, Region);
            AddIfPresent(parts, PostalCode);
            AddIfPresent(parts, Country);
            return string.Join(", ", parts);
        }

        private static void AddIfPresent(ICollection<string> parts, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value!.Trim());
            }
        }
    }
}
