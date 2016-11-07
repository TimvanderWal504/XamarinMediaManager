using System;
namespace Plugin.MediaManager.MediaCompat
{
    public interface Playback
    {
        /**
     * Start/setup the playback.
     * Resources/listeners would be allocated by implementations.
     */
        void start();

        /**
         * Stop the playback. All resources can be de-allocated by implementations here.
         * @param notifyListeners if true and a callback has been set by setCallback,
         *                        callback.onPlaybackStatusChanged will be called after changing
         *                        the state.
         */
        void stop(bool notifyListeners);

        /**
         * Set the latest playback state as determined by the caller.
         */
        void setState(int state);

        /**
         * Get the current {@link android.media.session.PlaybackState#getState()}
         */
        int getState();

        /**
         * @return boolean that indicates that this is ready to be used.
         */
        bool isConnected();

        /**
         * @return boolean indicating whether the player is playing or is supposed to be
         * playing when we gain audio focus.
         */
        bool isPlaying();

        /**
         * @return pos if currently playing an item
         */
        int getCurrentStreamPosition();

        /**
         * Set the current position. Typically used when switching players that are in
         * paused state.
         *
         * @param pos position in the stream
         */
        void setCurrentStreamPosition(int pos);

        /**
         * Query the underlying stream and update the internal last known stream position.
         */
        void updateLastKnownStreamPosition();

        /**
         * @param item to play
         */
        void play(Android.Support.V4.Media.Session.MediaSessionCompat.QueueItem item);

        /**
         * Pause the current playing item
         */
        void pause();

        /**
         * Seek to the given position
         */
        void seekTo(int position);

        /**
         * Set the current mediaId. This is only used when switching from one
         * playback to another.
         *
         * @param mediaId to be set as the current.
         */
        void setCurrentMediaId(String mediaId);

        /**
         *
         * @return the current media Id being processed in any state or null.
         */
        String getCurrentMediaId();

        /**
         * @param callback to be called
         */
        void setCallback(PlaybackCallback callback);
    }

    public interface PlaybackCallback
    {
        /**
         * On current music completed.
         */
        void onCompletion();
        /**
         * on Playback status changed
         * Implementations can use this callback to update
         * playback state on the media sessions.
         */
        void onPlaybackStatusChanged(int state);

        /**
         * @param error to be added to the PlaybackState
         */
        void onError(String error);

        /**
         * @param mediaId being currently played
         */
        void setCurrentMediaId(String mediaId);
    }
}
