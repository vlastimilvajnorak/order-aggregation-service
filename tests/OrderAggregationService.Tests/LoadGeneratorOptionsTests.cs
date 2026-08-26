using OrderAggregationService.LoadGenerator;

namespace OrderAggregationService.Tests;

/// <summary>
/// The load generator is a developer tool, but its argument parsing is still observable
/// behaviour: a silently ignored option would make a load run mean nothing.
/// </summary>
public sealed class LoadGeneratorOptionsTests
{
    [Fact]
    public void Parse_WithoutArguments_KeepsTheDefaults()
    {
        var options = LoadGeneratorOptions.Parse([]);

        Assert.Equal(LoadGeneratorOptions.Default, options);
    }

    [Fact]
    public void Parse_OverridesOnlyWhatWasGiven()
    {
        var options = LoadGeneratorOptions.Parse(["--rps", "500", "--products", "300"]);

        Assert.Equal(500, options.RequestsPerSecond);
        Assert.Equal(300, options.ProductCount);
        Assert.Equal(LoadGeneratorOptions.Default.OrdersPerRequest, options.OrdersPerRequest);
        Assert.Equal(LoadGeneratorOptions.Default.BaseAddress, options.BaseAddress);
    }

    [Fact]
    public void Parse_ReadsTheServiceAddress()
    {
        var options = LoadGeneratorOptions.Parse(["--url", "http://localhost:8080"]);

        Assert.Equal(new Uri("http://localhost:8080"), options.BaseAddress);
    }

    [Fact]
    public void Default_RunsContinuouslyInWaves()
    {
        // A generator that stops on its own looks like a crash to whoever is watching
        // the dashboard, so the default keeps going until interrupted.
        Assert.Equal(TimeSpan.Zero, LoadGeneratorOptions.Default.Duration);
        Assert.True(LoadGeneratorOptions.Default.PulsePeriod > TimeSpan.Zero);
    }

    [Fact]
    public void Parse_ReadsThePulsePeriod()
    {
        var options = LoadGeneratorOptions.Parse(["--pulse", "45"]);

        Assert.Equal(TimeSpan.FromSeconds(45), options.PulsePeriod);
    }

    [Fact]
    public void Parse_ZeroPulse_MeansAFlatRate()
    {
        var options = LoadGeneratorOptions.Parse(["--pulse", "0"]);

        Assert.Equal(TimeSpan.Zero, options.PulsePeriod);
    }

    [Fact]
    public void Parse_ZeroDuration_MeansRunUntilInterrupted()
    {
        var options = LoadGeneratorOptions.Parse(["--duration", "0"]);

        Assert.Equal(TimeSpan.Zero, options.Duration);
    }

    [Theory]
    [InlineData("--unknown", "1")]
    [InlineData("--rps", "0")]
    [InlineData("--rps", "-1")]
    [InlineData("--rps", "many")]
    [InlineData("--url", "not-a-url")]
    [InlineData("--duration", "-5")]
    [InlineData("--pulse", "-1")]
    public void Parse_RejectsUnusableArguments(string name, string value)
    {
        Assert.Throws<ArgumentException>(() => LoadGeneratorOptions.Parse([name, value]));
    }

    [Fact]
    public void Parse_OptionWithoutAValue_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => LoadGeneratorOptions.Parse(["--rps"]));
    }
}
