using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public interface IDatabaseReader
    {
        string Execute(string command, Dictionary<string, string> args, string jsonInput, string contentType, string source, string agent, out string outRedirect, out string outContentType, out int httpStatus);
        string GetUserInfo(string UserName, string UserId);
        string GetLiveOrders(string Symbol);
        string GetPlacedOrders(string UserName, string UserId);
        string GetMarketOrder();
    }
}
