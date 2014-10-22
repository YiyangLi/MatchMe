using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Linq;
using System.Text;
using System.ComponentModel;

using MatchMe.Common;

namespace MatchMe.Server
{
    public class MatchMeService : ServiceBase
    {
        private MatchMeServer server;
                
        public MatchMeService()
        {
            ServiceName = "MatchMe.Service";
        }

        protected override void OnStart(string[] args)
        {
            server = new MatchMeServer();
            server.Start();
            ServerLog.LogInfo("MatchMe Service Started");
        }

        protected override void OnStop()
        {
            server.Stop();
            ServerLog.LogInfo("MatchMe Service Stopped");
        }
    }
}
