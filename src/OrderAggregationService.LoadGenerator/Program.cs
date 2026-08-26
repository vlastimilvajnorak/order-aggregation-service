using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;

namespace OrderAggregationService.LoadGenerator;

/// <summary>
/// Submits random order batches at a running service, so the 20-second hand-over, the
/// dashboard and the aggregate have realistic traffic to show.
/// </summary>
/// <remarks>
/// Declared as an explicit class rather than top-level statements: the service uses
/// top-level statements too, and two of them in one solution both emit a global
/// <c>Program</c>, which makes <c>WebApplicationFactory&lt;Program&gt;</c> ambiguous in the
/// test project.
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help", StringComparer.Ordinal))
        {
            Console.WriteLine(LoadGeneratorOptions.Usage);
            return 0;
        }

        LoadGeneratorOptions options;

        try
        {
            options = LoadGeneratorOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(LoadGeneratorOptions.Usage);
            return 1;
        }

        using var cancellation = new CancellationTokenSource();
        var interrupted = false;

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            interrupted = true;
            cancellation.Cancel();
        };

        if (options.Duration > TimeSpan.Zero)
        {
            cancellation.CancelAfter(options.Duration);
        }

        // The start menu can change the target and the catalogue, so settle the settings
        // before anything is built from them.
        var chosen = ConsoleUi.RunStartMenu(options);

        if (chosen is null)
        {
            return 0;
        }

        options = chosen;

        if (options.Duration > TimeSpan.Zero)
        {
            cancellation.CancelAfter(options.Duration);
        }

        // Bounded resources throughout: a socket cap, an explicit per-request timeout,
        // and further down a ceiling on in-flight requests. Without them a slow server
        // makes the generator hoard sockets and memory until it stops dead.
        using var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 256,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = options.BaseAddress,
            Timeout = TimeSpan.FromSeconds(10),
        };

        var productIds = Enumerable.Range(1, options.ProductCount)
            .Select(static id => id.ToString(CultureInfo.InvariantCulture))
            .ToArray();

        // Fail fast when nothing is listening. Without this, starting the generator
        // before the service produces a full run of silent connection failures - and
        // under a debugger, a stream of first-chance HttpRequestExceptions.
        try
        {
            using var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var probe = await client.GetAsync("/health", probeTimeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  No service is answering at {options.BaseAddress}.");
            Console.Error.WriteLine("  Start it first: dotnet run --project src/OrderAggregationService");
            Console.Error.WriteLine($"  Or choose another target in the menu. ({exception.Message})");
            Console.Error.WriteLine();
            if (!Console.IsInputRedirected)
            {
                Console.Error.WriteLine("  Press any key to close.");
                Console.ReadKey(intercept: true);
            }

            return 2;
        }

        var countersRow = ConsoleUi.DrawRunHeader(options);

        var accepted = 0;
        var rejected = 0;
        var failed = 0;
        var timedOut = 0;
        var shed = 0;
        var ordersSent = 0;
        var unitsSent = 0L;

        var started = Stopwatch.GetTimestamp();
        using var tick = new PeriodicTimer(TimeSpan.FromSeconds(1));

        RunCounters Snapshot() => new(
            Volatile.Read(ref accepted),
            Volatile.Read(ref rejected),
            Volatile.Read(ref failed),
            Volatile.Read(ref timedOut),
            Volatile.Read(ref shed),
            Volatile.Read(ref ordersSent),
            Volatile.Read(ref unitsSent));

        // The in-flight ceiling is what keeps the cadence honest: each tick schedules its
        // requests without waiting for the previous second's to finish, so a slow server
        // cannot stretch the tick - it fills the window instead, and everything over the
        // ceiling is counted as shed rather than silently queued.
        var inFlight = 0;
        var inFlightCeiling = Math.Max(options.RequestsPerSecond * 2, 64);

        try
        {
            do
            {
                for (var index = 0; index < options.RequestsPerSecond; index++)
                {
                    if (Volatile.Read(ref inFlight) >= inFlightCeiling)
                    {
                        Interlocked.Increment(ref shed);
                        continue;
                    }

                    Interlocked.Increment(ref inFlight);
                    _ = TrackAsync();
                }
            }
            while (await tick.WaitForNextTickAsync(cancellation.Token));
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C, or the configured duration elapsed.
        }

        // Let what was already sent finish, so the final tally counts every answer. The
        // per-request timeout bounds this wait; it cannot hang.
        while (Volatile.Read(ref inFlight) > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        ConsoleUi.DrawCounters(countersRow, Snapshot(), elapsed, finished: true);

        Console.WriteLine(interrupted
            ? "  Stopped by Ctrl+C."
            : $"  Stopped after the configured duration of " +
              $"{options.Duration.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture)} s. " +
              "Set duration to 0 to run until Ctrl+C.");

        return failed == 0 && timedOut == 0 ? 0 : 1;

        async Task TrackAsync()
        {
            try
            {
                await SubmitAsync();
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        async Task SubmitAsync()
        {
            var orderCount = Random.Shared.Next(1, options.OrdersPerRequest + 1);
            var batch = new OrderRequest[orderCount];
            var units = 0;

            for (var index = 0; index < orderCount; index++)
            {
                var quantity = Random.Shared.Next(1, 11);
                batch[index] = new OrderRequest(
                    productIds[Random.Shared.Next(productIds.Length)],
                    quantity);
                units += quantity;
            }

            try
            {
                // Deliberately not passing the shutdown token: cancelling an in-flight
                // request tears the connection down with a TaskCanceledException on both
                // sides. The token ends the submit loop; requests already sent complete.
                using var response = await client.PostAsJsonAsync("/api/orders", batch);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Increment(ref accepted);
                    Interlocked.Add(ref ordersSent, orderCount);
                    Interlocked.Add(ref unitsSent, units);
                }
                else
                {
                    Interlocked.Increment(ref rejected);
                }
            }
            catch (OperationCanceledException)
            {
                // The shutdown token never reaches the request, so this is the
                // 10-second HttpClient timeout: the server did not answer in time.
                Interlocked.Increment(ref timedOut);
            }
            catch (HttpRequestException)
            {
                Interlocked.Increment(ref failed);
            }
        }
    }
}
