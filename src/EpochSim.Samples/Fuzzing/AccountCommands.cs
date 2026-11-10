using EpochSim.Kernel.Messaging;

namespace EpochSim.Samples.Fuzzing;

[MessageKind("Deposit")]
public sealed record DepositCommand(int Amount) : ICommand;

[MessageKind("Withdraw")]
public sealed record WithdrawCommand(int Amount) : ICommand;
