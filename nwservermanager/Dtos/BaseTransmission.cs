using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager.Dtos
{
    public enum ActionEnum : int
    {
        Ping = 0,
        Pong = 1,
        SendMessageToTarget = 2,
        ClientConnected = 3,
        ClientDisconnected = 4,
        GetAllConnectedClients = 5,
        WebRequest = 6,
    }

    public class BaseTransmission
    {
        public Guid SessionId { get; set; }
        public Guid TargetServerId { get; set; }
        public int Action { get; set; }
        public string Parameter { get; set; }
        public dynamic Data { get; set; }
    }
}
