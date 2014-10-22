using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatchMe.Common
{
    public class RestMethodAttribute : Attribute
    {
        private string restName;
        public RestMethodAttribute(string _restName)
        {
            restName = _restName;
        }

        public string RestName
        {
            get
            {
                return restName;
            }
        }
    }
}
