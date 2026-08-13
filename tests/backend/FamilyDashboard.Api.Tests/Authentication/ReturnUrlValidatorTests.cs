using FamilyDashboard.Api.Features.Authentication;

namespace FamilyDashboard.Api.Tests.Authentication;

public sealed class ReturnUrlValidatorTests
{
    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/households/123?tab=members", "/households/123?tab=members")]
    public void AcceptsOnlyLocalApplicationPaths(string? input, string expected)
    {
        Assert.True(ReturnUrlValidator.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example")]
    [InlineData("dashboard")]
    [InlineData("/safe\nunsafe")]
    public void RejectsExternalOrAmbiguousPaths(string input)
    {
        Assert.False(ReturnUrlValidator.TryNormalize(input, out _));
    }
}
