using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KP_Steam_Uploader.Util;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace KP_Steam_Uploader.Command.Upload
{
    [Command(Name = "upload", Description = "Uploads file (legacy) or folder to Steam workshop.")]
    public class UploadCommand: AbstractKpSteamCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "app", Description = "Steam AppId")]
        public uint AppId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "item", Description = "Workshop Id of item to update")]
        public ulong ItemId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "p", LongName = "path", Description = "Content path")]
        public string Path { get; set; }
        
        [Option(CommandOptionType.SingleOrNoValue, LongName = "legacy", Description = "Legacy, single file based upload mode.")]
        public (bool hasValue, string value) Legacy { get; }

        [Option(CommandOptionType.SingleValue, ShortName = "c", LongName = "changenotes", Description = "Change Notes")]
        public string ChangeNotes { get; set; } = "";

        public UploadCommand(ILogger<UploadCommand> logger, IConsole console)
        {
            Logger = logger;
            Console = console;
            SteamUgcUtil = new SteamUgc(logger);
        }

        protected SteamUgc SteamUgcUtil;

        protected override async Task<int> OnExecute(CommandLineApplication app)
        {
            Logger.LogInformation("Executing UploadCommand");
            
            if (AppId == 0)
            {
                Console.WriteLine("Arma3 - 107410");
                
                Console.Write("Please specify AppId: ");
                var appId = Console.In.ReadLine();
                AppId = uint.Parse(appId);
            }
            
            if (ItemId == 0)
            {
                Console.Out.Write("Please specify ItemId: ");
                var itemId = Console.In.ReadLine();
                ItemId = uint.Parse(itemId);
            }

            try
            {
                WriteSteamAppId(AppId);
                InitializeSteam();
                

                Logger.LogInformation($"Uploading item {ItemId} for app {AppId}");

                if (Legacy.hasValue)
                {
                    await SteamRemoteStorageUpload();
                }
                else
                {
                    await SteamUGCUpload();
                }
                
                Logger.LogInformation($"Upload finished");

                return 0;
            }
            catch (Exception ex)
            {
                OnException(ex);
                return 1;
            }
            
        }

        protected async Task SteamRemoteStorageUpload()
        {
            if (ItemId == 0)
            {
                throw new Exception("Uploading new files without Workshop Id is not supported yet!");
            }

            if (!File.Exists(Path))
            {
                throw new Exception($"There is no file under: \"{Path}\"");
            }

            var fileName = (new FileInfo(Path)).Name;
            var tempPath = NormalizePath($"kp-steam-tmp/{fileName}");
            var fileContent = File.ReadAllBytes(Path);
            
            Logger.LogDebug($"Copying file to temp path \"{tempPath}\"");
            if (!SteamRemoteStorage.FileWrite(tempPath, fileContent, fileContent.Length))
            {
                throw new Exception("Could not move file to temporary path");
            }

            var updateRequest = SteamRemoteStorage.CreatePublishedFileUpdateRequest(new PublishedFileId_t(ItemId));
            if (!SteamRemoteStorage.UpdatePublishedFileFile(updateRequest, tempPath))
            {
                throw new Exception("Steam file update failed!");
            }

            var uploadCall = SteamRemoteStorage.CommitPublishedFileUpdate(updateRequest);
            
            _updatePublishedCallResultTask = new TaskCompletionSource<bool>();
            _updatePublishedCallResult = CallResult<RemoteStorageUpdatePublishedFileResult_t>.Create(OnRemoteStorageUpdatePublishedFileResult);
            _updatePublishedCallResult.Set(uploadCall);
            
            // todo move callbacks loop to some better place
            while (!_updatePublishedCallResultTask.Task.IsCompleted)
            {
                Logger.LogDebug("Running callbacks");
                SteamAPI.RunCallbacks();
                Thread.Sleep(100);
            }
        
            if (SteamRemoteStorage.FileExists(tempPath))
            {
                SteamRemoteStorage.FileDelete(tempPath);
            }
            
            SteamAPI.Shutdown();

            // wait for task
            await _updatePublishedCallResultTask.Task;
        }
        
        void OnRemoteStorageUpdatePublishedFileResult(RemoteStorageUpdatePublishedFileResult_t pCallback, bool bIOFailure) {
            Logger.LogInformation("[RemoteStorageUpdatePublishedFileResult]" +
                                  $" Result: {pCallback.m_eResult} " +
                                  $"- Published file id: {pCallback.m_nPublishedFileId}" +
                                  $"- Needs to accept workshop legal agreement: {pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement}");
            
            if (pCallback.m_eResult != EResult.k_EResultOK)
            {
                _updatePublishedCallResultTask.TrySetException(
                    new Exception($"Uploading failed with result {pCallback.m_eResult}"));
                return;
            }

            _updatePublishedCallResultTask.TrySetResult(true);
        }
        
        private CallResult<RemoteStorageUpdatePublishedFileResult_t> _updatePublishedCallResult;
        private TaskCompletionSource<bool> _updatePublishedCallResultTask;
        
        protected async Task SteamUGCUpload()
        {
            if (ItemId == 0)
            {
                throw new Exception("Uploading new items without Workshop Id is not supported yet!");
            }

            if (!Directory.Exists(Path))
            {
                throw new Exception($"There is no directory under: \"{Path}\"");
            }
            
            var update = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(ItemId));
            if (!SteamUGC.SetItemContent(update, Path))
            {
                throw new Exception("Item Content could not be set!");
            }

            var itemDetails = await SteamUgcUtil.GetSingleQueryUgcResult(ItemId);
            // If uploading for Arma check for Scenario tag.
            // UGC upload over scenario is most likely a mistake which we will prevent.
            if (AppId == (uint)AppIds.Arma)
            {
                var tags = itemDetails.m_rgchTags.Split(',');
                if (tags.Contains("Scenario"))
                {
                    throw new Exception("Scenaarios can't be uploaded via UGC, use --legacy mode!");                    
                }
            }

            var updateCall = SteamUGC.SubmitItemUpdate(update, ChangeNotes);
            
            _submitItemUpdateResultTask = new TaskCompletionSource<bool>();
            _submitItemUpdateResult = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitItemUpdateResult);
            _submitItemUpdateResult.Set(updateCall);

            // todo move callbacks loop to some better place
            while (!_submitItemUpdateResultTask.Task.IsCompleted)
            {
                Logger.LogDebug("Running callbacks");
                SteamAPI.RunCallbacks();
                Thread.Sleep(100);
            }
            
            SteamAPI.Shutdown();

            // wait for task
            await _submitItemUpdateResultTask.Task;
        }
        
        void OnSubmitItemUpdateResult(SubmitItemUpdateResult_t pCallback, bool bIOFailure) {
            Logger.LogDebug("[OnSubmitItemUpdateResult]" +
                            $" Result: {pCallback.m_eResult}" +
                            $"- Published file id: {pCallback.m_nPublishedFileId}" +
                            $"- Needs to accept workshop legal agreement: {pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement}");
            
            if (pCallback.m_eResult != EResult.k_EResultOK)
            {
                _submitItemUpdateResultTask.TrySetException(
                    new Exception($"Uploading failed with result {pCallback.m_eResult}"));
                return;
            }

            _submitItemUpdateResultTask.TrySetResult(true);
        }
        
        private CallResult<SubmitItemUpdateResult_t> _submitItemUpdateResult;
        private TaskCompletionSource<bool> _submitItemUpdateResultTask;
        
        // see https://github.com/Facepunch/Facepunch.Steamworks/blob/legacy/Facepunch.Steamworks/Client/RemoteStorage.cs#L15
        public static string NormalizePath(string path)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new FileInfo($"x:/{path}").FullName.Substring(3)
                : new FileInfo($"/x/{path}").FullName.Substring(3);
        }
        
    }
}