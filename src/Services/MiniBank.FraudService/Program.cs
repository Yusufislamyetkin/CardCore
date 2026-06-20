using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniBank.FraudService.Application;
using MiniBank.FraudService.Infrastructure.Persistence;
using MiniBank.Shared.Infrastructure.Api;
using MiniBank.Shared.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5007");

builder.Services.AddDbContext<FraudDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FraudDb")));

builder.Services.AddSingleton<FraudScoringEngine>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "MiniBank Fraud Service", Version = "v1" }));

builder.Services.AddMiniBankObservability("MiniBank.FraudService", builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<GlobalExceptionHandler>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "MiniBank.FraudService", timestamp = DateTime.UtcNow }));

app.Run();
