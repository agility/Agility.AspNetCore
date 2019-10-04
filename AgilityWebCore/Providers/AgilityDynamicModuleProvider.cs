using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Agility.Web.Providers
{
    public class AgilityDynamicModuleProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            return new AgilityDynamicModuleDirectoryContents(subpath);
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            if (subpath.IndexOf("_ViewStart.", StringComparison.CurrentCultureIgnoreCase) > 0
                    || subpath.IndexOf("_ViewImports.", StringComparison.CurrentCultureIgnoreCase) > 0)
            {
                return null;
            }

            ////for Agility Code files, always return true on file existence
            return new AgilityDynamicModuleFile(subpath);
        }

        public IChangeToken Watch(string filter)
        {
            return new AgilityDynamicModuleChangeToken(filter);
        }
    }

    internal class AgilityDynamicModuleFile : IFileInfo
    {
        private string subpath;
        private int moduleID;

        public AgilityDynamicModuleFile(string subpath)
        {
            this.subpath = subpath;
            this.moduleID = GetModuleDefID(subpath);
        }

        private int GetModuleDefID(string subpath)
        {
            string idStr = subpath.Substring(subpath.LastIndexOf("/") + 1);
            idStr = idStr.Substring(0, idStr.IndexOf("."));

            int id = -1;
            if (int.TryParse(idStr, out id)) return id;

            return id;
        }

        public bool Exists
        {
            get
            {
                if (this.moduleID < 1)
                    return false;

                Agility.Web.AgilityContentServer.AgilityModule module = BaseCache.GetModule(this.moduleID, AgilityContext.WebsiteName);

                return module != null;
            }
        }

        public long Length
        {
            get
            {
                if (this.Exists) return Contents.Length;
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

        private string _contents = null;
        public string Contents
        {
            get
            {
                if (_contents == null)
                {
                    _contents = GetCodeContent();

                }
                return _contents;
            }
        }

        private string GetCodeContent()
        {
            Agility.Web.AgilityContentServer.AgilityModule module = BaseCache.GetModule(moduleID, AgilityContext.WebsiteName);

            return module.Markup;
        }

        public string Name => throw new NotImplementedException();

        public DateTimeOffset LastModified => throw new NotImplementedException();

        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(this.Contents);

            return new MemoryStream(bytes);
        }
    }

    internal class AgilityDynamicModuleChangeToken : IChangeToken
    {
        private string _viewPath;

        public AgilityDynamicModuleChangeToken(string viewPath)
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
            }
        }

        public IDisposable RegisterChangeCallback(Action<object> callback, object state) => EmptyDisposable.Instance;
    }

    internal class AgilityDynamicModuleDirectoryContents : IDirectoryContents
    {
        private string _viewPath;

        public AgilityDynamicModuleDirectoryContents(string viewPath)
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
}
