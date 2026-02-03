using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class StateMutationGuardMiddleware<TState>(
    TState state,
    IStateSerializer<TState> serializer) : IExecutionMiddleware
{
    private readonly Dictionary<string, string> _systemBefore = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _handlerBefore = new(StringComparer.Ordinal);

    public void OnSystemTickStart(SimTime time, string systemName)
        => _systemBefore[systemName] = Fingerprint();

    public void OnSystemTickEnd(SimTime time, string systemName)
    {
        if (!_systemBefore.TryGetValue(systemName, out var before))
            return;

        _systemBefore.Remove(systemName);
        CheckMutation(time, phase: "System.Tick", name: systemName, before: before);
    }

    public void OnCommandHandlerStart(SimTime time, string handlerName, ICommand command)
        => _handlerBefore[HandlerKey(handlerName, command)] = Fingerprint();

    public void OnCommandHandlerEnd(SimTime time, string handlerName, ICommand command)
    {
        var key = HandlerKey(handlerName, command);
        if (!_handlerBefore.TryGetValue(key, out var before))
            return;

        _handlerBefore.Remove(key);
        CheckMutation(time, phase: "CommandHandler", name: key, before: before);
    }

    private void CheckMutation(SimTime time, string phase, string name, string before)
    {
        var after = Fingerprint();
        if (string.Equals(before, after, StringComparison.Ordinal))
            return;

        throw new InvalidOperationException(
            $"Illegal state mutation detected at tick {time.Tick} during {phase} ({name}). " +
            $"Before={before}, After={after}.");
    }

    private string Fingerprint()
    {
        var json = serializer.Serialize(state);
        return Serialization.State.StateFingerprint.ComputeFromJson(json);
    }

    private static string HandlerKey(string handlerName, ICommand command)
        => $"{handlerName}:{command.GetType().Name}";
}
