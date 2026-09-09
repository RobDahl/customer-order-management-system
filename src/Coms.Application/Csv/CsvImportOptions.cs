namespace Coms.Application.Csv
{
    public sealed class CsvImportOptions
    {
        /// <summary>When a row matches an existing record (by number or SKU), overwrite it instead of reporting an error.</summary>
        public bool UpdateExisting { get; set; }

        /// <summary>Validate and report what would happen without writing anything.</summary>
        public bool DryRun { get; set; }
    }
}
