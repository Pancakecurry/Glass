using Glass.Core.Product;
using Xunit;

namespace Glass.Core.Tests;

public sealed class ProductVersionTests
{
    [Theory]
    [InlineData("0.9.0", "0.9.0.0")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("65535.0.1.2", "65535.0.1.2")]
    public void ValidVersions_ParseForPackageMetadata(string value, string package)
    {
        Assert.True(ProductVersion.TryParse(value, out var version));
        Assert.Equal(package, version.Package);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1.2.preview")]
    [InlineData("65536.0.0.0")]
    public void InvalidVersions_AreRejected(string value) =>
        Assert.False(ProductVersion.TryParse(value, out _));
}
