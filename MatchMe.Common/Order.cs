using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MatchMe.Common
{
    [Serializable]
    public class Order : MatchMeObject
    {
        public string UserID
        {
            get;
            set;
        }
        public string Symbol
        {
            get;
            set;
        }

        /// <summary>
        /// Well, if we consider the dividend, the volume of an order could be a decimal (double)
        /// </summary>
        public int Volume
        {
            get;
            set;
        }
        public double Price
        {
            get;
            set;
        }
        public enumOrderType OrderType
        {
            get;
            set;
        }
        public enumSide OrderSide
        {
            get;
            set;
        }

        public enumStatus OrderStatus
        {
            get;
            set;
        }

        public DateTime Created
        {
            get;
            set;
        }

        public Order()
        {
            OrderStatus = enumStatus.Initial;
            Created = DateTime.Now;
            SetId();
        }

        public Order(string symbol, int volume, double price, string userId, enumOrderType orderType, enumSide orderSide)
            : this()
        {
            Symbol = symbol;
            Volume = volume;
            if (orderSide.Equals(enumSide.Buy))
                Price = Math.Abs(price);
            if (orderSide.Equals(enumSide.Sell))
                Price = -Math.Abs(price);
            UserID = userId;
            OrderType = orderType;
            OrderSide = orderSide;
            OrderStatus = enumStatus.Initial;
            //OrderType = enumOrderType.Market;
            //OrderSide = enumSide.Buy;
        }
        public Order(string jsonDoc)
        {
            Order clonedOrder = JsonConvert.DeserializeObject<Order>(jsonDoc);
            Id = clonedOrder.Id;
            Symbol = clonedOrder.Symbol;
            Price = clonedOrder.Price;
            Volume = clonedOrder.Volume;
            UserID = clonedOrder.UserID;
            OrderType = clonedOrder.OrderType;
            OrderSide = clonedOrder.OrderSide;
            OrderStatus = clonedOrder.OrderStatus;
            Created = clonedOrder.Created;
        }

    }
}
