using EpochSim.Kernel.Messaging;
using EpochSim.Serialization.EventLog;

public sealed class JsonEventCodecBuilderTests
{
    [Fact]
    public void RoundTrip_EncodesAndDecodesRegisteredEvents()
    {
        var codec = new JsonEventCodecBuilder()
            .Register<RegisteredEventA>()
            .Register<RegisteredEventB>()
            .Build();

        var source = new RegisteredEventA(42);

        Assert.True(codec.TryEncode(source, out var kind, out var payload));
        Assert.Equal("A", kind);

        Assert.True(codec.TryDecode(kind, payload, out var decoded));
        var typed = Assert.IsType<RegisteredEventA>(decoded);
        Assert.Equal(42, typed.Value);
    }

    [Fact]
    public void Register_DuplicateType_Throws()
    {
        var builder = new JsonEventCodecBuilder()
            .Register<RegisteredEventA>();

        Assert.Throws<InvalidOperationException>(() => builder.Register<RegisteredEventA>());
    }

    [Fact]
    public void Register_DuplicateKind_Throws()
    {
        var builder = new JsonEventCodecBuilder()
            .Register<RegisteredEventA>();

        Assert.Throws<InvalidOperationException>(() => builder.Register<KindCollisionEvent>());
    }

    [Fact]
    public void UnknownKind_NonStrict_ReturnsFalse()
    {
        var codec = new JsonEventCodecBuilder()
            .Register<RegisteredEventA>()
            .Build();

        var ok = codec.TryDecode("Unknown", "{}", out _);

        Assert.False(ok);
    }

    [Fact]
    public void UnknownKind_Strict_Throws()
    {
        var codec = new JsonEventCodecBuilder()
            .Register<RegisteredEventA>()
            .WithStrictUnknownKinds()
            .Build();

        Assert.Throws<InvalidOperationException>(() => codec.TryDecode("Unknown", "{}", out _));
    }

    [MessageKind("A")]
    private sealed record RegisteredEventA(int Value) : IEvent;

    [MessageKind("B")]
    private sealed record RegisteredEventB(string Name) : IEvent;

    [MessageKind("A")]
    private sealed record KindCollisionEvent(int Value) : IEvent;
}
