using System.Security.Cryptography;
using System.Text;

namespace EpochSim.Serialization.State;

public static class StateFingerprint
{
    public static string ComputeFromJson(string stateJson)
    {
        var canon = CanonicalJson.Canonicalize(stateJson);
        var bytes = Encoding.UTF8.GetBytes(canon);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
