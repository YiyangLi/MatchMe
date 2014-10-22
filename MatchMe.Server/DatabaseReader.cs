using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatchMe.Common;
using MatchMe.TradeServer;

using Newtonsoft.Json;

namespace MatchMe.Server
{
    public class DatabaseReader : IDatabaseReader
    {
        private RestMethodBinder binder;
        private string server;

        public DatabaseReader()
        {
            binder = new RestMethodBinder(typeof(DatabaseReader));
            server = System.Environment.MachineName;
        }

        public string Execute(string command, Dictionary<string, string> args, string jsonInput, string contentType, string source, string agent, out string outRedirect, out string outContentType, out int httpStatus)
        {
            args.Add("contentType", contentType);
            args.Add("source", source);
            args.Add("agent", agent);
            return binder.CallRestMethod(this, command, args, jsonInput, out outRedirect, out outContentType, out httpStatus);
        }

        [RestMethod("GetUserInfo")]
        public string GetUserInfo(string UserName, string UserId)
        {
            string results = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(UserId))
                {
                    string adminUserId = MatchMeDB.Instance.GetUserId(Config.AdminUser);
                    var users = MatchMeDB.Instance.Users.Where(a => !a.Key.Equals(adminUserId)).Select(a => new { UserName = MatchMeDB.Instance.GetUserName(a.Key), UserInfo = a.Value }).ToArray();
                    results = JsonConvert.SerializeObject(users).Replace("\"", "'");
                }
                else
                {
                    if (!string.IsNullOrEmpty(UserId) && MatchMeDB.Instance.Users.ContainsKey(UserId))
                    {
                        var user = MatchMeDB.Instance.Users.Where(a => a.Key.Equals(UserId)).Select(a => new { UserName = MatchMeDB.Instance.GetUserName(a.Key), UserInfo = a.Value });
                        results = JsonConvert.SerializeObject(user).Replace("\"", "'");
                    }
                    if (!string.IsNullOrEmpty(UserName) && MatchMeDB.Instance.UserTable.ContainsKey(UserName))
                    {
                        var user = MatchMeDB.Instance.UserTable.Where(a => a.Key.Equals(UserName)).Select(a => new { UserName = MatchMeDB.Instance.GetUserName(a.Key), UserInfo = a.Value.ToString() });
                        results = JsonConvert.SerializeObject(user).Replace("\"", "'");
                    }
                }
            }
            catch (Exception ex)
            {
                results = ex.Message;
            }
            var result = new Dictionary<string, string> {
                    {"results", results}
                };
            return JsonConvert.SerializeObject(result);
        }

        [RestMethod("GetPosition")]
        public string GetPosition(string UserName, string UserId)
        {
            string results = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(UserId))
                {
                    string adminUserId = MatchMeDB.Instance.GetUserId(Config.AdminUser);
                    var positions = MatchMeDB.Instance.Positions.Where(a => !a.Key.Equals(adminUserId)).Select(a => new { UserName = MatchMeDB.Instance.GetUserName(a.Key), Position = JsonConvert.SerializeObject(a.Value).Replace("\"", "'") }).ToArray();
                    results = JsonConvert.SerializeObject(positions).Replace("\"", "'");
                }
                else
                {
                    if (!string.IsNullOrEmpty(UserId) && MatchMeDB.Instance.Users.ContainsKey(UserId))
                    {
                        var position = MatchMeDB.Instance.Positions.Where(a => a.Key.Equals(UserId)).Select(a => new { UserName = MatchMeDB.Instance.GetUserName(a.Key), Position = JsonConvert.SerializeObject(a.Value).Replace("\"", "'") }).ToArray();
                        results = JsonConvert.SerializeObject(position).Replace("\"", "'");
                    }
                    if (!string.IsNullOrEmpty(UserName) && MatchMeDB.Instance.UserTable.ContainsKey(UserName))
                    {
                        string userId = MatchMeDB.Instance.GetUserId(UserName);
                        var position = MatchMeDB.Instance.Positions.Where(a => a.Key.Equals(userId)).Select(a => new { UserName = UserName, Position = JsonConvert.SerializeObject(a.Value).Replace("\"", "'") }).ToArray();
                        results = JsonConvert.SerializeObject(position).Replace("\"", "'");
                    }
                }
            }
            catch (Exception ex)
            {
                results = ex.Message;
            }
            var result = new Dictionary<string, string> {
                    {"results", results}
                };
            return JsonConvert.SerializeObject(result);
        }

        [RestMethod("GetLiveOrders")]
        public string GetLiveOrders(string Symbol)
        {
            Dictionary<string, string> results = new Dictionary<string,string>();
            List<string> SymbolList = new List<string>();
            if (string.IsNullOrEmpty(Symbol))
            {
                foreach(string s in Exchange.Instance.Sells.Keys)
                {
                    if (!SymbolList.Contains(s))
                        SymbolList.Add(s);
                }
                foreach(string s in Exchange.Instance.Buys.Keys)
                {
                    if (!SymbolList.Contains(s))
                        SymbolList.Add(s);
                }
            }
            else
                SymbolList.Add(Symbol);
            try
            {
                foreach(string symbol in SymbolList)
                {
                    var rtn = new Dictionary<string, string>();
                    if (Exchange.Instance.Buys.ContainsKey(symbol))
                    {
                        rtn.Add("Buy", string.Empty);
                        rtn["Buy"] = JsonConvert.SerializeObject(Exchange.Instance.Buys[symbol].Select(a => new { Created = a.Created.ToString("G"), Value = string.Format("Offer: {0}, Volume: {1} ", Math.Abs(a.Offer), a.Volume) })).Replace("\"", "'"); 
                    }
                    if (Exchange.Instance.Sells.ContainsKey(symbol))
                    {
                        rtn.Add("Sell", string.Empty);
                        rtn["Sell"] = JsonConvert.SerializeObject(Exchange.Instance.Sells[symbol].Select(a => new { Created = a.Created.ToString("G"), Value = string.Format("Offer: {0}, Volume: {1} ", Math.Abs(a.Offer), a.Volume) })).Replace("\"", "'");
                    }
                    results.Add(symbol, JsonConvert.SerializeObject(rtn).Replace("\"", "'"));
                }
            }
            catch (Exception ex)
            {
                results.Add("Error", ex.Message);
            }
            return JsonConvert.SerializeObject(results);
        }

        [RestMethod("GetPlacedOrders")]
        public string GetPlacedOrders(string UserName, string UserId)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            List<string> UserList = new List<string>();
            if (!string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(UserId))
            {
                UserId = MatchMeDB.Instance.UserTable[UserName].Id;
            }
            if (string.IsNullOrEmpty(UserId))
            {
                foreach (string s in MatchMeDB.Instance.OrderTable.Keys)
                {
                    if (!UserList.Contains(s))
                        UserList.Add(s);
                }
            }
            else
                UserList.Add(UserId);
            try
            {
                foreach (string userId in UserList)
                {
                    string userName = MatchMeDB.Instance.GetUserName(userId);
                    if (!userName.Equals(Config.AdminUser))
                        results.Add(userName, JsonConvert.SerializeObject(MatchMeDB.Instance.OrderTable[userId].Select(a => a.ToString()).ToList()).Replace("\"", "'"));
                }
            }
            catch (Exception ex)
            {
                results.Add("Error", ex.Message);
            }
            return JsonConvert.SerializeObject(results);
        }

        [RestMethod("GetMarketOrder")]
        public string GetMarketOrder()
        {
            string userName = Config.AdminUser;
            string userId = MatchMeDB.Instance.GetUserId(userName);
            var results = new Dictionary<string, string>();
            if (MatchMeDB.Instance.OrderTable.ContainsKey(userId))
                results.Add(userName, JsonConvert.SerializeObject(MatchMeDB.Instance.OrderTable[userId].Select(a => a.ToString()).ToList()).Replace("\"", "'"));
            return JsonConvert.SerializeObject(results);
        }
    }
}
