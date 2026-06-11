using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class OpenAiCompatibleChatClient
{
    private const int MaxImageAttachments = 12;
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleChatClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public Task<string> CompleteAsync(LlmSettings settings, string prompt, CancellationToken cancellationToken = default) =>
        CompleteAsync(settings, prompt, [], cancellationToken);

    public async Task<string> CompleteAsync(
        LlmSettings settings,
        string prompt,
        IReadOnlyList<LlmImageAttachment> images,
        CancellationToken cancellationToken = default)
    {
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("LLM settings are incomplete.");
        }

        var endpoint = BuildEndpoint(settings.BaseUrl);
        var userContent = BuildUserContent(prompt, images);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = settings.Model,
            temperature = settings.Temperature,
            reasoning_split = true,
            messages = new object[]
            {
                new { role = "system", content = "你是资深 RPA / APA 需求分析师。输出严谨、结构化、可执行。" },
                new { role = "user", content = userContent }
            }
        }), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM request failed: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return string.IsNullOrWhiteSpace(content)
            ? throw new InvalidOperationException("LLM response did not contain message content.")
            : content.Trim();
    }

    private static object BuildUserContent(string prompt, IReadOnlyList<LlmImageAttachment> images)
    {
        var availableImages = images
            .Where(image => !string.IsNullOrWhiteSpace(image.Path) && File.Exists(image.Path))
            .Take(MaxImageAttachments)
            .ToList();
        if (availableImages.Count == 0)
        {
            return prompt;
        }

        var availableImageCount = images.Count(image => !string.IsNullOrWhiteSpace(image.Path) && File.Exists(image.Path));
        var omittedCount = availableImageCount - availableImages.Count;
        var labels = availableImages.Select((image, index) =>
            $"{index + 1}. {FirstNonBlank(image.Label, Path.GetFileName(image.Path))}");
        var visualInstruction = new StringBuilder(prompt)
            .AppendLine()
            .AppendLine()
            .AppendLine("# 附带截图资料")
            .AppendLine("以下截图与录制步骤对应。请同时结合文字步骤、页面标题/URL、元素选择器、DOM 语义和截图视觉信息推断业务意图。")
            .AppendLine(string.Join(Environment.NewLine, labels));
        if (omittedCount > 0)
        {
            visualInstruction.AppendLine($"另有 {omittedCount} 张截图因数量限制未附带，请优先依据已附截图和文字资料泛化。");
        }

        var parts = new List<object>
        {
            new { type = "text", text = visualInstruction.ToString() }
        };
        foreach (var image in availableImages)
        {
            var base64 = Convert.ToBase64String(File.ReadAllBytes(image.Path));
            parts.Add(new
            {
                type = "image_url",
                image_url = new
                {
                    url = $"data:{ResolveMediaType(image)};base64,{base64}"
                }
            });
        }

        return parts;
    }

    private static string ResolveMediaType(LlmImageAttachment image)
    {
        if (!string.IsNullOrWhiteSpace(image.MediaType))
        {
            return image.MediaType;
        }

        return Path.GetExtension(image.Path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string BuildEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + "/chat/completions";
    }
}
