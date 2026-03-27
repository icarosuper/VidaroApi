using VidroApi.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

// Feature endpoints registered here as slices are implemented:
// RegisterUser.MapEndpoint(app);

app.Run();
