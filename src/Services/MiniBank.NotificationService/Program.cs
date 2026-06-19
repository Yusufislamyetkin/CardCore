var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5010");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.NotificationService", timestamp = DateTime.UtcNow }));
app.Run();
