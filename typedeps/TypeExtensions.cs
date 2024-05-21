using System;
using System.Reflection;

namespace typedeps;

public static class TypeExtensions
{
    public static bool LooksLikeSystemType(
        this Type type
    )
    {
        return LooksLikeSystemNamespace(type?.Namespace);
    }

    public static bool LooksLikeSystemAssembly(
        this Assembly asm
    )
    {
        return LooksLikeSystemNamespace(asm?.FullName);
    }

    private static bool LooksLikeSystemNamespace(string ns)
    {
        return ns is not null &&
        (
            ns.StartsWith("system", StringComparison.OrdinalIgnoreCase) ||
            ns.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase)
        );
    }
}