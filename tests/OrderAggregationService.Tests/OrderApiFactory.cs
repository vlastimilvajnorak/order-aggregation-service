using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OrderAggregationService.Tests;

/// <summary>
/// Boots the real application in memory for integration tests.
/// </summary>
/// <remarks>
/// Every test creates and disposes its own instance, so no aggregation state is ever shared
/// between tests and the suite is order independent. Periodic dispatch is switched off so a
/// background drain cannot race with the assertions.
/// </remarks>
internal sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.UseSetting("OrderAggregation:DispatchEnabled", "false");
    }
}
