using EpochSim.Kernel.Validation;

namespace EpochSim.Fuzzing;

internal readonly record struct FuzzFailureIdentity(FuzzFailureKind Kind, string Name)
{
    public static FuzzFailureIdentity Of(Exception error)
        => error is InvariantViolationException violation
            ? new FuzzFailureIdentity(FuzzFailureKind.InvariantViolation, violation.InvariantName)
            : new FuzzFailureIdentity(FuzzFailureKind.Exception, error.GetType().FullName ?? error.GetType().Name);

    public static FuzzFailureIdentity NonDeterminism { get; } = new(FuzzFailureKind.NonDeterminism, nameof(FuzzFailureKind.NonDeterminism));
}
