﻿using System;
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
                var check = Path.Combine(root, name.Name + ".dll");
                if (!File.Exists(check))
                {
                    throw new FileNotFoundException(check);
                }

                asm = TryLoadPath(check);
                var loadedAsmName = asm?.FullName?.ToString();
                if (!(loadedAsmName is null) && loadedAsmName != asmName)
                {
                    var depMatch = deps.FirstOrDefault(d => d.FullName == asmName);
                    depMatch?.StoreLoadedAssemblyName(new AssemblyName(loadedAsmName));
                }
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
#if NET5_0
                var toAdd = File.Exists(asmFile)
                    ? Assembly.ReflectionOnlyLoadFrom(asmFile)
                    : Assembly.ReflectionOnlyLoad(asmFile);
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
        public IEnumerable<string> ListFileDeps(
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
        
        private readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
    }
}