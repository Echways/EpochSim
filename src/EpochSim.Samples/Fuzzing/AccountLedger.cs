using EpochSim.Execution;
using EpochSim.Fuzzing;

namespace EpochSim.Samples.Fuzzing;

public static class AccountLedger
{
    public const int CommandsPerSeed = 300;

    private const int MaxDeposit = 30;
    private const int MaxWithdrawal = 60;

    public static void Register(SimulationEngine<AccountState> engine)
    {
        engine.OnCommand<DepositCommand>((state, command, _) => state.Balance += command.Amount);
        engine.OnCommand<WithdrawCommand>((state, command, _) => state.Balance -= command.Amount);
    }

    public static void PlanCommands(FuzzCommandPlan plan)
    {
        for (var i = 0; i < CommandsPerSeed; i++)
        {
            var tick = plan.Rng.NextInt(0, (int)plan.EndTick + 1);
            plan.Enqueue(tick, plan.Rng.NextInt(0, 2) == 0
                ? new DepositCommand(plan.Rng.NextInt(1, MaxDeposit + 1))
                : new WithdrawCommand(plan.Rng.NextInt(1, MaxWithdrawal + 1)));
        }
    }
}
