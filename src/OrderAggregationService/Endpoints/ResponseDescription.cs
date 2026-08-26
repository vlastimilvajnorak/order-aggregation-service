using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OrderAggregationService.Endpoints;

/// <summary>
/// A human readable description for one status code of an endpoint.
/// </summary>
/// <remarks>
/// The built-in OpenAPI generator fills response descriptions with the reason phrase, so
/// every endpoint would document its failure as "Bad Request". Attaching this as endpoint
/// metadata keeps the real wording next to the route it describes, and
/// <see cref="ResponseDescriptionExtensions.AddResponseDescriptions"/> copies it into the
/// generated document.
/// </remarks>
/// <param name="StatusCode">The status code being described.</param>
/// <param name="Description">What the caller should understand from that status code.</param>
public sealed record ResponseDescription(int StatusCode, string Description);

/// <summary>
/// Wires <see cref="ResponseDescription"/> metadata into the generated OpenAPI document.
/// </summary>
public static class ResponseDescriptionExtensions
{
    /// <summary>
    /// Registers the transformer that applies every <see cref="ResponseDescription"/>.
    /// </summary>
    /// <param name="options">The OpenAPI options to extend.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions AddResponseDescriptions(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var descriptions = context.Description.ActionDescriptor.EndpointMetadata
                .OfType<ResponseDescription>();

            foreach (var described in descriptions)
            {
                var key = described.StatusCode.ToString(CultureInfo.InvariantCulture);

                if (operation.Responses is not null &&
                    operation.Responses.TryGetValue(key, out var response) &&
                    response is not null)
                {
                    response.Description = described.Description;
                }
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Removes the <c>null</c> branch the generator adds to a request body bound to a
    /// nullable parameter.
    /// </summary>
    /// <remarks>
    /// The submission handler takes <c>OrderRequest[]?</c> so that a JSON <c>null</c>
    /// body reaches validation and gets a real error message instead of a bare 400. The
    /// generator faithfully turns that into <c>oneOf: [null, array]</c> - and Swagger UI
    /// then renders <c>null</c> as the example request. The contract must not advertise
    /// a body the API rejects, so the null branch is stripped from the document.
    /// </remarks>
    /// <param name="options">The OpenAPI options to extend.</param>
    /// <returns>The same options, for chaining.</returns>
    public static OpenApiOptions AddNonNullableRequestBodies(this OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var contents = operation.RequestBody?.Content;

            if (contents is null)
            {
                return Task.CompletedTask;
            }

            foreach (var content in contents.Values)
            {
                if (content.Schema is OpenApiSchema { OneOf: { Count: > 0 } branches } schema)
                {
                    var value = branches.FirstOrDefault(
                        static branch => branch is OpenApiSchema { Type: not JsonSchemaType.Null });

                    if (value is OpenApiSchema valueSchema && branches.Count == 2)
                    {
                        schema.OneOf = null;
                        schema.Type = valueSchema.Type;
                        schema.Items = valueSchema.Items;
                        schema.Properties = valueSchema.Properties;
                    }
                }
            }

            return Task.CompletedTask;
        });

        return options;
    }
}
