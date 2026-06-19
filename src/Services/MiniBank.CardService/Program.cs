var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5003");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.CardService", timestamp = DateTime.UtcNow }));
app.Run();
