var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5004");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.AuthorizationService", timestamp = DateTime.UtcNow }));
app.Run();
