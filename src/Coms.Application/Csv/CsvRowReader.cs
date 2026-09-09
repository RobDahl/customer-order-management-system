using System.Collections.Generic;
using System.Globalization;
using CsvHelper;

namespace Coms.Application.Csv
{
    /// <summary>
    /// Reads typed values out of the current CSV record, collecting one
    /// error per bad field instead of throwing. Blank cells read as null.
    /// </summary>
    internal sealed class CsvRowReader
    {
        private readonly CsvReader _csv;
        private readonly int _rowNumber;
        private readonly List<CsvRowError> _errors;

        public CsvRowReader(CsvReader csv, int rowNumber, List<CsvRowError> errors)
        {
            _csv = csv;
            _rowNumber = rowNumber;
            _errors = errors;
        }

        public string? Text(string field)
        {
            if (!_csv.TryGetField(field, out string? value))
            {
                return null;
            }

            string? trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        public string Required(string field)
        {
            string? value = Text(field);
            if (value == null)
            {
                _errors.Add(new CsvRowError(_rowNumber, field, "Value is required."));
                return string.Empty;
            }

            return value;
        }

        public int? Int(string field)
        {
            string? text = Text(field);
            if (text == null)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            _errors.Add(new CsvRowError(_rowNumber, field, "'" + text + "' is not a whole number."));
            return null;
        }

        public decimal? Decimal(string field)
        {
            string? text = Text(field);
            if (text == null)
            {
                return null;
            }

            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
            {
                return value;
            }

            _errors.Add(new CsvRowError(_rowNumber, field, "'" + text + "' is not a number."));
            return null;
        }

        public bool? Bool(string field)
        {
            string? text = Text(field);
            if (text == null)
            {
                return null;
            }

            switch (text.ToLowerInvariant())
            {
                case "1":
                case "true":
                case "yes":
                case "y":
                    return true;
                case "0":
                case "false":
                case "no":
                case "n":
                    return false;
                default:
                    _errors.Add(new CsvRowError(_rowNumber, field, "'" + text + "' is not yes/no."));
                    return null;
            }
        }

        public T? Enum<T>(string field) where T : struct
        {
            string? text = Text(field);
            if (text == null)
            {
                return null;
            }

            if (System.Enum.TryParse(text, ignoreCase: true, out T value) && System.Enum.IsDefined(typeof(T), value))
            {
                return value;
            }

            _errors.Add(new CsvRowError(_rowNumber, field, "'" + text + "' is not one of: " + string.Join(", ", System.Enum.GetNames(typeof(T))) + "."));
            return null;
        }
    }
}
