using System.Text.Json;
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

    [Theory]
    [InlineData("evil\x00kind")]
    [InlineData("evil\nkind")]
    [InlineData("evil\x08kind")]
    [InlineData("evil\x0Ckind")]
    [InlineData("evil\u2028kind")]
    [InlineData("evil\u2029kind")]
    [InlineData("evil\"kind")]
    [InlineData("evil\\kind")]
    public void EventLogWriter_EvilKind_ProducesValidJson(string evilKind)
    {
        var path = Path.Combine(Path.GetTempPath(), $"epochsim-evil-{Guid.NewGuid():N}.jsonl");

        try
        {
            using (var writer = new EventLogWriter(path))
            {
                writer.Write(1, evilKind, "{\"v\":1}");
            }

            var line = File.ReadLines(path).First();

            using var doc = JsonDocument.Parse(line);
            var kindValue = doc.RootElement.GetProperty("kind").GetString();

            Assert.Equal(evilKind, kindValue);
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
        var fireScheduled = Assert.IsType<FireScheduledEvent>(ev2);
        Assert.Equal(30, fireScheduled.At.Tick);
        Assert.Equal(20, fireScheduled.Damage);
    }
}
