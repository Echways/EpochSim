using System.Security.Cryptography;
using System.Text;

namespace EpochSim.Observability.Tracing;

public static class TraceFingerprint
{
    public static string Compute(IEnumerable<TraceRecord> records)
    {
        using var sha = SHA256.Create();
        foreach (var r in records.OrderBy(x => x.Time.Tick).ThenBy(x => x.Type).ThenBy(x => x.Name))
        {
            Update(sha, r.Time.Tick);
            Update(sha, r.Type);
            Update(sha, r.Name);
            Update(sha, r.DurationTicks);
            Update(sha, r.Detail);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    private static void Update(HashAlgorithm sha, long v)
    {
        Span<byte> b = stackalloc byte[8];
        BitConverter.TryWriteBytes(b, v);
        sha.TransformBlock(b.ToArray(), 0, 8, null, 0);
    }

    private static void Update(HashAlgorithm sha, long? v)
    {
        Update(sha, v.HasValue ? v.Value : long.MinValue);
    }

    private static void Update(HashAlgorithm sha, string? s)
    {
        if (s is null)
        {
            Update(sha, -1L);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(s);
        Update(sha, bytes.Length);
        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
    }
}
