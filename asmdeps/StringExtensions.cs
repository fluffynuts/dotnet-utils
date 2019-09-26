using System.Drawing;
using System.Linq;
using Pastel;

namespace asmdeps
{
    public static class StringExtensions
    {
        private static readonly Color _brightRed = Color.FromArgb(1, 255, 50, 50);
        private static readonly Color _brightGreen = Color.FromArgb(1, 50, 255, 0);
        private static readonly Color _brightCyan = Color.FromArgb(1, 50, 255, 255);
        private static readonly Color _brightYellow = Color.FromArgb(1, 255, 255, 50);

        public static string BrightRed(this string str)
        {
            return str.Pastel(_brightRed);
        }

        public static string BrightGreen(this string str)
        {
            return str.Pastel(_brightGreen);
        }

        public static string BrightCyan(this string str)
        {
            return str.Pastel(_brightCyan);
        }

        public static string BrightYellow(this string str)
        {
            return str.Pastel(_brightYellow);
        }

        public static string ToHex(this byte[] bytes)
        {
            return string.Join("",
                bytes.Select(b => $"{b:x2}")
            );
        }

        public static string DefaultTo(this string str, string fallback)
        {
            return string.IsNullOrWhiteSpace(str)
                ? fallback
                : str;
        }
    }
}