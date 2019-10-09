using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using shared;

namespace find_orphaned_code_files
{
    public class Options
    {
        public bool Quiet { get; }
        public bool Rainbow { get; }
        public bool ShowHelp { get; }
        public bool ShowSizes { get; }
        public bool Verbose { get; }
        public string[] Remaining { get; }

        public Options(string[] arguments)
        {
            var args = arguments.ToList();
            Verbose = args.ReadFlag("-v", "--verbose");
            ShowSizes = args.ReadFlag("-s", "--show-sizes");
            ShowHelp = args.ReadFlag("-h", "--help");
            Rainbow = args.ReadFlag("-r", "--rainbow");
            Quiet = args.ReadFlag("-q", "--quiet");

            Remaining = args.ToArray();
        }

        public int ShowUsage()
        {
            var name = "find-orphaned-code-files".BrightGreen();
            var lines = new[]
            {
                $"Usage: {name} {{-v|--verbose}} {{-s|--show-sizes}} {{-q|--quiet}} {{{"[project path / folder]".BrightYellow()}}}...",
                "  where:",
                $"    {"{project path / folder}".BrightYellow()} is a path to a .csproj file",
                "      or a folder to recursively search for .csproj files",
                $"    -q|--quiet {"suppresses some error messages".Grey()}",
                $"    -s|--show-sizes {"will include sizes for unreferenced files".Grey()}",
                $"    -v|--verbose {"will also print out messages when no errors are found".Grey()}"
            };
            lines.PrintAll();

            return 0;
        }
    }

    class Program
    {
        static int Main(string[] arguments)
        {
            var options = new Options(arguments);
            if (options.ShowHelp)
            {
                return options.ShowUsage();
            }

            var processed = 0;
            foreach (var path in options.Remaining)
            {
                var projects = Directory.Exists(path)
                    ? FindProjectsUnder(path)
                    : new[] { path };

                foreach (var project in projects)
                {
                    if (DumpProjectOrphansFor(
                        project,
                        options,
                        () =>
                        {
                            if (processed > 0)
                            {
                                Console.WriteLine("");
                            }
                        }))
                    {
                        processed++;
                    }
                }
            }

            return processed > 0
                ? 0
                : 1;
        }

        private static string[] FindProjectsUnder(string path)
        {
            return Directory.EnumerateFileSystemEntries(
                    path,
                    "*.csproj",
                    SearchOption.AllDirectories)
                .ToArray();
        }

        private static bool DumpProjectOrphansFor(string path,
            Options options,
            Action beforeOutput)
        {
            var haveDoneHeader = false;
            try
            {
                var files = FetchProjectCompileAndContentFiles(path);
                if (files == null)
                {
                    return false;
                }

                var projectCompileFiles = files.Item1;
                var projectContentFiles = files.Item2;
                var projectDir = Path.GetDirectoryName(path);
                if (projectDir == null)
                {
                    Console.Error.WriteLine(
                        $"Unable to determine parent dir for {path}"
                    );
                    return false;
                }

                DumpMissingFiles(projectCompileFiles, path, "compile-time", options, Header);
                DumpMissingFiles(projectContentFiles, path, "content", options, Header);

                var existingCsFiles = FindFiles(projectDir, "*.cs", NotInObjFolder);
                DumpUnreferencedFiles(
                    existingCsFiles,
                    projectCompileFiles,
                    path,
                    options,
                    Header);

                DumpCsDirectories(existingCsFiles);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is UnsupportedProjectException unsupportedProjectException)
                {
                    if (unsupportedProjectException.IsValidModernCsProj &&
                        options.Quiet)
                    {
                        return false;
                    }
                }

                Console.Error.WriteLine(
                    $"Unable to work with {path}\n{ex.Message}\n{ex.StackTrace}"
                );
            }

            return false;

            void Header()
            {
                if (haveDoneHeader)
                {
                    return;
                }

                beforeOutput();
                Console.WriteLine($"[ {path} ]".BrightYellow());
                haveDoneHeader = true;
            }
        }

        private static string[] FindFiles(
            string dir,
            string mask,
            Func<string, bool> filter)
        {
            return Directory.EnumerateFileSystemEntries(
                    dir,
                    mask,
                    SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(filter)
                .ToArray();
        }

        // yes, ValueTuple is a better fit here, but requires an external assembly for the
        // lowest-denominator .net target of 452
        private static Tuple<HashSet<string>, HashSet<string>> FetchProjectCompileAndContentFiles(
            string path)
        {
            var project = new CsProjFileReader(path);
            var projectDir = Path.GetDirectoryName(path);
            if (projectDir == null)
            {
                Console.Error.WriteLine($"Unable to determine parent dir for {path}");
                return null;
            }

            var projectCompileFiles = project.CompiledFiles.Select(
                    rel => FullPath(projectDir, rel)
                )
                .Where(NotNull)
                .AsCaseInsensitiveHashSet();

            var projectContentFiles = project.ContentFiles.Select(
                    rel => FullPath(projectDir, rel)
                )
                .Where(NotNull)
                .AsCaseInsensitiveHashSet();
            return Tuple.Create(projectCompileFiles, projectContentFiles);
        }

        private static bool NotNull(string s)
        {
            return s != null;
        }

        private static readonly Regex IsGlob = new Regex("[\\?\\*]");

        private static string FullPath(
            string baseDir,
            string rel)
        {
            try
            {
                var combined = Path.Combine(baseDir, rel);
                if (IsGlob.IsMatch(combined))
                {
                    // - caller should ignore nulls
                    // - csproj files can have globs
                    // - we can't resolve the glob here
                    return null;
                }

                return Path.GetFullPath(
                    Path.Combine(
                        baseDir,
                        rel
                    )
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Can't get full path for:\n  baseDir: {baseDir}\n  rel: {rel}\n{ex.Message}\n{ex.StackTrace}"
                );
                return null;
            }
        }

        private static bool NotInObjFolder(string arg)
        {
            return arg.Split('/', '\\')
                .All(s => s.ToLowerInvariant() != "obj");
        }

        private static void DumpCsDirectories(
            string[] existingCsFiles)
        {
            var dirs = existingCsFiles.Where(Directory.Exists).ToArray();
            if (!dirs.Any())
            {
                return;
            }

            Console.WriteLine("WARNING: you have one or more folders with the .cs extension:");
            foreach (var dir in dirs)
            {
                Console.WriteLine($"  {dir}");
            }
        }

        private static void DumpUnreferencedFiles(
            string[] existingCsFiles,
            HashSet<string> projectCompileFiles,
            string projectFile,
            Options options,
            Action beforeReporting)
        {
            var missingFromProject = existingCsFiles.Where(
                file => !projectCompileFiles.Contains(file)
            ).ToArray();
            if (missingFromProject.Any())
            {
                beforeReporting();
                Console.WriteLine($"One or more .cs files were found on disk, but not referenced by {projectFile}");
                foreach (var f in missingFromProject)
                {
                    var info = new FileInfo(f);
                    var isEmpty = DetermineIfEmpty(info);
                    var line = isEmpty
                        ? $"  {f} (empty or whitespace)".BrightRed()
                        : options.ShowSizes
                            ? $"  {f} ({info.Length} bytes)"
                            : $"  {f}";
                    if (info.Length > 0 && options.Rainbow)
                    {
                        line = line.Rainbow();
                    }

                    Console.WriteLine(line);
                }
            }
            else if (options.Verbose)
            {
                beforeReporting();
                Console.WriteLine($"No orphaned .cs files were found for {projectFile}");
            }
        }

        private static bool DetermineIfEmpty(FileInfo info)
        {
            if (info.Length == 0)
            {
                return true;
            }

            return info.Length < 128 &&
                IsOnlyWhiteSpace(info);
        }

        private static bool IsOnlyWhiteSpace(FileInfo info)
        {
            var fullName = string.IsNullOrWhiteSpace(info.DirectoryName)
                ? info.Name
                : Path.Combine(info.DirectoryName, info.Name);
            return string.IsNullOrWhiteSpace(File.ReadAllText(fullName));
        }

        private static void DumpMissingFiles(
            HashSet<string> expectedFiles,
            string projectFile,
            string label,
            Options options,
            Action beforeDumping)
        {
            var missingFromFilesystem = expectedFiles.Where(
                file => !File.Exists(file)
            ).ToArray();
            if (missingFromFilesystem.Any())
            {
                beforeDumping();
                Console.WriteLine($"One or more {label} files referenced by project but missing from filesystem:");
                foreach (var f in missingFromFilesystem)
                {
                    Console.WriteLine(
                        options.Rainbow
                            ? $"  {f}".Rainbow()
                            : $"  {f}"
                    );
                }
            }
            else
            {
                if (options.Verbose)
                {
                    beforeDumping();
                    Console.WriteLine(
                        $"{projectFile} does not reference any missing {label} files"
                    );
                }
            }
        }
    }
}