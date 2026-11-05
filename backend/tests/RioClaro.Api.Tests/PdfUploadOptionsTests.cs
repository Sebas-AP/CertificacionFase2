using Microsoft.Extensions.Configuration;
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

    [Fact]
    public void PdfContentTypeConfigurationDoesNotFailWhenBinderAppendsTheDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PdfUpload:AllowedContentTypes:0"] = "application/pdf"
            })
            .Build();
        var options = configuration.GetSection(PdfUploadOptions.SectionName).Get<PdfUploadOptions>();

        Assert.NotNull(options);
        Assert.Equal(["application/pdf", "application/pdf"], options.AllowedContentTypes);
        Assert.True(options.IsValid(out _));
    }

    [Fact]
    public void PdfContentTypesRejectAnyNonPdfValue()
    {
        var options = new PdfUploadOptions
        {
            AllowedContentTypes = ["application/pdf", "text/plain"]
        };

        Assert.False(options.IsValid(out var failure));
        Assert.Equal("PdfUpload:AllowedContentTypes solo puede contener application/pdf.", failure);
    }
}
