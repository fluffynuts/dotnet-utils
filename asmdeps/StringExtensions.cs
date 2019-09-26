using System.Drawing;
using System.Linq;
using Pastel;

namespace asmdeps
{
    public static class StringExtensions
    {
        private static readonly Color _brightRed = Color.FromArgb(255, 255, 50, 50);
        private static readonly Color _brightGreen = Color.FromArgb(255, 50, 255, 0);
        private static readonly Color _brightCyan = Color.FromArgb(255, 50, 255, 255);
        private static readonly Color _brightYellow = Color.FromArgb(255, 255, 255, 50);
        private static readonly Color _grey = Color.FromArgb(255, 128, 128, 128);

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

        public static string Grey(this string str)
        {
            return str.Pastel(_grey);
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