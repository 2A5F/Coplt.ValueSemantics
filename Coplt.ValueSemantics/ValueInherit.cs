using System;

namespace Coplt.ValueSemantics;

/// <summary>
/// Inheritance simulation of value types
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ValueInheritAttribute : Attribute
{
    /// <summary>
    /// Whether to generate field forwarding, ref return in struct only c# 11+
    /// </summary>
    public bool ForwardFields { get; set; } = true;

    /// <summary>
    /// Whether to generate method forwarding
    /// </summary>
    public bool ForwardMethods { get; set; } = true;

    /// <summary>
    /// Whether to generate property forwarding
    /// </summary>
    public bool ForwardProperties { get; set; } = true;
}

/// <summary>
/// Specify base type
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ValueBaseAttribute : Attribute;
