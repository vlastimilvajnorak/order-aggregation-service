using OrderAggregationService.Components;
using OrderAggregationService.Endpoints;
using OrderAggregationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Presentation: Blazor Web App with interactive server components.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Aggregation pipeline, dispatch scaffold and the aggregation health check.
builder.Services.AddOrderAggregation(builder.Configuration);

// API concerns: RFC 9457 problem details and the OpenAPI document.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, transformerContext, cancellationToken) =>
    {
        if (document.Info is { } info)
        {
            info.Title = "Order Aggregation Service API";
            info.Version = "v1";
            info.Description =
                "REST API for submitting order batches and reading aggregated product quantities.";
        }

        return Task.CompletedTask;
    }));

var app = builder.Build();

// API callers get problem details; browser traffic gets the Blazor error and not-found pages.
app.UseWhen(IsApiRequest, static branch => branch.UseExceptionHandler());
app.UseWhen(
    static context => !IsApiRequest(context),
    static branch =>
    {
        branch.UseExceptionHandler("/Error", createScopeForErrors: true);
        branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    });

if (!app.Environment.IsDevelopment())
{
    // The default HSTS max-age is 30 days; review it before a real production rollout.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapOrderEndpoints();
app.MapHealthEndpoints();
app.MapOpenApi();

app.Run();

static bool IsApiRequest(HttpContext context) =>
    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

/// <summary>
/// Application entry point. Declared explicitly so that integration tests can use it as the
/// <c>WebApplicationFactory</c> type argument.
/// </summary>
public partial class Program;
