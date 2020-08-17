using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Agility.Web.Caching
{
    internal static class AgilityCache
    {
        private static IMemoryCache _cache = null;
        internal static Dictionary<string, CancellationTokenSource> KeyTokens = new Dictionary<string, CancellationTokenSource>();

        private static IMemoryCache MemoryCache
        {
            get
            {
                if (_cache == null)
                {
                    _cache = Extensions.HtmlHelperViewExtensions.GetServiceOrFail<IMemoryCache>(AgilityContext.HttpContext);
                }
                return _cache;
            }
        }

        internal static T Get<T>(string key)
        {
            return MemoryCache.Get<T>(key);
        }

        //TODO: implement caching layer...
        internal static object Get(string key)
        {
            return MemoryCache.Get(key);
        }


        internal static void Set(string key, object o, TimeSpan timeout, CacheDependency cacheDependency = null, CacheItemPriority priority = CacheItemPriority.Normal)
        {

            //get the cancellation token for this item and trigger it
            CancellationTokenSource tokenSource = null;
            // if (KeyTokens.TryGetValue(key, out tokenSource))
            // {
            //     tokenSource.Cancel();
            // }


            //set the cache options
            MemoryCacheEntryOptions options = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = timeout,
                Priority = priority
            };


            //add the change dependancy tokens
            if (cacheDependency != null
                && cacheDependency.ChangeToken != null
                && cacheDependency.ChangeToken.ChangeTokens.Count > 0)
            {
                // cacheDependency.ChangeToken.RegisterChangeCallback(PostCacheEviction2, cacheDependency.ChangeToken);
                options.AddExpirationToken(cacheDependency.ChangeToken);

                foreach (var tokenSourceKey in cacheDependency.TokenSources.Keys)
                {
                    AgilityCache.KeyTokens.TryAdd(tokenSourceKey, cacheDependency.TokenSources[tokenSourceKey]);
                }
            }

            options.RegisterPostEvictionCallback(PostCacheEviction);


            MemoryCache.Set<object>(key, o, options);
        }

        // static void PostCacheEviction2(object key)
        // {
        //     string keyStr = key as string;
        //     if (string.IsNullOrWhiteSpace(keyStr)) return;


        //     //if this object is kicked out of cache, make sure all of it's dependants are un-cached also
        //     CancellationTokenSource tokenSource = null;
        //     if (KeyTokens.TryGetValue($"{key}", out tokenSource))
        //     {
        //         tokenSource.Cancel();
        //     }
        // }

        /// <summary>
        /// When an object is kicked out of Agility's memory cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="reason"></param>
        /// <param name="state"></param>
        static void PostCacheEviction(object key, object value, EvictionReason reason, object state)
        {
            string keyStr = key as string;
            if (string.IsNullOrWhiteSpace(keyStr)) return;

            CancelToken(keyStr);
        }

        private static void CancelToken(string key)
        {
            //if this object is kicked out of cache, make sure all of it's dependants are un-cached also
            CancellationTokenSource tokenSource = null;
            if (KeyTokens.TryGetValue(key, out tokenSource))
            {
                KeyTokens.Remove(key);
                tokenSource.Cancel();
                tokenSource.Dispose();

            }
        }

        internal static void Remove(string key)
        {
            CancelToken(key);

            MemoryCache.Remove(key);
        }

        internal static bool UseAgilityOutputCache
        {
            get
            {
                return false;
            }
        }


        internal static void AddResponseCacheDependancy(List<string> cacheKeys)
        {
            //TODO: decide if we have to do anything here at all...
        }

        internal static void TurnOffCacheInProgress()
        {

        }


    }
}
