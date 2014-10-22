using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MatchMe.Common
{
    [Serializable]
    public class User : MatchMeObject
    {
        private string username;
        public string Email
        {
            get { return username; }
            set { username = value; }
        }

        public string UserName
        {
            get { return username; }
        }

        public double Balance
        {
            get;
            set;
        }

        public User()
        {
            username = string.Empty;
            Balance = 0.0;
            SetId();
        }

        public User(string UserName, double balance)
            :this()
        {
            username = UserName;
            Balance = balance;
        }

        public User(User user)
        {
            this.Email = user.Email;
            this.Balance = user.Balance;
            this.Id = user.Id;
            SetId();
        }

        public User(string JSON)
            : this(JsonConvert.DeserializeObject<User>(JSON))
        {
        }
    }
}
