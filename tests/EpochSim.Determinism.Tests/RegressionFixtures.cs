using EpochSim.Execution;
using EpochSim.Fuzzing;
using EpochSim.Samples.Fuzzing;

namespace RegressionFixtures;

public static class BuggyAccountSuite
{
    public const string SuiteName = "accounts";

    public static void Configure(FuzzBuilder<AccountState> fuzz)
        => AccountSuite.Apply(fuzz, buggy: true);
}

public static class FixedAccountSuite
{
    public static void Configure(FuzzBuilder<AccountState> fuzz)
        => AccountSuite.Apply(fuzz, buggy: false);
}

public static class NonDeterministicSuite
{
    public const string SuiteName = "counter";

    public static void Configure(FuzzBuilder<StaticCounterState> fuzz)
        => fuzz.Name(SuiteName)
            .State(() => new StaticCounterState())
            .Engine(engine => engine.AddSystem(new StaticCounterSystem()))
            .CheckDeterminism()
            .Seeds(1, 2)
            .Ticks(10);
}

internal static class AccountSuite
{
    public static void Apply(FuzzBuilder<AccountState> fuzz, bool buggy)
        => fuzz.Name(BuggyAccountSuite.SuiteName)
            .State(() => new AccountState())
            .Engine(engine => Register(engine, buggy))
            .Invariants(() => [new BalanceNonNegativeInvariant()])
            .Commands(AccountLedger.PlanCommands)
            .Seeds(1, 3)
            .Ticks(100);

    private static void Register(SimulationEngine<AccountState> engine, bool buggy)
    {
        engine.OnCommand<DepositCommand>((state, command, _) => state.Balance += command.Amount);
        engine.OnCommand<WithdrawCommand>((state, command, _) =>
            state.Balance = buggy ? state.Balance - command.Amount : Math.Max(0, state.Balance - command.Amount));
    }
}
