using System;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace KP_Steam_Uploader.Command.Upload
{
    [Command(Name = "upload", Description = "Uploads file (legacy) or folder to Steam workshop.")]
    public class DownloadCommand: AbstractKpSteamCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "app", Description = "Steam AppId")]
        public uint AppId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "item", Description = "Workshop Id of item to update")]
        public ulong ItemId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "p", LongName = "item", Description = "Content path")]
        public string Path { get; set; }
        
        [Option(CommandOptionType.SingleOrNoValue, LongName = "legacy", Description = "Legacy, single file based upload mode.")]
        public (bool hasValue, string value) Legacy { get; }

        public DownloadCommand(ILogger<DownloadCommand> logger, IConsole console)
        {
            Logger = logger;
            Console = console;
        }

        protected override async Task<int> OnExecute(CommandLineApplication app)
        {
            Logger.LogInformation("Executing UploadCommand");

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
                    throw new Exception("SteamUGC Upload not implemented yet!");
                }

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
            if (AppId == 0)
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
                throw new Exception("Steam file upload failed!");
            }
        }
    }
}