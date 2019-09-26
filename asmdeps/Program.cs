using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace asmdeps
{
    static class Program
    {
        static int Main(string[] args)
        {
            var noColor = args.Any(a => a == "--no-color");
            var otherArgs = args.Where(a => a != "--no-color").ToArray();
            if (otherArgs.Length == 0)
            {
                Console.Error.WriteLine("No assemblies provided to inspect. Exiting.");
                return 2;
            }

            foreach (var asmFile in otherArgs)
            {
                var root = Path.GetDirectoryName(asmFile);
                var asm = Assembly.ReflectionOnlyLoadFrom(asmFile);
                var deps = new List<AssemblyDependencyInfo>();
                var errors = ListFileDeps(asm, deps, root);
                DisplayDeps(asm, deps, noColor);
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

            return 0;
        }

        private static void DisplayDeps(
            Assembly asm,
            IEnumerable<AssemblyDependencyInfo> deps,
            bool noColor)
        {
            var name = asm.GetName();
            Console.WriteLine($"{name.Name} ({name.Version})");
            foreach (var dep in deps)
            {
                var indent = new string(' ', dep.Level);
                var message = (dep.Loaded)
                    ? ""
                    : "    (unable to load assembly)";
                var prefix = $"{indent}{indent}└-".Grey();
                Console.WriteLine(
                    noColor
                        ? $"{prefix}{dep.FullName}{message}"
                        : $"{prefix}{dep.PrettyFullName}{message}"
                );
            }
        }

        private static AssemblyName ReadAssemblyName(string asmFile)
        {
            var asm = Assembly.ReflectionOnlyLoad(asmFile);
            return asm.GetName();
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
            Assembly asm;
            try
            {
                asm = Assembly.ReflectionOnlyLoad(asmName);
            }
            catch
            {
                var name = new AssemblyName(asmName);
                var check = Path.Combine(root, name.Name + ".dll");
                asm = Assembly.ReflectionOnlyLoadFrom(check);
            }

            var refs = asm.GetReferencedAssemblies();
            foreach (var r in refs)
            {
                if (deps.Any(d => d.FullName == r.FullName))
                    continue;
                var dep = new AssemblyDependencyInfo(r, true, listLevel);
                deps.Add(dep);
                try
                {
                    var assemblyName = new AssemblyName(r.FullName);
                    var asmToCheckFile = Path.Combine(root, assemblyName.Name + ".dll");
                    if (File.Exists(asmToCheckFile))
                    {
                        var asmToCheck = Assembly.ReflectionOnlyLoadFrom(asmToCheckFile);
                        ListFileDeps(asmToCheck, deps, root);
                    }
                    else
                    {
                        ListAsmDeps(r.FullName, deps, root);
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