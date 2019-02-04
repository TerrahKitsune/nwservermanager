using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager.Dtos
{
    public class WebRequest
    {
        private DateTime _started;

        public WebRequest(BaseTransmission baseTransmission, Guid from)
        {
            _started = DateTime.UtcNow;
            Request = baseTransmission;
            Response = null;
            From = from;
        }

        public BaseTransmission Request { get; private set; }
        public BaseTransmission Response { get; private set; }
        public Guid From { get; private set; }
        public TimeSpan Age => DateTime.UtcNow - _started;

        internal void SetResponse(BaseTransmission request)
        {
            _started = DateTime.UtcNow;
            Response = request;
        }
    }
}
