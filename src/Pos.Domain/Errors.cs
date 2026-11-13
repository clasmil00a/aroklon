namespace Pos.Domain;

public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string Conflict = "conflict";
    public const string NotFound = "not_found";
    public const string PermissionDenied = "permission.denied";
    public const string AuthInvalid = "auth.invalid";
    public const string AuthLocked = "auth.locked";
    public const string AuthOnboardingRequired = "auth.onboarding_required";
    public const string Database = "database";
}

public class PosException : Exception
{
    public PosException(string code, string message) : base(message)
    {
        Code = code;
    }

    public PosException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class ValidationException : PosException
{
    public ValidationException(string code, string message) : base(code, message)
    {
    }
}

public sealed class ConflictException : PosException
{
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}

public sealed class NotFoundException : PosException
{
    public NotFoundException(string code, string message) : base(code, message)
    {
    }
}

public sealed class PermissionException : PosException
{
    public PermissionException(string message) : base(ErrorCodes.PermissionDenied, message)
    {
    }
}

public sealed class AuthException : PosException
{
    public AuthException(string code, string message) : base(code, message)
    {
    }
}
