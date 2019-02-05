using nwservermanager.Dtos;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly List<Dtos.WebRequest> _webrequests;
        private volatile bool _alive;
        private readonly IServerValidator _validator;
        private readonly IFeedback _feedback;
        private TimeSpan _timeout;
        private long _msgcnt;

        public ServerManager(int port, IServerValidator validator, IFeedback fb)
        {
            _msgcnt = 0;
            _timeout = TimeSpan.FromSeconds(10);
            _webrequests = new List<Dtos.WebRequest>();
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

        public BaseTransmission SetWebRequest(Guid id, Guid target, string ip, string request, string body, Dictionary<string, string> headers)
        {

            ServerClient targetcli = GetClientFromClientId(target);

            if (targetcli == null)
            {
                return null;
            }

            headers = headers ?? new Dictionary<string, string>();

            if (headers.ContainsKey("IP"))
            {
                headers.Remove("IP");
            }

            if (headers.ContainsKey("BODY"))
            {
                headers.Remove("BODY");
            }

            headers.Add("IP", ip);
            headers.Add("BODY", body ?? "");

            Dtos.WebRequest webrequest = new Dtos.WebRequest(new BaseTransmission
            {
                Action = (int)ActionEnum.WebRequest,
                Data = headers,
                SessionId = targetcli.SessionId,
                Parameter = request,
                TargetServerId = id
            }, id);

            lock (_webrequests)
            {
                _webrequests.Add(webrequest);
            }

            targetcli?.Send(Newtonsoft.Json.JsonConvert.SerializeObject(webrequest.Request));

            while (webrequest.Response == null)
            {
                Thread.Sleep(1);
                if (webrequest.Age > _timeout)
                {
                    lock (_webrequests)
                    {
                        _webrequests.Remove(webrequest);
                    }

                    throw new TimeoutException("Request timed out");
                }
            }

            lock (_webrequests)
            {
                _webrequests.Remove(webrequest);
            }

            return webrequest.Response;
        }

        private bool ProcessRequest(ServerClient from, string raw)
        {
            _msgcnt++;

            try
            {
                BaseTransmission request = Newtonsoft.Json.JsonConvert.DeserializeObject<BaseTransmission>(raw);

                if (request.SessionId != from.SessionId)
                {
                    return false;
                }

                ActionEnum action = (ActionEnum)request.Action;

                if (action == ActionEnum.Ping || action == ActionEnum.Pong)
                {
                    return true;
                }

                ServerClient target = request.TargetServerId == Guid.Empty ? null : GetClientFromClientId(request.TargetServerId);

                if (action == ActionEnum.SendMessageToTarget)
                {
                    _feedback.WriteLine("SendMessageToTarget: {0} -> {1}", from, raw);

                    target?.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new BaseTransmission
                    {
                        Action = (int)ActionEnum.SendMessageToTarget,
                        Data = request.Data,
                        SessionId = target.SessionId,
                        Parameter = request.Parameter,
                        TargetServerId = from.ServerId
                    }));
                }
                else if (action == ActionEnum.WebRequest)
                {
                    _feedback.WriteLine("WebRequest: {0} -> {1}", from, raw);

                    Dtos.WebRequest webRequest;

                    lock (_webrequests)
                    {
                        webRequest = _webrequests
                            .Where(i => i.Response == null && i.Request.TargetServerId == request.TargetServerId)
                            .OrderByDescending(i => i.Age)
                            .FirstOrDefault();
                    }

                    if (webRequest != null)
                    {
                        webRequest.SetResponse(request);
                    }
                }
                else if (action == ActionEnum.GetAllConnectedClients)
                {
                    _feedback.WriteLine("GetAllConnectedClients: {0} -> {1}", from, raw);

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
                else
                {
                    _feedback.WriteLine("Unknown request: {0} from {1}", action, from);
                    return false;
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

            bool update = false;

            while (_alive)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        ServerClient client = new ServerClient(_listener.AcceptTcpClient(), _timeout);
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
                            else if (item.Value.State == ClientState.Ok && !_validator.ServerIsAllowed(item.Value.ServerId))
                            {
                                dead.Add(item.Key);
                                continue;
                            }

                            string value = item.Value.Tick();
                            if (value != null)
                            {
                                update = true;

                                if (item.Value.State == ClientState.HalfOpen)
                                {
                                    if (Guid.TryParse(value, out Guid serverid))
                                    {
                                        //Don't allow duplicates
                                        if (!_validator.ServerIsAllowed(serverid))
                                        {
                                            _feedback.WriteLine("Unauthorized: {0} -> {1}", item.Value.IP, serverid);
                                            item.Value.Kill("Unauthorized");
                                        }
                                        else if (_clients.Any(i => i.Value.ServerId == serverid && i.Value.State == ClientState.Ok))
                                        {
                                            _feedback.WriteLine("Already connected: {0} -> {1}", item.Value.IP, serverid);
                                            item.Value.Kill("Already connected");
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
                                        item.Value.Kill("No valid serverid given");
                                    }
                                }
                                else if (!ProcessRequest(item.Value, value))
                                {
                                    item.Value.Kill("Request failed");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _feedback.Error(ex, false);
                            item.Value.Kill(ex.Message);
                        }
                    }

                    if (dead.Any())
                    {
                        bool fromOkState = false;

                        update = true;

                        dead.ForEach(i =>
                        {
                            if (_clients.TryRemove(i, out ServerClient dying))
                            {
                                _feedback.WriteLine("Disconnecting: {0} ({1})", dying, dying.DeathReason ?? "Disconnected");
                                fromOkState = fromOkState || dying.DeathState == ClientState.Ok;
                                dying.Dispose();
                            }
                        });
                        dead.Clear();

                        //Only send refresh of the clients if it was an established connection that died
                        if (fromOkState)
                        {
                            SendClientListToEveryone();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _feedback.Error(ex, true);
                }

                if(update)
                {
                    _feedback.SetStatusLine($"{_clients.Count(i => i.Value.State == ClientState.Ok)} clients connected {_msgcnt} messages processed");

                    update = false;
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

            if (_thread.ThreadState == System.Threading.ThreadState.WaitSleepJoin)
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
