using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatchMe.Common;

namespace MatchMe.TradeServer
{
    public class ExchangeOrder : MatchMeObject
    {
        public string OrderId
        {
            get;
            set;
        }

        public int Volume
        {
            get;
            set;
        }

        /// <summary>
        /// Bid price for long, ask for short, Positive for buy, negative for short
        /// </summary>
        public double Offer
        {
            get;
            set;
        }

        public DateTime Created
        {
            get;
            set;
        }

        public ExchangeOrder(int volume, double offer, string orderId, DateTime created)
        {
            OrderId = orderId;
            Offer = offer;
            Volume = volume;
            Created = created;
            SetId();
        }
    }
}
