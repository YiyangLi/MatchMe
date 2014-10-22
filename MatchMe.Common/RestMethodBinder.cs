using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reflection;

namespace MatchMe.Common
{
    /// <summary>
    /// Used for dynamic reflection, parse the Rest API to o.method(params[] )
    /// </summary>
    public class RestMethodBinder
    {
        private Dictionary<string, MethodInfo> methods;
        private Type restClass;
        public RestMethodBinder(Type _restClass)
        {
            restClass = _restClass;
            methods = new Dictionary<string, MethodInfo>();
            foreach (var mi in restClass.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attrs = mi.GetCustomAttributes(false);
                if (attrs.Length != 0)
                {
                    foreach (var attr in attrs)
                    {
                        if (attr.GetType() == typeof(RestMethodAttribute))
                        {
                            RestMethodAttribute rma = attr as RestMethodAttribute;
                            methods.Add(rma.RestName, mi);
                        }
                    }
                }
            }
        }

        public byte[] CallRestMethodBin(object o, string command, Dictionary<string, string> args, string jsonInput, out string contentType, out string redirect, out int httpStatus)
        {
            MethodInfo method;
            if (!methods.TryGetValue(command, out method))
            {
                throw new ApplicationException(string.Format("RestMethod {0} does not exist in class {1}", command, restClass.Name));
            }

            if (method.ReturnType != typeof(byte[]))
            {
                throw new ApplicationException(string.Format("RestMethod {0} does not return a byte array", command));
            }

            return CallRestMethodInternal(method, o, command, args, jsonInput, out contentType, out redirect, out httpStatus) as byte[];
        }

        public string CallRestMethod(object o, string command, Dictionary<string, string> args, string jsonInput, out string contentType, out string redirect, out int httpStatus)
        {
            MethodInfo method;
            if (!methods.TryGetValue(command, out method))
            {
                throw new ApplicationException(string.Format("RestMethod {0} does not exist in class {1}", command, restClass.Name));
            }

            if (method.ReturnType != typeof(string))
            {
                throw new ApplicationException(string.Format("RestMethod {0} does not return a string", command));
            }

            return CallRestMethodInternal(method, o, command, args, jsonInput, out contentType, out redirect, out httpStatus) as string;
        }

        /// <summary>
        /// reflection
        /// </summary>
        /// <param name="method">method Name</param>
        /// <param name="o">object that contains the method</param>
        /// <param name="command">command name, close to method</param>
        /// <param name="args">method arguments</param>
        /// <param name="jsonInput">we support json Input as well </param>
        /// <param name="contentType"></param>
        /// <param name="redirect"></param>
        /// <param name="httpStatus">Rest Status, 200 or 500</param>
        /// <returns></returns>
        private object CallRestMethodInternal(MethodInfo method, object o, string command, Dictionary<string, string> args, string jsonInput, out string contentType, out string redirect, out int httpStatus)
        {
            try
            {
                var pinfo = method.GetParameters();
                object[] paramArray = new object[pinfo.Length];

                int contentTypeIndex = -1;
                int RedirectIndex = -1;
                int httpStatusIndex = -1;

                int n = 0;
                foreach (var p in pinfo)
                {
                    if (p.Name == "jsonInput")
                    {
                        paramArray[n] = jsonInput;
                    }
                    else if (p.Name == "outContentType" && p.IsOut)
                    {
                        paramArray[n] = null;
                        contentTypeIndex = n;
                    }
                    else if (p.Name == "outRedirect" && p.IsOut)
                    {
                        paramArray[n] = null;
                        RedirectIndex = n;
                    }
                    else if (p.Name == "httpStatus" && p.IsOut)
                    {
                        paramArray[n] = null;
                        httpStatusIndex = n;
                    }
                    else
                    {
                        if (p.ParameterType != typeof(string))
                        {
                            throw new ApplicationException(string.Format("Rest Method {0} has a parameter with invalid type", command));
                        }
                        string val;
                        if (!args.TryGetValue(p.Name, out val))
                            paramArray[n] = null;
                        else
                            paramArray[n] = val;
                    }

                    n++;
                }

                object ret = method.Invoke(o, paramArray);

                if (contentTypeIndex != -1)
                {
                    contentType = paramArray[contentTypeIndex] as string;
                }
                else
                {
                    contentType = "application/json";
                }

                if (RedirectIndex != -1)
                {
                    redirect = paramArray[RedirectIndex] as string;
                }
                else
                {
                    redirect = null;
                }

                if (httpStatusIndex != -1)
                {
                    httpStatus = (int)paramArray[httpStatusIndex];
                }
                else
                {
                    httpStatus = 200;
                }

                return ret;
            }
            catch (TargetInvocationException tie)
            {
                ServerLog.LogException(tie.InnerException, "Exception thrown in bound REST method " + method.Name);
                throw tie.InnerException;
            }
        }
    }
}
