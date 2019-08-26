using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace KP_Steam_Uploader.Command
{
    public abstract class AbstractKpSteamCommand
    {
        protected ILogger Logger;
        protected IConsole Console;
        protected virtual Task<int> OnExecute(CommandLineApplication app)
        {            
            return Task.FromResult(0);
        }
        
        protected void OnException(Exception ex)
        {
            OutputError(ex.Message);
            Logger.LogError(ex.Message);
            Logger.LogDebug(ex, ex.Message);
        }

        protected void OutputError(string message)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Error.WriteLine(message);
            Console.ResetColor();
        }
    }
}