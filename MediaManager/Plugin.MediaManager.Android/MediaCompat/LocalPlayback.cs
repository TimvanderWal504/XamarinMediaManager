using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Text;
using Java.IO;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.EventArguments;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager.MediaCompat
{
    public class LocalPlayback : Java.Lang.Object, IAudioPlayer, AudioManager.IOnAudioFocusChangeListener,
        MediaPlayer.IOnCompletionListener, MediaPlayer.IOnErrorListener, MediaPlayer.IOnPreparedListener, MediaPlayer.IOnSeekCompleteListener
    {
        // The volume we set the media player to when we lose audio focus, but are
        // allowed to reduce the volume instead of stopping playback.
        public static float VOLUME_DUCK = 0.2f;
        // The volume we set the media player when we have audio focus.
        public static float VOLUME_NORMAL = 1.0f;

        // we don't have audio focus, and can't duck (play at a low volume)
        private static int AUDIO_NO_FOCUS_NO_DUCK = 0;
        // we don't have focus, but can duck (play at a low volume)
        private static int AUDIO_NO_FOCUS_CAN_DUCK = 1;
        // we have full audio focus
        private static int AUDIO_FOCUSED = 2;

        private Context mContext;
        private WifiManager.WifiLock mWifiLock;
        private int mState;
        private bool mPlayOnFocusGain;
        //private Callback mCallback;
        //private MusicProvider mMusicProvider;
        private volatile bool mAudioNoisyReceiverRegistered;
        private volatile int mCurrentPosition;
        private volatile String mCurrentMediaId;

        // Type of audio focus we have:
        private int mAudioFocus = AUDIO_NO_FOCUS_NO_DUCK;
        private AudioManager mAudioManager;
        private MediaPlayer mMediaPlayer;

        private IntentFilter mAudioNoisyIntentFilter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
        private BroadcastReceiver mAudioNoisyReceiver;

        public LocalPlayback(Context context)
        {
            this.mContext = context;
            mAudioNoisyReceiver = new AudioPlayerBroadcastReceiver(mContext, this);

            //this.mMusicProvider = musicProvider;
            this.mAudioManager = (AudioManager)context.GetSystemService(Context.AudioService);
            // Create the Wifi lock (this does not acquire the lock, this just creates it)
            this.mWifiLock = ((WifiManager)context.GetSystemService(Context.WifiService))
                    .CreateWifiLock(Android.Net.WifiMode.Full, "uAmp_lock");
            this.mState = PlaybackStateCompat.StateNone;
        }

        public TimeSpan Buffered
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public TimeSpan Duration
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public TimeSpan Position
        {
            get
            {
                return TimeSpan.FromMilliseconds(mMediaPlayer != null ?
                                                 mMediaPlayer.CurrentPosition : mCurrentPosition);
            }
        }

        public void updateLastKnownStreamPosition()
        {
            if (mMediaPlayer != null)
            {
                mCurrentPosition = mMediaPlayer.CurrentPosition;
            }
        }

        public MediaPlayerStatus Status
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event BufferingChangedEventHandler BufferingChanged;
        public event MediaFailedEventHandler MediaFailed;
        public event MediaFinishedEventHandler MediaFinished;
        public event PlayingChangedEventHandler PlayingChanged;
        public event StatusChangedEventHandler StatusChanged;

        public async Task Pause()
        {
            if (mState == PlaybackStateCompat.StatePlaying)
            {
                // Pause media player and cancel the 'foreground service' state.
                if (mMediaPlayer != null && mMediaPlayer.IsPlaying)
                {
                    mMediaPlayer.Pause();
                    mCurrentPosition = mMediaPlayer.CurrentPosition;
                }
                // while paused, retain the MediaPlayer but give up audio focus
                relaxResources(false);
                giveUpAudioFocus();
            }
            mState = PlaybackStateCompat.StatePaused;
            StatusChanged?.Invoke(this, new StatusChangedEventArgs(GetStatusByCompatValue(mState)));
            unregisterAudioNoisyReceiver();
        }

        public async Task Play(IMediaFile mediaFile)
        {
            mPlayOnFocusGain = true;
            tryToGetAudioFocus();
            registerAudioNoisyReceiver();
            string mediaId = mediaFile.Id.ToString(); // item.getDescription().getMediaId();
            bool mediaHasChanged = !TextUtils.Equals(mediaId, mCurrentMediaId);
            if (mediaHasChanged)
            {
                mCurrentPosition = 0;
                mCurrentMediaId = mediaId;
            }

            if (mState == PlaybackStateCompat.StatePaused && !mediaHasChanged && mMediaPlayer != null)
            {
                configMediaPlayerState();
            }
            else {
                mState = PlaybackStateCompat.StateStopped;
                relaxResources(false); // release everything except MediaPlayer
                //MediaMetadataCompat track = mMusicProvider.getMusic(MediaIDHelper.extractMusicIDFromMediaID(item.getDescription().getMediaId()));

                //noinspection ResourceType
                string source = mediaFile.Url; //track.getString(MusicProviderSource.CUSTOM_METADATA_TRACK_SOURCE);

                try
                {
                    createMediaPlayerIfNeeded();

                    mState = PlaybackStateCompat.StateBuffering;

                    mMediaPlayer.SetAudioStreamType(Stream.Music);
                    await mMediaPlayer.SetDataSourceAsync(source);

                    // Starts preparing the media player in the background. When
                    // it's done, it will call our OnPreparedListener (that is,
                    // the onPrepared() method on this class, since we set the
                    // listener to 'this'). Until the media player is prepared,
                    // we *cannot* call start() on it!
                    mMediaPlayer.PrepareAsync();

                    // If we are streaming from the internet, we want to hold a
                    // Wifi lock, which prevents the Wifi radio from going to
                    // sleep while the song is playing.
                    mWifiLock.Acquire();

                    StatusChanged?.Invoke(this, new StatusChangedEventArgs(GetStatusByCompatValue(mState)));

                }
                catch (IOException ex)
                {
                    MediaFailed?.Invoke(this, new MediaFailedEventArgs("", ex));
                }
            }
        }

        public async Task Seek(TimeSpan position)
        {
            if (mMediaPlayer == null)
            {
                // If we do not have a current media player, simply update the current position
                mCurrentPosition = Convert.ToInt32(position.TotalMilliseconds);
            }
            else {
                if (mMediaPlayer.IsPlaying)
                {
                    mState = PlaybackStateCompat.StateBuffering;
                }
                mMediaPlayer.SeekTo(Convert.ToInt32(position.TotalMilliseconds));
                StatusChanged?.Invoke(this, new StatusChangedEventArgs(GetStatusByCompatValue(mState)));
            }
        }

        public async Task Stop()
        {
            mState = PlaybackStateCompat.StateStopped;
            //if (notifyListeners && mCallback != null)
            {
                //mCallback.onPlaybackStatusChanged(mState);
            }
            //mCurrentPosition = getCurrentStreamPosition();
            // Give up Audio focus
            giveUpAudioFocus();
            unregisterAudioNoisyReceiver();
            // Relax all resources
            relaxResources(true);
        }

        public void setState(int state)
        {
            this.mState = state;
        }

        public int getState()
        {
            return mState;
        }

        public bool isConnected()
        {
            return true;
        }

        public bool isPlaying()
        {
            return mPlayOnFocusGain || (mMediaPlayer != null && mMediaPlayer.IsPlaying);
        }

        /**
     * Try to get the system audio focus.
     */
        private void tryToGetAudioFocus()
        {
            if (mAudioFocus != AUDIO_FOCUSED)
            {
                var result = mAudioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
                if (result == AudioFocusRequest.Granted)
                {
                    mAudioFocus = AUDIO_FOCUSED;
                }
            }
        }

        /**
         * Give up the audio focus.
         */
        private void giveUpAudioFocus()
        {
            if (mAudioFocus == AUDIO_FOCUSED)
            {
                if (mAudioManager.AbandonAudioFocus(this) == AudioFocusRequest.Granted)
                {
                    mAudioFocus = AUDIO_NO_FOCUS_NO_DUCK;
                }
            }
        }

        /**
     * Reconfigures MediaPlayer according to audio focus settings and
     * starts/restarts it. This method starts/restarts the MediaPlayer
     * respecting the current audio focus state. So if we have focus, it will
     * play normally; if we don't have focus, it will either leave the
     * MediaPlayer paused or set it to a low volume, depending on what is
     * allowed by the current focus settings. This method assumes mPlayer !=
     * null, so if you are calling it, you have to do so from a context where
     * you are sure this is the case.
     */
        private void configMediaPlayerState()
        {
            if (mAudioFocus == AUDIO_NO_FOCUS_NO_DUCK)
            {
                // If we don't have audio focus and can't duck, we have to pause,
                if (mState == PlaybackStateCompat.StatePlaying)
                {
                    Pause();
                }
            }
            else {  // we have audio focus:
                if (mAudioFocus == AUDIO_NO_FOCUS_CAN_DUCK)
                {
                    mMediaPlayer.SetVolume(VOLUME_DUCK, VOLUME_DUCK); // we'll be relatively quiet
                }
                else {
                    if (mMediaPlayer != null)
                    {
                        mMediaPlayer.SetVolume(VOLUME_NORMAL, VOLUME_NORMAL); // we can be loud again
                    } // else do something for remote client.
                }
                // If we were playing when we lost focus, we need to resume playing.
                if (mPlayOnFocusGain)
                {
                    if (mMediaPlayer != null && !mMediaPlayer.IsPlaying)
                    {
                        //LogHelper.d(TAG, "configMediaPlayerState startMediaPlayer. seeking to ", mCurrentPosition);
                        if (mCurrentPosition == mMediaPlayer.CurrentPosition)
                        {
                            mMediaPlayer.Start();
                            mState = PlaybackStateCompat.StatePlaying;
                        }
                        else {
                            mMediaPlayer.SeekTo(mCurrentPosition);
                            mState = PlaybackStateCompat.StateBuffering;
                        }
                    }
                    mPlayOnFocusGain = false;
                }
            }
            StatusChanged?.Invoke(this, new StatusChangedEventArgs(GetStatusByCompatValue(mState)));
        }

        /**
     * Makes sure the media player exists and has been reset. This will create
     * the media player if needed, or reset the existing media player if one
     * already exists.
     */
        private void createMediaPlayerIfNeeded()
        {
            //LogHelper.d(TAG, "createMediaPlayerIfNeeded. needed? ", (mMediaPlayer == null));
            if (mMediaPlayer == null)
            {
                mMediaPlayer = new MediaPlayer();

                // Make sure the media player will acquire a wake-lock while
                // playing. If we don't do that, the CPU might go to sleep while the
                // song is playing, causing playback to stop.
                mMediaPlayer.SetWakeMode(mContext.ApplicationContext, WakeLockFlags.Partial);

                // we want the media player to notify us when it's ready preparing,
                // and when it's done playing:
                mMediaPlayer.SetOnPreparedListener(this);
                mMediaPlayer.SetOnCompletionListener(this);
                mMediaPlayer.SetOnErrorListener(this);
                mMediaPlayer.SetOnSeekCompleteListener(this);
            }
            else {
                mMediaPlayer.Reset();
            }
        }

        /**
         * Releases resources used by the service for playback. This includes the
         * "foreground service" status, the wake locks and possibly the MediaPlayer.
         *
         * @param releaseMediaPlayer Indicates whether the Media Player should also
         *            be released or not
         */
        private void relaxResources(bool releaseMediaPlayer)
        {
            // stop and release the Media Player, if it's available
            if (releaseMediaPlayer && mMediaPlayer != null)
            {
                mMediaPlayer.Reset();
                mMediaPlayer.Release();
                mMediaPlayer = null;
            }

            // we can also release the Wifi lock, if we're holding it
            if (mWifiLock.IsHeld)
            {
                mWifiLock.Release();
            }
        }

        private void registerAudioNoisyReceiver()
        {
            if (!mAudioNoisyReceiverRegistered)
            {
                mContext.RegisterReceiver(mAudioNoisyReceiver, mAudioNoisyIntentFilter);
                mAudioNoisyReceiverRegistered = true;
            }
        }

        private void unregisterAudioNoisyReceiver()
        {
            if (mAudioNoisyReceiverRegistered)
            {
                mContext.UnregisterReceiver(mAudioNoisyReceiver);
                mAudioNoisyReceiverRegistered = false;
            }
        }

        public void OnAudioFocusChange([GeneratedEnum] AudioFocus focusChange)
        {
            if (focusChange == AudioFocus.Gain)
            {
                // We have gained focus:
                mAudioFocus = AUDIO_FOCUSED;

            }
            else if (focusChange == AudioFocus.Loss ||
                  focusChange == AudioFocus.LossTransient ||
                  focusChange ==  AudioFocus.LossTransientCanDuck)
            {
                // We have lost focus. If we can duck (low playback volume), we can keep playing.
                // Otherwise, we need to pause the playback.
                bool canDuck = focusChange == AudioFocus.LossTransientCanDuck;
                mAudioFocus = canDuck ? AUDIO_NO_FOCUS_CAN_DUCK : AUDIO_NO_FOCUS_NO_DUCK;

                // If we are playing, we need to reset media player by calling configMediaPlayerState
                // with mAudioFocus properly set.
                if (mState == PlaybackStateCompat.StatePlaying && !canDuck)
                {
                    // If we don't have audio focus and can't duck, we save the information that
                    // we were playing, so that we can resume playback once we get the focus back.
                    mPlayOnFocusGain = true;
                }
            }
            else {
                
            }
            configMediaPlayerState();
        }

        public void OnCompletion(MediaPlayer mp)
        {
            // The media player finished playing the current song, so we go ahead
            // and start the next.

            //TODO: set correct media file here
            MediaFinished?.Invoke(this, new MediaFinishedEventArgs(new MediaFile()));
        }

        public bool OnError(MediaPlayer mp, [GeneratedEnum] MediaError what, int extra)
        {
            MediaFailed?.Invoke(this, new MediaFailedEventArgs("MediaPlayer error " + what + " (" + extra + ")", null));
            return true; // true indicates we handled the error
        }

        public void OnPrepared(MediaPlayer mp)
        {
            // The media player is done preparing. That means we can start playing if we
            // have audio focus.
            configMediaPlayerState();
        }

        public void OnSeekComplete(MediaPlayer mp)
        {
            mCurrentPosition = mp.CurrentPosition;
            if (mState == PlaybackStateCompat.StateBuffering)
            {
                mMediaPlayer.Start();
                mState = PlaybackStateCompat.StatePlaying;
            }

            StatusChanged?.Invoke(this, new StatusChangedEventArgs(GetStatusByCompatValue(mState)));
        }

        public MediaPlayerStatus GetStatusByCompatValue(int state)
        {
            switch (state)
            {
                case PlaybackStateCompat.StateFastForwarding:
                case PlaybackStateCompat.StateRewinding:
                case PlaybackStateCompat.StateSkippingToNext:
                case PlaybackStateCompat.StateSkippingToPrevious:
                case PlaybackStateCompat.StateSkippingToQueueItem:
                case PlaybackStateCompat.StatePlaying:
                    return MediaPlayerStatus.Playing;

                case PlaybackStateCompat.StatePaused:
                    return MediaPlayerStatus.Paused;

                case PlaybackStateCompat.StateConnecting:
                case PlaybackStateCompat.StateBuffering:
                    return MediaPlayerStatus.Buffering;

                case PlaybackStateCompat.StateError:
                case PlaybackStateCompat.StateStopped:
                    return MediaPlayerStatus.Stopped;

                default:
                    return MediaPlayerStatus.Stopped;
            }
        }
    }
}
