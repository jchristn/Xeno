namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Decides which addresses web_retrieve may reach. Unless private networks are allowed, loopback, link-local,
    /// private, carrier-grade NAT, unspecified, multicast, and reserved addresses are refused, for IP literals in a URL
    /// and for every address a host name resolves to at connect time.
    /// </summary>
    internal static class WebAddressPolicy
    {
        #region Public-Methods

        /// <summary>
        /// True when the address is not on the public internet.
        /// </summary>
        /// <param name="address">Address.</param>
        /// <returns>True when refused without the private-network setting.</returns>
        public static bool IsNonPublic(IPAddress address)
        {
            if (address == null) return true;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (address.AddressFamily == AddressFamily.InterNetwork) return IsNonPublicV4(address.GetAddressBytes());
            if (address.AddressFamily != AddressFamily.InterNetworkV6) return true;

            if (address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None)) return true;
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
            byte[] bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true;
            if (IsPrefix(bytes, new byte[] { 0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0 }) || IsPrefix(bytes, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }))
            {
                // NAT64 and IPv4-compatible addresses carry an IPv4 address in the last four bytes.
                return IsNonPublicV4(new byte[] { bytes[12], bytes[13], bytes[14], bytes[15] });
            }

            return false;
        }

        /// <summary>
        /// Refuse a URL whose host is a non-public IP literal or a localhost name.
        /// </summary>
        /// <param name="uri">Absolute HTTP or HTTPS URL.</param>
        /// <param name="allowPrivate">Private networks are allowed.</param>
        /// <exception cref="UnauthorizedAccessException">The host is refused.</exception>
        public static void CheckHost(Uri uri, bool allowPrivate)
        {
            if (allowPrivate) return;
            string host = uri.IdnHost.Trim('[', ']').TrimEnd('.');
            if (String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw Refused(uri.Host);
            }

            if (IPAddress.TryParse(host, out IPAddress? address) && IsNonPublic(address)) throw Refused(uri.Host);
        }

        /// <summary>
        /// The exception for a refused address.
        /// </summary>
        /// <param name="host">Host or address.</param>
        /// <returns>Exception.</returns>
        public static UnauthorizedAccessException Refused(string host)
        {
            return new UnauthorizedAccessException("web_retrieve does not fetch loopback, link-local, or private network addresses ('" + host + "'). Turn on WebRetrieveAllowPrivateNetworks in chat settings to allow them.");
        }

        #endregion

        #region Private-Methods

        private static bool IsNonPublicV4(byte[] b)
        {
            if (b[0] == 0 || b[0] == 10 || b[0] == 127) return true;
            if (b[0] == 169 && b[1] == 254) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
            if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)) return true;
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return true;
            if (b[0] == 198 && b[1] == 51 && b[2] == 100) return true;
            if (b[0] == 203 && b[1] == 0 && b[2] == 113) return true;
            if (b[0] >= 224) return true;
            return false;
        }

        private static bool IsPrefix(byte[] bytes, byte[] prefix)
        {
            for (int i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i]) return false;
            }

            return true;
        }

        #endregion
    }
}
