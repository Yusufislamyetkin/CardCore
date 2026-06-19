var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5006");
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.InstallmentService", timestamp = DateTime.UtcNow }));
app.Run();
