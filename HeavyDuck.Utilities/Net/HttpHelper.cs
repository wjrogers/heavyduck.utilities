using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace HeavyDuck.Utilities.Net
{
    public static class HttpHelper
    {
        private const string USER_AGENT = "HeavyDuck.Utilities";

        private static readonly Encoding m_encoding = new UTF8Encoding(false);

        /// <summary>
        /// POSTs to an URL and returns the response.
        /// </summary>
        /// <param name="url">The url to request.</param>
        /// <param name="parameters">The form parameters.</param>
        /// <param name="autoRedirect">If true, the request will follow redirect responses automatically.</param>
        /// <returns>The response object.</returns>
        public static HttpWebResponse UrlPost(string url, IEnumerable<KeyValuePair<string, string>> parameters, bool autoRedirect)
        {
            return UrlPost(url, parameters, autoRedirect, null);
        }

        /// <summary>
        /// GETs an URL from the internets.
        /// </summary>
        /// <param name="url">The url to request.</param>
        /// <returns>The path to the temporary file.</returns>
        public static HttpWebResponse UrlGet(string url)
        {
            return UrlGet(url, null);
        }

        /// <summary>
        /// GETs an URL from the internets.
        /// </summary>
        /// <param name="url">The url to request.</param>
        /// <param name="cookies">The cookies to be included in the request.</param>
        /// <returns>The path to the temporary file.</returns>
        public static HttpWebResponse UrlGet(string url, CookieContainer cookies)
        {
            // create request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // set request properties
            request.CookieContainer = cookies;
            request.KeepAlive = false;
            request.Method = "GET";
            request.UserAgent = USER_AGENT;

            // do the actual net stuff
            return (HttpWebResponse)request.GetResponse();
        }


        /// <summary>
        /// POSTs to an URL and returns the response.
        /// </summary>
        /// <param name="url">The url to request.</param>
        /// <param name="parameters">The form parameters.</param>
        /// <param name="autoRedirect">If true, the request will follow redirect responses automatically.</param>
        /// <param name="cookies">The cookies to be included in the request.</param>
        /// <returns>The response object.</returns>
        public static HttpWebResponse UrlPost(string url, IEnumerable<KeyValuePair<string, string>> parameters, bool autoRedirect, CookieContainer cookies)
        {
            byte[] buffer;

            // create our request crap
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

            // set the standard request properties
            request.AllowAutoRedirect = autoRedirect;
            request.ContentType = "application/x-www-form-urlencoded";
            request.CookieContainer = cookies;
            request.KeepAlive = false;
            request.Method = "POST";
            request.UserAgent = USER_AGENT;
            request.ServicePoint.Expect100Continue = false; // fixes problems with POSTing to EVE-Central

            // write the request
            using (Stream s = request.GetRequestStream())
            {
                buffer = m_encoding.GetBytes(GetEncodedParameters(parameters));
                s.Write(buffer, 0, buffer.Length);
            }

            // here we actually send the request and get a response (we hope)
            return (HttpWebResponse)request.GetResponse();
        }

        /// <summary>
        /// Encodes a list of parameters for HTTP POST.
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <returns>A string suitable for inclusion in a POST request body.</returns>
        public static string GetEncodedParameters(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            StringBuilder list;
            List<KeyValuePair<string, string>> sorted;

            // check the, uh, parameter
            if (parameters == null) return "";

            // copy the parameters and sort them
            sorted = new List<KeyValuePair<string, string>>(parameters);
            sorted.Sort(delegate(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                if (a.Key == b.Key)
                    return string.Compare(a.Value, b.Value);
                else
                    return string.Compare(a.Key, b.Key);
            });

            // build the list
            list = new StringBuilder();
            foreach (KeyValuePair<string, string> parameter in sorted)
            {
                list.Append(System.Web.HttpUtility.UrlEncode(parameter.Key));
                list.Append("=");
                list.Append(System.Web.HttpUtility.UrlEncode(parameter.Value));
                list.Append("&");
            }
            if (list.Length > 0) list.Remove(list.Length - 1, 1);

            // done
            return list.ToString();
        }
    }
}
