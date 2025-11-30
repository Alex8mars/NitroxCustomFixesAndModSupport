using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nitrox.Model.Helper;
using Nitrox.Model.Platforms.Discovery.Models;
using Nitrox.Model.Platforms.OS.Shared;
using Nitrox.Model.Platforms.Store.Interfaces;

namespace Nitrox.Model.Platforms.Store;

public sealed class EpicGames : IGamePlatform
{
    public string Name => "Epic Games Store";
    public Platform Platform => Platform.EPIC;

    public bool OwnsGame(string gameDirectory)
    {
        string path = Path.Combine(gameDirectory, ".egstore");
        return Directory.Exists(path) && Directory.GetFiles(path).Length > 1;
    }

    public static async Task<ProcessEx> StartGameAsync(string pathToGameExe, string launchArguments)
    {
        // Normally should call StartPlatformAsync first. But Subnautica will start without EGS.
        string? gameDirectory = Path.GetDirectoryName(pathToGameExe);
        Dictionary<string, string> environment = new()
        {
            [NitroxUser.LAUNCHER_PATH_ENV_KEY] = NitroxUser.LauncherPath
        };
        BepInExIntegration.ApplyEnvironment(environment, gameDirectory);

        return await Task.FromResult(
            ProcessEx.Start(
                pathToGameExe,
                environment.Select(kv => (kv.Key, kv.Value)),
                gameDirectory,
                $"-EpicPortal -epicuserid=0 {launchArguments}")
        );
    }
}
