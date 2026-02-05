namespace EpochSim.Kernel.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class MessageKindAttribute(string kind) : Attribute
{
    public string Kind { get; } = kind;
}
