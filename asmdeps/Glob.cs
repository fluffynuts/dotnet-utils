using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace asmdeps
{
    public static class GlobExtensions
    {
        /// <summary>
        /// return a list of files that matches some wildcard pattern, e.g. 
        /// C:\p4\software\dotnet\tools\*\*.sln to get all tool solution files
        /// </summary>
        /// <param name="glob">pattern to match</param>
        /// <returns>all matching paths</returns>
        public static IEnumerable<string> Glob(this string glob)
        {
            var head = PathHead(glob);

            if (!new Regex("^[a-zA-Z]:$").IsMatch(head))
            {
                head = Path.GetFullPath(PathHead(glob));
            }

            var tail = PathTail(glob);
            return Glob(head + DirSep, tail);
        }

        /// <summary>
        /// uses 'head' and 'tail' -- 'head' has already been pattern-expanded
        /// and 'tail' has not.
        /// </summary>
        /// <param name="head">wildcard-expanded</param>
        /// <param name="tail">not yet wildcard-expanded</param>
        /// <returns></returns>
        private static IEnumerable<string> Glob(string head, string tail)
        {
            if (!Directory.Exists(head))
            {
                yield break;
            }

            if (PathTail(tail) == tail)
            {
                foreach (var path in Directory.GetFiles(head, tail).OrderBy(s => s))
                {
                    yield return path;
                }
            }
            else
            {
                foreach (var dir in Directory.GetDirectories(head, PathHead(tail)).OrderBy(s => s))
                {
                    foreach (var path in Glob(Path.Combine(head, dir), PathTail(tail)))
                    {
                        yield return path;
                    }
                }
            }
        }

        /// <summary>
        /// shortcut
        /// </summary>
        private static readonly string DirSep = Path.DirectorySeparatorChar.ToString();

        /// <summary>
        /// return the first element of a file path
        /// </summary>
        /// <param name="path">file path</param>
        /// <returns>first logical unit</returns>
        private static string PathHead(string path)
        {
            // handle case of \\share\vol\foo\bar -- return \\share\vol as 'head'
            // because the dir stuff won't let you interrogate a server for its share list
            // FIXME check behavior on Linux to see if this blows up -- I don't think so
            if (path.StartsWith("" + DirSep + DirSep))
            {
                var parts = path.Substring(2).Split(new[] { DirSep }, StringSplitOptions.None);
                return string.Join("",
                    new[]
                    {
                        path.Substring(0, 2),
                        parts[0],
                        DirSep,
                        parts[1]
                    });
            }

            var result = path.Split(new[] { DirSep }, StringSplitOptions.None)[0];
            return result;
        }

        /// <summary>
        /// return everything but the first element of a file path
        /// e.g. PathTail("C:\TEMP\foo.txt") = "TEMP\foo.txt"
        /// </summary>
        /// <param name="path">file path</param>
        /// <returns>all but the first logical unit</returns>
        private static string PathTail(string path)
        {
            return path.Contains(DirSep)
                ? path.Substring(1 + PathHead(path).Length)
                : path;
        }
    }
}