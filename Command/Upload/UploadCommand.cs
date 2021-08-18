using KP_Steam_Uploader.Util;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KP_Steam_Uploader.Command.Upload
{
    [Command(Name = "upload", Description = "Uploads file (legacy) or folder to Steam workshop.")]
    public class UploadCommand : AbstractKpSteamCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "app", Description = "Steam AppId")]
        public uint AppId { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "item", Description = "Workshop Id of item to update")]
        public ulong ItemId { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "p", LongName = "contentPath", Description = "Content path")]
        public string ContentPath { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "q", LongName = "previewPath", Description = "Preview file path")]
        public string PreviewPath { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "t", LongName = "title", Description = "Title for workshop item")]
        public string Title { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "d", LongName = "description", Description = "Description for workshop item")]
        public string Description { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "t", LongName = "tags", Description = "Tags for workshop item (comma seperated list, Example: \"Senario,Multiplayer,Singleplayer\")")]
        public string TagsString { get; set; }

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
            if (ItemId == 0 && Title == "" && Description == "" && TagsString == "")
            {
                Console.WriteLine("No ItemId or Item Attributes provided!");
                Console.Write("Please specify an ItemId or Title: ");
                var input = Console.In.ReadLine();
                if (ulong.TryParse(input, out ulong result))
                {
                    ItemId = result;
                }
                else
                {
                    Console.WriteLine("String detected, assuming it was a title...");
                    Console.Write("Please specify a Description: ");
                    Description = Console.In.ReadLine();
                    Console.Write("Please specify the tags as a comma-separated list: ");
                    TagsString = Console.In.ReadLine();
                }
            }

            var pTags = new List<string>(TagsString.Split(","));
            var appRunning = true;
            Thread callbackThread = null;
            try
            {
                WriteSteamAppId(AppId);
                InitializeSteam();
                callbackThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (appRunning)
                    {
                        Logger.LogDebug("Running callbacks");
                        SteamAPI.RunCallbacks();
                        Thread.Sleep(100);
                        SteamAPI.RestartAppIfNecessary(new AppId_t(AppId));
                    }
                });
                callbackThread.Start();

                Logger.LogInformation($"User has {SteamRemoteStorage.GetFileCount()} stale files in Steam Cloud.");
                SteamUgcUtil.DeleteStaleFiles();

                if (Legacy.hasValue)
                {
                    if (ItemId != 0)
                    {
                        Logger.LogInformation($"Uploading item {ItemId} for app {AppId}");
                        await SteamUgcUtil.SteamRemoteStorageUpload(ContentPath, PreviewPath, Title, Description, pTags.ToArray(), ItemId);
                    }
                    else
                    {
                        Logger.LogInformation($"Publishing item \"{Title}\" for app {AppId}");
                        await SteamUgcUtil.SteamRemoteStorageUpload(ContentPath, PreviewPath, Title, Description, pTags.ToArray(), 0);
                    }
                }
                else
                {
                    Logger.LogInformation($"Uploading item {ItemId} for app {AppId}");
                    await SteamUgcUtil.SteamUGCUpload(ContentPath, ItemId, ChangeNotes);
                }
                Logger.LogInformation($"Upload finished");

                SteamUgcUtil.DeleteStaleFiles();
                appRunning = false;
                callbackThread.Join();
                SteamAPI.Shutdown();
                return 0;
            }
            catch (Exception ex)
            {
                OnException(ex);
                SteamUgcUtil.DeleteStaleFiles();
                appRunning = false;
                callbackThread.Abort();
                SteamAPI.Shutdown();
                return 1;
            }
        }
    }
}