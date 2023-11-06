using System.Reflection;
using shared;

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
            var key = assemblyName.GetPublicKey();
            var tokenInfo = string.IsNullOrWhiteSpace(token)
                ? ""
                : $", PublicKeyToken={token.BrightRed()}";
            var keyInfo = (key?.Length ?? 0) == 0
                ? ", not signed"
                : $", signed with {key?.ToHex()}";
            return $"{name.BrightYellow()}, Version={version.ToString().BrightGreen()}, Culture={culture.BrightCyan()}{tokenInfo}{keyInfo}";
        }
    }
}