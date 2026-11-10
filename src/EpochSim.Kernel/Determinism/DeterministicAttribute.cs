namespace EpochSim.Kernel.Determinism;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DeterministicAttribute : Attribute;
