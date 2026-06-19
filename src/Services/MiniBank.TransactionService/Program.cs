var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5005");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.TransactionService", timestamp = DateTime.UtcNow }));
app.Run();
