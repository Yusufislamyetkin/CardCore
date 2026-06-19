var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5007");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.FraudService", timestamp = DateTime.UtcNow }));
app.Run();
