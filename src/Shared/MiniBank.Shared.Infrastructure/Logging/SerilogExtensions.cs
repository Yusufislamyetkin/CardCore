using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace MiniBank.Shared.Infrastructure.Logging;

public static class SerilogExtensions
{
    public static IHostBuilder UseMiniBankSerilog(this IHostBuilder hostBuilder, string serviceName) =>
        hostBuilder.UseSerilog((ctx, services, config) =>
        {
            config
                .ReadFrom.Configuration(ctx.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {CorrelationId} {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information);
        });
}
