var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5008");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.EodService", timestamp = DateTime.UtcNow }));
app.Run();
