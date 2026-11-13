using Pos.Domain.Security;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class PasswordPolicyTests
{
    [Fact]
    public void Eight_characters_is_accepted()
    {
        PasswordPolicy.Validate("Rahasia1");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(null)]
    public void Short_or_empty_password_is_rejected(string? password)
    {
        var exception = Assert.Throws<ValidationException>(() => PasswordPolicy.Validate(password));
        Assert.Equal("auth.password_weak", exception.Code);
    }

    [Fact]
    public void Overlong_password_is_rejected()
    {
        var exception = Assert.Throws<ValidationException>(() => PasswordPolicy.Validate(new string('a', 129)));
        Assert.Equal("auth.password_weak", exception.Code);
    }
}
