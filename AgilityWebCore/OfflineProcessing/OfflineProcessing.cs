using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Principal;
using Agility.Web.Tracing;
using Agility.Web.AgilityContentServer;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace Agility.Web
{
	public class OfflineProcessing : IHostedService, IDisposable
	{
		private  Thread _thread;
		private  Thread _folderSyncThread;

		private  object lockObj = new object();

		internal  bool IsOfflineThreadRunning { get; set; }
		internal  bool IsFileProcessThreadRunning { get; set; }

		public Task StartAsync(CancellationToken cancellationToken)
		{
			StartOfflineThread();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			try
			{
				if (_thread != null && IsOfflineThreadRunning)
				{
					_thread.Abort();
				}
			} catch { }

			try
			{
				if (_folderSyncThread != null && IsFileProcessThreadRunning)
				{
					_folderSyncThread.Abort();
				}
			} catch { }

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			_thread = null;
			_folderSyncThread = null;
		}


		internal  void StartOfflineThread()
		{
			
			lock (lockObj)
			{
				if (!IsOfflineThreadRunning)
				{

					ThreadStart ts = new ThreadStart(RunLogProcessingThread);
					_thread = new Thread(ts);
					_thread.Priority = ThreadPriority.BelowNormal;
					_thread.IsBackground = true;
					_thread.Start();

					IsOfflineThreadRunning = true;
				}

				if (! IsFileProcessThreadRunning)
				{

					ThreadStart ts = new ThreadStart(RunFolderSyncThread);
					_folderSyncThread = new Thread(ts);
					_folderSyncThread.Priority = ThreadPriority.BelowNormal;
					_folderSyncThread.IsBackground = true;
					_folderSyncThread.Start();

					IsFileProcessThreadRunning = true;
				}

			}
		}


		private  void RunLogProcessingThread()
		{
			try
			{
				//TODO: check impersonation?
				//WindowsImpersonationContext wi = Sync.SyncThread.ApplicationIdentity.Impersonate();


				//only do this processing if we need to...
				if (!Configuration.Current.Settings.Trace.EmailErrors) return;


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

		private  void RunFolderSyncThread()
		{
			try
			{
				string persistFolder = Configuration.Current.Settings.ContentCacheFilePath;
				string transientFolder = Configuration.Current.Settings.TransientCacheFilePath;

				if (persistFolder == transientFolder) return;

				Agility.Web.Tracing.WebTrace.WriteVerboseLine($"Starting folder sync thread\r\nPersist folder: {persistFolder}\r\ntransient folder: {transientFolder}.");

				string websiteName = AgilityContext.WebsiteName;
				string persistStateFileName = "FolderSync.txt";
				string folderStateFileName = Path.Combine(transientFolder, websiteName, persistStateFileName);

				string persistFolderLive = Path.Combine(persistFolder, websiteName, "Live");
				string transientFolderLive = Path.Combine(transientFolder, websiteName, "Live");

				//make sure the folder exists
				string dir = Path.GetDirectoryName(folderStateFileName);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}

				while (true)
				{
					//sleep at the top of the loop, so any "continue" statements will SLEEP
					Thread.Sleep(TimeSpan.FromSeconds(3));

					try
					{
						//if we are in a sync, don't do anything
						if (Sync.SyncThread.IsSyncInProgress
							|| Sync.SyncThread.IsSyncInProgressOnOtherMachine) continue;

						//CHECK THE STATE FILE TO SEE IF WE NEED TO DO ANY PROCESSING
						
						long folderState = -1;						
						if (File.Exists(folderStateFileName))
						{
							string content = File.ReadAllText(folderStateFileName);
							long.TryParse(content, out folderState);
						}

						double syncState = Sync.SyncThread.SyncState;

						if (syncState < folderState) continue;

						DateTime dtMirrorStart = DateTime.Now;
						Agility.Web.Tracing.WebTrace.WriteVerboseLine($"Syncing persistent folder to transient folder: {transientFolder}.");
						MirrorTransientFolder(persistFolderLive, transientFolderLive);
						Agility.Web.Tracing.WebTrace.WriteVerboseLine($"Transient folder synced up in { (DateTime.Now - dtMirrorStart).TotalSeconds} seconds.");


						//UPDATE THE STATE FILE
						//folderState = (long)(DateTime.Now - DateTime.UnixEpoch).TotalSeconds;
						File.WriteAllText(folderStateFileName, syncState.ToString());



					}
					catch (Exception ex)
					{
						Agility.Web.Tracing.WebTrace.WriteException(ex);
					}

					

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

		private  void MirrorTransientFolder(string persistFolder, string transientFolder)
		{

			var persistPaths = Directory.EnumerateFiles(persistFolder, "*.bin");

			if (! Directory.Exists(transientFolder))
			{
				Directory.CreateDirectory(transientFolder);
			}

			foreach (string persistedPath in persistPaths)
			{
				string fileName = persistedPath.Substring(persistFolder.Length);

				string transientFilePath = Path.Combine(transientFolder, fileName.TrimStart('\\'));

				FileInfo persistFileInfo = new FileInfo(persistedPath);
				FileInfo transientFileInfo = new FileInfo(transientFilePath);

				if (! transientFileInfo.Exists 
					|| transientFileInfo.LastWriteTimeUtc < persistFileInfo.LastWriteTimeUtc)
				{
					//copy the file...
					int numCopyTries = 3;

					for (int currentTry = 1; currentTry <= numCopyTries; currentTry++)					
					{
						try
						{

							File.Copy(persistedPath, transientFilePath, true);
							
							//if we make it here, no need to retry
							break;
						}
						catch (System.IO.FileNotFoundException)
						{
							//ignore file not found
							break;
						}
						catch (Exception ex)
						{
							if (currentTry >= numCopyTries)
							{
								Agility.Web.Tracing.WebTrace.WriteInfoLine($"Error {currentTry} moving file {persistedPath} to {transientFilePath}, giving up. {ex}");
								continue;
							}
							else
							{
								Agility.Web.Tracing.WebTrace.WriteInfoLine($"Error {currentTry} moving file {persistedPath} to {transientFilePath}, retrying. {ex}");
							}
						}

						//if we get here, we need to retry after 2 seconds
						Thread.Sleep(2000);
						
					}
				}
			}
		}

		
	}
}
