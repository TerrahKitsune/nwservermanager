using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager.Manager
{
    public interface IServerValidator
    {
        bool ServerIsAllowed(Guid id);

        string GetClientName(Guid id);
    }
}
