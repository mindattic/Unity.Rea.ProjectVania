using System.Collections.Generic;
using System.IO;
using Unity.Rea.ProjectVania.Core.Settings;
using UnityEngine;

namespace Unity.Rea.ProjectVania
{
    /// <summary>The outcome of the start-up load: the settings, or every error that stopped it.</summary>
    public sealed class StartupResult
    {
        StartupResult(GameSettings settings, IReadOnlyList<string> errors, string source)
        {
            Settings = settings;
            Errors = errors;
            Source = source;
        }

        public GameSettings Settings { get; }
        public IReadOnlyList<string> Errors { get; }
        public string Source { get; }
        public bool Succeeded => Settings != null;

        internal static StartupResult Ok(GameSettings settings, string source) => new StartupResult(settings, new string[0], source);

        internal static StartupResult Failed(IReadOnlyList<string> errors, string source) => new StartupResult(null, errors, source);
    }

    /// <summary>
    /// The start-up load of the settings registry (U.VAN-T5): reads every file of StreamingAssets/Settings through
    /// <see cref="GameSettings"/> and, on any bad file, returns every error listed rather than the first, so the
    /// bootstrap can stop with all of them.
    /// </summary>
    public static class Startup
    {
        /// <summary>The settings folder in the running build (StreamingAssets/Settings, a plain folder on desktop builds and in the Editor).</summary>
        public static ISettingsSource StreamingSource() =>
            new DirectorySettingsSource(Path.Combine(Application.streamingAssetsPath, GameSettings.Folder));

        public static StartupResult LoadSettings(ISettingsSource source)
        {
            try
            {
                return StartupResult.Ok(GameSettings.Load(source), source.Describe);
            }
            catch (SettingsException e)
            {
                return StartupResult.Failed(e.Errors, source.Describe);
            }
        }
    }
}
