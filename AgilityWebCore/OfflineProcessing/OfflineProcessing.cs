using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Principal;
using Agility.Web.Tracing;
using Agility.Web.AgilityContentServer;
using System.IO;

namespace Agility.Web
{
	internal static class OfflineProcessing
	{
		private static Thread _thread;
		
		private static object lockObj = new object();

		internal static bool IsOfflineThreadRunning { get; set; }
		

		internal static void StartOfflineThread()
		{
			
			lock (lockObj)
			{
				if (IsOfflineThreadRunning) return;



				ThreadStart ts = new ThreadStart(RunOfflineThread);
				_thread = new Thread(ts);
				_thread.Priority = ThreadPriority.BelowNormal;
				_thread.IsBackground = true;				
				_thread.Start();

				IsOfflineThreadRunning = true;


			}
		}


		private static void RunOfflineThread()
		{
			try
			{
				//TODO: check impersonation?
				//WindowsImpersonationContext wi = Sync.SyncThread.ApplicationIdentity.Impersonate();

				//wait a minute before we get started
				Thread.Sleep(TimeSpan.FromMinutes(1));

				Agility.Web.Tracing.WebTrace.WriteVerboseLine("Starting offline processing thread.");

				Int64 minutes = 1;
				Int64 ERROR_CHECK_MINUTES = 15; 
				Int64 CLEANUP_CHECK_MINUTES = 60;

				while (true)
				{

					try
					{
						if (minutes == 1 || minutes % ERROR_CHECK_MINUTES == 0)
						{
							Agility.Web.Tracing.WebTrace.WriteVerboseLine("Checking for errors in error log.");
							//do the error log check
							WebTrace.SendErrorSummary();
						}

						//MOD JOEL VARTY JUNE 18 2012 - NO LONGER SUPPORTING AUTOUPGRADING
						
						if (minutes == 1 || minutes % CLEANUP_CHECK_MINUTES == 0 || Configuration.Current.Settings.DevelopmentMode == false)
						{

							

								Agility.Web.Tracing.WebTrace.WriteVerboseLine("Cleaning up OutputCache files.");

								//only do cleanup on NON development mode boxes
								//do the cleanup 1 minute after app start or every 60 mins 

						//TODO: cleanup the OutputCache folder...
								//string outputCacheFolder = HttpModules.AgilityOutputCacheModule.GetOutputCacheFolder();

								//if (Directory.Exists(outputCacheFolder))
								//{
								//	//cleanup files older than 24 hours
								//	var oldDate = DateTime.Now.Subtract(TimeSpan.FromHours(24));

								//	var files = Agility.Web.Utils.FastDirectoryEnumerator.EnumerateFiles(outputCacheFolder);
								//	List<string> pathsToDelete = new List<string>();
								//	foreach (var file in files)
								//	{
								//		if (file.LastWriteTime < oldDate)
								//		{
								//			pathsToDelete.Add(file.Path);
								//		}
								//	}

								//	int errorCount = 0;
								//	Exception lastEx = null;
								//	foreach (string path in pathsToDelete)
								//	{
								//		try
								//		{
								//			File.Delete(path);
								//		}
								//		catch (Exception ex)
								//		{
								//			errorCount++;
								//			lastEx = ex;
								//		}
								//	}

								//	if (errorCount > 0)
								//	{
								//		Agility.Web.Tracing.WebTrace.WriteException(lastEx, string.Format("{0} error(s) occurred cleaning up folder {1}", errorCount, outputCacheFolder));
								//	}

								//}
							
						}
						 

					}
					catch (Exception ex)
					{
						Agility.Web.Tracing.WebTrace.WriteException(ex);
					}

					Thread.Sleep(TimeSpan.FromMinutes(1));
					
					minutes++;
				}


			}
			catch (ThreadAbortException)
			{
				//this will be called when the app is shutdown or the thread is killed

			}
			catch (Exception ex)
			{
				Agility.Web.Tracing.WebTrace.WriteException(ex);
			}
			finally
			{
			
				IsOfflineThreadRunning = false;
			}

			
		}
	}
}
