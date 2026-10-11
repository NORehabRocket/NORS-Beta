using System;
using System.IO;

namespace NORS.Plugin.Net
{
    /// <summary>
    /// A drop-in list of moderator Steam ids, merged with <c>Moderation/Moderators</c>.
    ///
    /// Moderation in P2P is receiver-decided: your ban only applies to a player whose client lists
    /// you. So a server's moderator list has to reach every player's machine, and the only way to
    /// do that was to ship the whole <c>com.dsr.nors.cfg</c> — which overwrites everyone's keybinds,
    /// mic device and volumes along with it. Communities understandably didn't, and moderation
    /// quietly did nothing on the servers that needed it.
    ///
    /// One small file solves that: a modpack or server download drops it in, updating it touches
    /// nothing a player has configured, and it reloads in place when the file changes so staff can
    /// be added without anyone restarting.
    ///
    /// Format is one entry per line, '#' starts a comment, so it can carry names:
    ///
    ///     # Critz staff
    ///     76561198000000001   # Zookers
    ///     76561198000000002   # Tarragon
    /// </summary>
    internal sealed class ModeratorsFile
    {
        public const string FileName = "nors-moderators.txt";

        private readonly string _path;
        private DateTime _stampUtc;
        private long _length = -1;

        /// <summary>Raw contents, or "" when the file is absent. Parsed by ModerationAuthority.</summary>
        public string Text { get; private set; } = "";

        public bool Exists { get; private set; }

        public string Path => _path;

        public ModeratorsFile(string path)
        {
            _path = path;
            Reload();
        }

        /// <summary>
        /// Re-reads if the file changed. Cheap enough to call on a timer: it stats the file and
        /// only reads when the timestamp or length moved.
        /// </summary>
        public bool Poll()
        {
            try
            {
                var fi = new FileInfo(_path);
                if (!fi.Exists)
                {
                    if (!Exists) return false;
                    Exists = false; Text = ""; _length = -1; _stampUtc = default;
                    NorsPlugin.Log.LogInfo("NORS: " + FileName + " removed; moderators now come from config only.");
                    return true;
                }
                if (Exists && fi.LastWriteTimeUtc == _stampUtc && fi.Length == _length) return false;
                return Reload();
            }
            catch
            {
                return false;   // mid-write, locked, permissions: try again next poll
            }
        }

        private bool Reload()
        {
            try
            {
                var fi = new FileInfo(_path);
                if (!fi.Exists) { Exists = false; Text = ""; return false; }

                string raw = File.ReadAllText(_path);
                _stampUtc = fi.LastWriteTimeUtc;
                _length = fi.Length;
                Exists = true;

                if (raw == Text) return false;
                Text = raw;
                NorsPlugin.Log.LogInfo($"NORS: loaded moderators from {FileName}.");
                return true;
            }
            catch (Exception e)
            {
                NorsPlugin.Log.LogWarning($"NORS: could not read {FileName}: {e.Message}");
                return false;
            }
        }
    }
}
