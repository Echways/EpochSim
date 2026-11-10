using System.Security.Cryptography;
using System.Text;

namespace EpochSim.Fuzzing;

internal static class FailureSignature
{
    private const int HexLength = 16;

    public static string Of(FuzzFailureIdentity identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{identity.Kind}|{identity.Name}"));
        return Convert.ToHexStringLower(hash)[..HexLength];
    }
}
