using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Imported.PeanutButter.EasyArgs;
using typedeps;

var opts = args.ParseTo<IOptions>(
    out var assemblies,
    new ParserOptions()
    {
        IgnoreUnknownSwitches = true
    }
);
if (
    Environment.GetEnvironmentVariable("NO_COLOR") is not null ||
    opts.DisableColor
)
{
    shared.StringExtensions.DisableColor = true;
}


var walker = new DependencyWalker(opts);
foreach (var path in assemblies)
{
    var asm = Assembly.LoadFrom(path);
    Node.CacheImplementationLookup();
    var types = asm.GetTypes().ToArray();
    var moo = types.FirstOrDefault(t => t.Name == "VoucherTemplateUiService");
    foreach (var type in types)
    {
        if (LooksLikeAnonymousType(type))
        {
            continue;
        }

        if (type.IsInterface || type.IsAbstract)
        {
            continue;
        }

        var result = walker.Walk(type);
        if (opts.SeekCyclic)
        {
            if (result.HasCyclicDependency())
            {
                PrintNode(result);
            }
        }
        else
        {
            PrintNode(result);
        }
    }
}

void PrintNode(INode node, string indent = "")
{
    Console.WriteLine($"{indent}{node.Name}");
    indent += " ";
    foreach (var child in node.Children)
    {
        PrintNode(child, indent);
    }
}

bool LooksLikeAnonymousType(Type type)
{
    if (type is null)
    {
        throw new ArgumentNullException(nameof(type));
    }

    // HACK: The only way to detect anonymous types right now.
    return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
        // && type.IsGenericType && type.Name.Contains("AnonymousType")
        && (type.Name.StartsWith("<>") || type.Name.StartsWith("VB$"))
        && type.Attributes.HasFlag(TypeAttributes.NotPublic);
}