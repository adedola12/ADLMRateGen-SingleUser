using System;
using ADLMRateGen.Helpers;
using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// The "Build with AI" panel is shown only when AppEnvironment.AiServiceUrl
/// resolves to something, so these tests guard the feature's visibility.
///
/// v2.8.1 and earlier read ADLM_AI_URL and nothing else. Nothing on a user's
/// machine sets that variable — not the installer, not the app — so the URL was
/// null on every install, the AI service was never constructed, and the panel
/// was hidden for everyone except the development box that had the variable
/// exported by hand. A default that survives an empty environment is the fix,
/// and that is what the first test here holds in place.
/// </summary>
[Collection(TestCollections.ProcessEnvironment)]
public class AiServiceUrlTests
{
    private const string ProductVar = "ADLM_RATEGEN_AI_URL";
    private const string FleetVar = "ADLM_AI_URL";

    [Fact]
    public void DefaultsToTheDeployedService_WhenNothingIsConfigured()
    {
        using var _ = new EnvironmentScope((ProductVar, null), (FleetVar, null));

        Assert.Equal(AppEnvironment.DefaultAiServiceUrl, AppEnvironment.AiServiceUrl);
    }

    [Fact]
    public void DefaultIsAnAbsoluteHttpsUrlWithNoTrailingSlash()
    {
        // The SDK appends "/api/ai/..." to this value verbatim.
        Assert.StartsWith("https://", AppEnvironment.DefaultAiServiceUrl, StringComparison.Ordinal);
        Assert.DoesNotContain(" ", AppEnvironment.DefaultAiServiceUrl);
        Assert.False(AppEnvironment.DefaultAiServiceUrl.EndsWith("/", StringComparison.Ordinal));
    }

    [Fact]
    public void FleetVariableOverridesTheDefault_AndLosesItsTrailingSlash()
    {
        using var _ = new EnvironmentScope(
            (ProductVar, null),
            (FleetVar, "https://staging.example.com/"));

        Assert.Equal("https://staging.example.com", AppEnvironment.AiServiceUrl);
    }

    [Fact]
    public void ProductVariableWinsOverTheFleetVariable()
    {
        using var _ = new EnvironmentScope(
            (ProductVar, "https://rategen.example.com"),
            (FleetVar, "https://fleet.example.com"));

        Assert.Equal("https://rategen.example.com", AppEnvironment.AiServiceUrl);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("OFF")]
    [InlineData("none")]
    [InlineData("disabled")]
    [InlineData("false")]
    [InlineData("0")]
    public void SwitchedOffExplicitly_HidesTheFeature(string value)
    {
        using var _ = new EnvironmentScope((ProductVar, null), (FleetVar, value));

        Assert.Null(AppEnvironment.AiServiceUrl);
    }

    [Fact]
    public void BlankValuesAreIgnored_NotTreatedAsOff()
    {
        // An installer that writes an empty string must not disable the feature.
        using var _ = new EnvironmentScope((ProductVar, "   "), (FleetVar, ""));

        Assert.Equal(AppEnvironment.DefaultAiServiceUrl, AppEnvironment.AiServiceUrl);
    }

    /// <summary>Sets process environment variables and restores them on dispose.</summary>
    private sealed class EnvironmentScope : IDisposable
    {
        private readonly (string Name, string? Original)[] _saved;

        public EnvironmentScope(params (string Name, string? Value)[] values)
        {
            _saved = new (string, string?)[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                _saved[i] = (values[i].Name, Environment.GetEnvironmentVariable(values[i].Name));
                Environment.SetEnvironmentVariable(values[i].Name, values[i].Value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, original) in _saved)
                Environment.SetEnvironmentVariable(name, original);
        }
    }
}
