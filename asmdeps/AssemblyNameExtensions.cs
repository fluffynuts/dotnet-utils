using System.Reflection;

namespace asmdeps
{
    public static class AssemblyNameExtensions
    {
        public static string PrettyFullName(this AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            var version = assemblyName.Version;
            var culture = assemblyName.CultureName.DefaultTo("neutral");
            var token = assemblyName.GetPublicKeyToken().ToHex();
            var tokenInfo = string.IsNullOrWhiteSpace(token)
                ? ""
                : $", PublicKeyToken={token.BrightRed()}";
            return $"{name.BrightYellow()}, Version={version.ToString().BrightGreen()}, Culture={culture.BrightCyan()}{tokenInfo}";
        }
    }
}