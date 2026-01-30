using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Execution.Middleware;

public interface IExecutionMiddleware
{
    void OnTickStart(SimTime time) { }
    void OnTickEnd(SimTime time) { }

    void OnEventDispatched(SimTime time, IEvent ev) { }

    void OnSystemTickStart(SimTime time, string systemName) { }
    void OnSystemTickEnd(SimTime time, string systemName) { }
}