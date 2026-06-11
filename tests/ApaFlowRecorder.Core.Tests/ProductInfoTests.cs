using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class ProductInfoTests
{
    [Fact]
    public void Product_info_exposes_author_version_and_display_text()
    {
        Assert.Equal("Yumin", ProductInfo.Author);
        Assert.StartsWith("版本 ", ProductInfo.DisplayVersion);
        Assert.Contains("作者 Yumin", ProductInfo.DisplayAttribution);
        Assert.Contains(ProductInfo.DisplayVersion, ProductInfo.DisplaySummary);
        Assert.Contains(ProductInfo.DisplayAttribution, ProductInfo.DisplaySummary);
    }
}
