using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Pastel;

namespace asmdeps
{
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Any(a => a == "-h" || a == "--help"))
            {
                ShowUsage();
                return 0;
            }

            var noColor = args.Any(a => a == "--no-color") || Console.IsOutputRedirected;
            var otherArgs = args.Where(a => a != "--no-color").ToArray();

            GrokReversalsFrom(args, out var reverseLookup, out var finalArgs);

            if (!finalArgs.Any())
            {
                Console.Error.WriteLine("No assemblies provided to inspect. Exiting.");
                return 2;
            }

            var asmPaths = finalArgs
                .Select(p => p.Glob())
                .SelectMany(o => o);

            if (reverseLookup.Any())
            {
                DumpReverseLookupFor(asmPaths, reverseLookup, noColor);
            }
            else
            {
                DumpAssemblyVersionInfoFor(asmPaths, noColor, otherArgs);
            }


            return 0;
        }
        

        private static void GrokReversalsFrom(
            IEnumerable<string> args,
            out List<string> reverseLookup,
            out List<string> remainingArgs)
        {
            reverseLookup = new List<string>();
            remainingArgs = new List<string>();
            var inReverse = false;
            foreach (var arg in args)
            {
                if (arg == "--reverse")
                {
                    inReverse = true;
                    continue;
                }

                if (inReverse)
                {
                    reverseLookup.Add(arg);
                    inReverse = false;
                    continue;
                }

                remainingArgs.Add(arg);
            }
        }

        private static void ShowUsage()
        {
            var asmDeps = "asmdeps".BrightGreen();
            Console.WriteLine($"Usage: {asmDeps} {{--no-color}} {{{"--reverse".BrightRed()}}} {{{"[assembly name]".BrightYellow()}}}... {{{"assembly file or glob".BrightCyan()}}}...");
            Console.WriteLine("  where:");
            Console.WriteLine($"    {"[assembly name]".BrightYellow()} is something like {"'System.Net.Http'".BrightYellow()}");
            Console.WriteLine($"    {"{assembly}".BrightCyan()} is the path to a dll, or a glob which results in at least one dll");
            Console.WriteLine($"    --no-color suppresses colorised output (default if command is piped)");
            Console.WriteLine($"    --help shows this masterpiece of creative writing");
            Console.WriteLine("Examples:");
            Console.WriteLine($"  {asmDeps} {"MyAssembly.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for MyAssembly.dll".Grey());
            Console.WriteLine($"  {asmDeps} {"C:\\MyProject\\bin\\Debug\\*".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for all assemblies in that folder".Grey());
            Console.WriteLine($"  {asmDeps} {"--reverse".BrightRed()} {"Some.Assembly".BrightYellow()} {"--reverse".BrightRed()} {"Some.Other.Assembly".BrightYellow()} {"*.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for assemblies, stopping at reverse-matches & highlighting".Grey());
        }

        private static void DumpReverseLookupFor(
            IEnumerable<string> asmPaths,
            IEnumerable<string> reverseLookup,
            bool noColor)
        {
            var final = new Dictionary<string, List<List<AssemblyDependencyInfo>>>();

            foreach (var asmFile in asmPaths)
            {
                var asm = TryLoadPathForReflectionOnly(asmFile);
                if (asm == null)
                {
                    continue;
                }

                var root = Path.GetDirectoryName(asmFile);
                var deps = new List<AssemblyDependencyInfo>();
                var errors = ListFileDeps(asm, deps, root);
                foreach (var seek in reverseLookup)
                {
                    if (deps.Any(d => d.Name.Equals(seek, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!final.ContainsKey(seek))
                        {
                            final[seek] = new List<List<AssemblyDependencyInfo>>();
                        }

                        foreach (var dep in deps)
                        {
                            dep.Level++;
                        }

                        deps.Insert(0, new AssemblyDependencyInfo(
                            asm.GetName(),
                            true,
                            0));
                        final[seek].Add(deps);
                    }
                }
            }

            var dumped = 0;
            foreach (var kvp in final)
            {
                if (dumped++ > 0)
                {
                    Console.WriteLine("\n");
                }

                foreach (var tree in kvp.Value)
                {
                    Console.WriteLine($"Depends on {kvp.Key.BrightRed()}:");
                    var trimmed = TrimTree(tree, kvp.Key);
                    DisplayDeps(trimmed, noColor, s => s.Contains(kvp.Key));
                }
            }
        }

        private static IEnumerable<AssemblyDependencyInfo> TrimTree(
            IEnumerable<AssemblyDependencyInfo> tree,
            string toNode)
        {
            var reversed = tree.Reversed();
            var result = new List<AssemblyDependencyInfo>();
            var inHit = false;
            foreach (var node in reversed)
            {
                if (node.Name.Equals(toNode, StringComparison.OrdinalIgnoreCase))
                {
                    inHit = true;
                }

                if (inHit)
                {
                    result.Add(node);
                }

                if (node.Level == 1)
                {
                    inHit = false;
                }
            }
            result.AddRange(tree.Where(n => n.Level == 0));
            return result.Reversed();
        }

        private static void DumpAssemblyVersionInfoFor(
            IEnumerable<string> assemblyPaths,
            bool noColor,
            string[] otherArgs)
        {
            foreach (var asmFile in assemblyPaths)
            {
                var asm = TryLoadPathForReflectionOnly(asmFile);
                if (asm == null)
                {
                    LogErrorIfExplicitlySelected(asmFile, otherArgs);
                    continue;
                }

                var deps = new List<AssemblyDependencyInfo>();
                var root = Path.GetDirectoryName(asmFile);
                var errors = ListFileDeps(asm, deps, root);
                DisplayAssemblyAndDeps(asm, deps, noColor);
                if (!errors.Any())
                {
                    continue;
                }

                Console.WriteLine("Errors follow:");
                foreach (var error in errors)
                {
                    Console.WriteLine(error);
                }
            }
        }

        private static void LogErrorIfExplicitlySelected(
            string asmFile,
            string[] otherArgs)
        {
            if (otherArgs.Any(o => o.ToLower() == asmFile.ToLower()))
            {
                Console.Error.WriteLine($"Unable to load explicitly selected path as assembly: {asmFile}".BrightRed());
            }
        }

        private static Dictionary<string, Assembly> LoadedAssemblies = new Dictionary<string, Assembly>();
        private static Assembly TryLoadPathForReflectionOnly(string asmFile)
        {
            if (LoadedAssemblies.TryGetValue(asmFile, out var result))
            {
                return result;
            }

            try
            {
                Debug($"Attempt load from: {asmFile}");
                var toAdd = File.Exists(asmFile)
                    ? Assembly.ReflectionOnlyLoadFrom(asmFile)
                    : Assembly.ReflectionOnlyLoad(asmFile);
                LoadedAssemblies[asmFile] = toAdd;
                return toAdd;
            }
            catch (Exception ex)
            {
                Debug($"Can't load assembly at {asmFile}");
                Debug($" -> {ex.Message}");
                return null;
            }
        }
        
        private static bool ShowDebug = Environment.GetEnvironmentVariable("DEBUG") != null;

        private static void Debug(params object[] args)
        {
            if (!ShowDebug)
            {
                return;
            }

            Console.WriteLine(
                $"{string.Join(" ", args).Grey()}"
            );
        }

        private static void DisplayAssemblyAndDeps(
            Assembly asm,
            IEnumerable<AssemblyDependencyInfo> deps,
            bool noColor)
        {
            var name = asm.GetName();
            Console.WriteLine(noColor ? name.FullName : name.PrettyFullName());
            DisplayDeps(deps, noColor);
        }

        private static void DisplayDeps(
            IEnumerable<AssemblyDependencyInfo> deps,
            bool noColor, 
            Func<string, bool> highlight = null)
        {
            foreach (var dep in deps)
            {
                var indent = new string(' ', dep.Level);
                var message = (dep.Loaded)
                    ? ""
                    : "    (unable to load assembly)";
                var prefix = $"{indent}{indent}{(noColor ? "-" :"└-")}";
                var toWrite = noColor
                        ? $"{prefix} {dep.FullName}{message}"
                        : $"{prefix.Grey()} {dep.PrettyFullName}{message}";
                if (!noColor)
                {
                    var shouldHighlight = highlight?.Invoke(toWrite) ?? false;
                    if (shouldHighlight)
                    {
                        toWrite = toWrite.PastelBg(Color.FromArgb(255, 0, 0, 128));
                    }
                }

                Console.WriteLine(toWrite);
            }
        }

        // Define other methods and classes here
        static IEnumerable<string> ListFileDeps(
            Assembly asm,
            List<AssemblyDependencyInfo> deps,
            string root,
            int listLevel = 0)
        {
            var errors = new List<string>();
            var refs = asm.GetReferencedAssemblies();
            foreach (var r in refs.OrderBy(r => r.Name))
            {
                if (deps.Any(d => d.FullName == r.FullName))
                {
                    continue;
                }

                var dep = new AssemblyDependencyInfo(r, true, listLevel);
                deps.Add(dep);
                try
                {
                    ListAsmDeps(r.FullName, deps, root, listLevel + 1);
                }
                catch (FileNotFoundException ex)
                {
                    errors.Add($"Unable to find dependency: {ex.FileName}");
                    dep.Loaded = false;
                }
            }

            return errors;
        }

        static void ListAsmDeps(
            string asmName,
            List<AssemblyDependencyInfo> deps,
            string root,
            int listLevel = 0)
        {
            var asm = TryLoadPathForReflectionOnly(asmName);
            if (asm == null)
            {
                var name = new AssemblyName(asmName);
                var check = Path.Combine(root, name.Name + ".dll");
                if (!File.Exists(check))
                {
                    throw new FileNotFoundException(check);
                }
                asm = TryLoadPathForReflectionOnly(check);
            }

            var refs = asm?.GetReferencedAssemblies() ?? new AssemblyName[0];
            foreach (var r in refs)
            {
                if (deps.Any(d => d.FullName == r.FullName))
                {
                    continue;
                }

                var dep = new AssemblyDependencyInfo(r, true, listLevel);
                deps.Add(dep);
                try
                {
                    var assemblyName = new AssemblyName(r.FullName);
                    var asmToCheckFile = Path.Combine(root, assemblyName.Name + ".dll");
                    if (File.Exists(asmToCheckFile))
                    {
                        var asmToCheck = TryLoadPathForReflectionOnly(asmToCheckFile);
                        if (asmToCheck == null)
                        {
                            continue;
                        }

                        ListFileDeps(asmToCheck, deps, root, listLevel + 1);
                    }
                    else
                    {
                        ListAsmDeps(r.FullName, deps, root, listLevel + 1);
                    }
                }
                catch (FileNotFoundException)
                {
                    dep.Loaded = false;
                }
            }
        }
    }
}