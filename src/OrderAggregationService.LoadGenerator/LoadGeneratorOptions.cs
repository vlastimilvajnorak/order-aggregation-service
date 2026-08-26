using System.Globalization;

namespace OrderAggregationService.LoadGenerator;

/// <summary>
/// How much load to generate and where to send it.
/// </summary>
/// <param name="BaseAddress">Address of the running service.</param>
/// <param name="RequestsPerSecond">Order batches submitted every second.</param>
/// <param name="OrdersPerRequest">Upper bound on the orders in one batch.</param>
/// <param name="ProductCount">Size of the product catalogue the ids are drawn from.</param>
/// <param name="Duration">How long to keep generating. Zero means until interrupted.</param>
/// <param name="PulsePeriod">Length of one traffic wave. Zero submits at a flat rate.</param>
public sealed record LoadGeneratorOptions(
    Uri BaseAddress,
    int RequestsPerSecond,
    int OrdersPerRequest,
    int ProductCount,
    TimeSpan Duration,
    TimeSpan PulsePeriod)
{
    /// <summary>
    /// Defaults chosen to match the stated design target: hundreds of small orders per
    /// second over a catalogue of hundreds of products, flowing continuously in waves
    /// until interrupted.
    /// </summary>
    public static LoadGeneratorOptions Default { get; } = new(
        new Uri("http://localhost:5212"),
        RequestsPerSecond: 200,
        OrdersPerRequest: 3,
        ProductCount: 200,
        Duration: TimeSpan.Zero,
        PulsePeriod: TimeSpan.FromSeconds(30));

    /// <summary>
    /// Usage text shown for <c>--help</c> and for an invalid argument.
    /// </summary>
    public static string Usage =>
        """
        Usage: dotnet run --project src/OrderAggregationService.LoadGenerator -- [options]

          --url <address>       Service base address        (default http://localhost:5212)
          --rps <n>             Requests per second         (default 200)
          --orders <n>          Max orders per request (default 3)
          --products <n>        Distinct product ids        (default 200)
          --duration <seconds>  Run time, 0 runs until Ctrl+C (default 0)
          --pulse <seconds>     Length of one traffic wave, 0 is a flat rate (default 30)
          --help                Show this text
        """;

    /// <summary>
    /// Parses command line arguments over <see cref="Default"/>.
    /// </summary>
    /// <param name="args">The raw arguments.</param>
    /// <returns>The parsed options.</returns>
    /// <exception cref="ArgumentException">An argument was unknown or not usable.</exception>
    public static LoadGeneratorOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = Default;

        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Option '{name}' needs a value.", nameof(args));
            }

            var value = args[index + 1];

            options = name switch
            {
                "--url" => options with { BaseAddress = ParseUri(value) },
                "--rps" => options with { RequestsPerSecond = ParsePositive(name, value) },
                "--orders" => options with { OrdersPerRequest = ParsePositive(name, value) },
                "--products" => options with { ProductCount = ParsePositive(name, value) },
                "--duration" => options with { Duration = ParseDuration(value) },
                "--pulse" => options with { PulsePeriod = ParseDuration(value) },
                _ => throw new ArgumentException($"Unknown option '{name}'.", nameof(args)),
            };
        }

        return options;
    }

    private static Uri ParseUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new ArgumentException($"'{value}' is not an absolute URL.", nameof(value));

    private static int ParsePositive(string name, string value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : throw new ArgumentException($"'{name}' needs a positive whole number.", nameof(value));

    private static TimeSpan ParseDuration(string value) =>
        int.TryParse(value, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException("The option needs a whole number of seconds.", nameof(value));
}
