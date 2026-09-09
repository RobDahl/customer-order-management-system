using System;
using System.Collections.Generic;
using Coms.Domain.Common;

namespace Coms.Application.Reports
{
    /// <summary>An inclusive date range for a report.</summary>
    public sealed class ReportPeriod
    {
        public const int MaxDays = 366 * 5;

        public ReportPeriod(DateTime fromDate, DateTime toDate)
        {
            FromDate = fromDate.Date;
            ToDate = toDate.Date;
        }

        public DateTime FromDate { get; }

        public DateTime ToDate { get; }

        public int Days => (ToDate - FromDate).Days + 1;

        /// <summary>The twelve full months ending with the month of <paramref name="today"/>.</summary>
        public static ReportPeriod LastTwelveMonths(DateTime today)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1);
            return new ReportPeriod(monthStart.AddMonths(-11), monthStart.AddMonths(1).AddDays(-1));
        }

        public static ReportPeriod YearToDate(DateTime today)
        {
            return new ReportPeriod(new DateTime(today.Year, 1, 1), today);
        }

        public IReadOnlyList<ValidationError> Validate()
        {
            var errors = new List<ValidationError>();

            if (FromDate > ToDate)
            {
                errors.Add(new ValidationError(nameof(FromDate), "From date must not be after to date."));
            }
            else if (Days > MaxDays)
            {
                errors.Add(new ValidationError(nameof(ToDate), "The period cannot exceed five years."));
            }

            return errors;
        }
    }
}
