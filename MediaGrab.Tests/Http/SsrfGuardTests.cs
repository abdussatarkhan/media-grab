using System.Net;
using MediaGrab.Infrastructure.Http;
using Xunit;

namespace MediaGrab.Tests.Http;

public class SsrfGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")] // CGNAT
    public void IsPrivateOrReservedIp_FlagsKnownPrivateRanges(string ip)
    {
        Assert.True(SsrfGuard.IsPrivateOrReservedIp(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    public void IsPrivateOrReservedIp_AllowsPublicAddresses(string ip)
    {
        Assert.False(SsrfGuard.IsPrivateOrReservedIp(IPAddress.Parse(ip)));
    }

    [Fact]
    public void IsPrivateOrReservedIp_FlagsIPv6Loopback()
    {
        Assert.True(SsrfGuard.IsPrivateOrReservedIp(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void IsPrivateOrReservedIp_FlagsUniqueLocalIPv6()
    {
        Assert.True(SsrfGuard.IsPrivateOrReservedIp(IPAddress.Parse("fc00::1")));
    }
}
