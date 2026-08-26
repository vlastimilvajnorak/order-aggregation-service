using OrderAggregationService.Components;
using OrderAggregationService.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Logging goes through the console provider, whose writes are queued to a dedicated
// background thread (ConsoleLoggerProcessor) and configured in appsettings to drop when
// the queue is full rather than block the caller. The default builder would also
// register the Debug and EventLog providers, which write synchronously with no queue.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
{
    // Debugging needs the full picture in the IDE, so Development keeps the Debug
    // provider that feeds the VS Output window. It writes synchronously and cannot be
    // queued, which is the accepted trade-off here - and the reason it stays out of
    // every other environment.
    builder.Logging.AddDebug();
}

// Presentation: Blazor Web App with interactive server components.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Aggregation pipeline, dispatch scaffold and the aggregation health check.
builder.Services.AddOrderAggregation(builder.Configuration);

// API concerns: RFC 9457 problem details and the OpenAPI document.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = static context =>
    {
        // A body the server could not read is the caller's problem, and the caller can
        // only fix it if told what was wrong with it. The message describes the request
        // the caller itself sent, so it exposes nothing about the server.
        if (context.Exception is BadHttpRequestException badRequest)
        {
            context.ProblemDetails.Title = "Malformed request";
            context.ProblemDetails.Detail = badRequest.Message;

            return;
        }

        // Statuses the framework produces on its own carry no explanation at all. Saying
        // nothing is the one thing an error contract must not do.
        context.ProblemDetails.Detail ??= context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest =>
                "The request body could not be read as a JSON array of orders.",
            StatusCodes.Status404NotFound => "No such API resource.",
            StatusCodes.Status405MethodNotAllowed =>
                "That method is not allowed on this resource.",
            StatusCodes.Status415UnsupportedMediaType =>
                "The request must declare Content-Type: application/json.",
            _ => null,
        };
    });

// Pinned rather than left to the default, which is true in Development and false
// elsewhere. A caller's malformed body would then be an exception on a developer machine
// and a plain 400 in production - the same request answered two different ways, and the
// environment where it is hardest to reproduce is the one with less detail. Here it is
// always a 400 the pipeline writes, never an exception logged as a server fault.
builder.Services.Configure<RouteHandlerOptions>(static options => options.ThrowOnBadRequest = false);
builder.Services.AddOpenApi(options =>
    options.AddResponseDescriptions()
        .AddNonNullableRequestBodies()
        .AddDocumentTransformer((document, transformerContext, cancellationToken) =>
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

// Machine callers get problem details; browser traffic gets the Blazor error and
// not-found pages.
app.UseWhen(IsMachineEndpoint, static branch =>
{
    // Without a status code selector every escaping exception becomes a 500, including
    // the BadHttpRequestException the framework throws for a body it could not parse or
    // one that exceeded the request size limit. Those are 400 and 413: reporting them as
    // server faults would both mislead the caller and bury real faults in the logs.
    branch.UseExceptionHandler(new ExceptionHandlerOptions
    {
        StatusCodeSelector = static exception => exception switch
        {
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => StatusCodes.Status500InternalServerError,
        },
    });

    // Framework-generated statuses - 404, 405, 415 - reach the client with an empty body
    // unless something writes one. An API contract that promises problem details has to
    // keep that promise for the responses it did not write itself.
    branch.UseStatusCodePages();
});
app.UseWhen(
    static context => !IsMachineEndpoint(context),
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

// Swagger UI renders the document produced by the built-in OpenAPI generator above.
// Only the UI package is referenced; nothing here generates a second document.
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(OpenApiEndpoints.DocumentPath, "Order Aggregation Service v1");
    options.RoutePrefix = OpenApiEndpoints.SwaggerUiPrefix;
    options.DocumentTitle = "Order Aggregation Service API";
    // Blends the page into the charcoal admin shell; see wwwroot/swagger-dark.css.
    options.InjectStylesheet("/swagger-dark.css");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapOrderEndpoints();
app.MapHealthEndpoints();
app.MapOpenApi();

app.Run();

// /health and the OpenAPI document are read by orchestrators and tools, not by people.
// Answering them with the Blazor error page would hand a machine a block of HTML where it
// expects a document, so they take the problem-details branch alongside /api.
static bool IsMachineEndpoint(HttpContext context) =>
    context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
    || context.Request.Path.StartsWithSegments(HealthEndpoints.HealthPath, StringComparison.OrdinalIgnoreCase)
    || context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);

/// <summary>
/// Application entry point. Declared explicitly so that integration tests can use it as the
/// <c>WebApplicationFactory</c> type argument.
/// </summary>
public partial class Program;
