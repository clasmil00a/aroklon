using Pos.Domain.Security;
using Xunit;

namespace Pos.Domain.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("Rahasia1")]
    public void Three_or_more_characters_is_accepted(string password)
    {
        PasswordPolicy.Validate(password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
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
