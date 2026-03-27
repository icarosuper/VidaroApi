using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VidroApi.Api.Middleware;
using VidroApi.Application;
using VidroApi.Infrastructure;
using VidroApi.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

// Application + Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Settings validation
builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MinioSettings>()
    .BindConfiguration("MinIO")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<VideoSettings>()
    .BindConfiguration("VideoSettings")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<TrendingSettings>()
    .BindConfiguration("TrendingSettings")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<WebhookSettings>()
    .BindConfiguration("Webhook")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret ?? string.Empty)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Feature endpoints registered here as slices are implemented:
// RegisterUser.MapEndpoint(app);

app.Run();
