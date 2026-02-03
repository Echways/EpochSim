namespace EpochSim.Serialization.EventLog;

public readonly record struct EventLogEntryV2(long Tick, string Kind, string PayloadJson);
