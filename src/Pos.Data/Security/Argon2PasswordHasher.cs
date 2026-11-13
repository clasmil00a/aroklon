using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Pos.Domain.Security;

namespace Pos.Data.Security;

public sealed record Argon2Options(
    int MemorySizeKb,
    int Iterations,
    int DegreeOfParallelism,
    int SaltLength = 16,
    int HashLength = 32)
{
    public static Argon2Options Default { get; } = new(65_536, 3, 2);

    /// <summary>Lower cost for test suites only; never use in production.</summary>
    public static Argon2Options FastForTests { get; } = new(8_192, 1, 1);
}

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private readonly Argon2Options _options;

    public Argon2PasswordHasher(Argon2Options options)
    {
        if (options.MemorySizeKb < 8 || options.Iterations < 1 || options.DegreeOfParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Parameter Argon2 tidak valid.");
        }

        _options = options;
    }

    public async Task<string> HashAsync(string password, CancellationToken cancellationToken = default)
    {
        if (password is null)
        {
            throw new ArgumentNullException(nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(_options.SaltLength);
        var hash = await DeriveAsync(
                password,
                salt,
                _options.MemorySizeKb,
                _options.Iterations,
                _options.DegreeOfParallelism,
                _options.HashLength,
                cancellationToken)
            .ConfigureAwait(false);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"$argon2id$v=19$m={_options.MemorySizeKb},t={_options.Iterations},p={_options.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public async Task<bool> VerifyAsync(
        string password,
        string encodedHash,
        CancellationToken cancellationToken = default)
    {
        if (password is null || string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        try
        {
            var parts = encodedHash.Split('$');
            if (parts.Length != 6 ||
                !string.Equals(parts[1], "argon2id", StringComparison.Ordinal) ||
                !string.Equals(parts[2], "v=19", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = ParseParameters(parts[3]);
            var salt = Convert.FromBase64String(parts[4]);
            var expected = Convert.FromBase64String(parts[5]);
            if (salt.Length == 0 || expected.Length == 0)
            {
                return false;
            }

            var actual = await DeriveAsync(
                    password,
                    salt,
                    parameters.MemorySizeKb,
                    parameters.Iterations,
                    parameters.DegreeOfParallelism,
                    expected.Length,
                    cancellationToken)
                .ConfigureAwait(false);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static async Task<byte[]> DeriveAsync(
        string password,
        byte[] salt,
        int memorySizeKb,
        int iterations,
        int parallelism,
        int hashLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySizeKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        var result = await argon2.GetBytesAsync(hashLength).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static Argon2Parameters ParseParameters(string text)
    {
        var values = text.Split(',');
        var memory = 0;
        var iterations = 0;
        var parallelism = 0;
        foreach (var value in values)
        {
            var pair = value.Split('=', 2);
            if (pair.Length != 2)
            {
                throw new FormatException("Parameter Argon2 tidak valid.");
            }

            var number = int.Parse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture);
            switch (pair[0])
            {
                case "m":
                    memory = number;
                    break;
                case "t":
                    iterations = number;
                    break;
                case "p":
                    parallelism = number;
                    break;
            }
        }

        if (memory <= 0 || iterations <= 0 || parallelism <= 0)
        {
            throw new FormatException("Parameter Argon2 tidak lengkap.");
        }

        return new Argon2Parameters(memory, iterations, parallelism);
    }

    private sealed record Argon2Parameters(int MemorySizeKb, int Iterations, int DegreeOfParallelism);
}
