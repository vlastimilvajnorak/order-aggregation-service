namespace OrderAggregationService.Endpoints;

/// <summary>
/// Where the API description is served from.
/// </summary>
/// <remarks>
/// The document itself is produced by the built-in OpenAPI support. Swagger UI is only
/// a reader of that document, so both addresses live here and neither is repeated as a
/// literal in the pipeline.
/// </remarks>
public static class OpenApiEndpoints
{
    /// <summary>
    /// Route of the generated OpenAPI document.
    /// </summary>
    public const string DocumentPath = "/openapi/v1.json";

    /// <summary>
    /// Route prefix Swagger UI is served under, without a leading slash.
    /// </summary>
    public const string SwaggerUiPrefix = "swagger";
}
