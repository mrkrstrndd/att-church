using System.Text.RegularExpressions;
using ChurchAttendance.Domain.Entities;

namespace ChurchAttendance.Tests;

public class MemberQrCodeTests
{
    [Fact]
    public void NewTokens_AreUniqueLowercaseHexAndParseable()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => MemberQrCode.NewToken()).ToHashSet();

        Assert.Equal(100, tokens.Count);
        foreach (var token in tokens)
        {
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), token);
            Assert.True(MemberQrCode.TryParseToken(MemberQrCode.BuildPayload(token), out var parsed));
            Assert.Equal(token, parsed);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-code")]
    [InlineData("church-attendance://member/abc")]
    [InlineData("church-attendance://member/zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void TryParseToken_RejectsMalformedValues(string value)
    {
        Assert.False(MemberQrCode.TryParseToken(value, out var token));
        Assert.Empty(token);
    }

    [Fact]
    public void TryParseToken_AcceptsRawToken()
    {
        var token = MemberQrCode.NewToken();

        Assert.True(MemberQrCode.TryParseToken(token, out var parsed));
        Assert.Equal(token, parsed);
    }
}
