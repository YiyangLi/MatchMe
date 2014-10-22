using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    /// <summary>
    /// The Interface could be used by either the REST API or unit tests
    /// </summary>
    public interface IActionProcessor
    {
        string Execute(string command, Dictionary<string, string> args, string jsonInput, string contentType, string source, string agent, out string outRedirect, out string outContentType, out int httpStatus);
        string AddUser(string UserName, string Balance, string jsonInput);
        string AdjustBalance(string UserName, string UserId, string AdjustAmount);
        string PlaceAnOrder(string UserName, string UserId, string Symbol, string Volume, string Price, string OrderType, string OrderSide);
    }
}
