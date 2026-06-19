var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5009");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.ReconciliationService", timestamp = DateTime.UtcNow }));
app.Run();
