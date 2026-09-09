namespace Coms.Domain.Orders
{
    /// <summary>Stored as text; names must match CK_Orders_Status.</summary>
    public enum OrderStatus
    {
        Draft,
        Submitted,
        Approved,
        Fulfilled,
        Invoiced,
        Cancelled
    }
}
