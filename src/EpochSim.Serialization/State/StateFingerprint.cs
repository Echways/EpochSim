using System.Security.Cryptography;
using System.Text;

namespace EpochSim.Serialization.State;

public static class StateFingerprint
{
    public static string ComputeFromJson(string stateJson)
    {
        var canonicalJson = CanonicalJson.Canonicalize(stateJson);
        ReadOnlySpan<char> canonicalSpan = canonicalJson;
        var byteCount = Encoding.UTF8.GetByteCount(canonicalSpan);
        var bytes = GC.AllocateUninitializedArray<byte>(byteCount);
        Encoding.UTF8.GetBytes(canonicalSpan, bytes);

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
