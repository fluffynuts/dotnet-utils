using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using find_orphaned_code_files;
using Imported.PeanutButter.Utils;
using shared;
using StringExtensions = shared.StringExtensions;

namespace remove_project_files
{
    public static class Program
    {
        public static int Main(string[] arguments)
        {
            var args = arguments.ToList();
            var showHelp = args.ReadFlag("-h", "--help");
            if (showHelp)
            {
                return ShowUsage();
            }

            StringExtensions.DisableColor = args.ReadFlag("-n", "--no-color") || Console.IsOutputRedirected;
            var pretend = args.ReadFlag("-p", "--pretend");
            var force = args.ReadFlag("-f", "--force");
            if (!pretend && !force)
            {
                Console.WriteLine("WARNING: this utility is experimental and may eat your project");
                Console.WriteLine("If you're REALLY sure you'd like to run without --pretend, please (for now)");
                Console.WriteLine(" add the --force flag");
                Console.WriteLine("Also: ALWAYS back up your files before allowing automated manipulation!");
                return 1;
            }

            if (force)
            {
                pretend = false;
            }

            var verbose = args.ReadFlag("-v", "--verbose");
            var projectsOrFolders = args.ReadParameter(
                ParameterOptions.Conservative,
                "-f",
                "--from");
            var projects = ResolveProjectsFrom(projectsOrFolders);

            if (!projects.Any())
            {
                Console.Error.WriteLine($"No projects specified or none found matching inputs");
                return 2;
            }

            if (!args.Any())
            {
                Console.Error.WriteLine("No files mentioned to remove");
                return 2;
            }

            projects.ForEach(project =>
            {
                var readResult = TryReadProject(project, out var reader);
                if (readResult == ReadProjectResult.Error)
                {
                    if (verbose)
                    {
                        Console.Error.WriteLine($"Unable to read project '{project}'".BrightRed());
                    }

                    return;
                }

                var projectDir = Path.GetDirectoryName(project);
                if (readResult == ReadProjectResult.Success)
                {
                    // FIXME: this doesn't actually modify the .csproj yet )':
                    RemoveProjectMatches(
                        pretend,
                        verbose,
                        reader,
                        args
                    );
                }

                RemoveFileSystemFiles(
                    pretend,
                    verbose,
                    projectDir,
                    args);
            });

            Status.Clear();
            return 0;
        }

        private static void RemoveFileSystemFiles(
            bool pretend,
            bool verbose,
            string projectDir,
            List<string> args)
        {
            var exactMatches = FindExactMatchesIn(args);
            var partialMatches = FindPartialMatchesIn(args);
            var files = Directory.EnumerateFileSystemEntries(projectDir)
                .Where(path =>
                {
                    StartInspect(verbose, $"Inspect {path}");
                    var result = IsMatched(path, partialMatches, exactMatches);
                    EndInspect(verbose, result);
                    return result;
                })
                .ToArray();
            if (pretend && !verbose)
            {
                files.ForEach(file =>
                    Console.WriteLine($"DELETE: {file}")
                );
                return;
            }

            files.ForEach(file =>
            {
                Status.Start($"delete: {file}", MakeSquisherFor("delete", file));
                try
                {
                    File.Delete(file);
                    Status.Ok();
                }
                catch (Exception ex)
                {
                    Status.Fail();
                    Console.WriteLine($"{ex.Message}");
                }
            });
        }

        private static Func<string, int, string> MakeSquisherFor(
            string prefix,
            string filePath)
        {
            return (s, maxCols) =>
            {
                var parts = SplitPath(filePath);
                while (s.Length > maxCols)
                {
                    parts = parts.Skip(1).ToArray();
                    s = $"{prefix}: ...{string.Join("\\", parts)}";
                }

                return s;
            };
        }

        private static string[] FindExactMatchesIn(List<string> args)
        {
            return args.Where(a => a.StartsWith("\\"))
                .Select(a => a.Substring(1))
                .ToArray();
        }

        private static string[] FindPartialMatchesIn(List<string> args)
        {
            return args.Where(a => !a.StartsWith("\\"))
                .ToArray();
        }

        private static bool IsMatched(
            string path,
            string[] partialMatches,
            string[] exactMatches)
        {
            return exactMatches.Any(e => e.Equals(path, StringComparison.OrdinalIgnoreCase)) ||
                partialMatches.Any(p =>
                {
                    var partialParts = SplitPath(p).Reverse().ToArray();
                    var pathParts = SplitPath(path).Reverse().ToArray();
                    if (partialParts.Length > pathParts.Length)
                    {
                        return false;
                    }

                    return partialParts
                        .Select((part, idx) => Tuple.Create(part, idx))
                        .Aggregate(
                            true,
                            (acc, cur) =>
                                acc &&
                                cur.Item1.Equals(
                                    pathParts[cur.Item2],
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );
                });
        }

        private static string[] SplitPath(string path)
        {
            return path.Split(new[] { "\\", "/" }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void RemoveProjectMatches(
            bool pretend,
            bool verbose,
            CsProjFileReader reader,
            List<string> args)
        {
            var exactMatches = FindExactMatchesIn(args);
            var partialMatches = FindPartialMatchesIn(args);
            var types = new[] { "Content", "Compile", "None" };
            var anythingRemoved = false;
            types.ForEach(type =>
            {
                reader.RemoveFilesOfType(
                    type,
                    fileName =>
                    {
                        StartInspect(verbose, $" INSPECT ({type}) file: {fileName}");
                        var result = IsMatched(fileName, partialMatches, exactMatches);
                        EndInspect(verbose, result);
                        anythingRemoved = anythingRemoved || result;
                        return result;
                    },
                    fileName =>
                    {
                        Status.Clear();
                        if (!verbose)
                        {
                            Status.Clear();
                            Console.WriteLine($"\rRemove ({type}) project file: {fileName}");
                        }

                        return !pretend;
                    });
            });
            
            if (anythingRemoved)
            {
                reader.Persist();
            }

            Status.Clear();
        }

        private static void StartInspect(bool verbose, string str)
        {
            if (verbose)
            {
                Status.Start(str, Status.Fit, 0);
            }
            else
            {
                Status.Write(str);
            }
        }

        private static void EndInspect(bool verbose, bool delete)
        {
            if (delete)
            {
                Status.Prefix(" DELETE ".BrightRed());
            }
            else
            {
                Status.Prefix("  KEEP  ".BrightGreen());
            }

            if (verbose)
            {
                // keep the data on-screen
                Status.Newline();
            }
        }

        private enum ReadProjectResult
        {
            Error,
            Success,
            ModernProject
        }

        private static ReadProjectResult TryReadProject(
            string path,
            out CsProjFileReader reader)
        {
            reader = null;
            try
            {
                Status.Start($"Read project: {path}");
                reader = new CsProjFileReader(path);
                Status.Ok();
                return ReadProjectResult.Success;
            }
            catch (UnsupportedProjectException unsupported)
            {
                if (unsupported.IsValidModernCsProj)
                {
                    Status.Ok();
                    return ReadProjectResult.ModernProject;
                }

                Status.Fail();
                return ReadProjectResult.Error;
            }
            catch
            {
                Status.Fail();
                return ReadProjectResult.Error;
            }
        }

        private static string[] ResolveProjectsFrom(string[] projectsOrFolders)
        {
            return projectsOrFolders.Aggregate(
                new List<string>(),
                (acc, cur) =>
                {
                    if (Directory.Exists(cur))
                    {
                        acc.AddRange(
                            Directory.EnumerateFileSystemEntries(
                                cur,
                                "*.csproj",
                                SearchOption.AllDirectories
                            )
                        );
                    }
                    else if (File.Exists(cur))
                    {
                        acc.Add(cur);
                    }

                    return acc;
                }).ToArray();
        }

        private static IEnumerable<string> FindProjectsUnder(string cur)
        {
            return Directory.EnumerateFileSystemEntries(
                cur,
                "*.csproj");
        }

        private static int ShowUsage()
        {
            var program = "remove-project-files".BrightGreen();
            Console.WriteLine(
                $"Usage: {program} {{-p|--pretend}} {{-f|--from}} {"[project or folder]".BrightYellow()} {{{"file name".BrightCyan()}}}...");
            Console.WriteLine($" removes compile-time and content files from projects & deletes off disk");
            Console.WriteLine("  where:");
            Console.WriteLine($"    -f|--from specifies a single source (project or folder) to inspect");
            Console.WriteLine($"    -p|--pretend specifies to just print out what would be done and exit");
            Console.WriteLine(
                $"    {"[project or folder]".BrightYellow()} is a project or a folder to search recursively for projects");
            Console.WriteLine($"    {"{file name}".BrightCyan()} is a file to remove");
            Console.WriteLine($"        if the file contains backslashes, it must be an exact match");
            Console.WriteLine($"        (leading backslash required for match in root of project only)");
            Console.WriteLine($"        otherwise any file with a matching name is considered");
            Console.WriteLine("Examples:");
            Console.WriteLine($"  {program} --from MyProject.csproj Foo.cs");
            Console.WriteLine($"    removes any file called Foo.cs from MyProject.csproj and disk");
            Console.WriteLine($"  {program} -p -f . Foo.cs");
            Console.WriteLine($"    searches all .csproj files from this folder down for Foo.cs".Grey());
            Console.WriteLine($"      and prints out what would be removed".Grey());
            return 0;
        }
    }
}