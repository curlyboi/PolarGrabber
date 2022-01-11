using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;



namespace PolarGrabber
{
    class Program
    {
        static WebProvider wp;
        static HrProvider hrp;
        static StreamWriter log;
        const string logfn = "polarlog_{0:yyyy-MM-dd_hh-mm-ss}.txt";
        const string logevent = "{0:yyyy-MM-dd_hh-mm-ss};{1}";

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Console.Write("Finding BLE devices with HR...");
            hrp = new HrProvider();
            hrp.FindDevice().Wait();
            if (hrp.deviceFound)
            {
                Console.WriteLine(string.Format("found {0}", hrp.deviceName));

            } else { 
                Console.WriteLine("not found");
                return;
            }

            Console.Write("Starting HR capture...");
            hrp.Start().Wait();
            if (hrp.isRunning)
            {
                Console.WriteLine("OK");
            } else { 
                Console.WriteLine("could not initialize");
                return;
            }

            string logfile = string.Format(logfn, DateTime.Now);
            log = new StreamWriter(logfile);

            hrp.HrTaken += SendHr;

            Console.Write("Initializing HTTP endpoint...");
            wp = new WebProvider();
            wp.ClientConnected += WpClientConnected;
            wp.ClientDisconnected += WpClientDisconnected;
            wp.Listen();
            Console.WriteLine(string.Format("listening on {0}", wp.url));


            Console.WriteLine("Press Enter to quit.");
            Console.ReadLine();

            hrp.Stop();
            wp.Stop();
        }

        private static void WpClientConnected(object sender, EventArgs e)
        {
            Console.Write("+");
        }

        private static void WpClientDisconnected(object sender, EventArgs e)
        {
            Console.Write("-");
        }

        private static void SendHr(object sender, HrEventArgs e)
        {
            wp.SendEvent(e.HrValue);
            
            string logline = string.Format(logevent, DateTime.Now, e.HrValue);
            log.WriteLine(logline);
            log.Flush();

            Console.Write("\u2665");
        }
    }



}
