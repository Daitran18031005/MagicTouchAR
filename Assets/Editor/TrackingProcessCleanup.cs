using UnityEditor;
using MagicTouch.Tracking;

// Static Process handles are lost on assembly reload. Shut down their owned
// streamers first, otherwise stale senders can survive after leaving Play mode.
[InitializeOnLoad]
public static class TrackingProcessCleanup
{
    static TrackingProcessCleanup()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopOwnedStreamers;
        EditorApplication.quitting += StopOwnedStreamers;
    }

    private static void StopOwnedStreamers()
    {
        HandTrackingBridge.TerminatePythonProcess();
        MediaPipeUDPReceiver.TerminateExternalTrackerProcess();
    }
}
