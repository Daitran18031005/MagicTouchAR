using System;
using System.Text;
using System.Threading;

namespace MagicTouch.Tracking
{
    /// <summary>Keep packets from old/manual streamers out of the current game's input.</summary>
    public static class TrackingStreamSession
    {
        private static volatile string _activeId = "";
        private static int _ignoredLandmarks, _ignoredVideo;
        public static string ActiveId => _activeId;
        public static int IgnoredLandmarkPackets => Volatile.Read(ref _ignoredLandmarks);
        public static int IgnoredVideoPackets => Volatile.Read(ref _ignoredVideo);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            _activeId = "";
            Interlocked.Exchange(ref _ignoredLandmarks, 0);
            Interlocked.Exchange(ref _ignoredVideo, 0);
        }

        public static string Begin()
        {
            _activeId = Guid.NewGuid().ToString("N");
            Interlocked.Exchange(ref _ignoredLandmarks, 0);
            Interlocked.Exchange(ref _ignoredVideo, 0);
            return _activeId;
        }

        // No active auto-launch session preserves compatibility with manual streamers.
        public static bool Matches(string expected, string received)
            => string.IsNullOrEmpty(expected) || string.Equals(expected, received, StringComparison.Ordinal);

        public static bool AcceptLandmarks(string received)
        {
            if (Matches(_activeId, received)) return true;
            Interlocked.Increment(ref _ignoredLandmarks);
            return false;
        }

        public static bool AcceptVideo(byte[] packet, out byte[] jpeg)
        {
            if (TryUnwrapVideo(packet, _activeId, out jpeg)) return true;
            Interlocked.Increment(ref _ignoredVideo);
            return false;
        }

        // New packets: ASCII "MTAR" + 32-character session ID + JPEG.
        // An untagged JPEG is accepted only when Unity did not auto-launch a session.
        public static bool TryUnwrapVideo(byte[] packet, string expected, out byte[] jpeg)
        {
            jpeg = null;
            if (packet == null || packet.Length < 2) return false;
            bool tagged = packet.Length >= 4 && packet[0] == 'M' && packet[1] == 'T'
                && packet[2] == 'A' && packet[3] == 'R';
            if (!tagged)
            {
                if (!string.IsNullOrEmpty(expected) || packet[0] != 0xff || packet[1] != 0xd8) return false;
                jpeg = packet;
                return true;
            }
            if (packet.Length < 38 || !Matches(expected, Encoding.ASCII.GetString(packet, 4, 32))
                || packet[36] != 0xff || packet[37] != 0xd8) return false;
            jpeg = new byte[packet.Length - 36];
            Buffer.BlockCopy(packet, 36, jpeg, 0, jpeg.Length);
            return true;
        }
    }
}
