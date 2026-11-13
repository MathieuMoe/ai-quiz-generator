using System.Net;
using System.Net.Sockets;

namespace QuizForge.Infrastructure.Web;

/// <summary>
/// Decides whether the server may connect to an IP address.
/// Blocks loopback, private, link-local, carrier-grade NAT, multicast and reserved ranges (SSRF protection).
/// </summary>
public static class NetworkGuard
{
    public static bool IsPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0                                   // 0.0.0.0/8 "this network"
                  || b[0] == 10                                  // 10.0.0.0/8 private
                  || b[0] == 127                                 // 127.0.0.0/8 loopback
                  || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // 100.64.0.0/10 carrier-grade NAT
                  || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 link-local (cloud metadata!)
                  || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12 private
                  || (b[0] == 192 && b[1] == 0 && b[2] == 0)     // 192.0.0.0/24 IETF
                  || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16 private
                  || (b[0] == 198 && (b[1] == 18 || b[1] == 19)) // 198.18.0.0/15 benchmarking
                  || b[0] >= 224);                               // multicast + reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.Equals(IPAddress.IPv6Any)
                  || address.IsIPv6LinkLocal
                  || address.IsIPv6SiteLocal
                  || address.IsIPv6Multicast
                  || (b[0] & 0xFE) == 0xFC);                     // fc00::/7 unique local
        }

        return false;
    }
}
