using nwservermanager.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nwservermanager
{
    public class ConsoleFeedback : IFeedback
    {
        public ConsoleFeedback(string title)
        {
            Console.Title = title;
        }

        public void Error(Exception ex, bool isCritical)
        {
            lock (this)
            {
                ConsoleColor original = Console.ForegroundColor;

                if (isCritical)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Out.WriteLine("{0}: {1}{2}{3}", ex.GetType().Name, ex.Message, Environment.NewLine, ex.StackTrace);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Out.WriteLine("{0}: {1}", ex.GetType().Name, ex.Message);
                }

                Console.ForegroundColor = original;
            }
        }

        public void WriteLine(string value)
        {
            lock (this)
            {
                Console.Out.WriteLine(value);
            }
        }

        public void WriteLine(string format, params object[] arg)
        {
            lock (this)
            {
                Console.Out.WriteLine(format, arg);
            }
        }
    }
}
