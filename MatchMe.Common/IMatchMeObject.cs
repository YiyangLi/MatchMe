using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MatchMe.Common
{
    interface IMatchMeObject
    {
        string Id { get; set; }
        string toJSON();
        void SetId();
    }
}
