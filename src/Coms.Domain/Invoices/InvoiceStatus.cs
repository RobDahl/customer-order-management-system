namespace Coms.Domain.Invoices
{
    /// <summary>Stored as text; names must match CK_Invoices_Status.</summary>
    public enum InvoiceStatus
    {
        Open,
        PartiallyPaid,
        Paid,
        Void
    }
}
