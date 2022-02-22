using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Pastel;
using shared;

namespace asmdeps
{
    static class Program
    {
        static int Main(string[] arguments)
        {
            var args = new List<string>(arguments);
            if (args.ReadFlag("-h", "--help"))
            {
                ShowUsage();
                return 0;
            }

            var showPaths = args.ReadFlag("--show-paths", "-p");
            var noColor = args.ReadFlag("--no-color", "-n") || Console.IsOutputRedirected;

            var reverseLookup = args.ReadParameter(
                ParameterOptions.Conservative,
                "--reverse",
                "-r"
            );

            if (!args.Any())
            {
                Console.Error.WriteLine("No assemblies provided to inspect. Exiting.");
                return 2;
            }

            var asmPaths = args
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
                    args.ToArray()
                );
            }


            return 0;
        }

        private static void ShowUsage()
        {
            var asmDeps = "asmdeps".BrightGreen();
            Console.WriteLine(
                $"Usage: {asmDeps} {{-n|--no-color}} {{-p|--show-paths}} {{{"-r|--reverse".BrightRed()}}} {{{"[assembly name]".BrightYellow()}}}... {{{"assembly file or glob".BrightCyan()}}}...");
            Console.WriteLine("  where:");
            Console.WriteLine(
                $"    {"[assembly name]".BrightYellow()} is something like {"'System.Net.Http'".BrightYellow()}");
            Console.WriteLine(
                $"    {"{assembly}".BrightCyan()} is the path to a dll, or a glob which results in at least one dll");
            Console.WriteLine($"    --no-color suppresses colorised output (default if command is piped)");
            Console.WriteLine($"    --show-paths shows paths to assemblies as they are found");
            Console.WriteLine($"    --help shows this masterpiece of creative writing");
            Console.WriteLine("Examples:");
            Console.WriteLine($"  {asmDeps} {"MyAssembly.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for MyAssembly.dll".Grey());
            Console.WriteLine($"  {asmDeps} {"C:\\MyProject\\bin\\Debug\\*".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for all assemblies in that folder".Grey());
            Console.WriteLine(
                $"  {asmDeps} {"--reverse".BrightRed()} {"Some.Assembly".BrightYellow()} {"--reverse".BrightRed()} {"Some.Other.Assembly".BrightYellow()} {"*.dll".BrightCyan()}");
            Console.WriteLine($"    prints out dependencies for assemblies, stopping at reverse-matches & highlighting"
                .Grey());
        }

        private static void DumpReverseLookupFor(IEnumerable<string> asmPaths,
            IEnumerable<string> reverseLookup,
            bool noColor,
            bool showPaths)
        {
            var final = new Dictionary<string, List<List<AssemblyDependencyInfo>>>();

            foreach (var asmFile in asmPaths)
            {
                var trawler = new AssemblyDependencyTrawler(Debug);
                var asm = trawler.TryLoadPath(asmFile);
                if (asm == null)
                {
                    continue;
                }

                var root = Path.GetDirectoryName(asmFile);
                var deps = new List<AssemblyDependencyInfo>();
                trawler.ListFileDeps(asm, deps, root);
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
                    Console.WriteLine(noColor
                        ? $"Depends on {kvp.Key}:"
                        : $"Depends on {kvp.Key.BrightRed()}:");

                    var trimmed = TrimTree(tree, kvp.Key);
                    // TODO: factor in rebinds for reverse lookup?
                    DisplayDeps(trimmed, new AssemblyRebind[0], noColor, showPaths, s => s.Contains(kvp.Key));
                }
            }
        }

        private static IEnumerable<AssemblyDependencyInfo> TrimTree(
            IList<AssemblyDependencyInfo> tree,
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
            string[] otherArgs)
        {
            foreach (var asmFile in assemblyPaths)
            {
                var lister = new AssemblyDependencyTrawler(Debug);
                var asm = lister.TryLoadPath(asmFile);
                if (asm == null)
                {
                    LogErrorIfExplicitlySelected(asmFile, otherArgs);
                    continue;
                }

                var rebinds = ListRebindsFor(asmFile);

                var deps = new List<AssemblyDependencyInfo>();
                var root = Path.GetDirectoryName(asmFile);
                var errors = lister.ListFileDeps(asm, deps, root);
                DisplayAssemblyAndDeps(asmFile, asm, deps, rebinds, noColor, showPaths);
                #if NET6_0_OR_GREATER
                if (showPaths && deps.Any(d => string.IsNullOrWhiteSpace(d.Path)))
                {
                    Console.WriteLine("* paths for assemblies bundled by publishing are not shown".DarkGrey());
                }
                #endif

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


        private static readonly bool ShowDebug = Environment.GetEnvironmentVariable("DEBUG") != null;

        private static void DisplayAssemblyAndDeps(
            string pathOnDisk,
            Assembly asm,
            IEnumerable<AssemblyDependencyInfo> deps,
            AssemblyRebind[] rebinds,
            bool noColor,
            bool showPaths)
        {
            var name = asm.GetName();
            var pathPart = showPaths && !string.IsNullOrWhiteSpace(pathOnDisk)
                ? $"\n|  {pathOnDisk}".Grey()
                : "";
            Console.WriteLine($"{(noColor ? name.FullName : name.PrettyFullName())}{pathPart}");
            DisplayDeps(deps, rebinds, noColor, showPaths);
        }

        private static AssemblyRebind[] ListRebindsFor(string assemblyPath)
        {
            var configFile = $"{assemblyPath}.config";
            if (!File.Exists(configFile))
            {
                return new AssemblyRebind[0];
            }

            var doc = XDocument.Load(configFile);
            var rebindsRoots = doc.Descendants()
                .Where(n => n.Name.LocalName == "assemblyBinding")
                .ToArray();
            var result = new List<AssemblyRebind>();
            foreach (var root in rebindsRoots)
            {
                var dependentAssemblies = root.Descendants()
                    .Where(n => n.Name.LocalName == "dependentAssembly")
                    .ToArray();
                foreach (var dep in dependentAssemblies)
                {
                    var identity = dep.Descendants().FirstOrDefault(n => n.Name.LocalName == "assemblyIdentity");
                    var bindingRedirect = dep.Descendants().FirstOrDefault(n => n.Name.LocalName == "bindingRedirect");

                    if (identity is null || bindingRedirect is null)
                    {
                        continue;
                    }

                    var rebind = new AssemblyRebind(
                        identity.Attribute("name")?.Value,
                        identity.Attribute("publicKeyToken")?.Value,
                        identity.Attribute("culture")?.Value,
                        bindingRedirect.Attribute("oldVersion")?.Value,
                        bindingRedirect.Attribute("newVersion")?.Value
                    );
                    result.Add(rebind);
                }
            }

            return result.ToArray();
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
            AssemblyRebind[] assemblyRebinds,
            bool noColor,
            bool showPaths,
            Func<string, bool> highlight = null)
        {
            var resolved = deps.ToArray();
            for (var i = 0; i < resolved.Length; i++)
            {
                var dep = resolved[i];
                var indent = new string(' ', dep.Level);
                var isMismatched = dep.IsMismatched(assemblyRebinds);
                var isRebound = !(dep.LoadedAssembly is null) && assemblyRebinds.Any(r => r.AssemblyName == dep.Name);
                var message = dep.Loaded
                    ? (isMismatched
                        ? $"* local version mismatch: {dep.LoadedAssembly.FullName}"
                        : "")
                    : "* unable to load assembly";
                var haveError = !dep.Loaded || isMismatched;
                var successfullyRebound = isRebound && !isMismatched;
                if (haveError && !noColor)
                {
                    message = message.BrightMagenta();
                }

                var prefix = $"{indent}{indent}{(noColor ? "-" : "└-")}";
                var spaceDiff = noColor
                    ? 1
                    : 2;
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

                var pathPart = showPaths && !string.IsNullOrWhiteSpace(dep.Path)
                    ? $"\n{spacing}  {dep.Path}"
                    : "";
                message = string.IsNullOrWhiteSpace(message)
                    ? message
                    : $"\n{spacing} {message}";

                if (!noColor)
                {
                    pathPart = pathPart.Grey();
                }

                var rebindMessage = CreateRebindMessage(
                    noColor,
                    isRebound,
                    isMismatched,
                    successfullyRebound,
                    dep,
                    spacing
                );

                var toWrite = noColor
                    ? $"{prefix} {dep.FullName}{rebindMessage}{message}{pathPart}"
                    : $"{prefix.Grey()} {dep.PrettyFullName}{rebindMessage}{message}{pathPart}";
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

                bool NextMatch(Func<AssemblyDependencyInfo, bool> logic)
                {
                    return i < (resolved.Length - 1) &&
                        logic(resolved[i + 1]);
                }
            }
        }

        private static string CreateRebindMessage(
            bool noColor,
            bool isRebound,
            bool isMismatched,
            bool successfullyRebound,
            AssemblyDependencyInfo dep,
            string spacing)
        {
            var successfulRebindMessage = successfullyRebound
                ? $"* rebound to:   {dep.LoadedAssembly.Version}"
                : "";
            if (!string.IsNullOrWhiteSpace(successfulRebindMessage))
            {
                return IndentAndColor(successfulRebindMessage, s => s.BrightBlue());
            }

            if (isMismatched)
            {
                var invalidRebindMessage = isRebound
                    ? "* invalid assembly rebind"
                    : "* assembly rebind suggested";
                return IndentAndColor(invalidRebindMessage, s => s.BrightMagenta());
            }

            return "";

            string IndentAndColor(string str, Func<string, string> colorizer)
            {
                if (string.IsNullOrWhiteSpace(str))
                {
                    return str;
                }

                if (!noColor)
                {
                    str = colorizer(str);
                }

                return $"\n{spacing} {str}";
            }
        }
    }
}