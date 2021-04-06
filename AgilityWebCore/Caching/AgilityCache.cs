using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Agility.Web.Caching
{
    internal static class AgilityCache
    {
        private static IMemoryCache _cache;
        internal static ConcurrentDictionary<string, CancellationTokenSource> KeyTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

        private static IMemoryCache MemoryCache =>
            _cache ??= Extensions.HtmlHelperViewExtensions.GetServiceOrFail<IMemoryCache>(AgilityContext.HttpContext);

        internal static T Get<T>(string key)
        {
            return MemoryCache.Get<T>(key);
        }

        internal static object Get(string key)
        {
            return MemoryCache.Get(key);
        }
        
        internal static void Set(string key, object o, TimeSpan timeout, CacheDependency cacheDependency = null, CacheItemPriority priority = CacheItemPriority.Normal)
        {
            //set the cache options
            var options = new MemoryCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = timeout,
                Priority = priority
            };

            //add the change dependancy tokens
            if (cacheDependency?.ChangeToken != null && cacheDependency.ChangeToken.ChangeTokens.Count > 0)
            {
                // cacheDependency.ChangeToken.RegisterChangeCallback(PostCacheEviction2, cacheDependency.ChangeToken);
                options.AddExpirationToken(cacheDependency.ChangeToken);

                foreach (var tokenSourceKey in cacheDependency.TokenSources.Keys)
                {
                    KeyTokens.TryAdd(tokenSourceKey, cacheDependency.TokenSources[tokenSourceKey]);
                }
            }

            options.RegisterPostEvictionCallback(PostCacheEviction);
            MemoryCache.Set(key, o, options);
        }

        /// <summary>
        /// When an object is kicked out of Agility's memory cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="reason"></param>
        /// <param name="state"></param>
        static void PostCacheEviction(object key, object value, EvictionReason reason, object state)
        {
            var keyStr = key as string;
            if (string.IsNullOrWhiteSpace(keyStr)) return;

            CancelToken(keyStr);
        }

        private static void CancelToken(string key)
        {
            //if this object is kicked out of cache, make sure all of it's dependants are un-cached also
            if (!KeyTokens.TryGetValue(key, out var tokenSource)) return;

            //KeyTokens.TryRemove(key);
            KeyTokens.TryRemove(key, out _);
            tokenSource.Cancel();
            tokenSource.Dispose();
        }

        internal static void Remove(string key)
        {
            CancelToken(key);

            MemoryCache.Remove(key);
        }

        internal static bool UseAgilityOutputCache => false;

        internal static void AddResponseCacheDependancy(List<string> cacheKeys)
        {
            //TODO: NOT IMPLEMENTED
        }

        internal static void TurnOffCacheInProgress()
        {
            //TODO: NOT IMPLEMENTED
        }


    }
}
