using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace KP_Steam_Uploader.Command.Download
{
    [Command(Name = "download", Description = "Download Steam Workshop item")]
    public class DownloadCommand: AbstractKpSteamCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "app", Description = "Steam AppId")]
        public int AppId { get; set; }
        
        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "item", Description = "Workshop Id of item to download")]
        public int ItemId { get; set; }

        public DownloadCommand(ILogger<DownloadCommand> logger, IConsole console)
        {
            Logger = logger;
            Console = console;
        }

        protected override async Task<int> OnExecute(CommandLineApplication app)
        {
            Logger.LogInformation($"Downloading item {ItemId} for app {AppId}");
            try
            {
                throw new System.NotImplementedException();
            }
            catch (Exception ex)
            {
                OnException(ex);
                return 1;
            }
            
        }
    }
}