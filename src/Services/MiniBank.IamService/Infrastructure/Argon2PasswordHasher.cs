using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using MiniBank.IamService.Application;

namespace MiniBank.IamService.Infrastructure;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int DegreeOfParallelism = 1;
    private const int MemorySize = 65536;
    private const int Iterations = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt);

        var saltBase64 = Convert.ToBase64String(salt);
        var hashBase64 = Convert.ToBase64String(hash);
        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool Verify(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return false;

            var paramPart = parts[2];
            var saltBase64 = parts[3];
            var hashBase64 = parts[4];

            var salt = Convert.FromBase64String(saltBase64);
            var expectedHash = Convert.FromBase64String(hashBase64);
            var actualHash = ComputeHash(password, salt);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] ComputeHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };
        return argon2.GetBytes(HashSize);
    }
}
