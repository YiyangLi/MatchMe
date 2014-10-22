using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using MatchMe.Common;

namespace MatchMe.Server
{
    class Program
    {
        static void WaitForQ()
        {
            while (true)
            {
                System.Threading.Thread.Sleep(500);
                while (Console.KeyAvailable)
                {
                    if (Console.ReadKey().Key == ConsoleKey.Q)
                        return;
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            Config.AppName = typeof(Program).Assembly.GetName().Name;
            Config.AppName = typeof(Program).Assembly.GetName().Version.ToString();
            Config.AdminUser = Config.GetSetting<string>("AdminUser", "Market");
            Config.DefaultPrice = Config.GetSetting<double>("DefaultPrice", "1.00");
            Config.DefaultVolume = Config.GetSetting<int>("DefaultVolume", "10000");
            Config.Commission = Config.GetSetting<double>("Commission", "0.0");
            User user = new User(Config.AdminUser, 0.0);
            MatchMeDB.Instance.AddUser(user);
            ServerLog.LogInfo("Starting {0} version: {1}", Config.AppName, Config.AppVersion);
            if (args.Length == 0)
            {
                ServerLog.LogInfo("Running MacheMe in service mode");
                System.Diagnostics.Debugger.Launch();
                System.ServiceProcess.ServiceBase.Run(new System.ServiceProcess.ServiceBase[] { new MatchMeService() });
            }
            else if (args[0].ToUpper() == "DEBUG")
            {
                ServerLog.ConsoleLogMode = true;
                ServerLog.LogInfo("Running Match Me Server in debug mode");
                ServerLog.LogInfo("Match Me version {0}", typeof(Program).Assembly.GetName().Version);
                MatchMeServer server = new MatchMeServer();
                server.Start();
                WaitForQ();
                server.Stop();
            }
            else
            {
                Console.WriteLine(@"Usage:DEBUG (run in console mode) (install service)");
            }
        }
    }
}
