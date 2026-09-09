using System;

namespace Coms.Application.Tests.Fakes
{
    /// <summary>Hands out increasing 8-byte row versions, like SQL Server's rowversion.</summary>
    internal static class Versions
    {
        private static long _counter;

        public static byte[] Next()
        {
            byte[] bytes = BitConverter.GetBytes(++_counter);
            Array.Reverse(bytes);
            return bytes;
        }

        public static bool Match(byte[] stored, byte[] supplied)
        {
            if (supplied.Length == 0)
            {
                return true;
            }

            if (stored.Length != supplied.Length)
            {
                return false;
            }

            for (int i = 0; i < stored.Length; i++)
            {
                if (stored[i] != supplied[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
