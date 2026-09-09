namespace Coms.Domain.Common
{
    /// <summary>
    /// Why an operation did not succeed. Codes are stable identifiers that
    /// both clients can switch on; the accompanying message is for people.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        Validation,
        NotFound,
        Conflict,
        InvalidStatus,
        NoLines,
        InsufficientStock,
        InvoiceExists,
        InvoiceNotOpen,
        PaymentExceedsBalance,
        InvoiceHasPayments,
        CreditLimitExceeded,
        CustomerNotActive,
        ProductInactive,
        NumberSeriesExhausted,
        Unexpected
    }
}
