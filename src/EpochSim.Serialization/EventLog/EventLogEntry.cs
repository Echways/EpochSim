namespace EpochSim.Serialization.EventLog;

public readonly record struct EventLogEntry(long Tick, string Kind, string Payload);
