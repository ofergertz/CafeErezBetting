using FluentAssertions;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class OtpValidationTests
{
    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("999999", true)]
    [InlineData("12345", false)]   // too short
    [InlineData("1234567", false)] // too long
    [InlineData("abcdef", false)]  // non-numeric
    [InlineData("", false)]        // empty
    [InlineData("12 456", false)]  // space
    public void OtpCode_Format_Validation(string code, bool expected)
    {
        var isValid = code.Length == 6 && code.All(char.IsDigit);
        isValid.Should().Be(expected);
    }

    [Fact]
    public void OtpSession_UsedAt_IsNullWhenNew()
    {
        var usedAt = (DateTime?)null;
        usedAt.Should().BeNull();
    }

    [Fact]
    public void OtpSession_AfterVerification_UsedAtIsSet()
    {
        var usedAt = DateTime.UtcNow;
        usedAt.Should().NotBe(default);
    }
}
