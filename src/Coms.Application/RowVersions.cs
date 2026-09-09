using System;

namespace Coms.Application
{
    internal static class RowVersions
    {
        /// <summary>
        /// True when the caller's copy of the rowversion equals the stored one.
        /// A caller that supplies nothing is trusted, so that batch jobs which
        /// just loaded the row do not have to echo it back.
        /// </summary>
        public static bool Match(byte[] stored, byte[]? supplied)
        {
            if (supplied == null || supplied.Length == 0)
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

        public static string ToHex(byte[] value)
        {
            return "0x" + BitConverter.ToString(value).Replace("-", string.Empty);
        }

        public static byte[] FromHex(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<byte>();
            }

            string hex = text!.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
