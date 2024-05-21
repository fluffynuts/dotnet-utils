using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Imported.PeanutButter.EasyArgs.Attributes;
using Imported.PeanutButter.Utils;
using Pastel;

namespace typedeps;

public enum NameStyles
{
    Short,
    FullyQualified
}

[Description("Usage: typedeps {options} {... assembly path, ... assembly path }")]
public interface IOptions
{
    [Description("The style to adopt for printing names: short, or fully-qualified")]
    NameStyles NameStyle { get; }

    [Description("Manually disable color output (The NO_COLOR environment variable is also observed)")]
    bool DisableColor { get; }

    [Description("Seek cyclic reference issues only")]
    bool SeekCyclic { get; set; }
}

public class DependencyWalker
{
    private readonly IOptions _options;

    public DependencyWalker(
        IOptions options
    )
    {
        _options = options;
    }

    public INode Walk<T>()
    {
        Node.CacheImplementationLookup();
        return Walk(typeof(T));
    }

    public INode Walk(Type type)
    {
        return Walk(type, null);
    }

    public INode Walk(Type type, INode parent)
    {
        if (type.IsInterface || type.IsAbstract)
        {
            throw new ArgumentException(
                "You should start dependency walking from a concrete type"
            );
        }

        var result = new Node(_options.NameStyle, parent, type);
        if (!result.IsCyclic() && !result.IsSystem())
        {
            result.Walk(this);
        }

        return result;
    }
}

public interface INode
{
    string Name { get; }
    Type Implementation { get; }
    HashSet<Type> Contracts { get; }

    INode[] Children { get; }
    INode Parent { get; set; }
    void AddChild(INode node);
    
    bool IsCyclic();
    bool HasCyclicDependency();
}

public class Node : INode
{
    public string Name =>
        _name ??= GenerateName();

    private string _name;
    private readonly NameStyles _nameStyle;

    public Type Implementation { get; set; }
    public HashSet<Type> Contracts { get; } = new();
    public INode[] Children => _children.ToArray();
    public INode Parent { get; set; }

    private readonly List<INode> _children = new();

    public Node(
        NameStyles nameStyle,
        INode parent,
        Type implementation
    )
    {
        var contracts = implementation.GetInterfaces();
        Parent = parent;
        Contracts.AddRange(contracts);
        Implementation = implementation;
        _nameStyle = nameStyle;
    }

    public bool IsSystem()
    {
        return Implementation?.LooksLikeSystemType() ?? false;
    }

    public bool IsCyclic()
    {
        var current = Parent;
        if (current is null)
        {
            return false;
        }
        while (current is not null)
        {
            if (current.Implementation == Implementation)
            {
                return true;
            }

            current = current.Parent;
        }
        return false;
    }

    public bool HasCyclicDependency()
    {
        foreach (var node in Children)
        {
            if (node.IsCyclic())
            {
                return true;
            }

            if (node.HasCyclicDependency())
            {
                return true;
            }
        }
        return false;
    }

    private string GenerateName()
    {
        var type = Implementation ?? Contracts.FirstOrDefault();
        if (type is null)
        {
            throw new InvalidOperationException(
                $"Node with no contract or implementation is invalid"
            );
        }

        var result = _nameStyle == NameStyles.Short
            ? type.Name
            : type.PrettyName();
        if (IsSystem())
        {
            result += $" ({"system".Pastel(Color.Yellow)})";
        }

        if (IsCyclic())
        {
            result += $" ({"cyclic".Pastel(Color.Red)})";
        }

        return result;
    }

    public void Walk(DependencyWalker walker)
    {
        Walk(walker, Parent);
    }

    protected void Walk(DependencyWalker walker, INode parent)
    {
        if (Implementation is null)
        {
            return;
        }

        var constructors = Implementation.GetConstructors();
        foreach (var constructor in constructors)
        {
            var parameterTypes = constructor.GetParameters()
                .Select(p => p.ParameterType)
                .ToArray();
            foreach (var p in parameterTypes)
            {
                if (!p.IsAbstract && !p.IsInterface)
                {
                    var node = walker.Walk(p, parent);
                    node.Parent = this;
                    _children.Add(node);
                    continue;
                }

                var implementations = FindImplementationsOf(p);
                foreach (var impl in implementations)
                {
                    var node = walker.Walk(impl, this);
                    _children.Add(node);
                }
            }
        }
    }

    private Type[] FindImplementationsOf(
        Type iface
    )
    {
        return !ImplementationLookup.TryGetValue(iface, out var implementations)
            ? []
            : implementations.ToArray();
    }

    public static void CacheImplementationLookup()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (!SeenAssemblies.Add(asm))
                {
                    continue;
                }

                if (asm.LooksLikeSystemAssembly())
                {
                    // we're not chasing dependencies into system locations
                    // -> cyclic deps here are not our problem
                    continue;
                }
                
                var asmTypes = asm.GetTypes();
                var contracts = asmTypes
                    .Where(t => t.IsInterface)
                    .AsHashSet();
                var implementations = asmTypes
                    .Where(t => t.GetInterfaces().Any(iface => contracts.Contains(iface)))
                    .ToArray();
                foreach (var implementation in implementations)
                {
                    foreach (var iface in implementation.GetInterfaces())
                    {
                        if (!ImplementationLookup.TryGetValue(iface, out var implementingTypes))
                        {
                            implementingTypes = new();
                            ImplementationLookup[iface] = implementingTypes;
                        }

                        implementingTypes.Add(implementation);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    private static readonly HashSet<Assembly> SeenAssemblies = new();
    private static readonly Dictionary<Type, List<Type>> ImplementationLookup = new();

    public void AddChild(INode node)
    {
        _children.Add(node);
    }
}