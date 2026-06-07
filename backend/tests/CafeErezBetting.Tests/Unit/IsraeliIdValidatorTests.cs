using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class IsraeliIdValidatorTests
{
    // Israeli ID Luhn-like algorithm
    private static bool IsValidId(string id)
    {
        if (id.Length != 9 || !id.All(char.IsDigit)) return false;
        var sum = id.Select((c, i) =>
        {
            var val = (c - '0') * (i % 2 == 0 ? 1 : 2);
            return val > 9 ? val - 9 : val;
        }).Sum();
        return sum % 10 == 0;
    }

    [Theory]
    [InlineData("123456782", true)]   // valid
    [InlineData("000000000", false)]  // invalid checksum
    [InlineData("12345678",  false)]  // too short
    [InlineData("1234567890", false)] // too long
    [InlineData("abcdefghi", false)]  // non-numeric
    public void IsValidId_ReturnsExpected(string id, bool expected)
    {
        Assert.Equal(expected, IsValidId(id));
    }
}
