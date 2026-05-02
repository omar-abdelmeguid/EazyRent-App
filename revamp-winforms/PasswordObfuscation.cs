using System;
using System.Text;

namespace EazyRentRevamp
{
    internal static class Obfuscation
    {
        private const uint Seed = 2746340051u;

        private static readonly int[] EncodedDigits = new[] { 4, 9, 2, 3, 8, 1, 0, 2 };

        public static string GetDbPassword() => DecodeDigits(EncodedDigits);

        private static string DecodeDigits(int[] encodedDigits)
        {
            if (encodedDigits == null) throw new ArgumentNullException(nameof(encodedDigits));

            uint s = Seed;
            var sb = new StringBuilder(encodedDigits.Length);
            for (int i = 0; i < encodedDigits.Length; i++)
            {
                s = XorShift32(s);
                int k = (int)(s % 97);
                int kd = k % 10;

                int x = encodedDigits[i];
                int y = x - kd;
                y %= 10;
                if (y < 0) y += 10;

                int digit = (3 * y) % 10;
                sb.Append((char)('0' + digit));
            }
            return sb.ToString();
        }



        private static uint XorShift32(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }
    }
}

