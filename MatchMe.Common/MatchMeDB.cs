using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public class MatchMeDB
    {
        /// <summary>
        /// Documents db, Key: Order.Id, Value: jsonDoc Order
        /// </summary>
        public Dictionary<string, string> Orders
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<string, List<Order>> OrderTable
        {
            get;
            private set;
        }

        /// <summary>
        /// Documents, Key: User.Id, Value: JSON version of User
        /// </summary>
        public Dictionary<string, string> Users
        {
            get;
            set;
        }

        /// <summary>
        /// Table, Key: UserName, Value: User
        /// </summary>
        public Dictionary<string, User> UserTable
        {
            get;
            set;
        }

        /// <summary>
        /// Table, Key: UserId, Value: Positions
        /// </summary>
        public Dictionary<string, List<Position>> Positions
        {
            get;
            set;
        }

        ///// <summary>
        ///// Key: UserId + '_' + Symbol, Value: List of the closed position. Yiyang: Change to documents instead of Queue
        ///// </summary>
        //public Dictionary<string, List<ClosePosition>> ClosedPositionTable
        //{
        //    get;
        //    set;
        //}

        ///// <summary>
        ///// Key: UserId + '_' + Symbol, Value: List of the open position. Yiyang: Change to documents instead of Queue
        ///// </summary>
        //public Dictionary<string, List<OpenPosition>> OpenPositionTable
        //{
        //    get;
        //    set;
        //}

        /// <summary>
        /// Add a valid (verified) user
        /// </summary>
        /// <param name="user"></param>
        public enumStatus AddUser(User user)
        {
            string jsonDoc = user.ToString();
            try
            {
                Users.Add(user.Id, jsonDoc);
                UserTable.Add(user.UserName, user);
                Positions.Add(user.Id, new List<Position>());
                return enumStatus.UserAdded;
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Add User: {0}", user.ToString()));
                return enumStatus.UserAddFailed;
            }
            //lock (syncRoot)
            //{
                
            //}
        }

        public enumStatus AddOrder(Order order)
        {
            string jsonDoc = order.ToString();
            try
            {
                Orders.Add(order.Id, jsonDoc);
                if (!OrderTable.ContainsKey(order.UserID))
                {
                    List<Order> orders = new List<Order>();
                    OrderTable[order.UserID] = orders;
                }
                OrderTable[order.UserID].Add(order);
                return enumStatus.OrderPlaced;
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Add Order: {0}", order.ToString()));
                return enumStatus.OrderPlaceError;
            }
            //lock (syncRoot)
            //{
                
            //}
        }

        public enumStatus UpdateOrder(Order order)
        {
            string jsonDoc = order.ToString();
            try
            {
                lock (syncRoot)
                {
                    if (!Orders.ContainsKey(order.Id))
                    {
                        ServerLog.LogError("Update Order, invalid Order Id: {0}", order.Id);
                        return enumStatus.Error;
                    }
                    if (!Users.ContainsKey(order.UserID) || !OrderTable.ContainsKey(order.UserID))
                    {
                        ServerLog.LogError("Update Order, invalid User Id {0} in the order: {1}", order.UserID, order);
                        return enumStatus.Error;
                    }
                    int i = OrderTable[order.UserID].Select(a => a.Id).ToList().IndexOf(order.Id);
                    if (i >= 0)
                    {
                        OrderTable[order.UserID][i] = order;
                    }
                    Orders[order.Id] = jsonDoc;
                }
                return enumStatus.Successful;
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Update Order: {0}", order.ToString()));
                return enumStatus.Error;
            }
        }

        /// <summary>
        /// Since balance is the only mutuable property in user, update User means update balance. 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public enumStatus UpdateUser(User user)
        {
            string jsonDoc = user.ToString();
            try
            {
                lock (syncRoot)
                {
                    if (!Users.ContainsKey(user.Id) || !UserTable.ContainsKey(user.UserName))
                        return enumStatus.Error;
                    Users[user.Id] = jsonDoc;
                    UserTable[user.UserName] = user;
                    return enumStatus.BalanceAdjusted;
                }
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Update User: {0}", jsonDoc));
                return enumStatus.BalanceAdjustFailed;
            }
        }

        

        /// <summary>
        /// Get UserId via UserName
        /// </summary>
        /// <param name="UserName"></param>
        /// <returns></returns>
        public string GetUserId(string UserName)
        {
            string UserId = string.Empty;
            if (UserTable.ContainsKey(UserName))
                UserId = UserTable[UserName].Id;
            return UserId;
        }

        /// <summary>
        /// Get UserName via UserId
        /// </summary>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public string GetUserName(string UserId)
        {
            string UserName = string.Empty;
            if (Users.ContainsKey(UserId))
            {
                User user = new User(Users[UserId]);
                UserName = user.UserName;
            }
            return UserName;
        }

        public Position GetPosition(string UserId, string Symbol)
        {
            Position rtnPos = null;
            lock (syncRoot)
            {
                if (Positions.ContainsKey(UserId))
                {
                    rtnPos = Positions[UserId].Where(a => a.Symbol.Equals(Symbol)).FirstOrDefault();
                }
                else
                {
                    Positions.Add(UserId, new List<Position>());
                }
            }
            if (rtnPos == null)
            {
                rtnPos = new Position(Symbol, UserId);
                Positions[UserId].Add(rtnPos);
            }
            return rtnPos;
        }

        public enumStatus UpdatePosition(Position pos)
        {
            try
            {
                lock (syncRoot)
                {
                    if (!Positions.ContainsKey(pos.UserId))
                    {
                        ServerLog.LogError("Error, can't locate the User in the positions table: {0}", pos.ToString());
                        return enumStatus.Successful;
                    }
                    int index = Positions[pos.UserId].IndexOf(Positions[pos.UserId].Where(p => p.Symbol.Equals(pos.Symbol)).FirstOrDefault());
                    if (pos.Quantity > 0)
                        Positions[pos.UserId][index] = pos;
                    if (pos.Quantity == 0)
                    {
                        Positions[pos.UserId].RemoveAt(index);
                    }
                    if (pos.Quantity < 0)
                    {
                        ServerLog.LogError("Error, the position can't be negative: {0}", pos.ToString());
                        return enumStatus.Error;
                    }
                }
                return enumStatus.Successful;
            }
            catch (Exception ex)
            {
                ServerLog.LogException(ex, string.Format("Update Position: {0}", pos.ToString()));
                return enumStatus.BalanceAdjustFailed;
            }
           
        }

        private MatchMeDB()
        { 
            Orders = new Dictionary<string, string>();
            Users = new Dictionary<string, string>();
            UserTable = new Dictionary<string,User>();
            Positions = new Dictionary<string, List<Position>>();
            OrderTable = new Dictionary<string, List<Order>>();
        }

        private static MatchMeDB instance = null;
        private static object syncRoot = new object();
        public static MatchMeDB Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new MatchMeDB();
                        }
                    }
                }
                return instance;
            }
        }
    }

}
