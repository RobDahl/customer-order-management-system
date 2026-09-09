using System;

namespace Coms.Domain.Common
{
    /// <summary>
    /// The single place where monetary rounding happens. Every total in the
    /// system goes through <see cref="Round"/> so that the web client, the
    /// desktop client and the database agree to the penny.
    /// </summary>
    public static class Money
    {
        public const int Scale = 2;

        public static decimal Round(decimal amount)
        {
            return Math.Round(amount, Scale, MidpointRounding.AwayFromZero);
        }

        public static decimal ApplyPercent(decimal amount, decimal percent)
        {
            return Round(amount * (percent / 100m));
        }
    }
}
