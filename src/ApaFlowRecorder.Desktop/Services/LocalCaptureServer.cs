using System.Text.Json;
using System.Text.Json.Serialization;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class LocalCaptureServer : IAsyncDisposable
{
    public const int Port = 18765;
    public const string ListenUrl = "http://127.0.0.1:18765";

    private readonly Func<CaptureEvent, CancellationToken, Task<bool>> _handleCaptureEventAsync;
    private readonly Action? _extensionHeartbeat;
    private readonly Func<object>? _healthStatusProvider;
    private WebApplication? _app;

    public LocalCaptureServer(
        Func<CaptureEvent, CancellationToken, Task<bool>> handleCaptureEventAsync,
        Action? extensionHeartbeat = null,
        Func<object>? healthStatusProvider = null)
    {
        _handleCaptureEventAsync = handleCaptureEventAsync;
        _extensionHeartbeat = extensionHeartbeat;
        _healthStatusProvider = healthStatusProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(ListenUrl);
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
        _app.MapGet("/health", () => Results.Ok(BuildHealthStatus()));
        _app.MapGet("/extension-check", () => Results.Content(BuildExtensionCheckPage(), "text/html; charset=utf-8"));
        _app.MapPost("/api/extension-heartbeat", () =>
        {
            _extensionHeartbeat?.Invoke();
            return Results.Ok(BuildHealthStatus());
        });
        _app.MapPost("/api/events", async (CaptureEvent captureEvent, CancellationToken requestCancellationToken) =>
        {
            if (ExtensionConnectionTracker.IsBrowserExtensionEvent(captureEvent.EventType))
            {
                _extensionHeartbeat?.Invoke();
            }

            var accepted = await _handleCaptureEventAsync(captureEvent, requestCancellationToken);
            return accepted ? Results.Accepted() : Results.NoContent();
        });

        try
        {
            await _app.StartAsync(cancellationToken);
        }
        catch
        {
            await _app.DisposeAsync();
            _app = null;
            throw;
        }
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

    private object BuildHealthStatus() => _healthStatusProvider?.Invoke() ?? new { status = "ok" };

    private static string BuildExtensionCheckPage() =>
        """
        <!doctype html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8">
          <title>APA &#x6269;&#x5C55;&#x8FDE;&#x63A5;&#x68C0;&#x6D4B;</title>
          <style>
            body {
              font-family: "Microsoft YaHei", "Segoe UI", sans-serif;
              margin: 40px;
              color: #172033;
              background: #f8fafc;
            }
            .box {
              max-width: 780px;
              border: 1px solid #cbd5e1;
              border-radius: 10px;
              padding: 24px;
              background: #fff;
              box-shadow: 0 12px 30px rgba(15, 23, 42, 0.08);
            }
            .hint { color: #475569; line-height: 1.7; }
            .status {
              margin: 18px 0;
              border-radius: 8px;
              padding: 14px 16px;
              font-weight: 700;
            }
            .checking { color: #92400e; background: #fffbeb; border: 1px solid #fde68a; }
            .ok { color: #166534; background: #f0fdf4; border: 1px solid #bbf7d0; }
            .warn { color: #991b1b; background: #fef2f2; border: 1px solid #fecaca; }
            code {
              background: #eef2ff;
              border-radius: 4px;
              padding: 2px 5px;
              color: #1e3a8a;
            }
            ol { color: #475569; line-height: 1.8; padding-left: 22px; }
          </style>
        </head>
        <body>
          <div class="box">
            <h1>APA &#x6269;&#x5C55;&#x8FDE;&#x63A5;&#x68C0;&#x6D4B;&#x9875;</h1>
            <p class="hint">
              &#x8FD9;&#x4E2A;&#x9875;&#x9762;&#x4F1A;&#x8F6E;&#x8BE2;&#x684C;&#x9762;&#x7AEF;&#x72B6;&#x6001;&#xFF0C;&#x53EA;&#x6709;&#x6536;&#x5230; Chrome &#x6269;&#x5C55;&#x5FC3;&#x8DF3;&#x65F6;&#x624D;&#x4F1A;&#x663E;&#x793A;&#x5DF2;&#x8FDE;&#x63A5;&#x3002;
            </p>
            <div id="status" class="status checking">&#x6B63;&#x5728;&#x68C0;&#x6D4B;...</div>
            <p id="details" class="hint"></p>
            <ol>
              <li>&#x5982;&#x679C;&#x672A;&#x8FDE;&#x63A5;&#xFF0C;&#x5148;&#x5728;&#x684C;&#x9762;&#x7AEF;&#x70B9;&#x51FB; <code>&#x6253;&#x5F00;&#x63D2;&#x4EF6;&#x76EE;&#x5F55;</code>&#x3002;</li>
              <li>&#x6253;&#x5F00; <code>chrome://extensions</code>&#xFF0C;&#x542F;&#x7528;&#x5F00;&#x53D1;&#x8005;&#x6A21;&#x5F0F;&#xFF0C;&#x70B9;&#x51FB; <code>&#x52A0;&#x8F7D;&#x5DF2;&#x89E3;&#x538B;&#x7684;&#x6269;&#x5C55;&#x7A0B;&#x5E8F;</code>&#xFF0C;&#x9009;&#x62E9;&#x8BE5;&#x76EE;&#x5F55;&#x3002;</li>
              <li>&#x6269;&#x5C55;&#x5DF2;&#x52A0;&#x8F7D;&#x4F46;&#x4ECD;&#x672A;&#x8FDE;&#x63A5;&#x65F6;&#xFF0C;&#x70B9;&#x6269;&#x5C55;&#x5361;&#x7247;&#x53F3;&#x4E0B;&#x89D2;&#x7684;&#x5237;&#x65B0;&#x6309;&#x94AE;&#xFF0C;&#x7136;&#x540E;&#x5237;&#x65B0;&#x4E1A;&#x52A1;&#x9875;&#x3002;</li>
            </ol>
          </div>

          <script>
            const statusNode = document.getElementById("status");
            const detailsNode = document.getElementById("details");

            async function refreshStatus() {
              try {
                const response = await fetch("/health", { cache: "no-store" });
                const health = await response.json();
                if (health.extensionRecentlySeen) {
                  statusNode.className = "status ok";
                  statusNode.innerHTML = "&#x63D2;&#x4EF6;&#x5DF2;&#x8FDE;&#x63A5;&#xFF0C;&#x53EF;&#x4EE5;&#x5F00;&#x59CB;&#x5F55;&#x5236;&#x6D4F;&#x89C8;&#x5668;&#x64CD;&#x4F5C;&#x3002;";
                  detailsNode.textContent = health.lastExtensionSeenAt
                    ? `最近心跳：${health.lastExtensionSeenAt}`
                    : "";
                  return;
                }

                statusNode.className = "status warn";
                statusNode.innerHTML = "&#x672A;&#x68C0;&#x6D4B;&#x5230; Chrome &#x6269;&#x5C55;&#x5FC3;&#x8DF3;&#x3002;";
                detailsNode.innerHTML = "&#x672C;&#x5730;&#x91C7;&#x96C6;&#x670D;&#x52A1;&#x5DF2;&#x542F;&#x52A8;&#xFF0C;&#x4F46;&#x8FD9;&#x4E0D;&#x4EE3;&#x8868;&#x63D2;&#x4EF6;&#x5DF2;&#x8FDE;&#x63A5;&#x3002;";
              } catch {
                statusNode.className = "status warn";
                statusNode.innerHTML = "&#x65E0;&#x6CD5;&#x8BBF;&#x95EE;&#x684C;&#x9762;&#x7AEF;&#x5065;&#x5EB7;&#x68C0;&#x6D4B;&#x63A5;&#x53E3;&#x3002;";
                detailsNode.innerHTML = "&#x8BF7;&#x786E;&#x8BA4; APA &#x6D41;&#x7A0B;&#x5F55;&#x5236;&#x5668;&#x684C;&#x9762;&#x7AEF;&#x6B63;&#x5728;&#x8FD0;&#x884C;&#x3002;";
              }
            }

            refreshStatus();
            setInterval(refreshStatus, 1000);
          </script>
        </body>
        </html>
        """;
}
