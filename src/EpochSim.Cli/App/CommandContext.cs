using System.Threading;
using EpochSim.Cli.Domain;

namespace EpochSim.Cli.App;

public sealed record CommandContext(
    string Root,
    IDomainAdapter Adapter,
    CancellationToken Cancellation);
