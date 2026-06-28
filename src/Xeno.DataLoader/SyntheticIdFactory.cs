namespace Xeno.DataLoader
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    internal static class SyntheticIdFactory
    {
        public static string Create(string prefix, string stableInput, int maximumLength)
        {
            if (String.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("ID prefix must not be empty.", nameof(prefix));
            if (String.IsNullOrWhiteSpace(stableInput)) throw new ArgumentException("Stable input must not be empty.", nameof(stableInput));
            if (maximumLength <= prefix.Length + 8) throw new ArgumentOutOfRangeException(nameof(maximumLength), "Maximum length is too short for the supplied prefix.");

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
            StringBuilder builder = new StringBuilder(prefix, maximumLength);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                if (builder.Length >= maximumLength) break;
            }

            return builder.ToString(0, maximumLength);
        }

        public static string CreateHashFragment(string stableInput, int length)
        {
            if (String.IsNullOrWhiteSpace(stableInput)) throw new ArgumentException("Stable input must not be empty.", nameof(stableInput));
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length), "Hash fragment length must be positive.");

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableInput));
            StringBuilder builder = new StringBuilder(length);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                if (builder.Length >= length) break;
            }

            return builder.ToString(0, length);
        }
    }
}
