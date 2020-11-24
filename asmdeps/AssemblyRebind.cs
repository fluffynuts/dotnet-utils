using System;
using System.Linq;
using System.Reflection;

namespace asmdeps
{
    public class AssemblyRebind
    {
        public string AssemblyName { get; }
        public string PublicKeyToken { get; }
        public string Culture { get; }
        public string OldVersion { get; }
        public string NewVersion { get; }

        public Version OldVersionFrom { get; }
        public Version OldVersionTo { get; }
        public Version RebindVersion { get; }

        public AssemblyRebind(
            string assemblyName,
            string publicKeyToken,
            string culture,
            string oldVersion,
            string newVersion
        )
        {
            AssemblyName = assemblyName;
            PublicKeyToken = publicKeyToken;
            Culture = culture;
            OldVersion = oldVersion;
            NewVersion = newVersion;

            var parts = oldVersion.Split('-');
            OldVersionFrom = new Version(parts.First());
            OldVersionTo = new Version(parts.Last());
            RebindVersion = new Version(newVersion);
        }

        public bool IsValidRebind(
            AssemblyName fromAssemblyName,
            AssemblyName toAssemblyName)
        {
            return CulturesMatch(fromAssemblyName, toAssemblyName) &&
                PublicKeyTokensMatch(fromAssemblyName, toAssemblyName) &&
                AssemblyVersionInRange(fromAssemblyName, OldVersionFrom, OldVersionTo) &&
                toAssemblyName.Version == RebindVersion;
        }

        private bool AssemblyVersionInRange(
            AssemblyName fromAssemblyName,
            Version oldVersionFrom,
            Version oldVersionTo)
        {
            if (fromAssemblyName.Version is null)
            {
                return false;
            }

            return fromAssemblyName.Version >= oldVersionFrom &&
                fromAssemblyName.Version <= oldVersionTo;
        }

        private bool PublicKeyTokensMatch(
            AssemblyName fromAssemblyName,
            AssemblyName toAssemblyName)
        {
            var fromToken = fromAssemblyName.GetPublicKeyToken();
            var toToken = toAssemblyName.GetPublicKeyToken();
            if (fromToken is null || toToken is null)
            {
                return false;
            }

            return fromToken.Zip(toToken, (left, right) => new { left, right })
                .Aggregate(true, (acc, cur) => acc && cur.left == cur.right);
        }

        private bool CulturesMatch(
            AssemblyName fromAssemblyName,
            AssemblyName toAssemblyName
        )
        {
            return fromAssemblyName.CultureName == toAssemblyName.CultureName;
        }
    }
}