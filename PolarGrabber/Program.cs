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
        static string logfn = Properties.Settings.Default.logFilename;
        static string logevent = Properties.Settings.Default.logLine;

        static void Main(string[] args)
        {
            // utf8 console so our hearts work :D
            Console.OutputEncoding = Encoding.UTF8;

            // find the first hr-capable device
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

            // start event-driven hr processing
            Console.Write("Starting HR capture...");
            hrp.Start().Wait();
            if (hrp.isRunning)
            {
                Console.WriteLine("OK");
            } else { 
                Console.WriteLine("could not initialize");
                return;
            }

            // initiate log file
            string logfile = string.Format(logfn, DateTime.Now);
            log = new StreamWriter(logfile);

            // subscribe to the hr events
            hrp.HrTaken += SendHr;

            // set up web server
            Console.Write("Initializing HTTP endpoint...");
            wp = new WebProvider();
            wp.ClientConnected += WpClientConnected;
            wp.ClientDisconnected += WpClientDisconnected;
            wp.Listen();
            Console.WriteLine(string.Format("listening on {0}", wp.url));

            // wait for enter to quit
            // (this does not block background hr processing)
            Console.WriteLine("Press Enter to quit.");
            Console.ReadLine();

            hrp.Stop();
            wp.Stop();
        }

        private static void WpClientConnected(object sender, EventArgs e)
        {
            // plus symbol indicates new web client subscriber
            Console.Write("+");
        }

        private static void WpClientDisconnected(object sender, EventArgs e)
        {
            // minus symbol indicates losing a web client subscriber
            Console.Write("-");
        }

        private static void SendHr(object sender, HrEventArgs e)
        {
            // send the hr value to all web clients
            wp.SendEvent(e.HrData.HrValue);
            
            // log it into a file
            string logline = string.Format(logevent, DateTime.Now, e.HrData.HrValue);
            log.WriteLine(logline);
            log.Flush();

            // heart symbol indicates processed hr data
            Console.Write("\u2665");
        }
    }



}
