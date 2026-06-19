var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.ApiGateway", timestamp = DateTime.UtcNow }));
app.Run();
