using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace MatchMe.Common
{
    public static class Config
    {
        public static T GetSetting<T>(string name, string defaultValue)
        {
            string s = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrEmpty(s))
            {
                s = defaultValue;
            }

            T val = (T)(Convert.ChangeType(s, typeof(T)));
            ServerLog.LogInfo("Config: {0} == {1}", name, val);
            return val;
        }

        public static string AdminUser
        {
            get;
            set;
        }

        public static string AppName
        {
            get;
            set;
        }

        public static string AppVersion
        {
            get;
            set;
        }

        public static double DefaultPrice
        {
            get;
            set;
        }

        public static int DefaultVolume
        {
            get;
            set;
        }

        public static double Commission
        {
            get;
            set;
        }
    }
}
