using Microsoft.Extensions.Logging;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KP_Steam_Uploader.Util
{
    public class SteamUgc
    {
        protected ILogger Logger;

        public SteamUgc(ILogger logger)
        {
            Logger = logger;
        }

        public void DeleteStaleFiles()
        {
            if (SteamRemoteStorage.GetFileCount() > 0)
            {
                for (int i = 0; i <= SteamRemoteStorage.GetFileCount(); i++)
                {
                    string sFileName = SteamRemoteStorage.GetFileNameAndSize(0, out int iFileSize);
                    Logger.LogInformation($"Deleting stale file: {sFileName}");
                    SteamRemoteStorage.FileDelete(sFileName);
                }
            }
        }

        public async Task<SteamUGCDetails_t> GetSingleQueryUgcResult(ulong itemId, bool noLog = false)
        {
            PublishedFileId_t[] itemIds = { new PublishedFileId_t(itemId) };
            // Create query and enable all wanted returns
            UGCQueryHandle_t queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(itemIds, (uint)itemIds.Length);
            SteamUGC.SetReturnKeyValueTags(queryHandle, true);
            SteamUGC.SetReturnMetadata(queryHandle, true);

            SteamAPICall_t queryUgcRequestCall = SteamUGC.SendQueryUGCRequest(queryHandle);

            TaskCompletionSource<SteamUGCDetails_t> getSingleQueryUgcResultTask = new TaskCompletionSource<SteamUGCDetails_t>();
            // Handle query result and resolve the task
            CallResult<SteamUGCQueryCompleted_t> resultHandler = CallResult<SteamUGCQueryCompleted_t>.Create((pCallback, bIoFailure) =>
            {
                if (!noLog)
                {
                    Logger.LogInformation("[SteamUGCQueryCompleted]" +
                                      $" Result: {pCallback.m_eResult} ");
                }

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
                Thread.Sleep(100);
            }

            SteamUGC.ReleaseQueryUGCRequest(queryHandle);
            resultHandler.Dispose();
            return await getSingleQueryUgcResultTask.Task;
        }

        public async Task<List<PublishedFileId_t>> GetAllUserPublishedworkshopFiles(bool noLog = false)
        {
            List<PublishedFileId_t> publishedWorkshopFiles = new List<PublishedFileId_t>();
            try
            {
                uint startIndex = 0;
                bool moreFiles = true;
                while (moreFiles)
                {
                    List<PublishedFileId_t> tempPublishedWorkshopFiles = await GetUserPublishedworkshopFiles(startIndex, noLog);
                    foreach (PublishedFileId_t publishedFileId in tempPublishedWorkshopFiles)
                    {
                        if (publishedWorkshopFiles.Contains(publishedFileId))
                        {
                            moreFiles = false;
                        }
                        else if (publishedFileId.m_PublishedFileId > 0)
                        {
                            publishedWorkshopFiles.Add(publishedFileId);
                        }
                    }
                    if (tempPublishedWorkshopFiles.Count < 50)
                    {
                        moreFiles = false;
                    }
                    startIndex = (uint)(publishedWorkshopFiles.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
            return publishedWorkshopFiles;
        }

        public async Task<List<PublishedFileId_t>> GetUserPublishedworkshopFiles(uint startIndex = 0, bool noLog = false)
        {
            List<PublishedFileId_t> publishedWorkshopFiles = new List<PublishedFileId_t>();
            SteamAPICall_t remoteStorageEnumerateUserSharedWorkshopFilesResultCall = SteamRemoteStorage.EnumerateUserSharedWorkshopFiles(SteamUser.GetSteamID(), startIndex, null, null);
            TaskCompletionSource<bool> remoteStorageEnumerateUserSharedWorkshopFilesResultTask = new TaskCompletionSource<bool>();
            CallResult<RemoteStorageEnumerateUserSharedWorkshopFilesResult_t> remoteStorageEnumerateUserSharedWorkshopFilesResult = new CallResult<RemoteStorageEnumerateUserSharedWorkshopFilesResult_t>((pCallback, bIOFailure) =>
            {
                if (!noLog)
                {
                    Logger.LogInformation("[RemoteStorageEnumerateUserSharedWorkshopFilesResult]" +
                                      $" Result: {pCallback.m_eResult} ");
                }
                if (pCallback.m_eResult != EResult.k_EResultOK || bIOFailure)
                {
                    remoteStorageEnumerateUserSharedWorkshopFilesResultTask.TrySetException(
                        new Exception($"Enumerating files failed with result {pCallback.m_eResult}"));
                    return;
                }
                foreach (PublishedFileId_t _publishedFileId in pCallback.m_rgPublishedFileId)
                {
                    publishedWorkshopFiles.Add(_publishedFileId);
                }
                remoteStorageEnumerateUserSharedWorkshopFilesResultTask.SetResult(true);
            });
            remoteStorageEnumerateUserSharedWorkshopFilesResult.Set(remoteStorageEnumerateUserSharedWorkshopFilesResultCall);

            while (!remoteStorageEnumerateUserSharedWorkshopFilesResultTask.Task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            remoteStorageEnumerateUserSharedWorkshopFilesResult.Dispose();

            await remoteStorageEnumerateUserSharedWorkshopFilesResultTask.Task;
            return publishedWorkshopFiles;
        }

        // see https://github.com/Facepunch/Facepunch.Steamworks/blob/legacy/Facepunch.Steamworks/Client/RemoteStorage.cs#L15
        public static string NormalizePath(string path)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new FileInfo($"x:/{path}").FullName.Substring(3)
                : new FileInfo($"/x/{path}").FullName.Substring(3);
        }

        public async Task<bool> DeletePublishedFile(ulong publishedFileId)
        {
            TaskCompletionSource<bool> deleteFileTask = new TaskCompletionSource<bool>();
            // Handle query result and resolve the task
            CallResult<RemoteStorageDeletePublishedFileResult_t> deleteFileResult = CallResult<RemoteStorageDeletePublishedFileResult_t>.Create((pCallback, bIoFailure) =>
            {
                Logger.LogInformation("[RemoteStorageDeletePublishedFile]" +
                                          $" Result: {pCallback.m_eResult} " +
                                          $"- Published file id: {pCallback.m_nPublishedFileId}");

                if (pCallback.m_eResult != EResult.k_EResultOK)
                {
                    deleteFileTask.TrySetException(
                        new Exception($"Query failed with result {pCallback.m_eResult}"));
                    return;
                }

                deleteFileTask.SetResult(true);
            });
            deleteFileResult.Set(SteamRemoteStorage.DeletePublishedFile(new PublishedFileId_t(publishedFileId)));

            while (!deleteFileTask.Task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            deleteFileResult.Dispose();
            return await deleteFileTask.Task;
        }

        public async Task<string> SteamRemoteStorageUploadFile(string sPath)
        {
            if (!File.Exists(sPath))
            {
                throw new Exception($"There is no file under: \"{sPath}\"");
            }

            string name = (new FileInfo(sPath)).Name;
            string tempPath = NormalizePath($"theace0296_{name}");
            byte[] content = File.ReadAllBytes(sPath);
            Logger.LogInformation($"Uploading \"{name}\" to Steam cloud as \"{tempPath}\".");

            if (SteamRemoteStorage.FileExists(tempPath))
            {
                Logger.LogInformation($"Deleting existing file");
                SteamRemoteStorage.FileDelete(tempPath);
            }

            TaskCompletionSource<string> fileUploadTask = new TaskCompletionSource<string>();
            // Handle query result and resolve the task
            CallResult<RemoteStorageFileWriteAsyncComplete_t> fileUploadResult = CallResult<RemoteStorageFileWriteAsyncComplete_t>.Create((pCallback, bIoFailure) =>
            {
                Logger.LogInformation("[RemoteStorageFileWriteAsync]" +
                                    $" Result: {pCallback.m_eResult} ");

                if (pCallback.m_eResult != EResult.k_EResultOK)
                {
                    fileUploadTask.TrySetException(
                        new Exception($"Query failed with result {pCallback.m_eResult}"));
                    return;
                }

                fileUploadTask.SetResult(tempPath);
            });
            fileUploadResult.Set(SteamRemoteStorage.FileWriteAsync(tempPath, content, (uint)content.Length));

            while (!fileUploadTask.Task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            if (!SteamRemoteStorage.FileExists(tempPath))
            {
                throw new Exception("Steam file share to cloud failed!");
            }
            Logger.LogInformation($"Uploaded \"{name}\" to Steam cloud.");

            fileUploadResult.Dispose();
            return await fileUploadTask.Task;
        }

        public async Task<bool> SteamRemoteStorageUpload(string filePath, string previewPath, string title, string description, string[] pTags, ulong itemId = 0)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception($"There is no file under: \"{filePath}\"");
            }
            if (title == "")
            {
                throw new Exception($"No title provied!");
            }
            if (description == "")
            {
                throw new Exception($"No description provied!");
            }

            string contentTempPath = await SteamRemoteStorageUploadFile(filePath);

            string previewFileTempPath = null;
            if (File.Exists(previewPath))
            {
                previewFileTempPath = await SteamRemoteStorageUploadFile(previewPath);
            }

            TaskCompletionSource<bool> remoteStoragePublishOrUpdateTask = new TaskCompletionSource<bool>();
            CallResult<RemoteStoragePublishFileResult_t> remoteStoragePublishFileResult = null;
            CallResult<RemoteStorageUpdatePublishedFileResult_t> updatePublishedCallResult = null;
            if (itemId == 0)
            {
                Logger.LogInformation($"Publishing \"{title}\" to Steam workshop");
                SteamAPICall_t publishCall = SteamRemoteStorage.PublishWorkshopFile(
                    contentTempPath,
                    previewFileTempPath,
                    SteamUtils.GetAppID(),
                    title,
                    description,
                    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
                    new List<string>(pTags),
                    EWorkshopFileType.k_EWorkshopFileTypeCommunity
                );

                remoteStoragePublishFileResult = CallResult<RemoteStoragePublishFileResult_t>.Create((pCallback, bIOFailure) =>
                {
                    Logger.LogInformation("[RemoteStoragePublishFileResult]" +
                                          $" Result: {pCallback.m_eResult} " +
                                          $"- Published file id: {pCallback.m_nPublishedFileId}" +
                                          $"- Needs to accept workshop legal agreement: {pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement}");

                    if (pCallback.m_eResult != EResult.k_EResultOK)
                    {
                        remoteStoragePublishOrUpdateTask.TrySetException(
                            new Exception($"Uploading failed with result {pCallback.m_eResult}"));
                        return;
                    }

                    remoteStoragePublishOrUpdateTask.SetResult(true);
                });
                remoteStoragePublishFileResult.Set(publishCall);
            }
            else
            {
                PublishedFileUpdateHandle_t updateRequest = SteamRemoteStorage.CreatePublishedFileUpdateRequest(new PublishedFileId_t(itemId));
                if (!SteamRemoteStorage.UpdatePublishedFileFile(updateRequest, contentTempPath))
                {
                    throw new Exception("Steam file update failed!");
                }
                if (!SteamRemoteStorage.UpdatePublishedFilePreviewFile(updateRequest, previewFileTempPath))
                {
                    throw new Exception("Steam preview file update failed!");
                }
                SteamUGCDetails_t itemDetails = await GetSingleQueryUgcResult(itemId);
                List<string> combinedTags = new List<string>(pTags);
                string[] tags = itemDetails.m_rgchTags.Split(",");
                foreach (string tag in tags)
                {
                    if (!combinedTags.Contains(tag))
                    {
                        combinedTags.Add(tag);
                    }
                }
                if (!SteamRemoteStorage.UpdatePublishedFileTags(updateRequest, combinedTags))
                {
                    throw new Exception("Updating tags failed!");
                }

                Logger.LogInformation($"Updating \"{title}\" on Steam workshop");
                SteamAPICall_t uploadCall = SteamRemoteStorage.CommitPublishedFileUpdate(updateRequest);

                updatePublishedCallResult = CallResult<RemoteStorageUpdatePublishedFileResult_t>.Create((RemoteStorageUpdatePublishedFileResult_t pCallback, bool bIOFailure) =>
                {
                    Logger.LogInformation("[RemoteStorageUpdatePublishedFileResult]" +
                                          $" Result: {pCallback.m_eResult} " +
                                          $"- Published file id: {pCallback.m_nPublishedFileId}" +
                                          $"- Needs to accept workshop legal agreement: {pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement}");

                    if (pCallback.m_eResult != EResult.k_EResultOK)
                    {
                        remoteStoragePublishOrUpdateTask.TrySetException(
                            new Exception($"Uploading failed with result {pCallback.m_eResult}"));
                        return;
                    }

                    remoteStoragePublishOrUpdateTask.SetResult(true);
                });
                updatePublishedCallResult.Set(uploadCall);
            }

            while (!remoteStoragePublishOrUpdateTask.Task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            if (remoteStoragePublishOrUpdateTask.Task.IsFaulted)
            {
                throw new Exception("Publish/Update to Steam workshop failed!");
            }

            Logger.LogInformation("File published/updated on Steam workshop");

            if (remoteStoragePublishFileResult != null)
            {
                remoteStoragePublishFileResult.Dispose();
            }
            if (updatePublishedCallResult != null)
            {
                updatePublishedCallResult.Dispose();
            }

            DeleteStaleFiles();

            // wait for task
            return await remoteStoragePublishOrUpdateTask.Task;
        }

        public async Task<bool> SteamUGCUpload(string filePath, ulong itemId = 0, string changeNotes = "")
        {
            if (itemId == 0)
            {
                throw new Exception("Uploading new items without Workshop Id is not supported yet!");
            }

            if (!Directory.Exists(filePath))
            {
                throw new Exception($"There is no directory under: \"{filePath}\"");
            }

            UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), new PublishedFileId_t(itemId));
            if (!SteamUGC.SetItemContent(update, filePath))
            {
                throw new Exception("Item Content could not be set!");
            }

            SteamUGCDetails_t itemDetails = await GetSingleQueryUgcResult(itemId);
            // If uploading for Arma check for Scenario tag.
            // UGC upload over scenario is most likely a mistake which we will prevent.
            if (SteamUtils.GetAppID().m_AppId == (uint)Command.AppIds.Arma)
            {
                string[] tags = itemDetails.m_rgchTags.Split(",");
                if (tags.Contains("Scenario"))
                {
                    throw new Exception("Scenarios can\"t be uploaded via UGC, use --legacy mode!");
                }
            }

            SteamAPICall_t updateCall = SteamUGC.SubmitItemUpdate(update, changeNotes);

            TaskCompletionSource<bool> submitItemUpdateResultTask = new TaskCompletionSource<bool>();
            CallResult<SubmitItemUpdateResult_t> submitItemUpdateResult = CallResult<SubmitItemUpdateResult_t>.Create((pCallback, bIOFailure) =>
            {
                Logger.LogInformation("[OnSubmitItemUpdateResult]" +
                                $" Result: {pCallback.m_eResult}" +
                                $" - Published file id: {pCallback.m_nPublishedFileId}" +
                                $" - Needs to accept workshop legal agreement: {pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement}");

                if (pCallback.m_eResult != EResult.k_EResultOK)
                {
                    submitItemUpdateResultTask.TrySetException(
                        new Exception($"Uploading failed with result {pCallback.m_eResult}"));
                    return;
                }

                submitItemUpdateResultTask.TrySetResult(true);
            });
            submitItemUpdateResult.Set(updateCall);

            while (!submitItemUpdateResultTask.Task.IsCompleted)
            {
                Thread.Sleep(100);
            }

            // wait for task
            return await submitItemUpdateResultTask.Task;
        }
    }
}