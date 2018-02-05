#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    public class SslWebContext : IDisposable {
        private const int MAX_CONTENT_LENGTH = 1024 * 1024 * 10; // 10MB
        internal long ContentBytesRead;
        internal byte[] ReadBuffer;
        internal int ReadLength;
        internal int ReadPos;
        internal SslStream Stream;
        private SemaphoreSlim _AsyncWriteAccess;
        private Socket _Client;
        private DeflateStream _Decompress;
        private bool _GotPreamble;
        private SslWebServer _Owner;
        private NamedValueList _QueryString;
        private string _RawUrl;
        private Uri _Uri;

        public SslWebContext(SslStream s, Socket client, SslWebServer owner) {
            _Owner = owner;
            _Client = client;
            _AsyncWriteAccess = new SemaphoreSlim(1);
            IsSecureConnection = true;
            ReadBuffer = new byte[4096];
            Stream = s;
            RequestHeaders = new NamedValueList();
            RemoteEndPoint = (IPEndPoint)client.RemoteEndPoint;
        }

        public long ContentLength { get; private set; }

        public bool IsConnected {
            get {
                var s = Stream;
                var c = _Client;
                return s != null && s.CanRead && c != null && c.Connected;
            }
        }

        public bool IsSecureConnection { get; private set; }
        public string Method { get; private set; }

        public NamedValueList QueryString {
            get {
                if (_QueryString == null)
                    _QueryString = ParseQuery(_RawUrl);
                return _QueryString;
            }
        }

        public IPEndPoint RemoteEndPoint { get; private set; }
        public NamedValueList RequestHeaders { get; private set; }

        public Uri Url {
            get {
                if (_Uri == null) {
                    string host = RequestHeaders["host"];
                    if (String.IsNullOrEmpty(_RawUrl))
                        _RawUrl = "/";
                    if (!String.IsNullOrWhiteSpace(host))
                        _Uri = new Uri("https://" + host + _RawUrl);
                    else
                        _Uri = new Uri(_RawUrl, UriKind.RelativeOrAbsolute);
                }
                return _Uri;
            }
        }
        public string FirstLine {
            get { return _FirstLine; }
        }
        string _FirstLine;

        public SslWebSocket WebSocket { get; private set; }

        public static NamedValueList ParseQuery(string url) {
            var ar = new NamedValueList();
            var idx = url.IndexOf('?');
            if (idx != -1) {
                url = url.Substring(idx + 1);
                int last = 0;
                do {
                    idx = url.IndexOf('&', last);
                    var segment = idx == -1 ? url.Substring(last) : url.Substring(last, idx - last);
                    var i = segment.IndexOf('=');
                    var key = i == -1 ? segment : segment.Substring(0, i);
                    var value = i == -1 ? String.Empty : segment.Substring(i + 1);
                    ar.Append(key, System.Net.WebUtility.UrlDecode(value));
                    last = idx + 1;
                } while (idx != -1);
            }
            return ar;
        }

        public void AppendLine(string line) {
            if (!_GotPreamble) {
                if (line.EndsWith("HTTP/1.1")) {
                    _FirstLine = line;
                    int i = line.IndexOf(' ');
                    Method = line.Substring(0, i);
                    _RawUrl = line.Substring(i + 1, line.Length - (8 + i + 2));

                    _GotPreamble = true;
                } else {
                    //Protocol violation
                    Debug.WriteLine("Protocol violation with HTTP header");
                    Close();
                }
            } else {
                var idx = line.IndexOf(':');
                if (idx == -1) {
                    Debug.WriteLine("Protocol violation in header.");
                    Close();
                    return;
                }
                var key = line.Substring(0, idx).ToLower().Trim();
                var value = line.Substring(idx + 1).Trim();
                RequestHeaders[key] = value;
                if (String.Compare(key, "content-length", true) == 0) {
                    long len;
                    if (!long.TryParse(value, out len)) {
                        //Protocol violation
                        Debug.WriteLine("Protocol violation with content-length");
                        Close();
                    } else {
                        if (len > MAX_CONTENT_LENGTH) {
                            Debug.WriteLine("Content-length too large");
                            Close();
                        }
                        ContentLength = len;
                    }
                }
            }
        }

        public void Close() {
            if (_IsDisposed)
                return;

            _IsDisposed = true; //prevent re-enter

            _Decompress?.Dispose();
            _Decompress = null;

            WebSocket?.Dispose();
#if !DESKTOPCLR
            _Client?.Dispose();
#else
            _Client?.Close();
#endif
            _Client = null;
            Stream?.Dispose();
            Stream = null;

            _Owner?.OnContextClosed(this);
            _Owner = null;
            _AsyncWriteAccess?.Dispose();
            _AsyncWriteAccess = null;
        }

        public async Task<int> ReadAsync(byte[] b, int offset, int count, System.Threading.CancellationToken token) {
            int done = 0;
            while (done < count) {
                if (ReadPos == ReadLength)
                    await UpdateBuffer(token).ConfigureAwait(false);
                if (ReadLength == 0)
                    return done;
                int i = ReadPos;
                for (; i < ReadLength && done < count; i++, done++) {
                    b[offset + done] = ReadBuffer[i];
                }
                ReadPos = i;
            }
            return done;
        }

        public async Task<int> ReadByte(System.Threading.CancellationToken token) {
            if (ReadPos == ReadLength)
                await UpdateBuffer(token).ConfigureAwait(false);
            return ReadBuffer[ReadPos++];
        }

        private async Task UpdateBuffer(System.Threading.CancellationToken token) {
            ReadPos = 0;
            ReadLength = 0;
            try {
                if (Stream != null)
                    ReadLength = await Stream.ReadAsync(ReadBuffer, 0, ReadBuffer.Length, token).ConfigureAwait(false);
            } catch { }
            if (ReadLength == 0) {// This should never happen unless there's a problem.
                Close();
            }
        }

        public async Task ReplyAsync(HttpStatusCode code, Dictionary<string, string> headers = null, byte[] payload = null, CancellationToken token = default(CancellationToken)) {
            var s = new StringBuilder();
            s.CRLF("HTTP/1.1 " + ((int)code) + " " + code);
            s.CRLF("Date: " + DateTime.UtcNow.ToString("ddd, d MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT");
            AppendAccessControlHeaders(s);
            bool hadContentLength = false;
            if (headers != null)
                foreach (var kp in headers) {
                    s.CRLF(kp.Key + ": " + kp.Value);
                    if (String.Compare("Content-Length", kp.Key, true) == 0)
                        hadContentLength = true;
                }
            if (!hadContentLength)
                s.CRLF("Content-Length: " + (payload != null ? payload.Length.ToString() : "0"));
            s.CRLF("");
            try {
                var head = Encoding.UTF8.GetBytes(s.ToString());
                await Stream.WriteAsync(head, 0, head.Length, token).ConfigureAwait(false);
                if (payload != null && payload.Length != 0)
                    await Stream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
            } catch {
                Close();
                throw;
            }
        }

        public async Task ReplyAsync(HttpStatusCode code, Dictionary<string, string> headers, System.IO.Stream payload, CancellationToken token = default(CancellationToken)) {
            var s = new StringBuilder();
            s.CRLF("HTTP/1.1 " + ((int)code) + " " + code);
            s.CRLF("Date: " + DateTime.UtcNow.ToString("ddd, d MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT");
            AppendAccessControlHeaders(s);
            bool hadContentLength = false;
            if (headers != null)
                foreach (var kp in headers) {
                    s.CRLF(kp.Key + ": " + kp.Value);
                    if (String.Compare("Content-Length", kp.Key, true) == 0)
                        hadContentLength = true;
                }
            if (!hadContentLength)
                s.CRLF("Content-Length: " + (payload != null ? payload.Length : 0).ToString());
            s.CRLF("");
            try {
                var head = Encoding.UTF8.GetBytes(s.ToString());
                await Stream.WriteAsync(head, 0, head.Length, token).ConfigureAwait(false);
                if (payload != null && payload.Length != 0) {
                    await payload.CopyToAsync(Stream, 81920, token).ConfigureAwait(false);
                }
            } catch {
                Close();
                throw;
            }
        }

        private static void AppendAccessControlHeaders(StringBuilder s) {
            s.CRLF("Access-Control-Allow-Origin: *");
            s.CRLF("Access-Control-Allow-Methods: GET, POST");
            s.CRLF("Access-Control-Allow-Headers: Content-Type, Accept, X-Requested-With, X-File-Name");
        }

        public async Task ReplyAsync(HttpStatusCode code, string mime, string content, CancellationToken token = default(CancellationToken)) {
            var s = new StringBuilder();
            s.CRLF("HTTP/1.1 " + ((int)code) + " " + code);
            s.CRLF("Date: " + DateTime.UtcNow.ToString("ddd, d MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT");
            AppendAccessControlHeaders(s);
            s.CRLF("Content-Type: " + (mime ?? "text/plain; charset=utf-8"));
            var payload = Encoding.UTF8.GetBytes(content);
            s.CRLF("Content-Length: " + payload.Length);
            s.CRLF("");
            try {
                var head = Encoding.UTF8.GetBytes(s.ToString());
                await Stream.WriteAsync(head, 0, head.Length, token).ConfigureAwait(false);
                await Stream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
            } catch {
                Close();
                throw;
            }
        }

        internal async Task<int> ReadCompressedAsync(byte[] b, int offset, int count, System.Threading.CancellationToken token) {
            if (_Decompress == null)
                _Decompress = new DeflateStream(Stream, CompressionMode.Decompress); // Mono doesn't support "leave open"
            try {
                return await _Decompress.ReadAsync(b, offset, count, token).ConfigureAwait(false);
            } catch {
                Close();
                throw;
            }
        }

        internal async Task Reset() {
            try {
                if (ContentBytesRead < ContentLength) {
                    Debug.WriteLine("Discarding {0} request bytes", ContentLength - ContentBytesRead);
                    byte[] b = new byte[4096];
                    while (ContentBytesRead < ContentLength
                        && !_IsDisposed) {
                        ContentBytesRead
                            += await ReadAsync(b, 0, (int)Math.Min(ContentLength - ContentBytesRead, b.Length), CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                }
            } catch {
                Close();
            }
            RequestHeaders.Clear();
            _GotPreamble = false;
            _QueryString = null;
            _Uri = null;
            _FirstLine = null;
            ContentBytesRead = 0;
            ContentLength = 0;
        }

        public async Task<string> ReadRequestAsUtf8String() {
            var b = new byte[ContentLength];
            while (ContentBytesRead < b.Length
                && !_IsDisposed) {
                ContentBytesRead
                    += await ReadAsync(b, (int)ContentBytesRead, (int)(b.Length - ContentBytesRead), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            return Encoding.UTF8.GetString(b);
        }

        internal void UpgradeToWebSocket() {
            WebSocket = new SslWebSocket(this);
        }

        internal async Task WriteAsync(string s, System.Threading.CancellationToken token) {
            var b = Encoding.UTF8.GetBytes(s);
            await WriteAsync(b, 0, b.Length, token).ConfigureAwait(false);
        }

        internal async Task<bool> WriteAsync(byte[] b, int offset, int count, System.Threading.CancellationToken token) {
            var sem = _AsyncWriteAccess;
            if (sem == null || token.IsCancellationRequested)
                return false;
            bool hasLock = false;
            try {
                // Only 1 op allowed at a time
                // SemaphoreSlim.Wait(token) has an uncatchable ArgumentNullException bug:
                // SemaphoreSlim.CancellationTokenCanceledEventHandler -> Monitor.Enter
                sem.Wait();
                hasLock = true;
                var s = Stream;
                if (s == null || !s.CanWrite)
                    return false;
                await s.WriteAsync(b, offset, count, token).ConfigureAwait(false);
                return true;
            } catch {
                Close();
                return false;
            } finally {
                if (hasLock) {
                    try {
                        sem.Release();
                    } catch (ObjectDisposedException) { }
                }
            }
        }

        #region IDisposable Support

        private bool _IsDisposed;

        public void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_IsDisposed) {
                if (disposing) {
                    Close();
                }
            }
        }

        #endregion IDisposable Support
    }
}