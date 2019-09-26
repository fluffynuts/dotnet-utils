using System;
using System.Reflection;

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
        
        public string PrettyFullName =>
            _fullName.PrettyFullName();

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
    }
}
