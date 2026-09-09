using System.Collections.Generic;
using System.Linq;

namespace Coms.Application.Csv
{
    public sealed class CsvRowError
    {
        public CsvRowError(int rowNumber, string field, string message)
        {
            RowNumber = rowNumber;
            Field = field;
            Message = message;
        }

        /// <summary>1-based line in the file, counting the header as line 1. 0 means the file as a whole.</summary>
        public int RowNumber { get; }

        public string Field { get; }

        public string Message { get; }

        public override string ToString()
        {
            string where = RowNumber == 0 ? "File" : "Row " + RowNumber;
            return Field.Length == 0 ? where + ": " + Message : where + ", " + Field + ": " + Message;
        }
    }

    /// <summary>
    /// Outcome of an import. Either every row was accepted and (unless it was
    /// a dry run) written in one transaction, or nothing was written and
    /// <see cref="Errors"/> says why, row by row.
    /// </summary>
    public sealed class CsvImportResult
    {
        public CsvImportResult(string entityName)
        {
            EntityName = entityName;
        }

        public string EntityName { get; }

        public int TotalRows { get; set; }

        public int ToInsert { get; set; }

        public int ToUpdate { get; set; }

        public bool DryRun { get; set; }

        public bool Committed { get; set; }

        public List<CsvRowError> Errors { get; } = new List<CsvRowError>();

        public bool HasErrors => Errors.Count > 0;

        public int RowsWithErrors => Errors.Where(e => e.RowNumber > 0).Select(e => e.RowNumber).Distinct().Count();

        public string Summary
        {
            get
            {
                if (HasErrors)
                {
                    return TotalRows + " " + EntityName + " row(s) read; " + Errors.Count + " error(s) in " + RowsWithErrors + " row(s). Nothing was imported.";
                }

                string action = Committed ? "Imported" : DryRun ? "Would import" : "Not imported";
                return action + " " + ToInsert + " new and " + ToUpdate + " updated " + EntityName + " record(s) from " + TotalRows + " row(s).";
            }
        }
    }
}
