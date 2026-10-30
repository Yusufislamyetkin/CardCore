using MiniBank.Shared.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://+:5000");

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddMiniBankObservability("MiniBank.ApiGateway", builder.Configuration);

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Gateway"] = "MiniBank.ApiGateway";
    await next();
});

// Demo console (wwwroot/index.html) calls the APIs through this gateway, so no CORS is needed
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "MiniBank.ApiGateway",
    timestamp = DateTime.UtcNow,
    upstreamServices = new[]
    {
        "iam:5001", "account:5002", "card:5003", "authorization:5004",
        "transaction:5005", "installment:5006", "fraud:5007",
        "eod:5008", "reconciliation:5009", "notification:5010"
    }
}));

app.Run();
