using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
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

    /// <summary>
    /// Maps the health endpoint onto the application.
    /// </summary>
    /// <remarks>
    /// Written as an ordinary route handler rather than <c>MapHealthChecks</c>, because a
    /// health-check endpoint is invisible to the API explorer and would be missing from the
    /// OpenAPI document and from Swagger UI.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(HealthPath, CheckHealthAsync)
            .WithTags("Diagnostics")
            .WithName("Health")
            .WithSummary("Reports whether the service is able to accept and aggregate orders.")
            .WithDescription(
                "Used by the container health check and by orchestrators. The body carries the " +
                "aggregated status, the result of every individual check, and the current backlog.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status200OK,
                "The service is healthy or degraded and is still accepting orders."))
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status503ServiceUnavailable,
                "At least one check reported Unhealthy. The body names the failing check."))
            .WithMetadata(new ResponseDescription(
                StatusCodes.Status500InternalServerError,
                "The health report itself could not be produced. An orchestrator should treat "
                + "this exactly as it treats 503."));

        return endpoints;
    }

    /// <summary>
    /// Runs every registered health check and projects the report onto the wire contract.
    /// </summary>
    /// <param name="healthChecks">The registered health check service.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The health report, with 503 when the service is unhealthy.</returns>
    private static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> CheckHealthAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
        var payload = ToResponse(report);

        return report.Status == HealthStatus.Unhealthy
            ? TypedResults.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(payload);
    }

    private static HealthResponse ToResponse(HealthReport report)
    {
        var checks = new List<HealthCheckEntryResponse>(report.Entries.Count);

        foreach (var (name, entry) in report.Entries)
        {
            checks.Add(new HealthCheckEntryResponse(
                name,
                entry.Status.ToString(),
                entry.Description,
                entry.Duration.TotalMilliseconds,
                new Dictionary<string, JsonElement>(
                    entry.Data.ToDictionary(
                        static pair => pair.Key,
                        static pair => JsonSerializer.SerializeToElement(pair.Value),
                        StringComparer.Ordinal),
                    StringComparer.Ordinal)));
        }

        return new HealthResponse(
            report.Status.ToString(),
            report.TotalDuration.TotalMilliseconds,
            checks);
    }
}

/// <summary>
/// Overall health of the service.
/// </summary>
/// <param name="Status">Aggregated health status: Healthy, Degraded or Unhealthy.</param>
/// <param name="DurationMs">Time it took to evaluate all checks, in milliseconds.</param>
/// <param name="Checks">Result of every individual check.</param>
public sealed record HealthResponse(
    string Status,
    double DurationMs,
    IReadOnlyList<HealthCheckEntryResponse> Checks);

/// <summary>
/// Result of a single health check.
/// </summary>
/// <param name="Name">Registered name of the check.</param>
/// <param name="Status">Status reported by the check.</param>
/// <param name="Description">Human readable description, when the check provided one.</param>
/// <param name="DurationMs">Time it took to evaluate the check, in milliseconds.</param>
/// <param name="Data">Additional data reported by the check, such as the current backlog.</param>
public sealed record HealthCheckEntryResponse(
    string Name,
    string Status,
    string? Description,
    double DurationMs,
    IReadOnlyDictionary<string, JsonElement> Data);
