using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace KP_Steam_Uploader.Util
{
    public class SteamUgc
    {
        protected ILogger Logger;
        
        public SteamUgc(ILogger logger)
        {
            Logger = logger;
        }
        
        public async Task<SteamUGCDetails_t> GetSingleQueryUgcResult(ulong itemId)
        {
            PublishedFileId_t[] itemIds = {new PublishedFileId_t(itemId)};
            // Create query and enable all wanted returns
            var queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(itemIds, (uint) itemIds.Length);
            SteamUGC.SetReturnKeyValueTags(queryHandle, true);
            SteamUGC.SetReturnMetadata(queryHandle, true);

            var queryUgcRequestCall = SteamUGC.SendQueryUGCRequest(queryHandle);
            
            var getSingleQueryUgcResultTask = new TaskCompletionSource<SteamUGCDetails_t>();
            // Handle query result and resolve the task
            var resultHandler = CallResult<SteamUGCQueryCompleted_t>.Create((pCallback, bIoFailure) =>
            {
                Logger.LogInformation("[SteamUGCQueryCompleted]" +
                                      $" Result: {pCallback.m_eResult} ");
            
                if (pCallback.m_eResult != EResult.k_EResultOK)
                {
                    getSingleQueryUgcResultTask.TrySetException(
                        new Exception($"Query failed with result {pCallback.m_eResult}"));
                    return;
                }

                SteamUGCDetails_t itemResult;
                SteamUGC.GetQueryUGCResult(pCallback.m_handle, 0, out itemResult);
                
                getSingleQueryUgcResultTask.SetResult(itemResult);
            });
            resultHandler.Set(queryUgcRequestCall);
            
            while (!getSingleQueryUgcResultTask.Task.IsCompleted)
            {
                Logger.LogDebug("Running callbacks");
                SteamAPI.RunCallbacks();
                Thread.Sleep(100);
            }
            
            return await getSingleQueryUgcResultTask.Task;
        }
        
    }
}