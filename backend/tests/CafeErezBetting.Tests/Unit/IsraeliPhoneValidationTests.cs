using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class IsraeliPhoneValidationTests
{
    private static readonly Regex PhoneRegex =
        new(@"^0(50|52|53|54|55|58|2|3|4|8|9)\d{7}$");

    [Theory]
    [InlineData("0501234567", true)]
    [InlineData("0521234567", true)]
    [InlineData("0531234567", true)]
    [InlineData("0541234567", true)]
    [InlineData("0551234567", true)]
    [InlineData("0581234567", true)]
    [InlineData("021234567", true)]  // area code 02 (9-digit landline)
    [InlineData("031234567", true)]  // area code 03 (9-digit landline)
    [InlineData("041234567", true)]  // area code 04 (9-digit landline)
    [InlineData("081234567", true)]  // area code 08 (9-digit landline)
    [InlineData("091234567", true)]  // area code 09 (9-digit landline)
    [InlineData("0701234567", false)] // 07x invalid
    [InlineData("0601234567", false)] // 06x invalid
    [InlineData("050123456", false)]  // too short
    [InlineData("05012345678", false)] // too long
    [InlineData("1501234567", false)] // doesn't start with 0
    [InlineData("", false)]
    public void IsraeliPhone_Regex_Validation(string phone, bool expected)
    {
        var result = PhoneRegex.IsMatch(phone);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("050-1234567", "0501234567", true)]  // dashes removed
    [InlineData("054 1234567", "0541234567", true)]  // spaces removed
    public void IsraeliPhone_AfterNormalization_IsValid(string rawPhone, string normalized, bool expected)
    {
        var clean = rawPhone.Replace("-", "").Replace(" ", "");
        clean.Should().Be(normalized);
        PhoneRegex.IsMatch(clean).Should().Be(expected);
    }
}
