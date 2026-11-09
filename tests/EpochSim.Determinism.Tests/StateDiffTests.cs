using EpochSim.Serialization.State;

public sealed class StateDiffTests
{
    [Fact]
    public void Compute_EqualDocumentsWithDifferentPropertyOrder_ReturnsEmpty()
    {
        var changes = StateDiff.Compute("""{"a":1,"b":{"x":true,"y":[1,2]}}""", """{"b":{"y":[1,2],"x":true},"a":1}""");

        Assert.Empty(changes);
    }

    [Fact]
    public void Compute_ChangedScalar_ReportsPathAndValues()
    {
        var changes = StateDiff.Compute("""{"Population":3}""", """{"Population":-2}""");

        var change = Assert.Single(changes);
        Assert.Equal(new StateChange("$.Population", StateChangeKind.Changed, "3", "-2"), change);
    }

    [Fact]
    public void Compute_AddedAndRemovedProperties_AreReported()
    {
        var changes = StateDiff.Compute("""{"Old":1,"Kept":2}""", """{"Kept":2,"New":"x"}""");

        Assert.Equal(
            [
                new StateChange("$.New", StateChangeKind.Added, null, "\"x\""),
                new StateChange("$.Old", StateChangeKind.Removed, "1", null)
            ],
            changes);
    }

    [Fact]
    public void Compute_NestedObject_UsesDottedPath()
    {
        var changes = StateDiff.Compute("""{"Map":{"key":1}}""", """{"Map":{"key":2}}""");

        Assert.Equal("$.Map.key", Assert.Single(changes).Path);
    }

    [Fact]
    public void Compute_ArraysOfDifferentLength_ReportTailByIndex()
    {
        var grown = StateDiff.Compute("""{"Items":[1,2]}""", """{"Items":[1,2,7]}""");
        var shrunk = StateDiff.Compute("""{"Items":[1,2,7]}""", """{"Items":[1]}""");

        Assert.Equal(new StateChange("$.Items[2]", StateChangeKind.Added, null, "7"), Assert.Single(grown));
        Assert.Equal(
            [
                new StateChange("$.Items[1]", StateChangeKind.Removed, "2", null),
                new StateChange("$.Items[2]", StateChangeKind.Removed, "7", null)
            ],
            shrunk);
    }

    [Fact]
    public void Compute_ArrayElementField_UsesIndexedPath()
    {
        var changes = StateDiff.Compute("""{"Items":[{"Count":1},{"Count":2}]}""", """{"Items":[{"Count":1},{"Count":5}]}""");

        Assert.Equal("$.Items[1].Count", Assert.Single(changes).Path);
    }

    [Fact]
    public void Compute_KeysWithDotsAndSpaces_UseBracketNotation()
    {
        var changes = StateDiff.Compute("""{"a.b":1,"c d":1}""", """{"a.b":2,"c d":2}""");

        Assert.Equal(["$[\"a.b\"]", "$[\"c d\"]"], changes.Select(c => c.Path));
    }

    [Fact]
    public void Compute_TypeChange_IsReportedAsChanged()
    {
        var changes = StateDiff.Compute("""{"v":1}""", """{"v":{"a":1}}""");

        var change = Assert.Single(changes);
        Assert.Equal(StateChangeKind.Changed, change.Kind);
        Assert.Equal("{\"a\":1}", change.After);
    }

    [Fact]
    public void Compute_NullValue_IsDistinctFromMissing()
    {
        var changes = StateDiff.Compute("""{"v":null}""", """{"v":1}""");

        Assert.Equal(new StateChange("$.v", StateChangeKind.Changed, "null", "1"), Assert.Single(changes));
    }

    [Fact]
    public void Compute_RootScalars_UseRootPath()
    {
        var changes = StateDiff.Compute("1", "2");

        Assert.Equal(new StateChange("$", StateChangeKind.Changed, "1", "2"), Assert.Single(changes));
    }

    [Fact]
    public void Compute_IsDeterministicAndOrdinallyOrdered()
    {
        const string before = """{"b":1,"a":1,"c":1}""";
        const string after = """{"c":2,"b":2,"a":2}""";

        var first = StateDiff.Compute(before, after);
        var second = StateDiff.Compute(before, after);

        Assert.Equal(first, second);
        Assert.Equal(["$.a", "$.b", "$.c"], first.Select(c => c.Path));
    }

    [Fact]
    public void Compute_LongValues_AreTruncatedWithEllipsis()
    {
        var longValue = new string('x', 200);

        var changes = StateDiff.Compute($$"""{"v":"{{longValue}}"}""", """{"v":"short"}""");

        var change = Assert.Single(changes);
        Assert.Equal(80, change.Before!.Length);
        Assert.EndsWith("…", change.Before);
        Assert.Equal("\"short\"", change.After);
    }

    [Fact]
    public void Compute_InvalidJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => StateDiff.Compute("{", "{}"));
    }

    [Fact]
    public void Format_ProducesOneLinePerChange()
    {
        var lines = StateDiffFormatter.Format(
        [
            new StateChange("$.Population", StateChangeKind.Changed, "3", "-2"),
            new StateChange("$.Items[2]", StateChangeKind.Added, null, "7"),
            new StateChange("$.Flag", StateChangeKind.Removed, "true", null)
        ]);

        Assert.Equal(["$.Population: 3 -> -2", "$.Items[2]: added 7", "$.Flag: removed true"], lines);
    }

    [Fact]
    public void Format_MoreThanMaxLines_AppendsSummaryLine()
    {
        var changes = Enumerable.Range(0, 5)
            .Select(i => new StateChange($"$.p{i}", StateChangeKind.Changed, "1", "2"))
            .ToArray();

        var lines = StateDiffFormatter.Format(changes, maxLines: 2);

        Assert.Equal(3, lines.Count);
        Assert.Equal("... and 3 more changes", lines[^1]);
    }

    [Fact]
    public void Format_NonPositiveMaxLines_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StateDiffFormatter.Format([], maxLines: 0));
    }
}
