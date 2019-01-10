using nwservermanager.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nwservermanager.Manager
{
    public class ServerManager : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Thread _thread;
        private readonly ConcurrentDictionary<Guid, ServerClient> _clients;
        private volatile bool _alive;
        private readonly IServerValidator _validator;
        private readonly IFeedback _feedback;

        public ServerManager(int port, IServerValidator validator, IFeedback fb)
        {
            _feedback = fb ?? throw new NullReferenceException("No feedback provided");
            _validator = validator ?? throw new NullReferenceException("No validator provided");
            _clients = new ConcurrentDictionary<Guid, ServerClient>();
            _thread = new Thread(Process);
            _listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            _alive = true;
            _listener.Start();
            _thread.Start();
        }

        private void SendClientListToEveryone()
        {
            Dictionary<string, object> clients = new Dictionary<string, object>();

            foreach (var item in _clients.Where(i => i.Value.State == ClientState.Ok).Select(i => i.Value))
            {
                clients.Add(item.ServerId.ToString(), _validator.GetClientName(item.ServerId));
            }

            foreach (var item in _clients.Where(i => i.Value.State == ClientState.Ok).Select(i => i.Value))
            {
                item.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new BaseTransmission
                {
                    Action = (int)ActionEnum.GetAllConnectedClients,
                    Data = clients,
                    SessionId = item.SessionId,
                    Parameter = clients.Count.ToString(),
                    TargetServerId = Guid.Empty
                }));
            }
        }

        private bool ProcessRequest(ServerClient from, string raw)
        {
            try
            {
                _feedback.WriteLine("Request: {0} -> {1}", from, raw);

                BaseTransmission request = Newtonsoft.Json.JsonConvert.DeserializeObject<BaseTransmission>(raw);

                if (request.SessionId != from.SessionId)
                {
                    return false;
                }

                ActionEnum action = (ActionEnum)request.Action;

                ServerClient target = request.TargetServerId == Guid.Empty ? null : GetClientFromClientId(request.TargetServerId);

                if (action == ActionEnum.SendMessageToTarget)
                {
                    target?.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new BaseTransmission
                    {
                        Action = (int)ActionEnum.SendMessageToTarget,
                        Data = request.Data,
                        SessionId = target.SessionId,
                        Parameter = request.Parameter,
                        TargetServerId = from.ServerId
                    }));
                }
                else if (action == ActionEnum.GetAllConnectedClients)
                {
                    Dictionary<string, object> clients = new Dictionary<string, object>();

                    foreach (var item in _clients.Where(i => i.Value.State == ClientState.Ok).Select(i => i.Value))
                    {
                        clients.Add(item.ServerId.ToString(), _validator.GetClientName(item.ServerId));
                    }

                    from.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new BaseTransmission
                    {
                        Action = (int)ActionEnum.GetAllConnectedClients,
                        Data = clients,
                        SessionId = from.SessionId,
                        Parameter = request.Parameter,
                        TargetServerId = Guid.Empty
                    }));
                }

                return true;
            }
            catch (Exception ex)
            {
                _feedback.Error(ex, false);
                return false;
            }
        }

        public ServerClient GetClientFromClientId(Guid serverid)
        {
            return _clients.Where(i => i.Value.State == ClientState.Ok && i.Value.ServerId == serverid).Select(i => i.Value).FirstOrDefault();
        }

        private void Process()
        {
            List<Guid> dead = new List<Guid>();

            while (_alive)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        ServerClient client = new ServerClient(_listener.AcceptTcpClient(), TimeSpan.FromSeconds(10));
                        _feedback.WriteLine("Connecting: {0}", client);
                        _clients.TryAdd(client.SessionId, client);
                    }

                    foreach (KeyValuePair<Guid, ServerClient> item in _clients)
                    {
                        try
                        {
                            if (item.Value.State == ClientState.Dead)
                            {
                                dead.Add(item.Key);
                                continue;
                            }

                            string value = item.Value.Tick();
                            if (value != null)
                            {
                                if (item.Value.State == ClientState.HalfOpen)
                                {
                                    if (Guid.TryParse(value, out Guid serverid))
                                    {
                                        //Don't allow duplicates
                                        if (!_validator.ServerIsAllowed(serverid))
                                        {
                                            _feedback.WriteLine("Unauthorized: {0} -> {1}", item.Value.IP, serverid);
                                            item.Value.Kill();
                                        }
                                        else if (_clients.Any(i => i.Value.ServerId == serverid && i.Value.State == ClientState.Ok))
                                        {
                                            _feedback.WriteLine("Already connected: {0} -> {1}", item.Value.IP, serverid);
                                            item.Value.Kill();
                                        }
                                        else
                                        {
                                            _feedback.WriteLine("Upgrading: {0} -> {1}", item.Value.IP, serverid);
                                            item.Value.Upgrade(serverid);
                                            SendClientListToEveryone();
                                        }
                                    }
                                    else
                                    {
                                        _feedback.WriteLine("No proper serverid given: {0} -> {1}", item.Value.IP, value);
                                        item.Value.Kill();
                                    }
                                }
                                else if (!ProcessRequest(item.Value, value))
                                {
                                    item.Value.Kill();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _feedback.Error(ex, false);
                            item.Value.Kill();
                        }
                    }

                    if (dead.Any())
                    {
                        dead.ForEach(i =>
                        {
                            if (_clients.TryRemove(i, out ServerClient dying))
                            {
                                _feedback.WriteLine("Disconnecting: {0}", dying);
                                dying.Dispose();
                            }
                        });
                        dead.Clear();

                        SendClientListToEveryone();
                    }
                }
                catch (Exception ex)
                {
                    _feedback.Error(ex, true);
                }

                try
                {
                    Thread.Sleep(1);
                }
                catch
                {
                    continue;
                }
            }
        }

        public void Dispose()
        {
            _alive = false;

            if (_thread.ThreadState == ThreadState.WaitSleepJoin)
            {
                _thread.Interrupt();
            }

            _thread.Join();

            foreach (KeyValuePair<Guid, ServerClient> item in _clients)
            {
                item.Value.Dispose();
            }

            _clients.Clear();

            _listener?.Stop();
        }
    }
}
