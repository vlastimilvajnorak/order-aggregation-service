using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OrderAggregationService.Endpoints;

/// <summary>
/// Exposes the health of the service as a machine readable document.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Route of the health endpoint.
    /// </summary>
    public const string HealthPath = "/health";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the health endpoint onto the application.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(HealthPath, new HealthCheckOptions { ResponseWriter = WriteResponseAsync })
            .WithTags("Diagnostics")
            .WithName("Health");

        return endpoints;
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var checks = new List<HealthCheckEntryResponse>(report.Entries.Count);

        foreach (var (name, entry) in report.Entries)
        {
            checks.Add(new HealthCheckEntryResponse(
                name,
                entry.Status.ToString(),
                entry.Description,
                entry.Duration.TotalMilliseconds,
                new Dictionary<string, object>(entry.Data, StringComparer.Ordinal)));
        }

        var payload = new HealthResponse(
            report.Status.ToString(),
            report.TotalDuration.TotalMilliseconds,
            checks);

        return context.Response.WriteAsJsonAsync(payload, SerializerOptions, context.RequestAborted);
    }
}

/// <summary>
/// Overall health of the service.
/// </summary>
/// <param name="Status">Aggregated health status.</param>
/// <param name="DurationMs">Time it took to evaluate all checks.</param>
/// <param name="Checks">Result of every individual check.</param>
internal sealed record HealthResponse(
    string Status,
    double DurationMs,
    IReadOnlyList<HealthCheckEntryResponse> Checks);

/// <summary>
/// Result of a single health check.
/// </summary>
/// <param name="Name">Registered name of the check.</param>
/// <param name="Status">Status reported by the check.</param>
/// <param name="Description">Human readable description, when the check provided one.</param>
/// <param name="DurationMs">Time it took to evaluate the check.</param>
/// <param name="Data">Additional data reported by the check.</param>
internal sealed record HealthCheckEntryResponse(
    string Name,
    string Status,
    string? Description,
    double DurationMs,
    IReadOnlyDictionary<string, object> Data);
