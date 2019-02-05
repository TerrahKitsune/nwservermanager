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
        private long _messages;

        public ConsoleFeedback(string title)
        {
            SetStatusLine(title);
            _messages = 0;
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

                Console.Out.WriteLine();

                Console.ForegroundColor = original;
            }
        }

        public void SetStatusLine(string value)
        {
            lock (this)
            {
                Console.Title = "NWBackend: " + value;
            }
        }

        public void WriteLine(string value)
        {
            lock (this)
            {
                AlternateColor();
                Console.Out.WriteLine(value);
                Console.Out.WriteLine();
            }
        }

        public void WriteLine(string format, params object[] arg)
        {
            lock (this)
            {
                AlternateColor();
                Console.Out.WriteLine(format, arg);
                Console.Out.WriteLine();
            }
        }

        private void AlternateColor()
        {
            if ((_messages++) % 2 == 0)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }
}
