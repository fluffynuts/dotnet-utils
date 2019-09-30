using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace asmdeps
{
    // this globbing code could do with some love -- it works, mostly.
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
            if (File.Exists(glob))
            {
                return new[] { Path.GetFullPath(glob) };
            }

            if (IsRelative(glob))
            {
                glob = Unrelatively(glob);
            }

            var head = PathHead(glob);

            if (!new Regex("^[a-zA-Z]:$").IsMatch(head))
            {
                head = Path.GetFullPath(PathHead(glob));
            }

            var tail = PathTail(glob);
            return Glob(head + DirSep, tail);
        }

        private static string Unrelatively(string glob)
        {
            var re = new Regex("[\\\\|/]");
            var pathParts = new List<string>(re.Split(glob));
            var pre = new List<string>();
            while (pathParts.Count > 0 && pathParts[0] == "..")
            {
                pre.Add(pathParts[0]);
                pathParts = pathParts.Skip(1).ToList();
            }

            glob = Path.Combine(
                Path.GetFullPath(Path.Combine(pre.ToArray())),
                Path.Combine(pathParts.ToArray())
            );
            return glob;
        }

        private static bool IsRelative(string glob)
        {
            return glob.StartsWith(".."); // naive
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
                var dirs = Directory.GetDirectories(head, PathHead(tail))
                    .OrderBy(s => s)
                    .ToArray();
                foreach (var dir in dirs)
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


        /// <summary>
        /// Finds files for the given glob path. It supports ** * and ? operators. It does not support !, [] or ![] operators
        /// </summary>
        /// <param name="path">the path</param>
        /// <returns>The files that match de glob</returns>
        public static ICollection<FileInfo> FindFiles(string path)
        {
            List<FileInfo> result = new List<FileInfo>();
            //The name of the file can be any but the following chars '<','>',':','/','\','|','?','*','"'
            const string folderNameCharRegExp = @"[^\<\>:/\\\|\?\*" + "\"]";
            const string folderNameRegExp = folderNameCharRegExp + "+";
            //We obtain the file pattern
            string filePattern = Path.GetFileName(path);
            List<string> pathTokens = new List<string>(Path.GetDirectoryName(path).Split('\\', '/'));
            //We obtain the root path from where the rest of files will obtained 
            string rootPath = null;
            bool containsWildcardsInDirectories = false;
            for (int i = 0; i < pathTokens.Count; i++)
            {
                if (!pathTokens[i].Contains("*")
                    && !pathTokens[i].Contains("?"))
                {
                    if (rootPath != null)
                        rootPath += "\\" + pathTokens[i];
                    else
                        rootPath = pathTokens[i];
                    pathTokens.RemoveAt(0);
                    i--;
                }
                else
                {
                    containsWildcardsInDirectories = true;
                    break;
                }
            }

            if (Directory.Exists(rootPath))
            {
                //We build the regular expression that the folders should match
                string regularExpression = rootPath.Replace("\\", "\\\\").Replace(":", "\\:").Replace(" ", "\\s");
                foreach (string pathToken in pathTokens)
                {
                    if (pathToken == "**")
                    {
                        regularExpression += string.Format(CultureInfo.InvariantCulture, @"(\\{0})*", folderNameRegExp);
                    }
                    else
                    {
                        regularExpression += @"\\" + pathToken.Replace("*", folderNameCharRegExp + "*")
                            .Replace(" ", "\\s").Replace("?", folderNameCharRegExp);
                    }
                }

                Regex globRegEx = new Regex(regularExpression,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                string[] directories = Directory.GetDirectories(rootPath, "*", containsWildcardsInDirectories
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly);
                foreach (string directory in directories)
                {
                    if (globRegEx.Matches(directory).Count > 0)
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(directory);
                        result.AddRange(directoryInfo.GetFiles(filePattern));
                    }
                }
            }

            return result;
        }
    }
}