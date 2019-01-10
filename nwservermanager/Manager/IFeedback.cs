using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager.Manager
{
    public interface IFeedback
    {
        void Error(Exception ex, bool isCritical);

        void WriteLine(string value);

        void WriteLine(string format, params object[] arg);
    }
}
