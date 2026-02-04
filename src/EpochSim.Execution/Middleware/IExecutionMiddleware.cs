using EpochSim.Execution;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Execution.Middleware;

public interface IExecutionMiddleware
{
    void OnRunStart(RunInfo info) { }
    void OnRunEnd(RunInfo info) { }
    void OnRunFailed(RunInfo info, Exception exception) { }

    void OnTickStart(SimTime time) { }
    void OnTickEnd(SimTime time) { }

    void OnEventDispatched(SimTime time, IEvent ev) { }

    void OnCommandHandlerStart(SimTime time, string handlerName, ICommand command) { }
    void OnCommandHandlerEnd(SimTime time, string handlerName, ICommand command) { }

    void OnSystemTickStart(SimTime time, string systemName) { }
    void OnSystemTickEnd(SimTime time, string systemName) { }
}
