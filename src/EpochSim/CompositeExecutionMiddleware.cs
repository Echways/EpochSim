using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim;

internal sealed class CompositeExecutionMiddleware(IReadOnlyList<IExecutionMiddleware> middleware) : IExecutionMiddleware
{
    public void OnRunStart(RunInfo info)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnRunStart(info);
    }

    public void OnRunEnd(RunInfo info)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnRunEnd(info);
    }

    public void OnRunFailed(RunInfo info, Exception exception)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnRunFailed(info, exception);
    }

    public void OnTickStart(SimTime time)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnTickStart(time);
    }

    public void OnTickEnd(SimTime time)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnTickEnd(time);
    }

    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnEventDispatched(time, ev);
    }

    public void OnCommandHandlerStart(SimTime time, string handlerName, ICommand command)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnCommandHandlerStart(time, handlerName, command);
    }

    public void OnCommandHandlerEnd(SimTime time, string handlerName, ICommand command)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnCommandHandlerEnd(time, handlerName, command);
    }

    public void OnSystemTickStart(SimTime time, string systemName)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnSystemTickStart(time, systemName);
    }

    public void OnSystemTickEnd(SimTime time, string systemName)
    {
        for (var i = 0; i < middleware.Count; i++)
            middleware[i].OnSystemTickEnd(time, systemName);
    }
}

internal sealed class RunCaptureMiddleware : IExecutionMiddleware
{
    public RunInfo? StartedInfo { get; private set; }
    public RunInfo? EndedInfo { get; private set; }
    public Exception? Failure { get; private set; }
    public long LastTick { get; private set; } = -1;

    public void OnRunStart(RunInfo info)
    {
        StartedInfo = info;
        EndedInfo = null;
        Failure = null;
        LastTick = info.StartTick.Tick - 1;
    }

    public void OnTickEnd(SimTime time)
        => LastTick = time.Tick;

    public void OnRunFailed(RunInfo info, Exception exception)
    {
        EndedInfo = info;
        Failure = exception;
    }

    public void OnRunEnd(RunInfo info)
        => EndedInfo = info;
}
