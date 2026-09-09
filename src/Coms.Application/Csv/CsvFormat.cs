using System;
using System.Globalization;
using CsvHelper.Configuration;

namespace Coms.Application.Csv
{
    /// <summary>
    /// One CSV dialect for the whole system: comma separated, invariant
    /// culture, ISO dates, header names matched case-insensitively.
    /// </summary>
    public static class CsvFormat
    {
        public const string DateFormat = "yyyy-MM-dd";

        public static CsvConfiguration Reader()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
                MissingFieldFound = null,
                HeaderValidated = null,
                BadDataFound = null,
                IgnoreBlankLines = true
            };
        }

        public static CsvConfiguration Writer()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                NewLine = "\r\n"
            };
        }

        public static string Money(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string Quantity(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Date(DateTime value)
        {
            return value.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static string Date(DateTime? value)
        {
            return value.HasValue ? Date(value.Value) : string.Empty;
        }
    }
}
