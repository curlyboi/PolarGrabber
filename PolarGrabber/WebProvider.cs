using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;

namespace PolarGrabber
{
    public class WebProvider
    {
        // listening web server
        HttpListener hli;
        // list of connected web clients
        List<HttpListenerResponse> subs;
        // configured listening url
        public string url = Properties.Settings.Default.webURL;

        // when a browser connects
        public event EventHandler<EventArgs> ClientConnected;
        // when a browser disconnects - we only discover this when attempting to send data
        public event EventHandler<EventArgs> ClientDisconnected;
        
        protected void OnClientConnected()
        {
            ClientConnected?.Invoke(this, new EventArgs());
        }

        protected void OnClientDisconnected()
        {
            ClientDisconnected?.Invoke(this, new EventArgs());
        }


        public WebProvider()
        {
            subs = new List<HttpListenerResponse>();
            
            hli = new HttpListener();
            hli.Prefixes.Add(url);
            hli.Start();
        }

        public void Stop()
        {
            // close all browser connections
            foreach (HttpListenerResponse sub in subs)
            {
                try
                {
                    sub.Close();
                }
                catch { }
            }
            subs.Clear();
            // stop listening
            hli.Stop();
        }

        public void Listen()
        {
            hli.BeginGetContext(HandleRequest, null);
        }

        private void HandleRequest(IAsyncResult ar)
        {
            // finalize the client request...
            HttpListenerContext ctx = null;
            try
            {
                ctx = hli.EndGetContext(ar);
            }
            catch
            {
            }
            // ...and start listening again
            Listen();

            if (ctx != null)
            {
                // send some headers
                HttpListenerResponse resp = ctx.Response;

                resp.AddHeader("Cache-Control", "no-cache");
                resp.AddHeader("Access-Control-Allow-Origin", "*");

                resp.ContentType = "text/event-stream";
                resp.ContentEncoding = Encoding.UTF8;
                resp.KeepAlive = true;

                // register the browser
                subs.Add(resp);
                OnClientConnected();
            }
        }


        public void SendEvent(int hr)
        {
            // send the hr data to all connected clients that we know about
            string msgData = string.Format("event: hr\ndata: {0}\n\n", hr);

            byte[] eventBytes = Encoding.UTF8.GetBytes(msgData);

            foreach (HttpListenerResponse sub in subs.ToArray()) // toarray so we can remove the dead ones from the original list
            {
                try
                {
                    sub.OutputStream.Write(eventBytes, 0, eventBytes.Length);
                    sub.OutputStream.Flush();
                }
                catch
                {
                    // the browser has already disconnected
                    sub.Close();
                    subs.Remove(sub);
                    OnClientDisconnected();
                }
            }
        }

    }
}
