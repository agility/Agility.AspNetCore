using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Web;
using System.Reflection;
using Agility.Web.Caching;

namespace Agility.Web.Utils
{
    public class FileUtils
    {
        public delegate void FileOperationDelegate(string filepath);

        public static byte[] ReadFileBytes(string filepath)
        {
            FileInfo fileInfo = new FileInfo(filepath);
            return BaseCache.ReadFileBytes(fileInfo);
        }

        public static void WriteFile(object item, string filepath)
        {
            WriteFile(item, filepath, DateTime.MinValue);
        }

        public static void WriteFileWithRetries(object item, string filepath)
        {
            WriteFile(item, filepath, DateTime.MinValue);
            //BaseCache.WriteFileWithRetries(item, filepath, DateTime.MinValue);
        }


        public static void WriteFile(object item, string filepath, DateTime overrideLastModifiedDate)
        {
            BaseCache.WriteFile(item, filepath, overrideLastModifiedDate);
        }

        public static void DeleteFile(string filepath)
        {
            BaseCache.DeleteFile(filepath);
        }

        public static void DoFileOperation(string filepath, FileOperationDelegate fileOperationDelegate)
        {
            BaseCache.DoFileOperation(filepath, fileOperationDelegate);
        }

        static object _typeLockObject = new object();

        internal static Type GetTypeFromReflection(string assemblyName, string typeName)
        {
			var context = AgilityContext.HttpContext;
            string typeCacheKey = string.Format("Agility.Web.MVC.RenderContentZone_{0}_{1}", assemblyName, typeName);
            Type modelType = AgilityCache.Get(typeCacheKey) as Type;
            if (modelType == null)
            {

                lock (_typeLockObject)
                {
                    modelType = modelType = AgilityCache.Get(typeCacheKey) as Type;
					if (modelType == null)
                    {
                        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        foreach (Assembly assembly in assemblies)
                        {

                            if (assemblyName == null)
                            {

                                //ignore Agility.Web, and anything from the GAC
                                if (assembly.GlobalAssemblyCache) continue;
                                if (assembly.FullName.StartsWith("Agility.Web")) continue;
                            }
                            else
                            {
                                if (assembly.FullName.IndexOf(assemblyName, StringComparison.CurrentCultureIgnoreCase) == -1) continue;
                            }

                            try
                            {
                                modelType = assembly.GetTypes().FirstOrDefault(type => type.Name.EndsWith(typeName, StringComparison.CurrentCultureIgnoreCase));
                                if (modelType != null)
                                {
									AgilityCache.Set(typeCacheKey, modelType, TimeSpan.FromDays(1));
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                                //ignore any types we don't have access to load...
                            }

                        }
                    }
                }
            }
            return modelType;
        }
    }
}
