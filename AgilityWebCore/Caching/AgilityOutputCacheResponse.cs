using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Agility.Web.Caching
{
    [Serializable]
    public class AgilityOutputCacheResponse
    {
        public int StatusCode = 200;
        public Dictionary<string, string> Headers = new Dictionary<string, string>();
        public byte[] Body = null;

        public string ETag = null;

        public AgilityOutputCacheResponse() { }

        public AgilityOutputCacheResponse(HttpContext context, byte[] bytes)
        {
            Body = bytes;

            var md5 = System.Security.Cryptography.MD5.Create();
            var hashedBytes = md5.ComputeHash(bytes);

            ETag = Convert.ToBase64String(hashedBytes);

            StatusCode = context.Response.StatusCode;

            foreach (var key in context.Response.Headers.Keys)
            {
                Headers[key] = context.Response.Headers[key];
            }

        }

        public static AgilityOutputCacheResponse ReadFromFile(string url, string filePath)
        {
            try
            {
                AgilityOutputCacheResponse res = null;
                BinaryFormatter bf = new BinaryFormatter();

                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    res = bf.Deserialize(fs) as AgilityOutputCacheResponse;
                }

                return res;
            }
            catch (Exception ex)
            {
                throw new ApplicationException(string.Format("Could not read cached response for url:{0}, file {1}", url, filePath), ex);
            }

        }

        public void WriteToFile(string filePath)
        {

            BinaryFormatter bf = new BinaryFormatter();

            using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                bf.Serialize(fs, this);
            }

        }


    }
}
