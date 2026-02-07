using EpochSim.Kernel.Messaging;

public sealed class MessageKindsTests
{
    [Fact]
    public void AttributeOverride_WinsOverTypeName()
    {
        MessageKinds.ResetForTests();

        var kind = MessageKinds.GetKind(typeof(AttributedEvent));

        Assert.Equal("Fire", kind);
    }

    [Fact]
    public void DefaultFallback_UsesTypeName()
    {
        MessageKinds.ResetForTests();

        var kind = MessageKinds.GetKind(typeof(DefaultNamedEvent));

        Assert.Equal(nameof(DefaultNamedEvent), kind);
    }

    [Fact]
    public void KindLookup_IsCachedByType()
    {
        MessageKinds.ResetForTests();

        _ = MessageKinds.GetKind(typeof(CachedEvent));
        _ = MessageKinds.GetKind(typeof(CachedEvent));

        Assert.Equal(1, MessageKinds.ResolveCountForTests(typeof(CachedEvent)));
    }

    [MessageKind("Fire")]
    private sealed record AttributedEvent() : IEvent;

    private sealed record DefaultNamedEvent() : IEvent;

    private sealed record CachedEvent() : IEvent;
}
