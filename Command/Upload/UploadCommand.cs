using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
        }

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
                

                Console.Out.WriteLine($"Uploading item {ItemId} for app {AppId}");

                if (Legacy.hasValue)
                {
                    SteamRemoteStorageUpload();
                }
                else
                {
                    SteamUGCUpload();
                }
                
                Console.Out.WriteLine($"Upload finished");

                return 0;
            }
            catch (Exception ex)
            {
                OnException(ex);
                return 1;
            }
            
        }

        protected void SteamRemoteStorageUpload()
        {
            if (ItemId == 0)
            {
                throw new Exception("Uploading new files without Workshop Id is not supported yet!");
            }

            if (!File.Exists(Path))
            {
                throw new Exception($"There is no file under: \"{Path}\"");
            }
            
            var updateRequest = SteamRemoteStorage.CreatePublishedFileUpdateRequest(new PublishedFileId_t(ItemId));
            if (!SteamRemoteStorage.UpdatePublishedFileFile(updateRequest, Path))
            {
                throw new Exception("Steam file update failed!");
            }

            var uploadCall = SteamRemoteStorage.CommitPublishedFileUpdate(updateRequest);
            
            _updatePublishedCallResult = CallResult<RemoteStorageUpdatePublishedFileResult_t>.Create(OnRemoteStorageUpdatePublishedFileResult);
            _updatePublishedCallResult.Set(uploadCall);
            
            SteamAPI.RunCallbacks();
            SteamAPI.Shutdown();
        }
        
        void OnRemoteStorageUpdatePublishedFileResult(RemoteStorageUpdatePublishedFileResult_t pCallback, bool bIOFailure) {
            Logger.LogDebug("[" + RemoteStorageUpdatePublishedFileResult_t.k_iCallback + " - RemoteStorageUpdatePublishedFileResult] - " + pCallback.m_eResult + " -- " + pCallback.m_nPublishedFileId + " -- " + pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement);
        }
        
        private CallResult<RemoteStorageUpdatePublishedFileResult_t> _updatePublishedCallResult;
        
        protected void SteamUGCUpload()
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
            
            var updateCall = SteamUGC.SubmitItemUpdate(update, ChangeNotes);
            
            _submitItemUpdateResult = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitItemUpdateResult);
            _submitItemUpdateResult.Set(updateCall);
            
            SteamAPI.RunCallbacks();
            SteamAPI.Shutdown();
        }
        
        void OnSubmitItemUpdateResult(SubmitItemUpdateResult_t pCallback, bool bIOFailure) {
            Logger.LogDebug("[" + SubmitItemUpdateResult_t.k_iCallback + " - OnSubmitItemUpdateResult] - " + pCallback.m_eResult + " -- " + pCallback.m_nPublishedFileId + " -- " + pCallback.m_bUserNeedsToAcceptWorkshopLegalAgreement);
        }

        private CallResult<SubmitItemUpdateResult_t> _submitItemUpdateResult;
    }
}