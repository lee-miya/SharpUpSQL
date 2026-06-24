using System;

namespace SharpUpSQL.Core.Helpers
{
    /// <summary>
    /// CIDR membership test matching PowerUpSQL Test-Subnet.
    /// </summary>
    public static class SubnetHelper
    {
        public static bool TestSubnet(string cidr, string ip)
        {
            if (string.IsNullOrWhiteSpace(cidr) || string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            var parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            var network = parts[0];
            int subnetLen;
            if (!int.TryParse(parts[1], out subnetLen))
            {
                return false;
            }

            var networkOctets = network.Split('.');
            var ipOctets = ip.Split('.');
            if (networkOctets.Length != 4 || ipOctets.Length != 4)
            {
                return false;
            }

            uint unetwork = 0;
            uint uip = 0;
            for (var i = 0; i < 4; i++)
            {
                byte noctet;
                byte ioctet;
                if (!byte.TryParse(networkOctets[i], out noctet) ||
                    !byte.TryParse(ipOctets[i], out ioctet))
                {
                    return false;
                }

                unetwork = (unetwork << 8) + noctet;
                uip = (uip << 8) + ioctet;
            }

            var mask = subnetLen == 0 ? 0u : (~0u << (32 - subnetLen));
            return unetwork == (mask & uip);
        }

        public static bool IsInScope(string ipRange, string ipAddress, out bool outOfScope)
        {
            outOfScope = false;
            if (string.IsNullOrWhiteSpace(ipRange) || string.IsNullOrWhiteSpace(ipAddress))
            {
                return true;
            }

            if (ipAddress.Contains(","))
            {
                foreach (var ip in ipAddress.Split(','))
                {
                    if (TestSubnet(ipRange, ip.Trim()))
                    {
                        return true;
                    }
                }

                outOfScope = true;
                return false;
            }

            if (!TestSubnet(ipRange, ipAddress))
            {
                outOfScope = true;
                return false;
            }

            return true;
        }
    }
}
