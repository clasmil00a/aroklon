namespace Pos.Domain.Security;

public interface IPasswordHasher
{
    Task<string> HashAsync(string password, CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(string password, string encodedHash, CancellationToken cancellationToken = default);
}

public static class PasswordPolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 128;

    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
        {
            throw new ValidationException(
                "auth.password_weak",
                $"Kata sandi minimal {MinimumLength} karakter.");
        }

        if (password.Length > MaximumLength)
        {
            throw new ValidationException(
                "auth.password_weak",
                $"Kata sandi maksimal {MaximumLength} karakter.");
        }
    }
}
