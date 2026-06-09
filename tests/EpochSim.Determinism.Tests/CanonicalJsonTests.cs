using EpochSim.Serialization.State;
using Xunit;

public sealed class CanonicalJsonTests
{
    [Theory]
    [InlineData("{\"a\":1.0}", "{\"a\":1}")]
    [InlineData("{\"a\":2.0,\"b\":3.0}", "{\"a\":2,\"b\":3}")]
    [InlineData("{\"x\":0.0}", "{\"x\":0}")]
    [InlineData("{\"n\":-5.0}", "{\"n\":-5}")]
    public void Canonicalize_IntegerValuedFloats_SameAsPureIntegers(string withDecimal, string withoutDecimal)
    {
        var c1 = CanonicalJson.Canonicalize(withDecimal);
        var c2 = CanonicalJson.Canonicalize(withoutDecimal);
        Assert.Equal(c1, c2);
    }

    [Theory]
    [InlineData("{\"a\":1.0}", "{\"a\":1}")]
    [InlineData("{\"b\":42.00}", "{\"b\":42}")]
    public void Fingerprint_IntegerValuedFloat_EqualsIntegerFingerprint(string jsonA, string jsonB)
    {
        var fp1 = StateFingerprint.ComputeFromJson(jsonA);
        var fp2 = StateFingerprint.ComputeFromJson(jsonB);
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void Canonicalize_SortsObjectKeysLexicographically()
    {
        var unordered = "{\"z\":1,\"a\":2,\"m\":3}";
        var ordered = "{\"a\":2,\"m\":3,\"z\":1}";
        Assert.Equal(CanonicalJson.Canonicalize(ordered), CanonicalJson.Canonicalize(unordered));
    }

    [Fact]
    public void Canonicalize_NonIntegerFloat_Preserved()
    {
        var c = CanonicalJson.Canonicalize("{\"v\":1.5}");
        Assert.Contains("1.5", c);
    }

    [Fact]
    public void Canonicalize_NegativeAndZero_DoNotThrow()
    {
        var c = CanonicalJson.Canonicalize("{\"a\":-1,\"b\":0,\"c\":-3.14}");
        Assert.NotEmpty(c);
    }
}
