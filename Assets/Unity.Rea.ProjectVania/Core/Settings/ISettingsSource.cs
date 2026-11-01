using System;
using System.Collections.Generic;
using System.IO;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>
    /// Where Core reads its data files from. Core only ever asks for a file by name ("chain.json") and
    /// never knows whether it came from disk, StreamingAssets, a test or (later) a server, so the rules
    /// stay engine-free. <see cref="GameSettings.Load"/> is the one reader.
    /// </summary>
    public interface ISettingsSource
    {
        /// <summary>A description of the source for error messages (a folder path, "memory").</summary>
        string Describe { get; }

        /// <summary>The text of the file called <paramref name="name"/>, or null if there is no such file.</summary>
        string Read(string name);
    }

    /// <summary>Data files in a folder on disk (the build's StreamingAssets/Settings, or the repo's copy in tests).</summary>
    public sealed class DirectorySettingsSource : ISettingsSource
    {
        public DirectorySettingsSource(string directory)
        {
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public string Directory { get; }

        public string Describe => Directory;

        public string Read(string name)
        {
            var path = Path.Combine(Directory, name);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    /// <summary>Data files held in memory: tests, and a source that patches a few files over another.</summary>
    public sealed class MemorySettingsSource : ISettingsSource
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        private readonly ISettingsSource _fallback;

        /// <param name="fallback">Read for any file not set here (null: none).</param>
        public MemorySettingsSource(ISettingsSource fallback = null)
        {
            _fallback = fallback;
        }

        public string Describe => _fallback != null ? $"memory over {_fallback.Describe}" : "memory";

        /// <summary>Sets (or, with null, hides) the file called <paramref name="name"/>.</summary>
        public MemorySettingsSource Set(string name, string text)
        {
            _files[name] = text;
            return this;
        }

        public string Read(string name)
        {
            if (_files.TryGetValue(name, out var text))
            {
                return text;
            }

            return _fallback?.Read(name);
        }
    }

    /// <summary>
    /// The data files failed to load. <see cref="Errors"/> lists every problem found, each naming its
    /// file, key and line, so one run reports everything wrong rather than the first thing.
    /// </summary>
    public sealed class SettingsException : Exception
    {
        public SettingsException(IReadOnlyList<string> errors, string source)
            : base(Format(errors, source))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }

        private static string Format(IReadOnlyList<string> errors, string source)
        {
            var heading = errors.Count == 1
                ? $"The settings in {source} are invalid:"
                : $"The settings in {source} are invalid ({errors.Count} problems):";
            return heading + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", errors);
        }
    }
}
