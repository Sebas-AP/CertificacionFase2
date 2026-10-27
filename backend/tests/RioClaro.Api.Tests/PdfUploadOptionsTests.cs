using RioClaro.Infrastructure.Invoices;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class PdfUploadOptionsTests
{
    [Fact]
    public void UploadLimitsRequireExactlyTwentyMiBFiftyPagesAndSixtyDays()
    {
        var options = new PdfUploadOptions();
        Assert.True(options.IsValid(out _));
        Assert.Equal(20_971_520, options.MaxFileSizeBytes);
        Assert.Equal(50, options.MaxPages);
        Assert.Equal(60, options.RetentionDays);

        options.MaxFileSizeBytes--;
        Assert.False(options.IsValid(out _));
        options.MaxFileSizeBytes = 20_971_520;
        options.MaxPages = 49;
        Assert.False(options.IsValid(out _));
        options.MaxPages = 50;
        options.RetentionDays = 61;
        Assert.False(options.IsValid(out _));
    }
}
