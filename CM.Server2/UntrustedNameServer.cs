#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// This service is run on authoritative servers only. It translates *.untrusted-server.com
    /// end-point addresses into IPs for the benefit of web browsers which must use TLS
    /// even though we don't know/trust servers by design. Running this on any server other
    /// than nsXX.civil.money accomplishes nothing.
    /// </summary>
    public class UntrustedNameServer {
        public static string[] TXT = new string[] { };
        private const int DNS_PORT = 53;

        // These are only needed for SSL certificate management with an authority. It doesn't matter
        // if somebody changes these and tries running this name server because our WHOIS points to
        // only trusted authoritative servers. 
        private static MX[] MXes = new MX[] {
            new MX(1, "aspmx.l.google.com"),
            new MX(5, "alt1.aspmx.l.google.com"),
            new MX(5, "alt2.aspmx.l.google.com"),
            new MX(10, "aspmx2.googlemail.com"),
            new MX(10, "aspmx3.googlemail.com"),
        };

        private bool _IsRunning;
        private Log _Log;
        private IPAddress _IP;
        public UntrustedNameServer(Log log, IPAddress ip) {
            _Log = log;
            _IP = ip;
        }

        public enum DnsClass {
            None = 0, IN = 1, CS = 2, CH = 3, HS = 4
        }

        public enum DnsType {
            None = 0, ANAME = 1, NS = 2, CNAME = 5, SOA = 6, PTR = 12, MX = 15, TXT = 16, OPT = 41, DNSKEY = 48, SPF = 99
        }

        public enum OpCode : byte {
            StandardQuery = 0,
            InverseQuerty = 1,
            StatusRequest = 2,
            Reserverd3 = 3,
            Reserverd4 = 4,
            Reserverd5 = 5,
            Reserverd6 = 6,
            Reserverd7 = 7,
            Reserverd8 = 8,
            Reserverd9 = 9,
            Reserverd10 = 10,
            Reserverd11 = 11,
            Reserverd12 = 12,
            Reserverd13 = 13,
            Reserverd14 = 14,
            Reserverd15 = 15,
        }

        public enum ReturnCode {
            Success = 0,
            FormatError = 1,
            ServerFailure = 2,
            NameError = 3,
            NotImplemented = 4,
            Refused = 5,
            Other = 6,
            /// <summary>
            /// requires OPT
            /// </summary>
            BadVersion = 16
        }


        /// <summary>
        /// This is a test method.
        /// </summary>
        public static Message Query(string serverIP, string domain, DnsType dnsType, DnsClass dnsClass, int maxRetries, int timeOutMs) {
            var q = new Message {
                ID = 0,
                IsRecursionDesired = true,
                Opcode = OpCode.StandardQuery,
                Questions = new Data[] {
                new Data() {
                     IsQuestion = true,
                     Domain = domain,
                     Type =dnsType,
                     Class = dnsClass
                }
            }
            };
            var b = q.ToBytes();
            var ep = new IPEndPoint(IPAddress.Parse(serverIP), DNS_PORT);
            var res = UdpQuery(ep, b, maxRetries, timeOutMs);
            if (res == null)
                return null;
            var m = new Message(res);
            if (m.IsTruncated) {
                using (var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                    tcp.NoDelay = true;
                    tcp.Connect(ep);
                    var b2 = new byte[b.Length + 2];
                    b2[0] = (byte)((b.Length >> 8) & 0xff);
                    b2[1] = (byte)((b.Length) & 0xff);
                    Buffer.BlockCopy(b, 0, b2, 2, b.Length);

                    tcp.Send(b2, 0, b2.Length, SocketFlags.None);
                    res = new byte[4096];
                    int len = tcp.Receive(res);
                    if (len != 0) {
                        int reportedLen = res[0] << 8 | res[1];
                        m = new Message(new ArraySegment<byte>(res, 2, reportedLen));
                    }
                }

            }
            return m;
        }

        /// <summary>
        /// Starts the DNS server.
        /// </summary>
        public async void Start() {

            NameserverIPs.Clear();
            try {
                for (int i = 0; i < CM.DNS.Nameservers.Length; i++) {
                    var server = CM.DNS.Nameservers[i];
                    if (!NameserverIPs.TryGetValue(server, out var ip)) {
                        var addresses = await Dns.GetHostAddressesAsync(server);
                        var first = addresses.FirstOrDefault();
                        if (first != null)
                            NameserverIPs[server] = first.ToString();
                    }
                }
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, "Unable to start the nameserver - no SOA name servers were resolvable.");
            }
            _IsRunning = true;
            Task.Run(new Action(LoopUDP));
            Task.Run(new Action(LoopTCP));
        }

        private static int ReadAt(System.IO.Stream ms, long index) {
            long p = ms.Position;
            ms.Position = index;
            int b = ms.ReadByte();
            ms.Position = p;
            return b;
        }

        private static int ReadInt(ArraySegment<byte> p, int maxBits, ref int cursor, int bits) {
            if (cursor + bits > maxBits || cursor < 0)
                throw new IndexOutOfRangeException();
            var byteIndex = (cursor >> 3);
            var offset = (cursor & 7);
            int pp = 0;
            int shift = 24;
            while (shift >= 0) {
                if (byteIndex < p.Count)
                    pp |= p[byteIndex++] << shift;
                shift -= 8;
            }
            var value = (int)((pp >> (32 - offset - bits)) & (0xFFFFFFFF >> 32 - bits));
            cursor += bits;
            return value;
        }

        private static string ReadString(ArraySegment<byte> p, int maxBits, ref int cursor) {
            var s = new StringBuilder();
            ReadString(s, p, maxBits, ref cursor);
            return s.ToString();
        }
        private static string ReadAscii(ArraySegment<byte> p, int maxBits, ref int cursor) {
            var length = ReadInt(p, maxBits, ref cursor, 8);
            var s = new StringBuilder();
            for (int i = cursor >> 3; s.Length < length; i++) {
                s.Append((char)p[i]);
            }
            cursor += length << 3;
            return s.ToString();
        }

        private static void ReadString(StringBuilder domain, ArraySegment<byte> p, int maxBits, ref int cursor) {
            if (cursor >= maxBits)
                return;
            int length = 0;
            while (cursor < maxBits
                && (length = ReadInt(p, maxBits, ref cursor, 8)) != 0) {

                // Top 2 bits set denotes domain name compression
                if ((length & 0xc0) == 0xc0) {
                    // Move to compression position
                    int newPos = (length & 0x3f) << 8 | ReadInt(p, maxBits, ref cursor, 8);
                    int tempCursor = newPos * 8;
                    ReadString(domain, p, maxBits, ref tempCursor);
                    return;
                }

                if ((length & 0xc0) == 0x40) {
                    // 01 = extended labels, not supported.
                    throw new NotSupportedException();
                }

                // 00 = No compression
                while (length > 0 && cursor < maxBits) {
                    domain.Append((char)(byte)ReadInt(p, maxBits, ref cursor, 8));
                    length--;
                }

                // if size of next label isn't null (end of domain name) add a period ready for next label
                if (cursor < maxBits && p[cursor >> 3] != 0) domain.Append('.');
            }
        }

        private static byte[] UdpQuery(IPEndPoint server, byte[] msg, int maxRetries, int timeOutMs) {
            int attempts = 0;
            var rnd = new Random();

            int uid = rnd.Next(1, short.MaxValue);

            while (attempts <= maxRetries) {
                // add the UID
                msg[0] = (byte)(uid >> 8);
                msg[1] = (byte)uid;

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeOutMs);
                socket.SendTo(msg, msg.Length, SocketFlags.None, server);

                var b = new byte[4096];

                try {
                    socket.Receive(b);

                    // make sure the message returned is ours
                    if (b[0] == msg[0] && b[1] == msg[1]) {
                        return b;
                    }
                } catch (SocketException) {
                    attempts++;
                } finally {
                    uid++;
                    socket.Dispose();
                }
            }

            return null;
        }

        private static void WriteAt(System.IO.Stream ms, byte b, int index) {
            long p = ms.Position;
            ms.Position = index;
            ms.Write(new byte[] { b }, 0, 1);
            ms.Position = p;
        }

        private static void WriteInt(System.IO.Stream ms, ref int bitCursor, int bitCount, int val) {
            int byteIndex = (bitCursor >> 3);// Same as (bitCursor / 8) but faster
            int offset = bitCursor & 7; //Same as (bitCursor % 8) but faster

            bitCursor += bitCount;
            uint v = (uint)val;

            while (bitCount > 0) {
                int read = offset + bitCount > 8 ? 8 - offset : bitCount;
                int mask = read <= bitCount ? ((1 << read) - 1) : ((1 << bitCount) - 1);
                int right = read <= bitCount ? 0 : (read - offset);

                int invMask = 255 & ~(255 >> offset);
                if (read + offset != 8 && offset != 0)
                    invMask = (255 & ~(255 >> offset + 1)) - 1;
                if (invMask == 0)
                    invMask = 255;

                while (ms.Length <= byteIndex) {
                    ms.Position = ms.Length;
                    ms.WriteByte(0x0);
                }

                if (read == 8 && right == 0 && mask == 0xff) {
                    WriteAt(ms, (byte)((v >> (bitCount - read)) & mask), byteIndex);
                } else {
                    if (bitCount > read)
                        WriteAt(ms, (byte)(((int)ReadAt(ms, byteIndex) & invMask) | (byte)((v >> (bitCount - read)) & mask)), byteIndex);
                    else
                        WriteAt(ms, (byte)(((int)ReadAt(ms, byteIndex) & invMask) | (byte)((v & mask) << (8 - (offset + read)))), byteIndex);
                }
                bitCount -= read;

                if (bitCount > 0) {
                    byteIndex++;
                    offset = 0;
                }
            }
        }

        private static void WriteString(List<byte> data, string s) {
            int position = 0;
            int length = 0;
            while (position < s.Length) {
                // look for a period, after where we are
                length = s.IndexOf('.', position) - position;

                // if there isn't one then this labels length is to the end of the string
                if (length < 0)
                    length = s.Length - position;

                // add the length
                data.Add((byte)length);

                // copy a char at a time to the array
                while (length-- > 0) {
                    data.Add((byte)s[position++]);
                }

                // step over '.'
                position++;
            }
            data.Add((byte)0);
        }

        private static void WriteString(System.IO.Stream ms, ref int cursor, string s) {
            int position = 0;
            int length = 0;
            while (position < s.Length) {
                // look for a period, after where we are
                length = s.IndexOf('.', position) - position;

                // if there isn't one then this labels length is to the end of the string
                if (length < 0)
                    length = s.Length - position;

                // add the length
                ms.WriteByte((byte)length);
                cursor += (length + 1) << 3;
                // copy a char at a time to the array
                while (length-- > 0) {
                    ms.WriteByte((byte)s[position++]);
                }

                // step over '.'
                position++;
            }
            ms.WriteByte(0); cursor += 8;
        }

        private async void LoopTCP() {
            TcpListener listener = null;
            try {
                _Log.Write(this, LogLevel.INFO, "DNS TCP Starting");
                listener = new TcpListener(_IP, DNS_PORT);
                listener.Server.SendBufferSize = 4096;
                listener.Server.ReceiveBufferSize = 4096;
                try {
                    listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    //listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    //listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                } catch {

                }
                listener.Start();

                while (_IsRunning) {
                    try {
                        var sock = await listener.Server.AcceptAsync();
                        ProcessTCP(sock);
                    } catch (Exception ex) {
                        Debug.WriteLine(ex);
                    }
                }
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, ex.ToString());
            } finally {
                listener.Stop();
            }
        }

        private async void ProcessTCP(Socket sock) {
            var b = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
            try {
                using (var s = new NetworkStream(sock)) {

                    while (sock.Connected) {
                        int read = await s.ReadAsync(b, 0, 2);
                        if (read == 0)
                            break;
                        int len = b[0] << 8 | b[1];
                        if (len > b.Length) {
                            // No DNS query will be even 512 bytes let alone 4096 for this
                            // echo server. Just close the client.
                            break;
                        }
                        read = await s.ReadAsync(b, 0, len);
                        if (read == 0)
                            break;
                        var res = await Process(new ArraySegment<byte>(b, 0, len), isUDP: false);
                        b[0] = (byte)(res.Length >> 8);
                        b[1] = (byte)(res.Length & 0xff);
                        Buffer.BlockCopy(res, 0, b, 2, res.Length);
                        await s.WriteAsync(b, 0, res.Length + 2);
                    }
                }
            } catch { } finally {
                System.Buffers.ArrayPool<byte>.Shared.Return(b);
                sock.Close();
            }

        }

        private void LoopUDP() {
            try {
                _Log.Write(this, LogLevel.INFO, "DNS UDP Starting");
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                try {
                    sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    sock.ExclusiveAddressUse = false;
                } catch {
                    // linux crashes on either of these
                }
                sock.Bind(new IPEndPoint(_IP, DNS_PORT));
                while (_IsRunning) {
                    try {
                        EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                        var b = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);
                        int len = sock.ReceiveFrom(b, ref sender);
                        ProcessUDP(sock, b, len, sender);
                    } catch (Exception ex) {
                        Debug.WriteLine(ex);
                    }
                }
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, ex.ToString());
            }
        }

        private async void ProcessUDP(Socket sock, byte[] b, int len, EndPoint sender) {
            try {
                var res = await Process(new ArraySegment<byte>(b, 0, len), isUDP: true);
                sock.SendTo(res, sender);
            } catch { } finally {
                System.Buffers.ArrayPool<byte>.Shared.Return(b);
            }
        }

        Dictionary<string, string> NameserverIPs = new Dictionary<string, string>();

        private async Task<byte[]> Process(ArraySegment<byte> b, bool isUDP) {
            try {


                var m = new Message(b, b.Count);
                //_Log.Write(this, LogLevel.INFO, "[DNS  IN] " + m);

                var answers = new List<Data>();
                var others = new List<Data>();
                var nameservers = new List<Data>();

                m.ReturnCode = ReturnCode.NameError;
                m.IsResponse = true;
                m.IsRecursionAvailable = false;
                m.IsAuthoritativeAnswer = false;
                m.IsTruncated = false;

                for (int qi = 0; qi < m.Other.Length; qi++) {
                    var q = m.Other[qi];
                    switch (q.Type) {
                        case DnsType.OPT: {
                                var opt = new Data() {
                                    Type = DnsType.OPT
                                };
                                opt.OPT = new Data.OPTInfo(opt);
                                opt.OPT.RequestorPayloadSize = isUDP ? (ushort)512 : (ushort)Math.Max(512, Math.Min((int)q.OPT.RequestorPayloadSize, 4096));
                                opt.OPT.DO = q.OPT.DO;
                                others.Add(opt);
                                if (q.OPT.Version != 0) {
                                    m.ReturnCode = ReturnCode.BadVersion;
                                } else if (q.OPT.ExtendedRCode != 0) {
                                    m.ReturnCode = ReturnCode.NotImplemented;
                                }
                            }
                            break;
                        default:
                            m.ReturnCode = ReturnCode.FormatError;
                            break;
                    }

                }

                if (m.ReturnCode == ReturnCode.NameError) {
                    for (int qi = 0; qi < m.Questions.Length; qi++) {
                        var q = m.Questions[qi];

                        try {
                            // This is going to purposefully throw for anything other than an *.untrusted-server.com host name.

                            switch (q.Type) {
                                case DnsType.SOA:
                                case DnsType.ANAME:
                                case DnsType.CNAME:

                                    if (String.Equals(q.Domain, DNS.UNTRUSTED_DOMAIN, StringComparison.OrdinalIgnoreCase)) {
                                        // SOA or no subdomain
                                        m.IsAuthoritativeAnswer = true;

                                        var tmp = NameserverIPs.ToArray();

                                        for (int i = 0; i < tmp.Length; i++) {
                                            var server = tmp[i].Key;
                                            var ip = tmp[i].Value;
                                            nameservers.Add(new Data() {
                                                Class = DnsClass.IN,
                                                Domain = q.Domain,
                                                TTL = 180 - 1,
                                                Type = DnsType.NS,
                                                Values = {
                                                    { "Domain", server},
                                                }
                                            });
                                            if (q.Type == DnsType.ANAME) {
                                                others.Add(new Data() {
                                                    Class = DnsClass.IN,
                                                    Domain = server,
                                                    TTL = 600 - 1,
                                                    Type = DnsType.ANAME,
                                                    Values = { { "IP", ip } }
                                                });
                                            }
                                        }
                                        // SOA answer

                                        answers.Add(new Data() {
                                            Class = DnsClass.IN,
                                            Domain = q.Domain,
                                            TTL = 180 - 1,
                                            Type = DnsType.SOA,
                                            Values = {
                                                { "PrimaryNameServer", CM.DNS.Nameservers[0] },
                                                { "ResponsibleMailAddress", "civil.money" },
                                                { "Serial",  "2019022700" },
                                                { "Refresh",  "43200" },
                                                { "Retry",  "3600" },
                                                { "Expire",  "1209600" },
                                                { "DefaultTtl",  "180" },
                                            }
                                        });
                                    } else if (q.Domain.EndsWith("." + DNS.UNTRUSTED_DOMAIN, StringComparison.OrdinalIgnoreCase)) {

                                        m.IsAuthoritativeAnswer = true;

                                        List<IPAddress> ips = new List<IPAddress>();
                                        var ep = CM.DNS.UntrustedDomainToEndpoint(q.Domain);
                                        var ipOrHostName = ep.Substring(0, ep.IndexOf(':')).ToLower();
                                        if (ipOrHostName.EndsWith(DNS.UNTRUSTED_DOMAIN)) {
                                            // Anybody querying untrusted-server.com.untrusted-server.com is being silly.
                                        } else {
                                            switch (ipOrHostName) {
                                                case "something":
                                                    // If we wanted something.untrusted-server.com it would go here.
                                                    break;

                                                default:
                                                    if (IPAddress.TryParse(ipOrHostName, out var ip)) {
                                                        ips.Add(ip);
                                                    } else {
                                                        var addresses = await Dns.GetHostAddressesAsync(ipOrHostName);
                                                        ips.AddRange(addresses);
                                                        ips.RemoveAll(x => x.AddressFamily != AddressFamily.InterNetwork || x.ToString() == "0.0.0.0");
                                                    }
                                                    break;
                                            }
                                        }
                                        for (int i = 0; i < ips.Count; i++) {
                                            answers.Add(new Data() {
                                                Class = DnsClass.IN,
                                                Domain = q.Domain,
                                                TTL = 600 - 1,
                                                Type = DnsType.ANAME,
                                                Values = { { "IP", ips[i].ToString() } }
                                            });
                                        }
                                        if (q.Type == DnsType.SOA) {
                                            answers.Add(new Data() {
                                                Class = DnsClass.IN,
                                                Domain = q.Domain,
                                                TTL = 180 - 1,
                                                Type = DnsType.SOA,
                                                Values = {
                                                { "PrimaryNameServer", CM.DNS.Nameservers[0] },
                                                { "ResponsibleMailAddress", "civil.money" },
                                                { "Serial",  "2019022700" },
                                                { "Refresh",  "43200" },
                                                { "Retry",  "3600" },
                                                { "Expire",  "1209600" },
                                                { "DefaultTtl",  "180" },
                                            }
                                            });
                                        }
                                    }
                                    if (m.ReturnCode == ReturnCode.NameError)
                                        m.ReturnCode = ReturnCode.Success;
                                    break;

                                case DnsType.NS:
                                    m.IsAuthoritativeAnswer = true;
                                    for (int i = 0; i < CM.DNS.Nameservers.Length; i++) {
                                        answers.Add(new Data() {
                                            Class = DnsClass.IN,
                                            Domain = q.Domain,
                                            TTL = 600 - 1,
                                            Type = DnsType.NS,
                                            Values = { { "Domain", CM.DNS.Nameservers[i] } }
                                        });
                                    }
                                    if (m.ReturnCode == ReturnCode.NameError)
                                        m.ReturnCode = ReturnCode.Success;
                                    break;

                                case DnsType.TXT: {
                                        m.IsAuthoritativeAnswer = true;
                                        for (int x = 0; x < TXT.Length; x++) {
                                            answers.Add(new Data() {
                                                Class = DnsClass.IN,
                                                Domain = q.Domain,
                                                TTL = 1800 - 1,
                                                Type = DnsType.TXT,
                                                Values = {
                                            { "TXT", TXT[x] }
                                        }
                                            });
                                        }
                                        if (m.ReturnCode == ReturnCode.NameError)
                                            m.ReturnCode = ReturnCode.Success;
                                    }
                                    break;

                                case DnsType.MX: {
                                        m.IsAuthoritativeAnswer = true;
                                        for (int x = 0; x < MXes.Length; x++) {
                                            answers.Add(new Data() {
                                                Class = DnsClass.IN,
                                                Domain = q.Domain,
                                                TTL = 1800 - 1,
                                                Type = DnsType.MX,
                                                Values = {
                                            { "Preference", MXes[x].level.ToString() },
                                            { "Domain", MXes[x].host }
                                        }
                                            });
                                        }
                                        if (m.ReturnCode == ReturnCode.NameError)
                                            m.ReturnCode = ReturnCode.Success;
                                    }
                                    break;
                                case DnsType.DNSKEY:
                                    //https://tools.ietf.org/html/rfc4034
                                    if (String.Equals(q.Domain, DNS.UNTRUSTED_DOMAIN, StringComparison.OrdinalIgnoreCase)) {
                                        m.IsAuthoritativeAnswer = true;
                                        if (m.ReturnCode == ReturnCode.NameError)
                                            m.ReturnCode = ReturnCode.Success;
                                    }
                                    break;
                                default:
                                    if (m.ReturnCode == ReturnCode.NameError)
                                        m.ReturnCode = ReturnCode.FormatError;
                                    break;
                            }
                        } catch {
                            // Somebody has queried us for something other than *.untrusted-server.com.
                            _Log.Write(this, LogLevel.INFO, "Unrecognised query for " + q.Domain);
                        }
                    }
                } else {
                    m.Questions = null;
                }
                m.Other = others != null ? others.ToArray() : null;
                m.Answers = answers != null ? answers.ToArray() : null;
                m.NameServers = nameservers != null ? nameservers.ToArray() : null;


                //_Log.Write(this, LogLevel.INFO, "[DNS OUT] " + m);
                return m.ToBytes();
            } catch (NotSupportedException) {
                // Ignore extended label queries
                // Example payload: "qYABAAABAAAAAAAAA3d3dwViYWlkdWUDY29tAAABAAE="
            }
#if DEBUG
            catch (IndexOutOfRangeException ex) { _Log.Write(this, LogLevel.INFO, " Message crash " + ex.ToString() + " \r\nSRC: " + Convert.ToBase64String(b.Array, b.Offset, b.Count)); }
#endif
            catch {
                // Ignore any other malformed issues
            }
            return null;
        }

        private struct MX {
            public string host;

            public int level;

            public MX(int l, string h) {
                level = l;
                host = h;
            }
        }

        public class Data {
            public DnsClass Class;
            public string Domain;
            public bool IsQuestion;
            public uint TTL;
            public DnsType Type;
            public byte[] Unknown;
            public Dictionary<string, string> Values = new Dictionary<string, string>();

            public OPTInfo OPT;
            public class OPTInfo {
                //https://tools.ietf.org/html/rfc6891

                public UInt16 RequestorPayloadSize { get => (UInt16)_Owner.Class; set { _Owner.Class = (DnsClass)value; } }
                // 0-7
                public byte ExtendedRCode {
                    get => (byte)(_Owner.TTL >> 24);
                    set { _Owner.TTL = (((uint)value << 24) | (_Owner.TTL & 0x00ffffff)); }
                }
                // 8-15
                public byte Version {
                    get => (byte)((_Owner.TTL >> 16) & 0xff);
                    set { _Owner.TTL = (((uint)value << 16) | (_Owner.TTL & 0xff00ffff)); }
                }
                /// <summary>
                /// DNSSEC OK
                /// </summary>
                public bool DO {
                    get => (_Owner.TTL & 0x8000) != 0;
                    set { _Owner.TTL = (((uint)(value ? 1 : 0) << 15) | (_Owner.TTL & 0xFFFF7FFF)); }
                }
                public UInt16 Z {
                    get => (UInt16)(_Owner.TTL & 0x7FFF);
                    set { _Owner.TTL = ((value) | (_Owner.TTL & 0xFFFF8000)); }
                }
                Data _Owner;
                public OPTInfo(Data d) {
                    _Owner = d;
                }
            }
            public Data() {
            }

            internal Data(ArraySegment<byte> p, int maxBits, ref int cursor, bool isQuestion) {
                IsQuestion = isQuestion;
                Domain = ReadString(p, maxBits, ref cursor);
                Type = (DnsType)ReadInt(p, maxBits, ref cursor, 16);
                Class = (DnsClass)ReadInt(p, maxBits, ref cursor, 16);
                if (isQuestion)
                    return;
                TTL = (uint)ReadInt(p, maxBits, ref cursor, 32);
                int recordLength = (int)ReadInt(p, maxBits, ref cursor, 16);
                int subMax = Math.Min(maxBits, cursor + (recordLength * 8));
                switch (Type) {
                    case DnsType.ANAME:
                        //var ipBytes = ReadInt(p, maxBits, ref cursor, 32);
                        var ip = new IPAddress(new byte[] {
                            p[(cursor>>3)+0],
                            p[(cursor>>3)+1],
                            p[(cursor>>3)+2],
                            p[(cursor>>3)+3]
                        }).ToString();
                        Values["IP"] = ip;
                        cursor += 32;
                        break;

                    case DnsType.MX:
                        Values["Preference"] = ReadInt(p, maxBits, ref cursor, 16).ToString();
                        Values["Domain"] = ReadString(p, subMax, ref cursor);
                        break;

                    case DnsType.PTR:
                    case DnsType.CNAME:
                    case DnsType.NS:
                        Values["Domain"] = ReadString(p, subMax, ref cursor);
                        break;

                    case DnsType.SPF:
                    case DnsType.TXT:
                        var txt = "";
                        while (cursor < subMax) {
                            txt += ReadAscii(p, subMax, ref cursor);
                        }
                        Values["TXT"] = txt;
                        break;

                    case DnsType.SOA:
                        Values["PrimaryNameServer"] = ReadString(p, subMax, ref cursor);
                        Values["ResponsibleMailAddress"] = ReadString(p, subMax, ref cursor);
                        Values["Serial"] = ReadInt(p, maxBits, ref cursor, 32).ToString();
                        Values["Refresh"] = ReadInt(p, maxBits, ref cursor, 32).ToString();
                        Values["Retry"] = ReadInt(p, maxBits, ref cursor, 32).ToString();
                        Values["Expire"] = ReadInt(p, maxBits, ref cursor, 32).ToString();
                        Values["DefaultTtl"] = ReadInt(p, maxBits, ref cursor, 32).ToString();
                        break;
                    case DnsType.OPT:
                        OPT = new OPTInfo(this);
                        Unknown = p.Slice(cursor >> 3, recordLength).ToArray();
                        cursor += recordLength * 8;
                        break;
                    default:
                        Unknown = p.Slice(cursor >> 3, recordLength).ToArray();
                        cursor += recordLength * 8;
                        break;
                }
            }

            public override string ToString() {
                var s = new StringBuilder();
                s.Append(Class + " ");
                s.Append(Type + " ");
                s.Append(Domain + " ");

                if (!IsQuestion) {
                    s.Append("TTL" + TTL + " = ");
                    foreach (var kp in Values) {
                        s.Append(kp.Value + " ");
                    }
                }
                return s.ToString();
            }

            public void Write(System.IO.Stream s, ref int cur) {
                WriteString(s, ref cur, Domain ?? String.Empty);
                WriteInt(s, ref cur, 16, (int)Type);
                WriteInt(s, ref cur, 16, (int)Class);
                if (IsQuestion)
                    return;

                WriteInt(s, ref cur, 32, (int)TTL);

                int lengthPosition = cur;
                WriteInt(s, ref cur, 16, 0);

                switch (Type) {
                    case DnsType.ANAME:
                        s.Write(IPAddress.Parse(Values["IP"]).GetAddressBytes(), 0, 4);
                        cur += 32;
                        break;

                    case DnsType.MX:
                        WriteInt(s, ref cur, 16, int.Parse(Values["Preference"]));
                        WriteString(s, ref cur, Values["Domain"]);
                        break;

                    case DnsType.PTR:
                    case DnsType.CNAME:
                    case DnsType.NS:
                        WriteString(s, ref cur, Values["Domain"]);
                        break;

                    case DnsType.SPF:
                    case DnsType.TXT: {
                            var b = Encoding.UTF8.GetBytes(Values["TXT"]);
                            for (int i = 0; i < b.Length;) {
                                int toWrite = Math.Min(255, b.Length - i);
                                WriteInt(s, ref cur, 8, toWrite);
                                s.Write(b, i, toWrite);
                                i += toWrite;
                            }
                        }
                        break;

                    case DnsType.SOA:
                        WriteString(s, ref cur, Values["PrimaryNameServer"]);
                        WriteString(s, ref cur, Values["ResponsibleMailAddress"]);
                        WriteInt(s, ref cur, 32, int.Parse(Values["Serial"]));
                        WriteInt(s, ref cur, 32, int.Parse(Values["Refresh"]));
                        WriteInt(s, ref cur, 32, int.Parse(Values["Retry"]));
                        WriteInt(s, ref cur, 32, int.Parse(Values["Expire"]));
                        WriteInt(s, ref cur, 32, int.Parse(Values["DefaultTtl"]));
                        break;

                    default:
                        if (Unknown != null) {
                            s.Write(Unknown, 0, Unknown.Length);
                            cur += Unknown.Length * 8;
                        }
                        break;
                }

                int length = (cur - (lengthPosition + 16)) >> 3;
                WriteInt(s, ref lengthPosition, 16, length);
            }
        }

        public class Message {
            public Data[] Answers;
            public ushort ID;
            public bool IsAuthoritativeAnswer;
            public bool IsRecursionAvailable;
            public bool IsRecursionDesired;
            public bool IsResponse;
            public bool IsTruncated;
            public Data[] NameServers;
            public OpCode Opcode;
            public Data[] Other;
            public Data[] Questions;
            public byte Reserved;
            public ReturnCode ReturnCode;

            public int Length;

            public Message() {
            }

            public Message(ArraySegment<byte> p, int len = -1) {
                if (len == -1)
                    len = p.Count;
                int max = len << 3;
                int cur = 0;
                ID = (ushort)ReadInt(p, max, ref cur, 16);
                IsResponse = ReadInt(p, max, ref cur, 1) != 0;
                Opcode = (OpCode)ReadInt(p, max, ref cur, 4);
                IsAuthoritativeAnswer = ReadInt(p, max, ref cur, 1) != 0;
                IsTruncated = ReadInt(p, max, ref cur, 1) != 0;
                IsRecursionDesired = ReadInt(p, max, ref cur, 1) != 0;
                IsRecursionAvailable = ReadInt(p, max, ref cur, 1) != 0;
                Reserved = (byte)ReadInt(p, max, ref cur, 3);
                ReturnCode = (ReturnCode)ReadInt(p, max, ref cur, 4);

                Questions = new Data[ReadInt(p, max, ref cur, 16)];
                Answers = new Data[ReadInt(p, max, ref cur, 16)];
                NameServers = new Data[ReadInt(p, max, ref cur, 16)];
                Other = new Data[ReadInt(p, max, ref cur, 16)];

                for (int i = 0; i < Questions.Length; i++)
                    Questions[i] = new Data(p, max, ref cur, true);
                for (int i = 0; i < Answers.Length; i++)
                    Answers[i] = new Data(p, max, ref cur, false);
                for (int i = 0; i < NameServers.Length; i++)
                    NameServers[i] = new Data(p, max, ref cur, false);
                for (int i = 0; i < Other.Length; i++)
                    Other[i] = new Data(p, max, ref cur, false);

                Debug.Assert((cur & 7) == 0, "Bits shouldn't be left over.");
                Length = cur >> 3;
            }

            public byte[] ToBytes() {
                var ms = new System.IO.MemoryStream();
                int cur = 0;
                WriteInt(ms, ref cur, 16, ID);

                WriteInt(ms, ref cur, 1, IsResponse ? 1 : 0);
                WriteInt(ms, ref cur, 4, (int)Opcode);
                WriteInt(ms, ref cur, 1, IsAuthoritativeAnswer ? 1 : 0);
                WriteInt(ms, ref cur, 1, IsTruncated ? 1 : 0);
                WriteInt(ms, ref cur, 1, IsRecursionDesired ? 1 : 0);
                WriteInt(ms, ref cur, 1, IsRecursionAvailable ? 1 : 0);

                WriteInt(ms, ref cur, 3, Reserved);
                WriteInt(ms, ref cur, 4, (int)ReturnCode);
                if ((int)ReturnCode > 15) {
                    // Extended OPT return code. There will always already be
                    // an OPT item in the collection during this scenario.
                    var edns = Other.FirstOrDefault(x => x.Type == DnsType.OPT);
                    if (edns != null) {
                        edns.OPT.ExtendedRCode = (byte)((int)ReturnCode >> 4);
                    }
                }

                WriteInt(ms, ref cur, 16, Questions != null ? Questions.Length : 0);
                WriteInt(ms, ref cur, 16, Answers != null ? Answers.Length : 0);
                WriteInt(ms, ref cur, 16, NameServers != null ? NameServers.Length : 0);
                WriteInt(ms, ref cur, 16, Other != null ? Other.Length : 0);
                if (Questions != null)
                    foreach (var item in Questions)
                        item.Write(ms, ref cur);

                if (Answers != null)
                    foreach (var item in Answers)
                        item.Write(ms, ref cur);

                if (NameServers != null)
                    foreach (var item in NameServers)
                        item.Write(ms, ref cur);

                if (Other != null)
                    foreach (var item in Other)
                        item.Write(ms, ref cur);

                return ms.ToArray();
            }

            public override string ToString() {
                var s = new StringBuilder();
                if (IsResponse)
                    s.Append("RES " + ReturnCode);
                else
                    s.Append("REQ " + Opcode);
                s.Append(" ID " + ID + " ");
                if (Questions != null && Questions.Length > 0) {
                    s.Append(" IsRecursionDesired " + IsRecursionDesired + " ");
                    s.Append(" IsTruncated " + IsTruncated + " ");
                    for (int i = 0; i < Questions.Length; i++) {
                        s.Append(" Q" + i + ": ");
                        s.Append(Questions[i] + " ");
                    }
                }
                if (Answers != null && Answers.Length > 0) {
                    s.Append("Answers: ");
                    for (int i = 0; i < Answers.Length; i++) {
                        s.Append(i + ": ");
                        s.Append(Answers[i] + " ");
                    }
                }
                if (Other != null && Other.Length > 0) {
                    s.Append("Others: ");
                    for (int i = 0; i < Other.Length; i++) {
                        s.Append(i + ": ");
                        s.Append(Other[i] + " ");
                    }
                }
                return s.ToString();
            }
        }
    }
}