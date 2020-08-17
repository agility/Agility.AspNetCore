using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Agility.Web.Caching
{
    /// <summary>
    /// //TODO: implement CacheDependency
    /// </summary>
    public class CacheDependency
    {
        internal Dictionary<string, CancellationTokenSource> TokenSources = new Dictionary<string, CancellationTokenSource>();
        public CompositeChangeToken ChangeToken = null;
        internal static PhysicalFileProvider fileProvider { get; set; }

        public CacheDependency(string[] filePaths = null, string[] cacheKeys = null)
        {
            var tokenList = new List<IChangeToken>();

            //add the key tokens
            if (cacheKeys != null)
            {
                foreach (string cacheKey in cacheKeys)
                {
                    if (string.IsNullOrEmpty(cacheKey))
                        continue;

                    CancellationTokenSource tokenSource = null;
                    if (!AgilityCache.KeyTokens.TryGetValue(cacheKey, out tokenSource))
                    {
                        //Add a cancellation token for this item					
                        tokenSource = new CancellationTokenSource();

                        this.TokenSources[cacheKey] = tokenSource;
                    }

                    //TODO: figure out what to do if the item we want a dependancy on isn't in cache yet...
                    var token = new CancellationChangeToken(tokenSource.Token);
                    tokenList.Add(token);
                }
            }

            //add the file tokens
            if (filePaths != null)
            {
                foreach (string filePath in filePaths)
                {
                    //unix vs windows
                    var lastSlash = filePath.LastIndexOf('/');

                    var fileName = filePath.Substring(lastSlash + 1);
                    if (fileName.StartsWith(fileProvider.Root))
                    {
                        fileName = fileName.Substring(fileProvider.Root.Length);
                    }

                    var changeToken = fileProvider.Watch(fileName);

                    tokenList.Add(changeToken);
                }
            }

            ChangeToken = new CompositeChangeToken(tokenList);
        }

        public CacheDependency(string filePath)
        {
            var lastSlash = filePath.LastIndexOf('/');

            var fileName = filePath.Substring(lastSlash + 1);

            if (fileName.StartsWith(fileProvider.Root))
            {
                fileName = fileName.Substring(fileProvider.Root.Length);
            }

            var changeToken = fileProvider.Watch(fileName);

            ChangeToken = new CompositeChangeToken(new List<IChangeToken>()
            {
                changeToken
            });
        }
    }
}
