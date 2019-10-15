using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Pastel;

namespace shared
{
    public static class StringExtensions
    {
        public static bool DisableColor { get; set; } = false;
        private static readonly Color _brightRed = Color.FromArgb(255, 255, 128, 128);
        private static readonly Color _brightGreen = Color.FromArgb(255, 128, 255, 0);
        private static readonly Color _brightBlue = Color.FromArgb(255, 128, 128, 255);
        private static readonly Color _brightCyan = Color.FromArgb(255, 128, 255, 255);
        private static readonly Color _brightYellow = Color.FromArgb(255, 255, 255, 128);
        private static readonly Color _brightMagenta = Color.FromArgb(255, 255, 128, 255);
        private static readonly Color _brightPink = Color.FromArgb(255, 255, 128, 160);
        private static readonly Color _grey = Color.FromArgb(255, 128, 128, 128);

        public static string BrightRed(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightRed);
        }

        public static string BrightGreen(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightGreen);
        }

        public static string BrightCyan(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightCyan);
        }

        public static string BrightYellow(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightYellow);
        }

        public static string BrightMagenta(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightMagenta);
        }

        public static string BrightPink(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightPink);
        }

        public static string BrightBlue(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_brightBlue);
        }

        private static int _rainbow;

        private static readonly Dictionary<int, Func<string, string>> RainbowLookup
            = new Dictionary<int, Func<string, string>>()
            {
                [0] = BrightPink,
                [1] = BrightGreen,
                [2] = BrightCyan,
                [3] = BrightYellow,
                [4] = BrightMagenta,
                [5] = BrightBlue
            };

        private static readonly int RainbowOptions = RainbowLookup.Keys.Count;

        public static string Rainbow(this string str)
        {
            var handler = RainbowLookup[_rainbow++ % RainbowOptions];
            return handler(str);
        }

        public static string Grey(this string str)
        {
            return DisableColor
                ? str
                : str.Pastel(_grey);
        }

        public static string ToHex(this byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

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

        public static HashSet<string> AsCaseInsensitiveHashSet(
            this IEnumerable<string> input)
        {
            return new HashSet<string>(
                input,
                StringComparer.OrdinalIgnoreCase
            );
        }

        public static void PrintAll(
            this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }
    }
}