using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

using MatchMe.Common;

namespace MatchMe.TradeServer
{
    /// <summary>
    /// Exchange is used to match order and generate execution reports
    /// </summary>
    public class Exchange
    {
        private Dictionary<string, List<ExchangeOrder>> buys;
        public Dictionary<string, List<ExchangeOrder>> Buys
        {
            get
            {
                return buys;
            }
        }

        private Dictionary<string, List<ExchangeOrder>> sells;
        public Dictionary<string, List<ExchangeOrder>> Sells
        {
            get
            {
                return sells;
            }
        }

        private Exchange()
        {
            buys = new Dictionary<string, List<ExchangeOrder>>();
            sells = new Dictionary<string, List<ExchangeOrder>>();
        }

        private static Exchange instance = null;
        private static object syncRoot = new object();
        public static Exchange Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                        {
                            instance = new Exchange();
                        }
                    }
                }
                return instance;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        /// <param name="AdminMode">PlaceSell supports AdminMode to add company</param>
        public enumStatus PlaceSell(Order order, bool AdminMode = false)
        {
            if (!AdminMode && !CanSell(order))
            {
                ServerLog.LogError("Exchange: ShortSell is not allowed");
                return enumStatus.Rejected;
            }
            if (MatchMeDB.Instance.AddOrder(order).Equals(enumStatus.OrderPlaced))
            {
                order.OrderStatus = enumStatus.PendingNew;
                MatchMeDB.Instance.UpdateOrder(order);
            }
            if (!order.OrderSide.Equals(enumSide.Sell))
            {
                ServerLog.LogError("Exchange: This is for the sell Orders, don't insert Others to here");
                return enumStatus.OrderPlaceError;
            }
            ExchangeOrder newExOrder = new ExchangeOrder(order.Volume, order.Price, order.Id, order.Created);
            List<ExchangeOrder> matchedBuy = new List<ExchangeOrder>();
            MatchSell(order.Symbol, newExOrder, out matchedBuy);
            Order orderInDB = new Order(MatchMeDB.Instance.Orders[order.Id]);
            int matchedQuantity = matchedBuy != null && matchedBuy.Count > 0 ? matchedBuy.Sum(a => a.Volume) : 0;
            if (newExOrder.Volume.Equals(matchedQuantity))
            {
                orderInDB.OrderStatus = enumStatus.Filled;
            }
            else
            {
                orderInDB.OrderStatus = matchedQuantity > 0 ? enumStatus.PartiallyFilled : enumStatus.New;
                if (!Sells.ContainsKey(order.Symbol))
                {
                    Sells[order.Symbol] = new List<ExchangeOrder>();
                }
                Sells[order.Symbol].Add(newExOrder);
            }
            MatchMeDB.Instance.UpdateOrder(orderInDB);
            if (matchedQuantity > 0)
            {
                //Update balance and position;
                User user = new User(MatchMeDB.Instance.Users[orderInDB.UserID]);
                user.Balance -= newExOrder.Volume.Equals(matchedQuantity) ? matchedQuantity * order.Price + Config.Commission
                                        : matchedQuantity * order.Price;
                MatchMeDB.Instance.UpdateUser(user);

                Position position = MatchMeDB.Instance.GetPosition(orderInDB.UserID, orderInDB.Symbol);
                position.Quantity -= matchedQuantity;
                MatchMeDB.Instance.UpdatePosition(position);
            }

            foreach (ExchangeOrder matched in matchedBuy)
            {
                Order matchedInDB = new Order(MatchMeDB.Instance.Orders[matched.OrderId]);
                if (matchedInDB.Volume.Equals(matched.Volume))
                {
                    matchedInDB.OrderStatus = enumStatus.Filled;
                    int index = buys[order.Symbol].Select(a => a.Id).ToList().IndexOf(matched.Id);
                    buys[order.Symbol].RemoveAt(index);
                }
                else
                {
                    int index = buys[order.Symbol].Select(a => a.Id).ToList().IndexOf(matched.Id);
                    buys[order.Symbol][index].Volume -= matched.Volume;
                    matchedInDB.OrderStatus = enumStatus.PartiallyFilled;
                    matchedInDB.Volume -= matched.Volume;
                }
                User user = new User(MatchMeDB.Instance.Users[matchedInDB.UserID]);
                user.Balance -= user.UserName.Equals(Config.AdminUser) ? 0 :
                                    matchedInDB.Volume.Equals(matched.Volume) ? (matched.Volume * matched.Offer + Config.Commission) : matched.Volume * matched.Offer;
                MatchMeDB.Instance.UpdateUser(user);
                MatchMeDB.Instance.UpdateOrder(matchedInDB);

                Position position = MatchMeDB.Instance.GetPosition(matchedInDB.UserID, matchedInDB.Symbol);
                position.Quantity += matched.Volume;
                MatchMeDB.Instance.UpdatePosition(position);
            }
            return enumStatus.OrderPlaced;

            //****************Jing Mai**********
            //if (!AdminMode)
            //{
            //    sells[order.Symbol].OrderBy(x => x.Offer).ThenBy(x => x.Created);
            //    order.OrderStatus = enumStatus.PendingNew;
            //    for (int vol = 1; vol <= order.Volume; vol++)
            //    {
            //        string MatcherOrderId = MatchMe(eo, order);
            //        if (!string.IsNullOrEmpty(MatcherOrderId))
            //            FillOrders(order.Id, MatcherOrderId);
            //    }
            //}
        }
        /// <summary>
        /// Used to validate if the order can be sold, since short sell doesn't allowed
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private bool CanSell(Order order)
        {
            Position pos = MatchMeDB.Instance.GetPosition(order.UserID, order.Symbol);
            return pos.Quantity >= order.Volume;
        }

        private void MatchSell(string symbol, ExchangeOrder exchangeOrder, out List<ExchangeOrder> matchedBuy)
        {
            matchedBuy = new List<ExchangeOrder>();
            List<ExchangeOrder> clonedBuy = buys.ContainsKey(symbol) ? new List<ExchangeOrder>(
                                                    buys[symbol].Where(a => exchangeOrder.Offer >= -a.Offer)
                                                        .OrderByDescending(a => a.Offer).OrderByDescending(a => a.Created).Select(a => (ExchangeOrder)a.Clone())
                                                        ) : new List<ExchangeOrder>();
            int index = 0, remaining = exchangeOrder.Volume;
            while (index < clonedBuy.Count && remaining > 0)
            {
                matchedBuy.Add(clonedBuy[index]);
                if (BuyingPowerCheck(clonedBuy[index], exchangeOrder.Offer))
                    remaining -= clonedBuy[index].Volume;
                else
                    matchedBuy[index].Volume = 0;
                if (remaining < 0)
                {
                    matchedBuy[index].Volume += remaining;
                    remaining = 0;
                }
                index++;
            }
        }

        private bool BuyingPowerCheck(ExchangeOrder exchangeOrder, double price)
        {
            Order order = new Order(MatchMeDB.Instance.Orders[exchangeOrder.OrderId]);
            double requiredAmount = exchangeOrder.Volume * price;
            string UserName = MatchMeDB.Instance.GetUserName(order.UserID);
            return UserName.Equals(Config.AdminUser) || MatchMeDB.Instance.UserTable[UserName].Balance > requiredAmount;
        }

        /// <summary>
        /// Place a buy order 
        /// </summary>
        /// <param name="order"></param>
        public enumStatus PlaceBuy(Order order)
        {
            if (MatchMeDB.Instance.AddOrder(order).Equals(enumStatus.OrderPlaced))
            {
                order.OrderStatus = enumStatus.PendingNew;
                MatchMeDB.Instance.UpdateOrder(order);
            }
            if (!order.OrderSide.Equals(enumSide.Buy))
            {
                ServerLog.LogError("Exchange: This is for the buy Orders, don't insert Others to here");
                throw new InvalidOperationException("Exchange: This is for the Buy Orders, don't insert Others to here");
            }
            ExchangeOrder newExOrder = new ExchangeOrder(order.Volume, order.Price, order.Id, order.Created);
            List<ExchangeOrder> matchedSell = new List<ExchangeOrder>();
            MatchBuy(order.Symbol, newExOrder, out matchedSell);
            Order orderInDB = new Order(MatchMeDB.Instance.Orders[order.Id]);
            int matchedQuantity = matchedSell !=null && matchedSell.Count > 0 ? matchedSell.Sum(a => a.Volume) : 0;
            if (newExOrder.Volume.Equals(matchedQuantity))
            {
                orderInDB.OrderStatus = enumStatus.Filled;
            }
            else
            {
                orderInDB.OrderStatus = matchedQuantity > 0 ? enumStatus.PartiallyFilled : enumStatus.New;
                if (!Buys.ContainsKey(order.Symbol))
                {
                    Buys[order.Symbol] = new List<ExchangeOrder>();
                }
                Buys[order.Symbol].Add(newExOrder);
            }
            MatchMeDB.Instance.UpdateOrder(orderInDB);
            if (matchedQuantity > 0)
            {
                //Update Balance and balance;
                User user = new User(MatchMeDB.Instance.Users[orderInDB.UserID]);
                user.Balance -= newExOrder.Volume.Equals(matchedQuantity) ? matchedQuantity * order.Price + Config.Commission
                                        : matchedQuantity * order.Price;
                MatchMeDB.Instance.UpdateUser(user);

                Position position = MatchMeDB.Instance.GetPosition(orderInDB.UserID, orderInDB.Symbol);
                position.Quantity += matchedQuantity;
                MatchMeDB.Instance.UpdatePosition(position);
            }

            foreach (ExchangeOrder matched in matchedSell)
            {
                Order matchedInDB = new Order(MatchMeDB.Instance.Orders[matched.OrderId]);
                if (matchedInDB.Volume.Equals(matched.Volume))
                {
                    matchedInDB.OrderStatus = enumStatus.Filled;
                    int index = sells[order.Symbol].Select(a => a.Id).ToList().IndexOf(matched.Id);
                    sells[order.Symbol].RemoveAt(index);
                    
                }
                else
                {
                    int index = sells[order.Symbol].Select(a => a.Id).ToList().IndexOf(matched.Id);
                    sells[order.Symbol][index].Volume -= matched.Volume;
                    matchedInDB.OrderStatus = enumStatus.PartiallyFilled;
                    matchedInDB.Volume -= matched.Volume;
                }
                User user = new User(MatchMeDB.Instance.Users[matchedInDB.UserID]);
                user.Balance -= user.UserName.Equals(Config.AdminUser) ? 0 :
                                    matchedInDB.Volume.Equals(matched.Volume) ? (matched.Volume * matched.Offer + Config.Commission) : matched.Volume * matched.Offer;
                MatchMeDB.Instance.UpdateUser(user);
                MatchMeDB.Instance.UpdateOrder(matchedInDB);

                Position position = MatchMeDB.Instance.GetPosition(matchedInDB.UserID, matchedInDB.Symbol);
                position.Quantity -= matched.Volume;
                MatchMeDB.Instance.UpdatePosition(position);
            }
            return enumStatus.OrderPlaced;
            //************Jing Mai ************
            //if (!buys.ContainsKey(order.Symbol))
            //{
            //    eo = new ExchangeOrder(1, order.Price, order.Id, order.Created);
            //    List<ExchangeOrder> orderList = new List<ExchangeOrder>();
            //    orderList.Add(eo);
            //    buys.Add(order.Symbol, orderList);
            //    for (int vol = 2; vol <= order.Volume; vol++)
            //    {
            //        buys[order.Symbol].Add(eo);
            //    }
            //}
            //else
            //{
            //    eo = new ExchangeOrder(1, order.Price, order.Id, order.Created);
            //    for (int vol = 1; vol <= order.Volume; vol++)
            //    {
            //        buys[order.Symbol].Add(eo);
            //    }
            //}
            //buys[order.Symbol].OrderBy(x => x.Offer).ThenBy(x => x.Created);
            //order.OrderStatus = enumStatus.PendingNew;
            //for (int vol = 1; vol <= order.Volume; vol++)
            //{
            //    string MatcherOrderId = MatchMe(eo, order);
            //    if (!string.IsNullOrEmpty(MatcherOrderId))
            //        FillOrders(order.Id, MatcherOrderId);
            //}
            //***********************************
            
        }
        private void MatchBuy(string symbol, ExchangeOrder exchangeOrder, out List<ExchangeOrder> matchedSell)
        {
            matchedSell = new List<ExchangeOrder>();
            List<ExchangeOrder> clonedSell = sells.ContainsKey(symbol) ? new List<ExchangeOrder>(
                                                    sells[symbol].Where(a => exchangeOrder.Offer >= -a.Offer)
                                                        .OrderByDescending(a => a.Offer).OrderByDescending(a => a.Created).Select(a => (ExchangeOrder)a.Clone())
                                                        ) : new List<ExchangeOrder>();
            int index = 0, remaining = exchangeOrder.Volume;
            while (index < clonedSell.Count && remaining > 0)
            {
                matchedSell.Add(clonedSell[index]);
                if (BuyingPowerCheck(clonedSell[index], exchangeOrder.Offer))
                    remaining -= clonedSell[index].Volume;
                else
                    matchedSell[index].Volume = 0;
                if (remaining < 0)
                {
                    matchedSell[index].Volume += remaining;
                    remaining = 0;
                }
                index++;
            }
        }


        
        /// <summary>
        /// Calculate the VWAP price
        /// </summary>
        /// <returns></returns>
        private double GetVWAP(string Symbol, enumOrderType orderType, enumSide side)
        {
            double VWAP = 0;
            if (orderType.Equals(enumOrderType.Market))
                VWAP = Convert.ToDouble(Int16.MinValue); // For a market type, return a -32768
            else
            {
                List<ExchangeOrder> liveOrders = null;
                int qty = 0;
                if (side.Equals(enumSide.Buy) && Sells.ContainsKey(Symbol))
                    liveOrders = Sells[Symbol];
                if (side.Equals(enumSide.Sell) && Buys.ContainsKey(Symbol))
                    liveOrders = Buys[Symbol];
                if (liveOrders == null)
                    VWAP = Convert.ToDouble(Int16.MaxValue);
                foreach (ExchangeOrder eo in liveOrders)
                {
                    VWAP += eo.Offer * eo.Volume;
                    qty += eo.Volume;
                }
                if (!qty.Equals(0.0))
                    VWAP /= qty;
                else
                    throw new DivideByZeroException(string.Format("Quantity becomes ZERO in GetVWAP, how come! Sybmol {0}, Side {1}", Symbol, side));
            }
            return VWAP;
        }

        //private string MatchMe(ExchangeOrder eo, Order order)
        //{
        //    //return string.Empty;
        //    List<ExchangeOrder> liveOrders = null;
        //    string OrderId = string.Empty;
        //    if (order.OrderSide.Equals(enumSide.Buy) && Sells.ContainsKey(order.Symbol))
        //        liveOrders = sells[order.Symbol];
        //    if (order.OrderSide.Equals(enumSide.Sell) && Buys.ContainsKey(order.Symbol))
        //        liveOrders = buys[order.Symbol];
        //    if (order.OrderType == enumOrderType.Market)
        //    {
        //        foreach (ExchangeOrder target in liveOrders)
        //        {
        //            OrderId = target.Id;
        //            break;
        //        }
        //    }
        //    else
        //    {
        //        foreach (ExchangeOrder target in liveOrders)
        //        {
        //            if (order.OrderSide.Equals(enumSide.Buy) && target.Offer >= eo.Offer)
        //            {
        //                OrderId = target.Id; break;
        //            }
        //            else if (order.OrderSide.Equals(enumSide.Sell) && target.Offer <= eo.Offer)
        //            {
        //                OrderId = target.Id; break;
        //            }
        //        }
        //    }
        //    return OrderId;
        //}

        public double GetMarketPrice(enumSide side, string Symbol, int volume)
        {
            double marketPrice = side.Equals(enumSide.Buy) ? 65535.0 : 0.0;
            if (side.Equals(enumSide.Buy) && sells.ContainsKey(Symbol))
                marketPrice = sells[Symbol].OrderByDescending(a => a.Offer).FirstOrDefault().Offer;
            if (side.Equals(enumSide.Sell) && buys.ContainsKey(Symbol))
                marketPrice = buys[Symbol].OrderBy(a => a.Offer).FirstOrDefault().Offer;
            if (side.Equals(enumSide.Buy))
                marketPrice = Math.Abs(marketPrice);
            if (side.Equals(enumSide.Sell))
                marketPrice = -Math.Abs(marketPrice);
            return marketPrice;
        }

        //private bool FillOrders(string MatcheeId, string MatcherId)
        //{
        //    //Update DB
        //    //Remove from Exchange
        //    //Send Emails to the both
        //    double offerPrice;
        //    Order matcheeOrder = null;
        //    Order matcherOrder = null;
        //    User matcheeUser = null;
        //    User matcherUser = null;
        //    User adminUser = null;
        //    List<ExchangeOrder> liveOrders = null;
        //    List<ExchangeOrder> MySideOrders = null;
        //    string userId = MatchMeDB.Instance.UserTable[Config.AdminUser].Id;
        //    ExchangeOrder matcher = liveOrders.First(x => x.OrderId == MatcherId);
        //    ExchangeOrder matchee = MySideOrders.First(x => x.OrderId == MatcheeId);
        //    List<List<Order>> orders = MatchMeDB.Instance.OrderTable.Values.ToList();
        //    foreach (List<Order> testorderlist in orders)
        //    {
        //        foreach (Order testorder in testorderlist)
        //        {
        //            if (testorder.Id == matchee.OrderId)
        //                matcheeOrder = testorder;
        //            if (testorder.Id == matcher.OrderId)
        //                matcherOrder = testorder;
        //        }
        //    }
        //    if (matcheeOrder.OrderSide.Equals(enumSide.Buy) && Sells.ContainsKey(matcheeOrder.Symbol))
        //    {
        //        liveOrders = sells[matcheeOrder.Symbol];
        //        MySideOrders = buys[matcheeOrder.Symbol];
        //        offerPrice = matchee.Offer;
        //    }
        //    else if (matcheeOrder.OrderSide.Equals(enumSide.Sell) && Buys.ContainsKey(matcheeOrder.Symbol))
        //    {
        //        liveOrders = buys[matcheeOrder.Symbol];
        //        MySideOrders = sells[matcheeOrder.Symbol];
        //        offerPrice = matchee.Offer * -1;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //    //Update DB
        //    //both party(user)
        //    if ( !string.IsNullOrEmpty(matcheeOrder.UserID))
        //    {
        //        string userDoc = string.Empty;
        //        MatchMeDB.Instance.Users.TryGetValue(matcheeOrder.UserID, out userDoc);
        //        if (userDoc != null)
        //            matcheeUser = new User(userDoc);
        //    }
        //    matcheeUser.Balance += offerPrice;
        //    MatchMeDB.Instance.UpdateUser(matcheeUser);
        //    if (!string.IsNullOrEmpty(matcherOrder.UserID))
        //    {
        //        string userDoc = string.Empty;
        //        MatchMeDB.Instance.Users.TryGetValue(matcherOrder.UserID, out userDoc);
        //        if (userDoc != null)
        //            matcherUser = new User(userDoc);
        //    }
        //    matcherUser.Balance += offerPrice;
        //    MatchMeDB.Instance.UpdateUser(matcherUser);
        //    //admin balance
        //    if (!string.IsNullOrEmpty(userId))
        //    {
        //        string userDoc = string.Empty;
        //        MatchMeDB.Instance.Users.TryGetValue(userId, out userDoc);
        //        if (userDoc != null)
        //            adminUser = new User(userDoc);
        //    }
        //    adminUser.Balance += Math.Abs(matchee.Offer - matcher.Offer);
        //    MatchMeDB.Instance.UpdateUser(adminUser);

        //    //Send to open position
        //    OpenPosition newOpenposition = new OpenPosition(offerPrice, 1);
        //    string matcherKey = matcherOrder.UserID + '_' + matcherOrder.Symbol;
        //    if (!MatchMeDB.Instance.OpenPositionTable.ContainsKey(matcherKey))
        //    {
        //        List<OpenPosition> newOpenpositionList = new List<OpenPosition>();
        //        newOpenpositionList.Add(newOpenposition);
        //        MatchMeDB.Instance.OpenPositionTable.Add(matcherKey, newOpenpositionList);
        //    }
        //    else
        //    {
        //        MatchMeDB.Instance.OpenPositionTable[matcherKey].Add(newOpenposition);
        //    }
        //    //Remove from Exchange
        //    liveOrders.Remove(matcher);
        //    MySideOrders.Remove(matchee);
        //    //both party(order status)
        //    if (liveOrders.Any(x => x.OrderId == matcher.OrderId) == false)
        //    {
        //        matcherOrder.OrderStatus = enumStatus.Filled;
        //    }
        //    if (MySideOrders.Any(x => x.OrderId == matchee.OrderId) == false)
        //        matcheeOrder.OrderStatus = enumStatus.Filled;
        //    else matcheeOrder.OrderStatus = enumStatus.PartiallyFilled;
        //    //Send email

        //    return true;
        //}
    }
}
