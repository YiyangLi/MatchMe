using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

using MatchMe.Common;
using MatchMe.TradeServer;

namespace MatchMe.Server
{
    public class ActionProcessor : IActionProcessor
    {
        private RestMethodBinder binder;
        private string server;


        public ActionProcessor()
        {
            binder = new RestMethodBinder(typeof(ActionProcessor));
            server = System.Environment.MachineName;
        }

        public string Execute(string command, Dictionary<string, string> args, string jsonInput, string contentType, string source, string agent, out string outRedirect, out string outContentType, out int httpStatus)
        {
            args.Add("contentType", contentType);
            args.Add("source", source);
            args.Add("agent", agent);
            return binder.CallRestMethod(this, command, args, jsonInput, out outRedirect, out outContentType, out httpStatus);
        }

        /// <summary>
        /// Used to add a new user, the default balance is 0.0
        /// Example: http://localhost:86/action/AddUser?UserName=Zhe@gmail.com&Balance=1000.00
        /// </summary>
        /// <param name="UserName"></param>
        /// <param name="Balance"></param>
        /// <param name="jsonInput"></param>
        /// <returns></returns>
        [RestMethod("AddUser")]
        public string AddUser(string UserName, string Balance, string jsonInput)
        {
            User user = null;
            string message = string.Empty;
            enumStatus status = enumStatus.Unknown;
            double balance = 0.0;
            try
            {
                if (!double.TryParse(Balance, out balance))
                {
                    message = string.Format("Error: Invalid Balance: {0}", Balance);
                }
                if (!string.IsNullOrEmpty(jsonInput))
                {
                    user = new User(jsonInput);
                }
                else
                {
                    user = new User(UserName, balance);
                }

                if (MatchMeDB.Instance.UserTable.ContainsKey(user.UserName) || MatchMeDB.Instance.Users.ContainsKey(user.Id))
                {
                    string existingUser = MatchMeDB.Instance.UserTable[user.UserName].ToString();
                    message = string.Format("Error: duplicate user. Existing User: {0} New User (rejected): {1}", existingUser, user.ToString());
                }
                if (string.IsNullOrEmpty(message))
                {
                    status = MatchMeDB.Instance.AddUser(user);
                    if (status.Equals(enumStatus.UserAdded))
                    {
                        message = string.Format("User Added: {0}", user.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Add User: {0}", user.ToString()));
                message = ex.Message;
                status = enumStatus.UserAddFailed;
            }
            var result = new Dictionary<string, string> {
                    {"status", status.ToString()},
                    {"message", message}
                };
            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Used to adjust the balance, you could either pass the UserId or the UserName to adjust (either increase or decrease) the balance
        /// Example: http://localhost:86/action/AdjustBalance?UserName=Jing@gmail.com&AdjustAmount=1000.00
        ///         http://localhost:86/action/AdjustBalance?UserId=4bd59688bbdf4056b0df6283b27b2c84&AdjustAmount=1000.00
        /// </summary>
        /// <param name="UserName"></param>
        /// <param name="userId"></param>
        /// <param name="AdjustAmount"></param>
        /// <returns></returns>
        [RestMethod("AdjustBalance")]
        public string AdjustBalance(string UserName, string UserId, string AdjustAmount)
        {
            User user = null;
            string message = string.Empty;
            enumStatus status = enumStatus.Unknown;
            double adjustAmount;
            try
            {
                if (!double.TryParse(AdjustAmount, out adjustAmount))
                {
                    message = string.Format("Error: Invalid adjustment: {0}", adjustAmount);
                    status = enumStatus.BalanceAdjustFailed;
                }
                if (!string.IsNullOrEmpty(UserName))
                {
                    MatchMeDB.Instance.UserTable.TryGetValue(UserName, out user);
                }
                if (user == null && !string.IsNullOrEmpty(UserId))
                {
                    string userDoc = string.Empty;
                    MatchMeDB.Instance.Users.TryGetValue(UserId, out userDoc);
                    if (userDoc != null)
                        user = new User(userDoc);
                }
                if (user == null)
                {
                    message = string.Format("Error: Can't retrieve the User from the db via the keys. UserName{0}, UserId{1}", UserName, UserId);
                    status = enumStatus.BalanceAdjustFailed;
                }
                else
                {
                    //We found the User in DB, so we update it!
                    user.Balance += adjustAmount;
                    if (user.Balance < 0)
                    {
                        message = string.Format("Error: Balance less than 0. UserName{0}, UserId{1}", UserName, user.Balance);
                        status = enumStatus.BalanceAdjustFailed;
                    }
                    status = MatchMeDB.Instance.UpdateUser(user);
                    if (status.Equals(enumStatus.BalanceAdjusted))
                        message = string.Format("Balance Adjusted. {0}", user.ToString());
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Add User: {0}", user.ToString()));
                message = ex.Message;
                status = enumStatus.BalanceAdjustFailed;
            }
            var result = new Dictionary<string, string> {
                    {"status", status.ToString()},
                    {"message", message}
                };
            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Company here is assumed to be the position for a special user (hard code as Market(or whatever you want to name in config setting))
        /// We don't need to check buying power for a buy on AdminUser (Market)
        /// Example: http://localhost:86/action/AddCompany?Symbol=AAPL&Volume=100&Price=500.00
        /// </summary>
        /// <param name="Symbol">Name of the Company</param>
        /// <param name="Volume">Securities quantity of the company</param>
        /// <param name="Price">Optional, default $1.00</param>
        /// <returns></returns>
        [RestMethod("AddCompany")]
        public string AddCompany(string Symbol, string Volume, string Price)
        {
            if (!MatchMeDB.Instance.UserTable.ContainsKey(Config.AdminUser))
            {
                User user = new User(Config.AdminUser, 0.0);
                MatchMeDB.Instance.AddUser(user);
            }
            string message = string.Empty;
            double price = Config.DefaultPrice;
            int volume = Config.DefaultVolume;
            string userId = MatchMeDB.Instance.GetUserId(Config.AdminUser);
            var status = enumStatus.Unknown;
            enumOrderType ordertype;
            enumSide orderside;
            try
            {
                Symbol = Symbol.ToUpper();
                if (string.IsNullOrEmpty(Price) || !double.TryParse(Price, out price))
                {
                    ServerLog.LogInfo(message);
                }
                if (string.IsNullOrEmpty(message)  && (string.IsNullOrEmpty(Volume) || !int.TryParse(Volume, out volume)))
                {
                    ServerLog.LogInfo(message);
                }
                List<Order> companies = MatchMeDB.Instance.OrderTable.ContainsKey(userId) ? MatchMeDB.Instance.OrderTable[userId] : new List<Order>();
                foreach (Order company in companies)
                {
                    if (company.Symbol.Equals(Symbol))
                    {
                        message = string.Format("Error: duplicate company. Existing User: {0} New Company Symbol (rejected): {1}", company, Symbol);
                        status = enumStatus.CompanyAddFailed;
                    }
                }
                ordertype = enumOrderType.Market;
                orderside = enumSide.Sell;
                price = -Math.Abs(price);
                Order newOrder = new Order(Symbol, volume, price, userId, ordertype, orderside);
                if (string.IsNullOrEmpty(message) 
                    && Exchange.Instance.PlaceSell(newOrder, true).Equals(enumStatus.OrderPlaced))
                {
                    Position position = MatchMeDB.Instance.GetPosition(userId, Symbol);
                    position.Quantity = volume;
                    MatchMeDB.Instance.UpdatePosition(position);
                    message = string.Format("Company Created: {0}, Volume: {1}, Price: {2}", newOrder.Symbol, newOrder.Volume, newOrder.Price);
                    status = enumStatus.CompanyAdded;
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Add Company, Symbol {0}, Volume {1}, Price", Symbol, Volume, price.ToString()));
                message = ex.Message;
                status = enumStatus.CompanyAddFailed;
            }
            var result = new Dictionary<string, string> {
                    {"status", status.ToString()},
                    {"message", message}
                };
            return JsonConvert.SerializeObject(result);
        }

        /// <summary>
        /// Used to place an order, you could either pass the UserId or the UserName to place an order
        /// Example: http://localhost:86/action/PlaceAnOrder?UserName=Jing&Symbol=AAPL&Volume=5&OrderType=1&OrderSide=1
        /// </summary>
        /// <param name="UserName"></param>
        /// <param name="userId"></param>
        /// <param name="Symbol"></param>
        /// <param name="Volume"></param>
        /// <param name="Price"></param>
        /// <param name="OrderType"></param>
        /// <param name="OrderSide"></param>
        /// 
        /// <returns></returns>
        [RestMethod("PlaceAnOrder")]
        public string PlaceAnOrder(string UserName, string UserId, string Symbol, string Volume, string Price, string OrderType, string OrderSide)
        {
            User user = null;
            string message = string.Empty;
            string userId = MatchMeDB.Instance.UserTable[Config.AdminUser].Id;
            enumStatus status = enumStatus.Unknown;
            enumSide orderside = enumSide.Invalid;
            enumOrderType ordertype = enumOrderType.Invalid;
            double price = 0.0;
            int volume = 0;
            bool exist = false;
            double requiredbalance = 0.0;
            double remainbalance = 0.0; 
            string symbol = Symbol.ToUpper();
            try
            {
                Symbol = Symbol.ToUpper();
                if (!int.TryParse(Volume, out volume))
                {
                    message = string.Format("Error: Invalid Volume: {0}", volume);
                    status = enumStatus.OrderPlaceError;
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && !string.IsNullOrEmpty(symbol))
                {
                    List<Order> companies = MatchMeDB.Instance.OrderTable.ContainsKey(userId) ? MatchMeDB.Instance.OrderTable[userId] : new List<Order>();
                    exist = companies.Where(a => a.Symbol.Equals(symbol)).Count() > 0;
                    if (!exist)
                    {
                        message = string.Format("Error: The Company does not exist {0}", symbol);
                        status = enumStatus.OrderPlaceError;
                    }
                }
                else
                {
                    message = string.Format("Error: The Company does not exist {0}", symbol);
                    status = enumStatus.OrderPlaceError;
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && !Enum.TryParse(OrderSide, true, out orderside))
                {
                    message = string.Format("Error: Invalid Order Side: {0}", OrderSide);
                    status = enumStatus.OrderPlaceError;
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && !Enum.TryParse(OrderType, true, out ordertype))
                {
                    message = string.Format("Error: Invalid Order Type: {0}", OrderType);
                    status = enumStatus.OrderPlaceError;
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && ordertype.Equals(enumOrderType.Market))
                {
                    price = Exchange.Instance.GetMarketPrice(orderside, symbol, volume);
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && ordertype.Equals(enumOrderType.Limit))
                {
                    if (!double.TryParse(Price, out price))
                    {
                        message = string.Format("Error: Invalid Price: {0}", Price);
                        status = enumStatus.OrderPlaceError;
                    }
                    if (!status.Equals(enumStatus.OrderPlaceError) && orderside.Equals(enumSide.Sell))
                    {
                        price = (-1) * Math.Abs(price);
                    }
                }

                if (!status.Equals(enumStatus.OrderPlaceError) && !string.IsNullOrEmpty(UserName))
                {
                    MatchMeDB.Instance.UserTable.TryGetValue(UserName, out user);
                    if (user == null)
                    {
                        message = string.Format("Error: Can't retrieve the User from the db via the keys. UserName{0}, UserId{1}", UserName, UserId);
                        status = enumStatus.OrderPlaceError;
                    }
                }
                if (!status.Equals(enumStatus.OrderPlaceError) && string.IsNullOrEmpty(message) && exist)
                {
                    if (price > 0)
                    {
                        //price larger than zero means that it's a buy limit order or an order with valid market price 
                        requiredbalance = price * volume + Config.Commission;
                    }
                    if (requiredbalance > user.Balance)
                    {
                        //check if balance is enough
                        remainbalance = requiredbalance - user.Balance;
                        message = string.Format("Error: No Enough Balance: Remain balance {0}, Requires {1} to place the order", user.Balance, remainbalance);
                        status = enumStatus.Rejected;
                    }
                    else
                    {
                        Order newOrder = new Order(symbol, volume, price, user.Id, ordertype, orderside);
                        if (orderside.Equals(enumSide.Sell))
                        {
                            status = Exchange.Instance.PlaceSell(newOrder);
                            if (status.Equals(enumStatus.Rejected))
                            {
                                message = string.Format("Error: Short Sell is not allowed. {0}", newOrder);
                            }
                        }
                        else if (orderside.Equals(enumSide.Buy))
                            status = Exchange.Instance.PlaceBuy(newOrder);
                        if (!status.Equals(enumStatus.OrderPlaceError) && string.IsNullOrEmpty(message))
                        {
                            message = "New Order Placed:" + MatchMeDB.Instance.Orders[newOrder.Id];
                            status = enumStatus.OrderPlaced;
                        }
                        
                    }
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Place Order Failed, User {0}, Symbol {1}", user.ToString(), Symbol));
                message = ex.Message;
                status = enumStatus.OrderPlaceError;
            }
            var result = new Dictionary<string, string> { { "status", status.ToString() }, { "message", message } };
            return JsonConvert.SerializeObject(result);
        }
    }
}



