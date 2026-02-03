using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using Xunit;

public sealed class EventLogV2Tests
{
    [Fact]
    public void EventLogWriter_WritesRawJsonPayload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"epochsim-eventlog-{Guid.NewGuid():N}.jsonl");

        try
        {
            using (var writer = new EventLogWriter(path))
            {
                writer.Write(1, "Test", "{\"a\":1}");
            }

            var line = File.ReadLines(path).First();
            Assert.Contains("\"payload\":{", line);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void PopulationEventCodec_DecodesLegacyStringPayloads()
    {
        var codec = new PopulationEventCodec();

        Assert.True(codec.TryDecode("PopulationDelta", "\"5\"", out var ev1));
        var pd = Assert.IsType<PopulationDeltaEvent>(ev1);
        Assert.Equal(5, pd.Delta);

        Assert.True(codec.TryDecode("FireScheduled", "\"30|20\"", out var ev2));
        var fs = Assert.IsType<FireScheduledEvent>(ev2);
        Assert.Equal(30, fs.At.Tick);
        Assert.Equal(20, fs.Damage);
    }
}
