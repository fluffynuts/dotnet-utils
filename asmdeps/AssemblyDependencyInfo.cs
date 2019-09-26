using System;
using System.Reflection;

namespace asmdeps
{
    public class AssemblyDependencyInfo
    {
        public string FullName { get; }
        public bool Loaded { get; set; }
        public int Level { get; }
        public Version Version { get; }
        public string Name { get; set; }
        public string CultureName { get; set; }
        public string PublicKeyToken { get; set; }
        
        public string PrettyFullName =>
            $"{Name.BrightYellow()}, Version={Version.ToString().BrightGreen()}, Culture={CultureName.BrightCyan()}, PublicKeyToken={PublicKeyToken.BrightRed()}";

        public AssemblyDependencyInfo(
            AssemblyName fullName, 
            bool loaded, 
            int level)
        {
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
