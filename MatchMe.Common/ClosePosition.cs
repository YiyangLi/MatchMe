using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public class ClosePosition
    {
        /// <summary>
        /// Value between 0 to OriginalQty
        /// </summary>
        public int ClosedQty
        {
            get;
            set;
        }
        
        /// <summary>
        /// It's the spread between the Cost (from Open Position) and Closed Amount: RealizedGainsAndLosses = ClosedAmount - Cost * ClosedAmount / OriginalQty 
        /// </summary>
        public double RealizedGainsAndLosses
        {
            get;
            set;
        }

        /// <summary>
        /// Proportional to the ClosedQty, initial value is zero then accumulated by ClosedAmount += ClosedQty * ClosePrice + Commission
        /// </summary>
        public double ClosedAmount
        {
            get;
            set;
        }

        /// <summary>
        /// Well, technically, it's a price considered commissions. It should be ClosedAmount / ClosedQty 
        /// </summary>
        public double ClosedPrice
        {
            get;
            set;
        }

        /// <summary>
        /// A position might be closed by several orders
        /// </summary>
        public List<string> ClosedOrderIds
        {
            get;
            set;
        }
    }
}
