using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace KP_Steam_Uploader.Command.Download
{
    [Command(Name = "download", Description = "Queues Steam Workshop item for download without subscribing the item.")]
    public class DownloadCommand: AbstractKpSteamCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "app", Description = "Steam AppId")]
        public uint AppId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "item", Description = "Workshop Id of item to download")]
        public ulong ItemId { get; set; }

        public DownloadCommand(ILogger<DownloadCommand> logger, IConsole console)
        {
            Logger = logger;
            Console = console;
        }

        protected override async Task<int> OnExecute(CommandLineApplication app)
        {
            Logger.LogInformation($"Downloading item {ItemId} for app {AppId}");
            Console.Out.WriteLine($"Downloading item {ItemId} for app {AppId}");

            try
            {
                InitializeSteam();
                WriteSteamAppId(AppId);
                
                if (!SteamUGC.DownloadItem(new PublishedFileId_t(ItemId), true))
                {
                    throw new Exception($"Could not download item {ItemId}");
                }
                
                Console.Out.WriteLine($"Item {ItemId} queued for download in Steam");

                return 0;
            }
            catch (Exception ex)
            {
                OnException(ex);
                return 1;
            }
            
        }
    }
}