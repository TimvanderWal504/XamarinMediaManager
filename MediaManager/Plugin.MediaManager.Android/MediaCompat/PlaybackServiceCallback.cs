using System;
using Android.Support.V4.Media.Session;

namespace Plugin.MediaManager.MediaCompat
{
    public interface PlaybackServiceCallback
    {
        void onPlaybackStart();

        void onNotificationRequired();

        void onPlaybackStop();

        void onPlaybackStateUpdated(PlaybackStateCompat newState);
    }
}
