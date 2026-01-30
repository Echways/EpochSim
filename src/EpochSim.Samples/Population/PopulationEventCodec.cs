using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Samples.Population;

public sealed class PopulationEventCodec : IEventCodec
{
    public bool TryEncode(IEvent ev, out string kind, out string payload)
    {
        switch (ev)
        {
            case PopulationDeltaEvent pd:
                kind = "PopulationDelta";
                payload = pd.Delta.ToString();
                return true;

            case FireEvent f:
                kind = "Fire";
                payload = f.Damage.ToString();
                return true;

            case FireScheduledEvent fs:
                kind = "FireScheduled";
                payload = $"{fs.At.Tick}|{fs.Damage}";
                return true;

            default:
                kind = "";
                payload = "";
                return false;
        }
    }

    public bool TryDecode(string kind, string payload, out IEvent ev)
    {
        switch (kind)
        {
            case "PopulationDelta":
                if (!int.TryParse(payload, out var d))
                {
                    ev = default!;
                    return false;
                }
                ev = new PopulationDeltaEvent(d);
                return true;

            case "Fire":
                if (!int.TryParse(payload, out var dmg))
                {
                    ev = default!;
                    return false;
                }
                ev = new FireEvent(dmg);
                return true;

            case "FireScheduled":
                {
                    var parts = payload.Split('|');
                    if (parts.Length != 2)
                    {
                        ev = default!;
                        return false;
                    }

                    if (!long.TryParse(parts[0], out var at))
                    {
                        ev = default!;
                        return false;
                    }

                    if (!int.TryParse(parts[1], out var d2))
                    {
                        ev = default!;
                        return false;
                    }

                    ev = new FireScheduledEvent(new SimTime(at), d2);
                    return true;
                }

            default:
                ev = default!;
                return false;
        }
    }
}
