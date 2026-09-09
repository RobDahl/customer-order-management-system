namespace Coms.Application
{
    /// <summary>
    /// Business settings that vary per installation. Bound from
    /// appsettings.json in the web host and App.config in the desktop client.
    /// </summary>
    public sealed class ComsOptions
    {
        /// <summary>Single tax rate applied to every order, as a fraction (0.08 = 8%).</summary>
        public decimal TaxRate { get; set; } = 0.08m;

        /// <summary>When true, fulfilment may drive stock below zero.</summary>
        public bool AllowNegativeStock { get; set; }

        /// <summary>Maximum rows a CSV import will accept in one file.</summary>
        public int MaxImportRows { get; set; } = 10000;
    }
}
