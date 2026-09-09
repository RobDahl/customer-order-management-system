using Coms.Domain.Common;
using Microsoft.Data.SqlClient;

namespace Coms.Data.Repositories
{
    /// <summary>
    /// Translates the THROW numbers raised by the workflow procedures
    /// (see db/migrations/0008) into result codes. Anything outside the
    /// 50000 range is a real fault and is left to propagate.
    /// </summary>
    internal static class SqlErrorMapper
    {
        public static bool IsBusinessError(SqlException exception)
        {
            return exception.Number >= 50000 && exception.Number < 51000;
        }

        public static Result ToResult(SqlException exception)
        {
            ErrorCode code = exception.Number switch
            {
                50000 => ErrorCode.NumberSeriesExhausted,
                50001 => ErrorCode.NotFound,
                50002 => ErrorCode.InvalidStatus,
                50003 => ErrorCode.InsufficientStock,
                50004 => ErrorCode.NotFound,
                50005 => ErrorCode.InvoiceExists,
                50006 => ErrorCode.InvoiceNotOpen,
                50007 => ErrorCode.PaymentExceedsBalance,
                50008 => ErrorCode.InvoiceHasPayments,
                50009 => ErrorCode.NoLines,
                50010 => ErrorCode.Conflict,
                50020 => ErrorCode.Validation,
                _ => ErrorCode.Unexpected
            };

            return Result.Failure(code, exception.Message);
        }

        public static Result<T> ToResult<T>(SqlException exception)
        {
            return Result<T>.From(ToResult(exception));
        }
    }
}
