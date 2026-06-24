using System;
using System.Security.Cryptography;
using System.Text;

namespace SharpUpSQL.Core.Auth
{
    /// <summary>
    /// Hand-rolled NTLMv2 for pass-the-hash (bypasses SSPI password derivation).
    /// </summary>
    internal static class NtlmHelper
    {
        private const uint NegotiateUnicode = 0x00000001;
        private const uint NegotiateOem = 0x00000002;
        private const uint RequestTarget = 0x00000004;
        private const uint NegotiateNtlm = 0x00000200;
        private const uint NegotiateAlwaysSign = 0x00008000;
        private const uint NegotiateExtendedSecurity = 0x00080000;
        private const uint NegotiateTargetInfo = 0x00800000;
        private const uint NegotiateVersion = 0x02000000;

        internal static byte[] ParseNtHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException("NT hash is required for pass-the-hash.", "hash");
            }

            var normalized = hash.Trim().Replace(":", string.Empty);
            if (normalized.Length == 64)
            {
                normalized = normalized.Substring(32, 32);
            }

            if (normalized.Length != 32)
            {
                throw new ArgumentException("NT hash must be 32 hex characters (or LM:NT format).", "hash");
            }

            var bytes = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                bytes[i] = Convert.ToByte(normalized.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        internal static byte[] BuildType1Negotiate()
        {
            var flags = NegotiateUnicode |
                        NegotiateOem |
                        RequestTarget |
                        NegotiateNtlm |
                        NegotiateAlwaysSign |
                        NegotiateExtendedSecurity |
                        NegotiateTargetInfo |
                        NegotiateVersion;

            var signature = Encoding.ASCII.GetBytes("NTLMSSP\0");
            var message = new byte[40];
            Buffer.BlockCopy(signature, 0, message, 0, 8);
            BitConverter.GetBytes((uint)1).CopyTo(message, 8);
            BitConverter.GetBytes(flags).CopyTo(message, 12);
            return message;
        }

        internal static byte[] BuildType3Authenticate(
            byte[] type2Challenge,
            string username,
            string domain,
            string workstation,
            byte[] ntHash)
        {
            if (type2Challenge == null || type2Challenge.Length < 32)
            {
                throw new ArgumentException("Invalid NTLM Type 2 challenge.", "type2Challenge");
            }

            var signature = Encoding.ASCII.GetBytes("NTLMSSP\0");
            if (!StartsWith(type2Challenge, signature))
            {
                throw new ArgumentException("Challenge is not an NTLMSSP message.", "type2Challenge");
            }

            var serverFlags = BitConverter.ToUInt32(type2Challenge, 20);
            var challenge = new byte[8];
            Buffer.BlockCopy(type2Challenge, 24, challenge, 0, 8);

            byte[] targetInfo = Array.Empty<byte>();
            if ((serverFlags & NegotiateTargetInfo) != 0 && type2Challenge.Length >= 48)
            {
                var targetInfoLen = BitConverter.ToUInt16(type2Challenge, 40);
                var targetInfoOffset = BitConverter.ToUInt32(type2Challenge, 44);
                if (targetInfoOffset > 0 && targetInfoOffset + targetInfoLen <= type2Challenge.Length)
                {
                    targetInfo = new byte[targetInfoLen];
                    Buffer.BlockCopy(type2Challenge, (int)targetInfoOffset, targetInfo, 0, targetInfoLen);
                }
            }

            var user = username ?? string.Empty;
            var dom = domain ?? string.Empty;
            var host = string.IsNullOrWhiteSpace(workstation) ? Environment.MachineName : workstation;

            var clientChallenge = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(clientChallenge);
            }

            var timestamp = DateTime.UtcNow.Ticks;
            var ntResponse = ComputeNtlmv2Response(ntHash, user, dom, challenge, clientChallenge, timestamp, targetInfo);
            var lmResponse = ComputeLmv2Response(ntHash, user, dom, challenge, clientChallenge);

            var domainBytes = Encoding.Unicode.GetBytes(dom.ToUpperInvariant());
            var userBytes = Encoding.Unicode.GetBytes(user);
            var hostBytes = Encoding.Unicode.GetBytes(host.ToUpperInvariant());
            var sessionKey = Array.Empty<byte>();

            var flags = NegotiateUnicode |
                        NegotiateOem |
                        RequestTarget |
                        NegotiateNtlm |
                        NegotiateAlwaysSign |
                        NegotiateExtendedSecurity |
                        NegotiateTargetInfo |
                        NegotiateVersion;

            var payloadOffset = 72;
            var domainOffset = payloadOffset;
            var userOffset = domainOffset + domainBytes.Length;
            var hostOffset = userOffset + userBytes.Length;
            var lmOffset = hostOffset + hostBytes.Length;
            var ntOffset = lmOffset + lmResponse.Length;
            var sessionOffset = ntOffset + ntResponse.Length;

            var message = new byte[sessionOffset + sessionKey.Length];
            Buffer.BlockCopy(signature, 0, message, 0, 8);
            BitConverter.GetBytes((uint)3).CopyTo(message, 8);
            WriteSecurityBuffer(message, 12, domainBytes.Length, (ushort)domainOffset);
            WriteSecurityBuffer(message, 20, userBytes.Length, (ushort)userOffset);
            WriteSecurityBuffer(message, 28, hostBytes.Length, (ushort)hostOffset);
            WriteSecurityBuffer(message, 36, lmResponse.Length, (ushort)lmOffset);
            WriteSecurityBuffer(message, 44, ntResponse.Length, (ushort)ntOffset);
            WriteSecurityBuffer(message, 52, sessionKey.Length, (ushort)sessionOffset);
            BitConverter.GetBytes(flags).CopyTo(message, 60);

            Buffer.BlockCopy(domainBytes, 0, message, domainOffset, domainBytes.Length);
            Buffer.BlockCopy(userBytes, 0, message, userOffset, userBytes.Length);
            Buffer.BlockCopy(hostBytes, 0, message, hostOffset, hostBytes.Length);
            Buffer.BlockCopy(lmResponse, 0, message, lmOffset, lmResponse.Length);
            Buffer.BlockCopy(ntResponse, 0, message, ntOffset, ntResponse.Length);

            return message;
        }

        private static byte[] ComputeNtlmv2Response(
            byte[] ntHash,
            string username,
            string domain,
            byte[] serverChallenge,
            byte[] clientChallenge,
            long timestamp,
            byte[] targetInfo)
        {
            var ntlmv2Hash = HmacMd5(
                ntHash,
                Encoding.Unicode.GetBytes(username.ToUpperInvariant() + domain.ToUpperInvariant()));

            var blob = new byte[28 + targetInfo.Length + 4];
            blob[0] = 0x01;
            blob[1] = 0x01;
            Buffer.BlockCopy(BitConverter.GetBytes(timestamp), 0, blob, 8, 8);
            Buffer.BlockCopy(clientChallenge, 0, blob, 16, 8);
            if (targetInfo.Length > 0)
            {
                Buffer.BlockCopy(targetInfo, 0, blob, 28, targetInfo.Length);
            }

            var challengeBlob = new byte[serverChallenge.Length + blob.Length];
            Buffer.BlockCopy(serverChallenge, 0, challengeBlob, 0, serverChallenge.Length);
            Buffer.BlockCopy(blob, 0, challengeBlob, serverChallenge.Length, blob.Length);

            var ntProof = HmacMd5(ntlmv2Hash, challengeBlob);
            var response = new byte[ntProof.Length + blob.Length];
            Buffer.BlockCopy(ntProof, 0, response, 0, ntProof.Length);
            Buffer.BlockCopy(blob, 0, response, ntProof.Length, blob.Length);
            return response;
        }

        private static byte[] ComputeLmv2Response(
            byte[] ntHash,
            string username,
            string domain,
            byte[] serverChallenge,
            byte[] clientChallenge)
        {
            var ntlmv2Hash = HmacMd5(
                ntHash,
                Encoding.Unicode.GetBytes(username.ToUpperInvariant() + domain.ToUpperInvariant()));

            var challengeClient = new byte[serverChallenge.Length + clientChallenge.Length];
            Buffer.BlockCopy(serverChallenge, 0, challengeClient, 0, serverChallenge.Length);
            Buffer.BlockCopy(clientChallenge, 0, challengeClient, serverChallenge.Length, clientChallenge.Length);

            var lmHash = HmacMd5(ntlmv2Hash, challengeClient);
            var response = new byte[lmHash.Length + clientChallenge.Length];
            Buffer.BlockCopy(lmHash, 0, response, 0, lmHash.Length);
            Buffer.BlockCopy(clientChallenge, 0, response, lmHash.Length, clientChallenge.Length);
            return response;
        }

        private static byte[] HmacMd5(byte[] key, byte[] data)
        {
            using (var hmac = new HMACMD5(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        private static void WriteSecurityBuffer(byte[] message, int offset, int length, ushort valueOffset)
        {
            BitConverter.GetBytes((ushort)length).CopyTo(message, offset);
            BitConverter.GetBytes(valueOffset).CopyTo(message, offset + 2);
        }

        private static bool StartsWith(byte[] buffer, byte[] prefix)
        {
            if (buffer.Length < prefix.Length)
            {
                return false;
            }

            for (var i = 0; i < prefix.Length; i++)
            {
                if (buffer[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
