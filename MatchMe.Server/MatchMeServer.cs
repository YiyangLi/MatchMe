using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Web;
using System.Net;

using MatchMe.Common;


namespace MatchMe.Server
{
    public class MatchMeServer
    {
        private HttpListener listener;
        private Encoding serverCharSet = Encoding.UTF8;

        private HashSet<Task> runningTasks;
        private bool stopping;

        private long totalBytesOut;
        private int totalRequests;
        private int total550s;
        private int total500s;
        private int total200s;

        private DateTime startTime;

        private IActionProcessor actionProcessor;
        private IDatabaseReader dataProcessor;

        public MatchMeServer()
        {
            startTime = DateTime.Now;
            totalBytesOut = 0;
            totalRequests = 0;
            total550s = 0;
            total500s = 0;
            total200s = 0;

            actionProcessor = new ActionProcessor();
            dataProcessor = new DatabaseReader();

            listener = new HttpListener();
            listener.Prefixes.Add("http://+:86/action/");
            listener.Prefixes.Add("http://+:86/test/");
            listener.Prefixes.Add("http://+:86/db/");

            runningTasks = new HashSet<Task>();
            stopping = false;
        }

        public void Start()
        {
            AppDomain.CurrentDomain.UnhandledException += (o, ea) =>
            {
                if (ea.ExceptionObject is Exception)
                    ServerLog.LogException(ea.ExceptionObject as Exception, "Unhandled exception handler called");
                else
                    ServerLog.LogError("Unhandled exception handler called with unrecognized exception objhect");
            };

            ServerLog.LogInfo("Starting MatchMe Host");
            listener.Start();
            listener.BeginGetContext(OnContext, null);
        }

        public void Stop()
        {
            ServerLog.LogInfo("Stopping remaining tasks");
            Task[] remainingTasks;
            lock (runningTasks)
            {
                stopping = true;
                remainingTasks = new Task[runningTasks.Count];
                runningTasks.CopyTo(remainingTasks);
            }

            Task.WaitAll(remainingTasks);


            ServerLog.LogInfo("Stopping HTTP listener");
            listener.Stop();
        }

        private void OnContext(IAsyncResult ar)
        {
            DateTime reqStartTime = DateTime.Now;

            HttpListenerContext context;

            try
            {
                context = listener.EndGetContext(ar);
            }
            catch (HttpListenerException)
            {
                ServerLog.LogInfo("HttpListener has been shut down");
                return;
            }

            if (!stopping) listener.BeginGetContext(OnContext, null);

            var processWebRequest = new Task(() =>
            {
                System.Threading.Interlocked.Increment(ref totalRequests);
                byte[] json = null;
                int httpStatus = 200;
                string redirectUrl = null;
                string outContentType = "application/json";
                try
                {
                    string processor;
                    string command;
                    string jsonInput;

                    string module;
                    string contentType;
                    string source;
                    string agent;


                    var args = ParseUrl(context.Request, out processor, out command, out jsonInput, out module, out contentType, out source, out agent);

                    if (module == "test")
                    {
                        if (processor == "util")
                        {
                            if (command == "stats")
                                StatRespond(context, reqStartTime);
                            else if (command == "health")
                                HealthRespond(context, reqStartTime);
                            else
                                throw new ApplicationException(string.Format("Unknown util command: {0}", command));
                            return;
                        }
                    }
                    else if (module == "action")
                    {
                        string jsonString = actionProcessor.Execute(command, args, jsonInput, contentType, source, agent, out outContentType, out redirectUrl, out httpStatus);
                        if (jsonString == null)
                            jsonString = "";

                        json = Encoding.UTF8.GetBytes(jsonString);
                    }
                    else if (module == "db")
                    {
                        string jsonString = dataProcessor.Execute(command, args, jsonInput, contentType, source, agent, out outContentType, out redirectUrl, out httpStatus);
                        if (jsonString == null)
                            jsonString = "";

                        json = Encoding.UTF8.GetBytes(jsonString);
                    }
                    else
                    {
                        throw new ApplicationException(string.Format("Unknown module: {0}", module));
                    }

                }
                catch (Exception e)
                {
                    ExceptionRespond(context, e, 500, reqStartTime);
                    return;
                }

                if (redirectUrl != null)
                    RespondRedirect(context, redirectUrl);
                else
                    Respond(context, json, httpStatus, reqStartTime, outContentType);

            });

            lock (runningTasks)
            {
                if (stopping)
                {
                    Respond(context,
                        serverCharSet.GetBytes("MatchMe is shutting down.")
                        , 500, reqStartTime);
                    return;
                }

                runningTasks.Add(processWebRequest);
            }

            processWebRequest.ContinueWith(t =>
            {
                lock (runningTasks)
                {
                    runningTasks.Remove(t);
                }

                if (t.IsFaulted)
                {
                    ServerLog.LogException(t.Exception.InnerException, "Exception was unhandled in task");
                }

            });


            processWebRequest.Start();

        }

        private Dictionary<string, string> ParseUrl(HttpListenerRequest req, out string processor, out string command, out string jsonInput, out string module, out string contentType, out string source, out string agent)
        {
            contentType = string.Empty;
            processor = string.Empty;
            command = string.Empty;
            agent = req.UserAgent;

            var vals = req.Headers.GetValues("X-Forwarded-For");
            if (vals == null)
                source = req.RemoteEndPoint.Address.ToString();
            else
                source = string.Join(",", vals);

            module = Path.GetDirectoryName(req.Url.AbsolutePath).TrimStart('\\');

            if (module == "action" || module == "db")
            {
                command = Path.GetFileName(req.Url.AbsolutePath);
            }
            else
            {
                string[] proccmd = Path.GetFileName(req.Url.AbsolutePath).Split('.');

                if (proccmd.Length != 2)
                    throw new ApplicationException(string.Format("Invalid query processor invocation {0}, must be <proc>.<command>", Path.GetFileName(req.Url.AbsolutePath)));

                processor = proccmd[0];
                command = proccmd[1];
            }
            string query = req.Url.Query;
            var col = HttpUtility.ParseQueryString(query);
            var dictionary = MakeDictionaryFromNVC(col);

            jsonInput = null;

            if (req.HasEntityBody)
            {
                contentType = req.Headers["content-type"];
                Stream body = req.InputStream;
                Encoding encoding = req.ContentEncoding;
                StreamReader reader = new StreamReader(body, encoding);

                jsonInput = reader.ReadToEnd();
                
                body.Close();
                reader.Close();
            }

            return dictionary;
        }


        private void ExceptionRespond(HttpListenerContext context, Exception e, int code, DateTime reqStartTime)
        {
            byte[] message = serverCharSet.GetBytes(
                string.Format("An {0} exception was thrown.\n{1}\n{2}", e.GetType(), e.Message, e.StackTrace));
            ServerLog.LogException(e, "An exception was thrown rendering URL " + context.Request.Url);

            System.Threading.Interlocked.Add(ref totalBytesOut, message.Length);
            if (code == 500)
                System.Threading.Interlocked.Increment(ref total500s);
            else if (code == 550)
                System.Threading.Interlocked.Increment(ref total550s);

            Respond(context, message, code, reqStartTime, "text/plain");
        }

        private void Respond(HttpListenerContext context, byte[] buffer, int returnCode, DateTime reqStartTime, string contentType = "text/plain")
        {
            int len = buffer != null ? buffer.Length : 0;

            try
            {
                context.Response.AddHeader("Cache-Control", "no-cache");
                context.Response.AddHeader("Content-Type", contentType);
                context.Response.AddHeader("Access-Control-Allow-Origin", "*");
                context.Response.ContentEncoding = serverCharSet;
                context.Response.ContentLength64 = len;
                context.Response.StatusCode = returnCode;
                context.Response.OutputStream.Write(buffer, 0, len);
                context.Response.ContentType = contentType;

                ServerLog.LogHttpRequest(context.Request, returnCode, len, string.Empty, (int)((startTime - DateTime.Now).TotalMilliseconds));
                System.Threading.Interlocked.Add(ref totalBytesOut, buffer.Length);
                System.Threading.Interlocked.Increment(ref total200s);
            }
            catch (HttpListenerException hle)
            {
                ServerLog.LogException(hle, "Exception while returning data to http client");
            }

        }

        private static Dictionary<string, string> MakeDictionaryFromNVC(System.Collections.Specialized.NameValueCollection nvc)
        {
            var d = new Dictionary<string, string>();
            foreach (string key in nvc.Keys)
            {
                var value = nvc.Get(key);
                if (value == null)
                    d.Add(key, "");
                else
                    d.Add(key, value);
            }
            return d;
        }

        private void HealthRespond(HttpListenerContext context, DateTime reqStartTime)
        {
            string healthtext = "<pre>\nHealth Check: Success\n</pre>";
            byte[] output = serverCharSet.GetBytes(healthtext);

            Respond(context, output, 200, reqStartTime, "text/html");
        }

        private void RespondRedirect(HttpListenerContext context, string redirectUrl)
        {
            context.Response.Redirect(redirectUrl);
            context.Response.StatusCode = 302;
            context.Response.Close();
        }

        private void StatRespond(HttpListenerContext context, DateTime reqStartTime)
        {
            string statstext = string.Format("<pre>Match Me Server Stats Page\n\nTotal Uptime:{0:0.00} hours\n\n", (DateTime.Now - startTime).TotalHours)
                + GetServerStats() + "</pre>";
            byte[] output = serverCharSet.GetBytes(statstext);

            Respond(context, output, 200, reqStartTime, "text/html");
        }

        private string GetServerStats()
        {
            StringBuilder text = new StringBuilder();
            text.AppendFormat("MatchMe Server Stats:\n");
            text.AppendFormat("Hostname: {0}\n", System.Environment.MachineName);
            text.AppendFormat("Total Users: {0}\n", MatchMeDB.Instance.Users.Count());
            text.AppendFormat("Total Orders: {0}\n", MatchMeDB.Instance.Orders.Count());
            text.AppendFormat("Total Requests: {0}\n", totalRequests);
            text.AppendFormat("Total 200s: {0}\n", total200s);
            text.AppendFormat("Total 500s: {0}\n", total500s);
            text.AppendFormat("Total 550s: {0}\n", total550s);
            text.AppendFormat("Total Bytes Out: {0}\n\n", totalBytesOut);

            return text.ToString();
        }
    }
}
