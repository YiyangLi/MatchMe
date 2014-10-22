using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public class BasePosition
    {
        /// <summary>
        /// Trader Id
        /// </summary>
        public string UserId
        {
            get;
            set;
        }

        /// <summary>
        /// Id of the Order that creates (opens) this position (OriginalQty)
        /// </summary>
        public string OrderId
        {
            get;
            set;
        }

        /// <summary>
        /// The Symbol of the Securities
        /// </summary>
        public string Symbol
        {
            get;
            set;
        }

        /// <summary>
        /// The initial size of the open position, once it's an immutable property once it's set
        /// </summary>
        public int OriginalQty
        {
            get;
            set;
        }

        /// <summary>
        /// The remaining size of the position, it keeps decreasing from OriginalQty to zero
        /// </summary>
        public int RemainingQty
        {
            get;
            set;
        }

        /// <summary>
        /// The Price to open the position, positive for long, negative for short, it's immutable once it's set
        /// </summary>
        public double OpenPrice
        {
            get;
            set;
        }

        /// <summary>
        /// When the OpenPosition (closed position) is initially opened (closed)
        /// </summary>
        public DateTime CreatedDate
        {
            get;
            set;
        }

        /// <summary>
        /// Updated whenever a quantity is closed
        /// </summary>
        public DateTime LastModified
        {
            get;
            set;
        }
    }
}
