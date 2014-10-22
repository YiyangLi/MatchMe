using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace MatchMe.Common
{
    public static class ServerLog
    {
        private static readonly log4net.ILog infoLogger = log4net.LogManager.GetLogger("MatchMeInfo");
        private static readonly log4net.ILog errorLogger = log4net.LogManager.GetLogger("MatchMeInfo");

        public static bool ConsoleLogMode
        {
            get;
            set;
        }

        public static void LogInfo(string format, params object[] args)
        {
            if (ConsoleLogMode)
                Console.WriteLine(format, args);
            else
                infoLogger.InfoFormat(format, args);
        }

        public static void LogError(string format, params object[] args)
        {
            if (ConsoleLogMode)
                Console.WriteLine(format, args);
            else
            {
                errorLogger.InfoFormat(format, args);
                infoLogger.InfoFormat(format, args);
            }
        }

        public static void LogException(Exception ex, string msg)
        {
            if (ConsoleLogMode)
                Console.WriteLine(msg + ": {0}", ex.Message);
            else
                if (!string.IsNullOrEmpty(ex.InnerException.Message))
                {
                    errorLogger.InfoFormat(msg + ": {0}", ex.InnerException.Message);
                    infoLogger.InfoFormat(msg + ": {0}", ex.InnerException.Message);
                }
                else
                {
                    errorLogger.InfoFormat(msg + ": {0}", ex.Message);
                    infoLogger.InfoFormat(msg + ": {0}", ex.Message);
                }
        }

        public static void LogHttpRequest(HttpListenerRequest req, int returnCode, int size, string uid, int latency = 0)
        {
            string text = string.Format("{0} {7} {8} [{1}] \"{2} {3} {4}\" {5} {6} {7}",
                    req.RemoteEndPoint.Address, //0
                    FormatDateTime(DateTime.Now), //1
                    req.HttpMethod, //2
                    req.Url.ToString(), //3
                    HttpVersion(req), //4
                    returnCode, //5
                    size, //6
                    req.UserAgent == null ? "-" : req.UserAgent.Replace(' ', '+'),
                    uid,
                    latency
                    );
        }

        private static string FormatDateTime(DateTime dt)
        {

            return dt.ToString("dd/MMM/yyyy:HH:mm:ss zz00");
        }

        static string HttpVersion(HttpListenerRequest req)
        {
            return string.Format("HTTP/{0}", req.ProtocolVersion);
        }
    }
}
