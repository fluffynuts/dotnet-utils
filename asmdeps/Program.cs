using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace asmdeps
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var asmFile in args)
            {
                var root = Path.GetDirectoryName(asmFile);
                var deps = new List<AssemblyDependencyInfo>();
                var errors = ListFileDeps(asmFile, deps, root);
                DisplayDeps(asmFile, deps);
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

        private static void DisplayDeps(string asmFile, List<AssemblyDependencyInfo> deps)
        {
            Console.WriteLine(asmFile);
            foreach (var dep in deps)
            {
                var indent = new string(' ', dep.Level);
                var message = (dep.Loaded) ? "" : "    (unable to load assembly)";
                Console.WriteLine("{0}{0}└-{1}{2}", indent, dep.FullName, message);
            }
        }

        // Define other methods and classes here
        static IEnumerable<string> ListFileDeps(string asmFile, List<AssemblyDependencyInfo> deps, string root, int listLevel = 0)
        {
            var errors = new List<string>();
            var asm = Assembly.ReflectionOnlyLoadFrom(asmFile);
            var refs = asm.GetReferencedAssemblies();
            foreach (var r in refs)
            {
                if (deps.Any(d => d.FullName == r.FullName))
                    continue;
                var dep = new AssemblyDependencyInfo(r.FullName, true, listLevel);
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

        static void ListAsmDeps(string asmName, List<AssemblyDependencyInfo> deps, string root, int listLevel = 0)
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
                var dep = new AssemblyDependencyInfo(r.FullName, true, listLevel);
                deps.Add(dep);
                try
                {
                    var assemblyName = new AssemblyName(r.FullName);
                    var asmToCheck = Path.Combine(root, assemblyName.Name + ".dll");
                    if (File.Exists(asmToCheck))
                    {
                        ListFileDeps(asmToCheck, deps, root);
                    }
                    else
                        ListAsmDeps(r.FullName, deps, root);
                }
                catch (FileNotFoundException)
                {
                    dep.Loaded = false;
                }
            }
        }
    }
}
