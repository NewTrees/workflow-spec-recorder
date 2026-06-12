using System.Net;
using System.Text;
using System.Text.Json;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class OpenAiCompatibleChatClientTests
{
    [Fact]
    public async Task CompleteAsync_sends_text_prompt_as_plain_message_content_without_images()
    {
        var handler = new CapturingHandler();
        var client = new OpenAiCompatibleChatClient(new HttpClient(handler));

        await client.CompleteAsync(BuildSettings(), "请生成需求文档", CancellationToken.None);

        using var requestJson = JsonDocument.Parse(handler.RequestBody);
        var userContent = requestJson.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content");

        Assert.Equal(JsonValueKind.String, userContent.ValueKind);
        Assert.Equal("请生成需求文档", userContent.GetString());
    }

    [Fact]
    public async Task CompleteAsync_sends_screenshots_as_image_url_parts_when_images_are_supplied()
    {
        var handler = new CapturingHandler();
        var client = new OpenAiCompatibleChatClient(new HttpClient(handler));
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(screenshotPath, [1, 2, 3]);

        try
        {
            await client.CompleteAsync(
                BuildSettings(),
                "请结合录屏和截图理解业务意图",
                [
                    new LlmImageAttachment
                    {
                        Path = screenshotPath,
                        Label = "步骤 1：点击示例分类",
                        MediaType = "image/png"
                    }
                ],
                CancellationToken.None);

            using var requestJson = JsonDocument.Parse(handler.RequestBody);
            var userContent = requestJson.RootElement
                .GetProperty("messages")[1]
                .GetProperty("content");

            Assert.Equal(JsonValueKind.Array, userContent.ValueKind);
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "text" &&
                part.GetProperty("text").GetString()!.Contains("步骤 1：点击示例分类", StringComparison.Ordinal));
            Assert.Contains(userContent.EnumerateArray(), part =>
                part.GetProperty("type").GetString() == "image_url" &&
                part.GetProperty("image_url").GetProperty("url").GetString() == "data:image/png;base64,AQID");
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    [Fact]
    public async Task CompleteAsync_sends_text_only_for_deepseek_even_when_images_are_supplied()
    {
        var handler = new CapturingHandler();
        var client = new OpenAiCompatibleChatClient(new HttpClient(handler));
        var screenshotPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(screenshotPath, [1, 2, 3]);

        try
        {
            await client.CompleteAsync(
                new LlmSettings
                {
                    ProviderName = "DeepSeek",
                    BaseUrl = "https://api.deepseek.com",
                    Model = "deepseek-chat",
                    ApiKey = "plain-text-key",
                    Temperature = 0.1
                },
                "请结合录屏和截图理解业务意图",
                [
                    new LlmImageAttachment
                    {
                        Path = screenshotPath,
                        Label = "步骤 1：点击示例分类",
                        MediaType = "image/png"
                    }
                ],
                CancellationToken.None);

            using var requestJson = JsonDocument.Parse(handler.RequestBody);
            Assert.False(requestJson.RootElement.TryGetProperty("reasoning_split", out _));

            var userContent = requestJson.RootElement
                .GetProperty("messages")[1]
                .GetProperty("content");

            Assert.Equal(JsonValueKind.String, userContent.ValueKind);
            Assert.Contains("请结合录屏和截图理解业务意图", userContent.GetString(), StringComparison.Ordinal);
            Assert.Contains("当前模型配置不发送截图", userContent.GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("image_url", handler.RequestBody, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(screenshotPath);
        }
    }

    private static LlmSettings BuildSettings() => new()
    {
        ProviderName = "Test",
        BaseUrl = "https://api.example.test/v1",
        Model = "vision-model",
        ApiKey = "plain-text-key",
        Temperature = 0.1
    };

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"生成成功\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
