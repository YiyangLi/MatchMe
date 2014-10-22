using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace MatchMe.Common
{
    public class MyStringEnumConverter : Newtonsoft.Json.Converters.StringEnumConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Action)
            {
                writer.WriteValue(Enum.GetName(typeof(Action), (Action)value));// or something else
                return;
            }

            base.WriteJson(writer, value, serializer);
        }
    }
    public abstract class MatchMeObject : IMatchMeObject, ICloneable
    {
        public string Id
        {
            get;
            set;
        }
        public virtual string toJSON()
        {
            return JsonConvert.SerializeObject(this, new MyStringEnumConverter());
        }

        public virtual void SetId()
        {
            if (string.IsNullOrEmpty(Id))
                Id = Guid.NewGuid().ToString("N");
        }

        public override string ToString()
        {
            return toJSON().Replace("\"", "'");
        }
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
