using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace asmdeps
{
    public class AssemblyDependencyTrawler
    {
        private readonly Action<object[]> _debugLogger;

        public AssemblyDependencyTrawler(Action<object[]> debugLogger)
        {
            _debugLogger = debugLogger ?? Console.WriteLine;
        }

        public void ListAsmDeps(
            string asmName,
            List<AssemblyDependencyInfo> deps,
            string root,
            int listLevel = 0)
        {
            var asm = TryLoadPath(asmName);
            if (asm == null)
            {
                var name = new AssemblyName(asmName);
                var seek = new[]
                {
                    Path.Combine(root, name.Name + ".dll"),
                    Path.Combine(root, name.Name + ".exe")
                };
                var found = seek.FirstOrDefault(File.Exists)
                    ?? throw new FileNotFoundException(Path.Combine(root, name.Name + ".(dll|exe)"));
                
                asm = TryLoadPath(found);
                var loadedAsmName = asm?.FullName?.ToString();
                if (!(loadedAsmName is null) && loadedAsmName != asmName)
                {
                    var matches = deps
                        .Where(d => d.Name == name.Name && d.LoadedAssembly is null)
                        .ToArray();
                    foreach (var match in matches)
                    {
                        match.StoreLoadedAssemblyName(new AssemblyName(loadedAsmName));
                    }
                }
            }

            if (asm != null)
            {
#pragma warning disable IL3000
                var assemblyLocation = string.IsNullOrWhiteSpace(asm.Location)
                    ? ""
                    : asm.Location;
                var localPath = assemblyLocation == ""
                    ? ""
                    : new Uri(assemblyLocation).LocalPath;
#pragma warning restore IL3000
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
                        var asmToCheck = TryLoadPath(asmToCheckFile);
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

        public Assembly TryLoadPath(string asmFile)
        {
            if (_loadedAssemblies.TryGetValue(asmFile, out var result))
            {
                return result;
            }

            try
            {
                DebugLog($"Attempt load from: {asmFile}");
#if NET6_0_OR_GREATER
                var toAdd = File.Exists(asmFile)
                    ? Assembly.LoadFrom(asmFile)
                    : Assembly.Load(asmFile);
#else
                var toAdd = File.Exists(asmFile)
                    ? Assembly.ReflectionOnlyLoadFrom(asmFile)
                    : Assembly.ReflectionOnlyLoad(asmFile);
#endif
                _loadedAssemblies[asmFile] = toAdd;
                return toAdd;
            }
            catch (Exception ex)
            {
                DebugLog($"Can't load assembly at {asmFile}");
                DebugLog($" -> {ex.Message}");
                return null;
            }
        }

        private void DebugLog(params object[] args)
        {
            _debugLogger?.Invoke(args);
        }

        // Define other methods and classes here
        public string[] ListFileDeps(
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

            return errors.ToArray();
        }

        private readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
    }
}