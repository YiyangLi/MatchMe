using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public enum enumStatus
    {
        Unknown = 0,
        UserAdded = 1,
        UserAddFailed = 2,
        CompanyAdded = 3,
        CompanyAddFailed = 4,
        BalanceAdjusted = 5,
        BalanceAdjustFailed = 6,
        OrderPlaced = 7,
        OrderPlaceError = 8,
        Successful = 9,
        Error = 10,

        Initial = 20,
        PartiallyFilled = 21,
        Filled = 22, 
        DoneForDay = 23,
        Cancelled = 24,
        Replaced = 25,
        PendingCancel = 26,
        Stopped = 27, 
        Rejected = 28, 
        Suspended = 29,
        PendingNew = 30,
        New = 31,
        Expired = 32, 
        AcceptedForBidding = 33,
        PendingReplace = 34
    }
    public enum enumSide
    {
        Invalid = 0,
        Buy = 1,
        Sell = 2
    }

    public enum enumOrderType
    {
        Invalid = 0,
        Market = 1,
        Limit = 2
    }
}
