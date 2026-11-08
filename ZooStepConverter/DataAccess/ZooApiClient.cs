using System;
using System.IO;
using System.Net;
using System.Text;

namespace ZooStepConverter.DataAccess
{
    /// <summary>
    /// Data Access Layer: Handles remote network communication with the Zoo API.
    /// </summary>
    public static class ZooApiClient
    {
        // The Zoo CLI's own OAuth client id - its source marks it safe to embed:
        // github.com/KittyCAD/cli src/cmd_auth.rs
        public const string ClientId = "6bd9f64f-0ed6-40c2-ada0-87e1fc699227";
        public const string ApiHost = "https://api.zoo.dev/";

        public static string HttpPostForm(string url, string body, out int status)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            var data = Encoding.UTF8.GetBytes(body);
            req.ContentLength = data.Length;
            using (var s = req.GetRequestStream()) s.Write(data, 0, data.Length);
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream()))
                {
                    status = (int)resp.StatusCode;
                    return r.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null) throw;
                using (var r = new StreamReader(resp.GetResponseStream()))
                {
                    status = (int)resp.StatusCode;
                    return r.ReadToEnd();
                }
            }
        }

        public static string HttpGet(string url, string bearer)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Headers.Add("Authorization", "Bearer " + bearer);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream()))
            {
                return r.ReadToEnd();
            }
        }
    }
}

