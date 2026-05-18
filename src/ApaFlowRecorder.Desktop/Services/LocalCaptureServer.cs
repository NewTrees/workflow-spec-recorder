using System.Text.Json;
using System.Text.Json.Serialization;
using ApaFlowRecorder.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class LocalCaptureServer : IAsyncDisposable
{
    private readonly Func<CaptureEvent, CancellationToken, Task<bool>> _handleCaptureEventAsync;
    private WebApplication? _app;

    public LocalCaptureServer(Func<CaptureEvent, CancellationToken, Task<bool>> handleCaptureEventAsync)
    {
        _handleCaptureEventAsync = handleCaptureEventAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:8765");
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        _app = builder.Build();
        _app.UseCors();
        _app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        _app.MapPost("/api/events", async (CaptureEvent captureEvent, CancellationToken requestCancellationToken) =>
        {
            var accepted = await _handleCaptureEventAsync(captureEvent, requestCancellationToken);
            return accepted ? Results.Accepted() : Results.NoContent();
        });

        await _app.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }
}
