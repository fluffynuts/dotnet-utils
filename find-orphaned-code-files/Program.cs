using System;
using System.IO;
using System.Linq;
using shared;

namespace find_orphaned_code_files
{
    class Program
    {
        static void Main(string[] arguments)
        {
            var args = arguments.ToList();
            var verbose = args.ReadFlag("-v", "--verbose");
            foreach (var path in args)
            {
                try
                {
                    var project = new CsProjFileReader(path);
                    var projectDir = Path.GetDirectoryName(path);
                    var projectCompileFiles = project.CompiledFiles.Select(
                        rel => Path.GetFullPath(
                            Path.Combine(
                                projectDir,
                                rel
                            )
                        )
                    ).ToArray();
                    var projectContentFiles = project.ContentFiles.Select(
                        rel => Path.GetFullPath(
                            Path.Combine(
                                projectDir,
                                rel
                            )
                        )
                    ).ToArray();
                    DumpMissingFiles(projectCompileFiles, path, "compile-time", verbose);
                    DumpMissingFiles(projectContentFiles, path, "content", verbose);

                    var existingCsFiles = Directory.EnumerateFileSystemEntries(
                        projectDir,
                        "*.cs",
                        SearchOption.AllDirectories);
                    var missingFromProject = existingCsFiles.Where(
                        file => !projectCompileFiles.Contains(file)
                    ).ToArray();
                    if (missingFromProject.Any())
                    {
                        Console.WriteLine($"One or more .cs files were found on disk, but not referenced by {path}");
                        foreach (var f in missingFromProject)
                        {
                            Console.WriteLine($"  {f}");
                        }
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"No orphaned .cs files were found for {path}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Unable to work with {path}\n{ex.Message}"
                    );
                }
            }
        }

        private static void DumpMissingFiles(
            string[] expectedFiles,
            string projectFile,
            string label,
            bool verbose)
        {
            var missingFromFilesystem = expectedFiles.Where(
                file => !File.Exists(file)
            ).ToArray();
            if (missingFromFilesystem.Any())
            {
                Console.WriteLine($"One or more {label} files referenced by project but missing from filesystem:");
                foreach (var f in missingFromFilesystem)
                {
                    Console.WriteLine($"  {f}");
                }
            }
            else
            {
                if (verbose)
                {
                    Console.WriteLine(
                        $"{projectFile} does not reference any missing {label} files"
                    );
                }
            }
        }

        private static bool IsCsharpFile(string arg)
        {
            return Path.GetExtension(arg)?.ToLower() == ".cs";
        }
    }
}