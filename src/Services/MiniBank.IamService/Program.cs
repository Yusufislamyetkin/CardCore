var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5001");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.IamService", timestamp = DateTime.UtcNow }));
app.Run();
