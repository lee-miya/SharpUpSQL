using System.Linq;

namespace SharpUpSQL.Core.Helpers
{
    public static class LuhnHelper
    {
        public static bool IsValidCreditCard(string number)
        {
            if (string.IsNullOrWhiteSpace(number))
            {
                return false;
            }

            var digits = new string(number.Where(char.IsDigit).ToArray());
            if (digits.Length < 13 || digits.Length > 19)
            {
                return false;
            }

            var sum = 0;
            var alternate = false;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var n = digits[i] - '0';
                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                    {
                        n -= 9;
                    }
                }

                sum += n;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }
    }
}
