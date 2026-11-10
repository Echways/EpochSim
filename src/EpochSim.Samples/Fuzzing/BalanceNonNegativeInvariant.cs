using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

namespace EpochSim.Samples.Fuzzing;

public sealed class BalanceNonNegativeInvariant : IInvariant<AccountState>
{
    public string Name => "BalanceNonNegative";

    public bool Check(SimTime time, AccountState state, out string message)
    {
        message = state.Balance >= 0 ? string.Empty : $"Balance={state.Balance}";
        return state.Balance >= 0;
    }
}
