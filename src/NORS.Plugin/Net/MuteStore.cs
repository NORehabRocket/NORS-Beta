using System.Collections.Generic;
using NORS.Common;

namespace NORS.Plugin.Net
{
    /// <summary>
    /// Temporary mutes this client has issued as a moderator.
    ///
    /// Deliberately NOT persisted, unlike <see cref="HostBanStore"/>. A ten-minute mute that
    /// survived a restart and came back as "ten minutes from whenever you next launch" would be a
    /// worse tool than a ban, because nobody could predict when it ended. Temporary means temporary;
    /// if it should outlast the session it should have been a ban.
    ///
    /// Stored as an absolute local expiry time and broadcast as seconds remaining, so peers never
    /// have to agree with our clock.
    /// </summary>
    internal sealed class MuteStore
    {
        private struct Entry
        {
            public float ExpiresAt;    // Time.unscaledTime on this machine
            public string Reason;
        }

        private readonly Dictionary<ulong, Entry> _mutes = new Dictionary<ulong, Entry>();
        private readonly List<ulong> _expired = new List<ulong>();

        /// <summary>Bumped when the set changes, so the hub rebuilds only when it needs to.</summary>
        public int Revision { get; private set; }

        public int Count => _mutes.Count;

        public void Mute(ulong steamId, float seconds, string reason, float now)
        {
            if (steamId == 0 || seconds <= 0f) return;
            _mutes[steamId] = new Entry { ExpiresAt = now + seconds, Reason = reason ?? "" };
            Revision++;
        }

        public bool Unmute(ulong steamId)
        {
            if (!_mutes.Remove(steamId)) return false;
            Revision++;
            return true;
        }

        /// <summary>Seconds left on a mute, or 0 if this id isn't muted by us.</summary>
        public float Remaining(ulong steamId, float now)
        {
            if (!_mutes.TryGetValue(steamId, out var e)) return 0f;
            float left = e.ExpiresAt - now;
            return left > 0f ? left : 0f;
        }

        public string ReasonFor(ulong steamId)
        {
            Entry e;
            return _mutes.TryGetValue(steamId, out e) ? e.Reason : null;
        }

        /// <summary>Drops anything that has run out. Bumps Revision only if something actually went.</summary>
        public void Expire(float now)
        {
            if (_mutes.Count == 0) return;
            _expired.Clear();
            foreach (var kv in _mutes) if (kv.Value.ExpiresAt <= now) _expired.Add(kv.Key);
            if (_expired.Count == 0) return;
            foreach (ulong id in _expired) _mutes.Remove(id);
            Revision++;
        }

        /// <summary>Fills a wire array with the live mutes. Returns how many were written.</summary>
        public int CopyToWire(Packets.MuteEntry[] buffer, float now)
        {
            int n = 0;
            foreach (var kv in _mutes)
            {
                if (n >= buffer.Length) break;
                float left = kv.Value.ExpiresAt - now;
                if (left <= 0f) continue;
                // Round up, so a mute never advertises 0 seconds while still being enforced here.
                int secs = (int)(left + 0.999f);
                if (secs > ushort.MaxValue) secs = ushort.MaxValue;
                buffer[n++] = new Packets.MuteEntry
                {
                    SteamId = kv.Key,
                    Seconds = (ushort)secs,
                    Reason = kv.Value.Reason,
                };
            }
            return n;
        }

        public void CopyIdsTo(List<ulong> outList)
        {
            foreach (var kv in _mutes) outList.Add(kv.Key);
        }

        public void Clear()
        {
            if (_mutes.Count == 0) return;
            _mutes.Clear();
            Revision++;
        }
    }
}
