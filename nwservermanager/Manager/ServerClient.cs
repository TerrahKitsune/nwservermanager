using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager.Manager
{
    public enum ClientState
    {
        Ok = 0,
        HalfOpen = 1,
        Dead = 2,
    }

    public class ServerClient : IDisposable
    {
        private TcpClient _client;
        private DateTime _lastact;
        private MemoryStream _stream;
        private int _buildsize;
        private readonly TimeSpan _timeout;
        private TimeSpan _extendedTimeout
        {
            get
            {
                if(State == ClientState.HalfOpen)
                {
                    return TimeSpan.FromSeconds(5);
                }
                else
                {
                    return _timeout;
                }
            }
        }

        public IPAddress IP { get; }

        public Guid ServerId { get; private set; }
        public Guid SessionId { get; private set; }
        public ClientState State { get; private set; }
        public TimeSpan LastActivity => DateTime.UtcNow - _lastact;

        public ServerClient(TcpClient client, TimeSpan timeout)
        {
            _timeout = timeout;
            _stream = null;
            SessionId = Guid.NewGuid();
            _client = client;
            ServerId = Guid.Empty;
            State = ClientState.HalfOpen;
            _lastact = DateTime.UtcNow;

            IP = (client.Client.RemoteEndPoint as IPEndPoint).Address;

            Send(SessionId.ToString());
        }

        public void Upgrade(Guid serverid)
        {
            ServerId = serverid;
            _lastact = DateTime.UtcNow;
            State = ClientState.Ok;
            Send(SessionId.ToString());
        }

        public void Send(string data)
        {
            if (State == ClientState.Dead)
            {
                throw new InvalidOperationException("Cannot send data on dead socket");
            }

            using (BinaryWriter writer = new BinaryWriter(new MemoryStream()))
            {
                byte[] raw = Encoding.UTF8.GetBytes(data);
                writer.Write(raw.Length);
                writer.Write(raw);

                writer.BaseStream.Seek(0, SeekOrigin.Begin);
                byte[] buffer = new byte[writer.BaseStream.Length];
                writer.BaseStream.Read(buffer, 0, (int)writer.BaseStream.Length);

                try
                {
                    _client.Client.Send(buffer);
                }
                catch
                {
                    Kill();
                }
            }
        }

        public void Kill()
        {
            State = ClientState.Dead;
            _client?.Dispose();
            _client = null;
            _stream?.Dispose();
            _stream = null;
        }

        public string Tick()
        {
            try
            {
                if (State == ClientState.Dead || _client.Client.Available <= 0)
                {
                    if(LastActivity > _extendedTimeout)
                    {
                        Kill();
                    }

                    return null;
                }
            }
            catch
            {
                Kill();
                return null;
            }

            if (_stream == null)
            {
                try
                {
                    if (_client.Client.Available < 4)
                    {
                        return null;
                    }
                }
                catch
                {
                    Kill();
                    return null;
                }

                byte[] rawheader = new byte[4];
                try
                {
                    _client.Client.Receive(rawheader);
                }
                catch
                {
                    Kill();
                    return null;
                }

                _lastact = DateTime.UtcNow;

                _buildsize = BitConverter.ToInt32(rawheader, 0);
                _stream = new MemoryStream(_buildsize);
            }

            byte[] buffer = new byte[Math.Min(_buildsize, 1500)];
            int read = _client.Client.Receive(buffer);

            if (read <= 0)
            {
                Kill();
                return null;
            }
            else
            {
                _stream.Write(buffer, 0, buffer.Length);
            }

            if (_stream.Length == _stream.Capacity)
            {
                buffer = new byte[_stream.Length];
                _stream.Seek(0, SeekOrigin.Begin);
                _stream.Read(buffer, 0, (int)_stream.Length);

                _stream.Dispose();
                _stream = null;

                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                return null;
            }
        }

        public void Dispose()
        {
            State = ClientState.Dead;
            _client?.Dispose();
            _client = null;
            _stream?.Dispose();
            _stream = null;
        }

        public override string ToString()
        {
            return $"{IP} ({ServerId})";
        }
    }
}
