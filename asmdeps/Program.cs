using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Pastel;

namespace asmdeps
{
    static class Program
    {
        static int Main(string[] arguments)
        {
            var args = new List<string>(arguments);
            if (HaveFlag(args, "-h", "--help"))
            {
                ShowUsage();
                return 0;
            }

            var showPaths = HaveFlag(args, "--show-paths", "-p");
            var noColor = HaveFlag(args, "--no-color", "-n") || Console.IsOutputRedirected;

            GrokReversalsFrom(args, out var reverseLookup, out var finalArgs);

            if (!finalArgs.Any())
            {
                Console.Error.WriteLine("No assemblies provided to inspect. Exiting.");
                return 2;
            }

            var asmPaths = finalArgs
                .Select(p => p.Glob())
                .SelectMany(o => o)
                .ToArray();

            if (reverseLookup.Any())
            {
                DumpReverseLookupFor(
                    asmPaths, 
                    reverseLookup, 
                    noColor,
                    showPaths);
            }
            else
            {
                DumpAssemblyVersionInfoFor(
                    asmPaths, 
                    noColor, 
                    showPaths, 
                    args);
            }


            return 0;
        }

        private static bool HaveFlag(
            List<string> args,
            params string[] flags)
        {
            var found = args.Where(flags.Contains).ToArray();
            foreach (var flag in found)
            {
                args.Remove(flag);
            }
            return found.Any();
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
                if (arg == "--reverse" || arg == "-r")
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
            Console.WriteLine($"Usage: {asmDeps} {{-n|--no-color}} {{-p|--show-paths}} {{{"-r|--reverse".BrightRed()}}} {{{"[assembly name]".BrightYellow()}}}... {{{"assembly file or glob".BrightCyan()}}}...");
            Console.WriteLine("  where:");
            Console.WriteLine($"    {"[assembly name]".BrightYellow()} is something like {"'System.Net.Http'".BrightYellow()}");
            Console.WriteLine($"    {"{assembly}".BrightCyan()} is the path to a dll, or a glob which results in at least one dll");
            Console.WriteLine($"    --no-color suppresses colorised output (default if command is piped)");
            Console.WriteLine($"    --show-paths shows paths to assemblies as they are found");
            Console.WriteLine($"    --help shows this masterpiece of creative writing");
            Console.WriteLine("Examples:");
            Console.WriteLine($"  {asmDeps} {"MyAssembly.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for MyAssembly.dll".Grey());
            Console.WriteLine($"  {asmDeps} {"C:\\MyProject\\bin\\Debug\\*".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for all assemblies in that folder".Grey());
            Console.WriteLine($"  {asmDeps} {"--reverse".BrightRed()} {"Some.Assembly".BrightYellow()} {"--reverse".BrightRed()} {"Some.Other.Assembly".BrightYellow()} {"*.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for assemblies, stopping at reverse-matches & highlighting".Grey());
        }

        private static void DumpReverseLookupFor(IEnumerable<string> asmPaths,
            IEnumerable<string> reverseLookup,
            bool noColor,
            bool showPaths)
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
                ListFileDeps(asm, deps, root);
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
                        deps[0].SetPathOnDisk(asmFile);
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
                    DisplayDeps(trimmed, noColor, showPaths, s => s.Contains(kvp.Key));
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
            bool showPaths,
            IEnumerable<string> otherArgs)
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
                DisplayAssemblyAndDeps(asmFile, asm, deps, noColor, showPaths);
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
            IEnumerable<string> otherArgs)
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

        private static void DisplayAssemblyAndDeps(
            string pathOnDisk,
            Assembly asm,
            IEnumerable<AssemblyDependencyInfo> deps,
            bool noColor,
            bool showPaths)
        {
            var name = asm.GetName();
            var pathPart = showPaths ? $"\n|  {pathOnDisk}".Grey() : "";
            Console.WriteLine($"{(noColor ? name.FullName : name.PrettyFullName())}{pathPart}");
            DisplayDeps(deps, noColor, showPaths);
        }

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

        private static void DisplayDeps(
            IEnumerable<AssemblyDependencyInfo> deps,
            bool noColor, 
            bool showPaths,
            Func<string, bool> highlight = null)
        {
            var resolved = deps.ToArray();
            for (var i = 0; i < resolved.Length; i++)
            {
                var dep = resolved[i];
                var indent = new string(' ', dep.Level);
                var message = (dep.Loaded)
                    ? ""
                    : "    (unable to load assembly)";
                var prefix = $"{indent}{indent}{(noColor ? "-" :"└-")}";
                var spaceDiff = noColor ? 1 : 2;
                var spacing = new String(' ', prefix.Length - spaceDiff);

                
                if (NextIsDependency())
                {
                    spacing += "  |  ";
                } 
                else if (NextIsSameLevel())
                {
                    spacing += "|  ";
                }
                else
                {
                    spacing += "   ";
                }

                var pathPart = showPaths ? $"\n{spacing}  {dep.Path}" : "";
                
                if (!noColor)
                {
                    pathPart = pathPart.Grey();
                }

                var toWrite = noColor
                        ? $"{prefix} {dep.FullName}{message}{pathPart}"
                        : $"{prefix.Grey()} {dep.PrettyFullName}{message}{pathPart}";
                if (!noColor)
                {
                    var shouldHighlight = highlight?.Invoke(toWrite) ?? false;
                    if (shouldHighlight)
                    {
                        toWrite = toWrite.PastelBg(Color.FromArgb(255, 0, 0, 128));
                    }
                }

                Console.WriteLine(toWrite);

                bool NextIsDependency()
                {
                    return NextMatch(next => next.Level > dep.Level);
                }

                bool NextIsSameLevel()
                {
                    return NextMatch(next => next.Level == dep.Level);
                }
                
                bool NextMatch(Func<AssemblyDependencyInfo,bool> logic)
                {
                    return i < (resolved.Length - 1) &&
                        logic(resolved[i + 1]);
                }
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
                    errors.Add($"Unable to find dependency: {r.FullName} {ex.FileName}");
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

            if (asm != null)
            {
                var localPath = new Uri(asm.Location).LocalPath;
                foreach (var dep in deps.Where(d => d.Name == asm.GetName().Name))
                {
                    dep.SetPathOnDisk(localPath);
                }
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
                        dep.SetPathOnDisk(asmToCheckFile);
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