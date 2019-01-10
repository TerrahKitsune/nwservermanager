using nwservermanager.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nwservermanager
{
    class Program
    {
        static void Main(string[] args)
        {
            IFeedback fb = new ConsoleFeedback();

            using (ServerManager manager = new ServerManager(19836, new JsonFileValidator("clients.json",fb), fb))
            {
                while (true)
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
