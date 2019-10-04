using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using Agility.Web.AgilityContentServer;
using System.IO;
using Agility.Web.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Data;
using System.Collections;

namespace Agility.Web.Providers
{
    public class AgilityDynamicCodeProvider : IFileProvider
    {
        public AgilityDynamicCodeProvider() { }

        public IFileInfo GetFileInfo(string virtualPath)
        {
            if (virtualPath.IndexOf("_ViewStart.", StringComparison.CurrentCultureIgnoreCase) > 0
                    || virtualPath.IndexOf("_ViewImports.", StringComparison.CurrentCultureIgnoreCase) > 0)
            {
                return null;
            }

            ////for Agility Code files, always return true on file existence
            return new AgilityDynamicCodeFile(virtualPath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return new AgilityDynamicCodeDirectoryContents(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            return new AgilityDynamicCodeChangeToken(filter);
        }
    }

    internal class AgilityDynamicCodeDirectoryContents : IDirectoryContents
    {
        private string _viewPath;

        public AgilityDynamicCodeDirectoryContents(string viewPath)
        {
            this._viewPath = viewPath;
        }

        public bool Exists => false;

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

    public class AgilityDynamicCodeChangeToken : IChangeToken
    {
        private string _viewPath;

        public AgilityDynamicCodeChangeToken(string viewPath)
        {
            _viewPath = viewPath;
        }

        public bool ActiveChangeCallbacks => false;

        public bool HasChanged
        {
            get
            {
                //TODO: actually check if the model is changed, otherwise it will always be returned from cache
                return false;

                //var query = "SELECT LastRequested, LastModified FROM Views WHERE Location = @Path;";
                //try
                //{
                //    using (var conn = new SqlConnection(_connection))
                //    using (var cmd = new SqlCommand(query, conn))
                //    {
                //        cmd.Parameters.AddWithValue("@Path", _viewPath);
                //        conn.Open();
                //        using (var reader = cmd.ExecuteReader())
                //        {
                //            if (reader.HasRows)
                //            {
                //                reader.Read();
                //                if (reader["LastRequested"] == DBNull.Value)
                //                {
                //                    return false;
                //                }
                //                else
                //                {
                //                    return Convert.ToDateTime(reader["LastModified"]) > Convert.ToDateTime(reader["LastRequested"]);
                //                }
                //            }
                //            else
                //            {
                //                return false;
                //            }
                //        }
                //    }

                //}
                //catch (Exception)
                //{
                //    return false;
                //}
            }
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state) => EmptyDisposable.Instance;
    }

    internal class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new EmptyDisposable();
        private EmptyDisposable() { }
        public void Dispose() { }
    }


    internal class AgilityDynamicCodeFile : IFileInfo
    {

        string VirtualPath = null;

        //these are matched with constants in the Agility.Shared assembly.
        public const string LANGUAGECODE_CODE = "code";
        public const string REFNAME_AgilityModuleCodeTemplates = "AgilityModuleCodeTemplates";
        public const string REFNAME_AgilityGlobalCodeTemplates = "AgilityGlobalCodeTemplates";
        public const string REFNAME_AgilityPageCodeTemplates = "AgilityPageCodeTemplates";
        public const string REFNAME_AgilityCSSFiles = "AgilityCSSFiles";
        public const string REFNAME_AgilityJavascriptFiles = "AgilityJavascriptFiles";

        public AgilityDynamicCodeFile(string virtualPath)
        {
            this.VirtualPath = virtualPath;
        }

        private string _contents = null;
        public string Contents
        {
            get
            {
                if (_contents == null)
                {
                    _contents = GetCodeContent(VirtualPath);

                }
                return _contents;
            }
        }

        public bool Exists
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Contents);
            }
        }

        public long Length
        {
            get
            {
                if (Exists) return Contents.Length;
                return 0;
            }
        }


        public string PhysicalPath
        {
            get
            {
                return null;
            }
        }

        public string Name
        {
            get
            {
                return GetReferenceName(VirtualPath);
            }
        }

        public DateTimeOffset LastModified => throw new NotImplementedException();

        public bool IsDirectory => false;



        internal static string GetReferenceName(string path)
        {

            //path is like this: DynamicAgilityCode/[ContentReferenceName]/[ItemReferenceName].ext
            var pre = "DynamicAgilityCode/";

            string p1 = path.Substring(path.IndexOf(pre, StringComparison.CurrentCultureIgnoreCase) + pre.Length);
            if (p1.IndexOf("/") == -1 || p1.IndexOf(".") < p1.IndexOf("/")) return string.Empty;

            string referenceName = p1.Substring(0, p1.IndexOf("/"));
            return referenceName;
        }



        internal static string GetCodeContent(string path)
        {
            DataRow row = GetCodeItem(path);
            if (row == null) return string.Empty;
            string textblob = row["TextBlob"] as string;
            return textblob;
        }


        public static DataRow GetCodeItem(string path)
        {
            //path is like this: DynamicAgilityCode/[ContentReferenceName]/[ItemReferenceName].ext
            var pre = Agility.Web.HttpModules.AgilityHttpModule.DynamicCodePrepend;

            string p1 = path.Substring(path.IndexOf(pre, StringComparison.CurrentCultureIgnoreCase) + pre.Length);
            if (p1.IndexOf("/") == -1 || p1.IndexOf(".") < p1.IndexOf("/")) return null;

            string referenceName = p1.Substring(0, p1.IndexOf("/"));

            //get the content.
            var content = BaseCache.GetContent(referenceName, LANGUAGECODE_CODE, AgilityContext.WebsiteName);
            if (content == null
                || content.DataSet == null
                || (!content.DataSet.Tables.Contains("ContentItems"))
                || content.DataSet.Tables["ContentItems"].Rows.Count == 0)
            {
                return null;
            }

            int slashIndex = p1.IndexOf("/");
            int dotIndex = p1.IndexOf(".");

            string itemReferenceName = p1.Substring(slashIndex + 1, dotIndex - slashIndex - 1);
            string filter = string.Format("ReferenceName = '{0}'", itemReferenceName.Replace("'", "''"));

            StringBuilder sb = new StringBuilder();

            DataRow[] rows = null;
            try
            {
                rows = content.DataSet.Tables["ContentItems"].Select(filter);
            }
            catch { }


            if (rows == null || rows.Length == 0) return null;


            DataRow row = rows[0];

            return row;
        }

        public Stream CreateReadStream()
        {
            byte[] bytes = UTF8Encoding.UTF8.GetBytes(Contents);
            return new MemoryStream(bytes);
        }
    }
}
