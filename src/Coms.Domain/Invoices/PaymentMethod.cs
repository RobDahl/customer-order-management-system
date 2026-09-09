namespace Coms.Domain.Invoices
{
    /// <summary>Stored as text; names must match CK_Payments_Method.</summary>
    public enum PaymentMethod
    {
        Cash,
        Cheque,
        BankTransfer,
        Card
    }
}
