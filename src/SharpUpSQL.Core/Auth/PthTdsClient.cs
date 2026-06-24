using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using SharpUpSQL.Core.Helpers;
using SharpUpSQL.Core.Output;

namespace SharpUpSQL.Core.Auth
{
    /// <summary>
    /// Raw TDS 7.4 client with NTLM pass-the-hash authentication.
    /// </summary>
    public sealed class PthTdsClient : IDisposable
    {
        private const byte TdsPrelogin = 0x12;
        private const byte TdsLogin7 = 0x10;
        private const byte TdsSqlBatch = 0x01;
        private const byte TdsSspi = 0x11;

        private const byte TokenLoginAck = 0xAD;
        private const byte TokenError = 0xAA;
        private const byte TokenDone = 0xFD;
        private const byte TokenDoneProc = 0xFE;
        private const byte TokenDoneInProc = 0xFF;
        private const byte TokenColMetadata = 0x81;
        private const byte TokenRow = 0xD1;
        private const byte TokenSspi = 0xED;

        private readonly TcpClient _tcp;
        private Stream _stream;
        private byte _packetId;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _domain;
        private readonly byte[] _ntHash;
        private readonly string _database;
        private readonly int _timeoutSeconds;

        public PthTdsClient(SqlConnectionOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (string.IsNullOrWhiteSpace(options.Hash))
            {
                throw new ArgumentException("Hash is required for pass-the-hash.", "options");
            }

            if (string.IsNullOrWhiteSpace(options.Username))
            {
                throw new ArgumentException("Username is required for pass-the-hash.", "options");
            }

            var server = ServerAddressHelper.FormatServer(options.Instance, options.Port, options.ForceNamedPipe);
            ServerAddressHelper.ParseHostPort(server, out _host, out _port);
            if (options.Port.HasValue && options.Port.Value > 0)
            {
                _port = options.Port.Value;
            }

            _username = options.Username;
            _domain = options.Domain ?? string.Empty;
            _ntHash = NtlmHelper.ParseNtHash(options.Hash);
            _database = string.IsNullOrWhiteSpace(options.Database) ? "master" : options.Database;
            _timeoutSeconds = options.TimeOut <= 0 ? 30 : options.TimeOut;

            _tcp = new TcpClient();
            _tcp.ReceiveTimeout = _timeoutSeconds * 1000;
            _tcp.SendTimeout = _timeoutSeconds * 1000;
            _tcp.Connect(_host, _port);
            _stream = _tcp.GetStream();
        }

        public static bool SupportsPassTheHash(SqlConnectionOptions options)
        {
            return options != null && !string.IsNullOrWhiteSpace(options.Hash);
        }

        public List<Dictionary<string, object>> ExecuteQuery(string query, VerboseWriter verbose, bool debugSql)
        {
            if (debugSql && verbose != null)
            {
                verbose.Write("[DEBUG SQL] " + query);
            }

            Authenticate(verbose);
            SendSqlBatch(query);
            return ReadResultSet();
        }

        public void TestConnection(VerboseWriter verbose)
        {
            Authenticate(verbose);
        }

        private void Authenticate(VerboseWriter verbose)
        {
            if (verbose != null)
            {
                verbose.Write("PTH: negotiating TDS prelogin with " + _host + ":" + _port);
            }

            var encrypt = ExchangePrelogin();
            if (encrypt == 1)
            {
                if (verbose != null)
                {
                    verbose.Write("PTH: enabling TLS");
                }

                var ssl = new SslStream(_stream, false);
                ssl.AuthenticateAsClient(_host);
                _stream = ssl;
            }

            var type1 = NtlmHelper.BuildType1Negotiate();
            SendPacket(TdsLogin7, BuildLogin7(type1));

            var challenge = ReadSspiChallenge();
            var type3 = NtlmHelper.BuildType3Authenticate(
                challenge,
                _username,
                _domain,
                Environment.MachineName,
                _ntHash);

            SendPacket(TdsSspi, type3);

            if (!WaitForLoginAck())
            {
                throw new InvalidOperationException("PTH authentication failed.");
            }

            if (verbose != null)
            {
                verbose.Write("PTH: authentication succeeded.");
            }
        }

        private byte ExchangePrelogin()
        {
            var prelogin = BuildPrelogin(0x00);
            SendPacket(TdsPrelogin, prelogin);

            var response = ReadPacket();
            if (response.Length < 9 || response[0] != TdsPrelogin)
            {
                throw new InvalidOperationException("Invalid PRELOGIN response.");
            }

            var offset = 8;
            while (offset + 5 <= response.Length)
            {
                var token = response[offset];
                if (token == 0xFF)
                {
                    break;
                }

                var valueOffset = (response[offset + 1] << 8) | response[offset + 2];
                var valueLength = (response[offset + 3] << 8) | response[offset + 4];
                if (token == 0x01 && valueOffset + valueLength <= response.Length)
                {
                    return response[valueOffset];
                }

                offset += 5;
            }

            return 0;
        }

        private static byte[] BuildPrelogin(byte encryption)
        {
            var data = new byte[13];
            data[0] = 0x00;
            data[1] = 0x00;
            data[2] = 0x00;
            data[3] = 0x0D;
            data[4] = encryption;
            data[5] = 0x01;
            data[6] = 0x00;
            data[7] = 0x0D;
            data[8] = 0x00;
            data[9] = 0xFF;
            return data;
        }

        private byte[] BuildLogin7(byte[] sspi)
        {
            var appName = "SharpUpSQL";
            var clientName = Environment.MachineName;
            var language = "us_english";

            var hostBytes = Encoding.Unicode.GetBytes(_host);
            var userBytes = Encoding.Unicode.GetBytes(_username);
            var passBytes = Encoding.Unicode.GetBytes(string.Empty);
            var appBytes = Encoding.Unicode.GetBytes(appName);
            var serverBytes = Encoding.Unicode.GetBytes(_host);
            var clientBytes = Encoding.Unicode.GetBytes(clientName);
            var languageBytes = Encoding.Unicode.GetBytes(language);
            var databaseBytes = Encoding.Unicode.GetBytes(_database);

            var offset = 94;
            var hostOffset = offset;
            offset += hostBytes.Length;
            var userOffset = offset;
            offset += userBytes.Length;
            var passOffset = offset;
            offset += passBytes.Length;
            var appOffset = offset;
            offset += appBytes.Length;
            var serverOffset = offset;
            offset += serverBytes.Length;
            var clientOffset = offset;
            offset += clientBytes.Length;
            var languageOffset = offset;
            offset += languageBytes.Length;
            var databaseOffset = offset;
            offset += databaseBytes.Length;
            var sspiOffset = offset;
            offset += sspi.Length;

            var login = new byte[offset];
            login[0] = 0x10;
            login[1] = 0x01;
            BitConverter.GetBytes((uint)offset).CopyTo(login, 2);
            BitConverter.GetBytes((uint)0x74000004).CopyTo(login, 4);
            BitConverter.GetBytes((uint)sspi.Length).CopyTo(login, 8);
            BitConverter.GetBytes((uint)sspiOffset).CopyTo(login, 12);
            BitConverter.GetBytes((ushort)hostBytes.Length).CopyTo(login, 16);
            BitConverter.GetBytes((ushort)hostOffset).CopyTo(login, 18);
            BitConverter.GetBytes((ushort)userBytes.Length).CopyTo(login, 20);
            BitConverter.GetBytes((ushort)userOffset).CopyTo(login, 22);
            BitConverter.GetBytes((ushort)passBytes.Length).CopyTo(login, 24);
            BitConverter.GetBytes((ushort)passOffset).CopyTo(login, 26);
            BitConverter.GetBytes((ushort)appBytes.Length).CopyTo(login, 28);
            BitConverter.GetBytes((ushort)appOffset).CopyTo(login, 30);
            BitConverter.GetBytes((ushort)serverBytes.Length).CopyTo(login, 32);
            BitConverter.GetBytes((ushort)serverOffset).CopyTo(login, 34);
            BitConverter.GetBytes((ushort)clientBytes.Length).CopyTo(login, 36);
            BitConverter.GetBytes((ushort)clientOffset).CopyTo(login, 38);
            BitConverter.GetBytes((ushort)languageBytes.Length).CopyTo(login, 40);
            BitConverter.GetBytes((ushort)languageOffset).CopyTo(login, 42);
            BitConverter.GetBytes((ushort)databaseBytes.Length).CopyTo(login, 44);
            BitConverter.GetBytes((ushort)databaseOffset).CopyTo(login, 46);

            Buffer.BlockCopy(hostBytes, 0, login, hostOffset, hostBytes.Length);
            Buffer.BlockCopy(userBytes, 0, login, userOffset, userBytes.Length);
            Buffer.BlockCopy(passBytes, 0, login, passOffset, passBytes.Length);
            Buffer.BlockCopy(appBytes, 0, login, appOffset, appBytes.Length);
            Buffer.BlockCopy(serverBytes, 0, login, serverOffset, serverBytes.Length);
            Buffer.BlockCopy(clientBytes, 0, login, clientOffset, clientBytes.Length);
            Buffer.BlockCopy(languageBytes, 0, login, languageOffset, languageBytes.Length);
            Buffer.BlockCopy(databaseBytes, 0, login, databaseOffset, databaseBytes.Length);
            Buffer.BlockCopy(sspi, 0, login, sspiOffset, sspi.Length);

            return login;
        }

        private byte[] ReadSspiChallenge()
        {
            while (true)
            {
                var packet = ReadPacket();
                var offset = 8;
                while (offset < packet.Length)
                {
                    var token = packet[offset++];
                    if (token == TokenSspi)
                    {
                        if (offset + 2 > packet.Length)
                        {
                            break;
                        }

                        var length = packet[offset] | (packet[offset + 1] << 8);
                        offset += 2;
                        var challenge = new byte[length];
                        Buffer.BlockCopy(packet, offset, challenge, 0, length);
                        return challenge;
                    }

                    if (token == TokenError)
                    {
                        throw new InvalidOperationException("Login error: " + ParseErrorToken(packet, offset));
                    }

                    offset = SkipToken(packet, offset, token);
                    if (offset < 0)
                    {
                        break;
                    }
                }
            }
        }

        private bool WaitForLoginAck()
        {
            while (true)
            {
                var packet = ReadPacket();
                var offset = 8;
                while (offset < packet.Length)
                {
                    var token = packet[offset++];
                    if (token == TokenLoginAck)
                    {
                        return true;
                    }

                    if (token == TokenError)
                    {
                        throw new InvalidOperationException("Login failed: " + ParseErrorToken(packet, offset));
                    }

                    offset = SkipToken(packet, offset, token);
                    if (offset < 0)
                    {
                        break;
                    }
                }
            }
        }

        private void SendSqlBatch(string query)
        {
            var text = Encoding.Unicode.GetBytes(query);
            var payload = new byte[8 + text.Length];
            payload[0] = 0x01;
            payload[1] = 0x00;
            payload[2] = 0x00;
            payload[3] = 0x00;
            payload[4] = 0x00;
            payload[5] = 0x00;
            payload[6] = 0x00;
            payload[7] = 0x00;
            Buffer.BlockCopy(text, 0, payload, 8, text.Length);
            SendPacket(TdsSqlBatch, payload);
        }

        private List<Dictionary<string, object>> ReadResultSet()
        {
            var results = new List<Dictionary<string, object>>();
            List<ColumnInfo> columns = null;

            while (true)
            {
                var packet = ReadPacket();
                var offset = 8;
                while (offset < packet.Length)
                {
                    var token = packet[offset++];
                    if (token == TokenColMetadata)
                    {
                        columns = ParseColMetadata(packet, ref offset);
                    }
                    else if (token == TokenRow)
                    {
                        if (columns == null)
                        {
                            continue;
                        }

                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < columns.Count; i++)
                        {
                            object value;
                            offset = ReadColumnValue(packet, offset, columns[i], out value);
                            row[columns[i].Name] = value;
                        }

                        results.Add(row);
                    }
                    else if (token == TokenError)
                    {
                        throw new InvalidOperationException(ParseErrorToken(packet, offset));
                    }
                    else if (token == TokenDone || token == TokenDoneProc || token == TokenDoneInProc)
                    {
                        return results;
                    }
                    else
                    {
                        offset = SkipToken(packet, offset, token);
                        if (offset < 0)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private sealed class ColumnInfo
        {
            public string Name { get; set; }
            public byte Type { get; set; }
            public ushort MaxLength { get; set; }
        }

        private static List<ColumnInfo> ParseColMetadata(byte[] packet, ref int offset)
        {
            if (offset + 2 > packet.Length)
            {
                return new List<ColumnInfo>();
            }

            var count = packet[offset] | (packet[offset + 1] << 8);
            offset += 2;
            var columns = new List<ColumnInfo>(count);

            for (var i = 0; i < count; i++)
            {
                if (offset >= packet.Length)
                {
                    break;
                }

                offset++;
                if (offset + 4 > packet.Length)
                {
                    break;
                }

                var userType = BitConverter.ToUInt32(packet, offset);
                offset += 4;
                if (offset >= packet.Length)
                {
                    break;
                }

                var type = packet[offset++];
                ushort maxLength = 0;
                if (type == 0xE7 || type == 0xA7 || type == 0xEF)
                {
                    if (offset + 2 > packet.Length)
                    {
                        break;
                    }

                    maxLength = (ushort)(packet[offset] | (packet[offset + 1] << 8));
                    offset += 2;
                }
                else if (type == 0x26 || type == 0x6E)
                {
                    offset += 1;
                }
                else
                {
                    offset += GetFixedTypeLength(type);
                }

                if (offset >= packet.Length)
                {
                    break;
                }

                var collation = new byte[5];
                if (type == 0xE7 || type == 0xA7 || type == 0xEF || type == 0x6E)
                {
                    if (offset + 5 > packet.Length)
                    {
                        break;
                    }

                    Buffer.BlockCopy(packet, offset, collation, 0, 5);
                    offset += 5;
                }

                if (offset >= packet.Length)
                {
                    break;
                }

                var nameLength = packet[offset++];
                if (offset + nameLength * 2 > packet.Length)
                {
                    break;
                }

                var name = Encoding.Unicode.GetString(packet, offset, nameLength * 2);
                offset += nameLength * 2;

                columns.Add(new ColumnInfo
                {
                    Name = name,
                    Type = type,
                    MaxLength = maxLength
                });
            }

            return columns;
        }

        private static int ReadColumnValue(byte[] packet, int offset, ColumnInfo column, out object value)
        {
            value = null;
            if (offset >= packet.Length)
            {
                return offset;
            }

            if (column.Type == 0xE7 || column.Type == 0xA7 || column.Type == 0xEF)
            {
                if (offset + 2 > packet.Length)
                {
                    return offset;
                }

                var length = packet[offset] | (packet[offset + 1] << 8);
                offset += 2;
                if (length == 0xFFFF)
                {
                    value = null;
                    return offset;
                }

                value = Encoding.Unicode.GetString(packet, offset, length);
                return offset + length;
            }

            if (column.Type == 0x6E)
            {
                if (offset + 4 > packet.Length)
                {
                    return offset;
                }

                var length = BitConverter.ToInt32(packet, offset);
                offset += 4;
                if (length <= 0)
                {
                    value = null;
                    return offset;
                }

                value = Encoding.Unicode.GetString(packet, offset, length);
                return offset + length;
            }

            var size = GetFixedTypeLength(column.Type);
            if (offset + size > packet.Length)
            {
                return offset;
            }

            switch (column.Type)
            {
                case 0x38:
                    value = packet[offset];
                    break;
                case 0x34:
                    value = packet[offset];
                    break;
                case 0x3F:
                    value = packet[offset] == 1;
                    break;
                case 0x26:
                    value = BitConverter.ToInt32(packet, offset);
                    break;
                default:
                    var bytes = new byte[size];
                    Buffer.BlockCopy(packet, offset, bytes, 0, size);
                    value = BitConverter.ToString(bytes).Replace("-", string.Empty);
                    break;
            }

            return offset + size;
        }

        private static int GetFixedTypeLength(byte type)
        {
            switch (type)
            {
                case 0x38:
                case 0x34:
                case 0x3F:
                    return 1;
                case 0x26:
                    return 4;
                case 0x3E:
                case 0x6F:
                    return 8;
                default:
                    return 4;
            }
        }

        private static int SkipToken(byte[] packet, int offset, byte token)
        {
            if (token == TokenDone || token == TokenDoneProc || token == TokenDoneInProc)
            {
                return offset + 8;
            }

            if (token == TokenError)
            {
                if (offset + 2 > packet.Length)
                {
                    return -1;
                }

                var length = packet[offset] | (packet[offset + 1] << 8);
                return offset + 2 + length;
            }

            return offset;
        }

        private static string ParseErrorToken(byte[] packet, int offset)
        {
            if (offset + 2 > packet.Length)
            {
                return "Unknown SQL error.";
            }

            var length = packet[offset] | (packet[offset + 1] << 8);
            if (offset + 2 + length > packet.Length)
            {
                return "Unknown SQL error.";
            }

            if (length < 18)
            {
                return "SQL error.";
            }

            var messageLength = packet[offset + 16] | (packet[offset + 17] << 8);
            if (offset + 18 + messageLength * 2 > packet.Length)
            {
                return "SQL error.";
            }

            return Encoding.Unicode.GetString(packet, offset + 18, messageLength * 2);
        }

        private void SendPacket(byte type, byte[] payload)
        {
            var length = payload.Length + 8;
            var header = new byte[8];
            header[0] = type;
            header[1] = 0x01;
            header[2] = (byte)((length >> 8) & 0xFF);
            header[3] = (byte)(length & 0xFF);
            header[4] = 0x00;
            header[5] = 0x00;
            header[6] = ++_packetId;
            header[7] = 0x00;

            _stream.Write(header, 0, 8);
            _stream.Write(payload, 0, payload.Length);
            _stream.Flush();
        }

        private byte[] ReadPacket()
        {
            var header = ReadExact(8);
            var length = (header[2] << 8) | header[3];
            if (length < 8)
            {
                throw new InvalidOperationException("Invalid TDS packet length.");
            }

            var bodyLength = length - 8;
            var body = bodyLength > 0 ? ReadExact(bodyLength) : Array.Empty<byte>();
            var packet = new byte[length];
            Buffer.BlockCopy(header, 0, packet, 0, 8);
            if (bodyLength > 0)
            {
                Buffer.BlockCopy(body, 0, packet, 8, bodyLength);
            }

            return packet;
        }

        private byte[] ReadExact(int count)
        {
            var buffer = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = _stream.Read(buffer, read, count - read);
                if (n <= 0)
                {
                    throw new EndOfStreamException("TDS connection closed.");
                }

                read += n;
            }

            return buffer;
        }

        public void Dispose()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_tcp != null)
            {
                _tcp.Close();
            }
        }
    }
}
