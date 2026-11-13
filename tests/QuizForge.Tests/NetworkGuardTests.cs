using System.Net;
using QuizForge.Infrastructure.Web;

namespace QuizForge.Tests;

public class NetworkGuardTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("151.101.1.140")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string ip)
    {
        Assert.True(NetworkGuard.IsPublic(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.1.1")]
    public void Private_and_special_addresses_are_blocked(string ip)
    {
        Assert.False(NetworkGuard.IsPublic(IPAddress.Parse(ip)));
    }
}
