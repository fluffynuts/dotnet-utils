using System;
using System.Threading;

namespace shared
{
    public class Status
    {
        public static void Write(string str)
        {
            Clear();
            WriteOut($"\r{Fit(str, Console.WindowWidth)}");
        }

        public static void Start(string str)
        {
            Start(str, Fit);
        }

        public static void Start(
            string str,
            Func<string, int, string> fit)
        {
            Start(str, fit, 7);
        }

        public static void Start(
            string str, 
            Func<string, int, string> fit,
            int leftMargin)
        {
            var formatted = $"{new String(' ', leftMargin)}{fit(str, Console.WindowWidth - leftMargin)}";
            Clear();
            WriteOut(formatted);
        }

        public static string Fit(string str, int maxCols)
        {
            if (str.Length < maxCols)
            {
                return str;
            }
            return str.Substring(0, maxCols - 4) + "...";
        }

        public static void Ok()
        {
            Prefix($"{"[".Grey()}{" OK ".BrightGreen()}{"]".Grey()}");
        }

        public static void Prefix(string str)
        {
            WriteOut($"\r{str}\r");
        }

        private static int _clearSize = 0;
        private static void WriteOut(string str)
        {
            _clearSize = Math.Max(str.Length, _clearSize);
            Console.Out.Write(str);
        }

        public static void Fail()
        {
            Prefix($"{"[".Grey()}{"FAIL".BrightRed()}{"]".Grey()}");
        }

        public static void Clear()
        {
            Console.Out.Write($"\r{new String(' ', _clearSize) }\r");
            _clearSize = 0;
        }

        public static void Newline()
        {
            Console.Out.Write("\r\n");
        }
    }
}