using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using VidroApi.Api.BackgroundServices;
using VidroApi.Api.Extensions;
using VidroApi.Api.Middleware;
using VidroApi.Api;
using VidroApi.Application;
using VidroApi.Infrastructure;
using VidroApi.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Application + Infrastructure
builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddApplication(typeof(Program).Assembly);
builder.Services.AddInfrastructure(builder.Configuration);

// Settings validation
builder.Services.AddSettings();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddHostedService<VideoReconciliationService>();
builder.Services.AddHostedService<StorageCleanupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// CorrelationId must come first so the ID is in scope for all subsequent logs,
// including Serilog's own request log entry.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapAllEndpoints();

app.Lifetime.ApplicationStarted.Register(() =>
    Log.Information("Application listening on: {Urls}", string.Join(", ", app.Urls)));

app.Run();

public partial class Program;
