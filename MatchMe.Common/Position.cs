using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    [Serializable]
    public class Position : MatchMeObject
    {
        public string UserId
        {
            get;
            set;
        }
        public string Symbol
        {
            get;
            set;
        }
        public double Quantity
        {
            get;
            set;
        }
        public Position()
        {
            UserId = string.Empty;
            Symbol = string.Empty;
            Quantity = 0.0;
            SetId();
        }

        public Position(string symbol, string userId)
            :this()
        {
            Symbol = symbol;
            UserId = userId;
        }
    }
}
