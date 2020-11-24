using System;
using System.Linq;
using System.Reflection;
using shared;

namespace asmdeps
{
    public class AssemblyDependencyInfo
    {
        private readonly AssemblyName _fullName;
        public string FullName { get; }
        public bool Loaded { get; set; }
        public int Level { get; set; }
        public Version Version { get; }
        public string Name { get; set; }
        public string CultureName { get; set; }
        public string PublicKeyToken { get; set; }
        public string Path { get; private set; }
        public AssemblyName LoadedAssembly { get; private set; }

        public string PrettyFullName =>
            _fullName.PrettyFullName();

        public bool IsMismatched(AssemblyRebind[] rebinds)
        {
            if (LoadedAssembly is null)
            {
                return false;
            }

            var matchingRebind = rebinds.FirstOrDefault(r => r.AssemblyName == _fullName.Name);
            if (matchingRebind is null)
            {
                return true;
            }

            return !matchingRebind.IsValidRebind(
                _fullName,
                LoadedAssembly
            );
        }

        public AssemblyDependencyInfo(
            AssemblyName fullName,
            bool loaded,
            int level)
        {
            _fullName = fullName;
            FullName = fullName.FullName;
            Version = fullName.Version;
            Name = fullName.Name;
            CultureName = fullName.CultureName.DefaultTo("neutral");
            PublicKeyToken = fullName.GetPublicKeyToken().ToHex();
            Loaded = loaded;
            Level = level;
        }

        public void SetPathOnDisk(string path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                Path = path;
            }
        }

        public void StoreLoadedAssemblyName(AssemblyName expected)
        {
            LoadedAssembly = expected;
        }
    }
}