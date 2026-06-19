var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5002");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.AccountService", timestamp = DateTime.UtcNow }));
app.Run();
