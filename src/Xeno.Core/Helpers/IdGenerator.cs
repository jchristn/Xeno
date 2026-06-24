namespace Xeno.Core.Helpers
{
    using System;
    using System.Security.Cryptography;
    using System.Threading;

    /// <summary>
    /// Compact k-sortable identifier generator.
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// Maximum generated ID length, including prefix.
        /// </summary>
        public const int MaxIdLength = 32;

        private const int TimestampLength = 9;
        private const int SequenceLength = 4;
        private const string Base36Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        private const string Base32Alphabet = "0123456789abcdefghijklmnopqrstuv";
        private static readonly object _Lock = new object();
        private static long _LastUnixMilliseconds = -1;
        private static int _Sequence = -1;

        /// <summary>
        /// Generate an ID with the supplied prefix.
        /// </summary>
        /// <param name="prefix">Identifier prefix.</param>
        /// <returns>K-sortable identifier.</returns>
        public static string NewId(string prefix)
        {
            if (prefix == null) throw new ArgumentNullException(nameof(prefix));
            if (prefix.Length >= MaxIdLength - TimestampLength - SequenceLength)
            {
                throw new ArgumentException("Prefix leaves insufficient room for the sortable identifier suffix.", nameof(prefix));
            }

            long timestamp;
            int sequence;
            lock (_Lock)
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (timestamp == _LastUnixMilliseconds)
                {
                    _Sequence++;
                    if (_Sequence >= Pow(36, SequenceLength))
                    {
                        do
                        {
                            Thread.Sleep(1);
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        }
                        while (timestamp <= _LastUnixMilliseconds);
                        _Sequence = 0;
                    }
                }
                else
                {
                    _LastUnixMilliseconds = timestamp;
                    _Sequence = 0;
                }

                sequence = _Sequence;
                _LastUnixMilliseconds = timestamp;
            }

            string sortable = ToBase36(timestamp, TimestampLength) + ToBase36(sequence, SequenceLength);
            int randomLength = MaxIdLength - prefix.Length - sortable.Length;
            return prefix + sortable + RandomBase32(randomLength);
        }

        /// <summary>
        /// Generate a request history identifier.
        /// </summary>
        /// <returns>Request history identifier.</returns>
        public static string NewRequestHistoryId()
        {
            return NewId("req_");
        }

        private static string ToBase36(long value, int width)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            char[] buffer = new char[width];
            for (int i = width - 1; i >= 0; i--)
            {
                buffer[i] = Base36Alphabet[(int)(value % 36)];
                value /= 36;
            }

            if (value > 0) throw new InvalidOperationException("Value exceeds fixed-width base36 capacity.");
            return new string(buffer);
        }

        private static string RandomBase32(int length)
        {
            if (length < 1) return String.Empty;

            char[] output = new char[length];
            byte[] bytes = RandomNumberGenerator.GetBytes(length);
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Base32Alphabet[bytes[i] & 31];
            }

            return new string(output);
        }

        private static int Pow(int value, int exponent)
        {
            int result = 1;
            for (int i = 0; i < exponent; i++)
            {
                result *= value;
            }

            return result;
        }
    }
}
