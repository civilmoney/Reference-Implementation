#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Text;

namespace CM {

    /// <summary>
    /// A reader which emits Messages from raw UTF-8 bytes.
    /// </summary>
    public class MessageReader {
        private Message _Current;
        private byte[] _CurrentLine = new byte[1024];
        private int _CurrentLinePos;

        private string _NOnce;
        private Action<string> _OnError;
        private Action<Message> _OnMessage;

        public MessageReader(Action<Message> onMessage, Action<string> onError) {
            _OnMessage = onMessage;
            _OnError = onError;
        }

        public void Write(byte[] b, int offset, int count) {
            for (int i = offset; i < offset + count; i++) {
                if (_CurrentLine.Length == _CurrentLinePos) {
                    Array.Resize(ref _CurrentLine, _CurrentLinePos * 2);
                }
                if (b[i] == 10) {
                    // End of line
                    AppendLine(Encoding.UTF8.GetString(_CurrentLine, 0, _CurrentLinePos).TrimEnd(Constants.NewLineChars));
                    _CurrentLinePos = 0;
                } else {
                    _CurrentLine[_CurrentLinePos++] = b[i];
                }
            }
        }

        public void Write(string payload) {
            var lines = payload.TrimEnd(Constants.NewLineChars).Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                AppendLine(lines[i].TrimEnd(Constants.NewLineChars));
            }
        }

        private void AppendLine(string s) {
            if (_Current == null) {
                // New message, expect CMD or RES.
                if (s.StartsWith(Message.RequestHeader.PREFIX + " ")) {
                    _Current = new Message();
                    _Current.Request = new Message.RequestHeader(s);
                    if (!_Current.Request.IsValid) {
                        OnError("Invalid request header: " + s);
                        _Current = null;
                    } else
                        _NOnce = _Current.Request.NOnce;
                } else if (s.StartsWith(Message.ResponseHeader.PREFIX + " ")) {
                    _Current = new Message();
                    _Current.Response = new Message.ResponseHeader(s);
                    if (!_Current.Response.IsValid) {
                        OnError("Invalid response header: " + s);
                        _Current = null;
                    } else
                        _NOnce = _Current.Response.NOnce;
                } else {
                    OnError("Expect CMD or RES - got '" + s + "'");
                }
            } else {
                if (s.Equals("END " + _NOnce)) {
                    // End of message
                    _OnMessage(_Current);
                    _Current = null;
                } else {
                    _Current.Values.Append(s);
                }
            }
        }

        private void OnError(string msg) {
            if (_OnError != null)
                _OnError(msg);
        }
    }
}