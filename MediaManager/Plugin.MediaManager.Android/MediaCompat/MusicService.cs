using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Media;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Support.V7.Media;
using Java.Lang;
using Plugin.MediaManager.MediaCompat;

namespace Plugin.MediaManager.Audio
{
    [Service]
    [IntentFilter(new[] { MediaBrowserService.ServiceInterface })]
    public class MusicService : MediaBrowserServiceCompat, PlaybackServiceCallback
    {
        // Extra on MediaSession that contains the Cast device name currently connected to
        public static string EXTRA_CONNECTED_CAST = "com.example.android.uamp.CAST_NAME";
        // The action of the incoming Intent indicating that it contains a command
        // to be executed (see {@link #onStartCommand})
        public static string ACTION_CMD = "com.example.android.uamp.ACTION_CMD";
        // The key in the extras of the incoming Intent indicating the command that
        // should be executed (see {@link #onStartCommand})
        public static string CMD_NAME = "CMD_NAME";
        // A value of a CMD_NAME key in the extras of the incoming Intent that
        // indicates that the music playback should be paused (see {@link #onStartCommand})
        public static string CMD_PAUSE = "CMD_PAUSE";
        // A value of a CMD_NAME key that indicates that the music playback should switch
        // to local playback from cast playback.
        public static string CMD_STOP_CASTING = "CMD_STOP_CASTING";
        // Delay stopSelf by using a handler.
        private static int STOP_DELAY = 30000;

        //private MusicProvider mMusicProvider;
        private PlaybackManager mPlaybackManager;

        private MediaSessionCompat _mediaSession;
        private MediaNotificationManager _mediaNotificationManager;
        private Bundle _sessionExtras;
        private DelayedStopHandler _delayedStopHandler;
        private MediaRouter _mediaRouter;
        //private PackageValidator mPackageValidator;
        //private SessionManager mCastSessionManager;
        //private SessionManagerListener<CastSession> mCastSessionManagerListener;

        private bool mIsConnectedToCar;
        private BroadcastReceiver mCarConnectionReceiver;

        public MusicService()
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            _delayedStopHandler = new DelayedStopHandler(this);
            //_packageValidator = new PackageValidator(this);

            // Start a new MediaSession
            _mediaSession = new MediaSessionCompat(this, nameof(MusicService));
            SessionToken = _mediaSession.SessionToken;
            _mediaSession.SetCallback(new MediaSessionCallback());
            _mediaSession.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons |
                                   MediaSessionCompat.FlagHandlesTransportControls);

            //TODO: Move to method to let user set their Activity
            /*
            Context context = ApplicationContext;
            Intent intent = new Intent(context, NowPlayingActivity.class);
        PendingIntent pi = PendingIntent.getActivity(context, 99,
                intent, PendingIntent.FLAG_UPDATE_CURRENT);
        mSession.setSessionActivity(pi);
        */

        _sessionExtras = new Bundle();
        //CarHelper.setSlotReservationFlags(mSessionExtras, true, true, true);
        //WearHelper.setSlotReservationFlags(mSessionExtras, true, true);
        //WearHelper.setUseBackgroundFromTheme(mSessionExtras, true);
        _mediaSession.SetExtras(_sessionExtras);

        mPlaybackManager.updatePlaybackState(null);

        try 
        {
            _mediaNotificationManager = new MediaNotificationManager(this, _mediaSession);
        } 
        catch (RemoteException e) 
        {
            throw new IllegalStateException("Could not create a MediaNotificationManager", e);
        }

            /*
        if (!TvHelper.isTvUiMode(this)) {
            mCastSessionManager = CastContext.getSharedInstance(this).getSessionManager();
            mCastSessionManagerListener = new CastSessionManagerListener();
            mCastSessionManager.addSessionManagerListener(mCastSessionManagerListener,
            CastSession.class);
        }
            */

            _mediaRouter = MediaRouter.GetInstance(ApplicationContext);

            //registerCarConnectionReceiver();
        }

        public override StartCommandResult OnStartCommand(Intent startIntent, StartCommandFlags flags, int startId)
        {
            if (startIntent != null)
            {
                MediaButtonReceiver.HandleIntent(_mediaSession, startIntent);
            }

            // Reset the delay handler to enqueue a message to stop the service if
            // nothing is playing.
            _delayedStopHandler.RemoveCallbacksAndMessages(null);
            _delayedStopHandler.SendEmptyMessageDelayed(0, STOP_DELAY);
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            //unregisterCarConnectionReceiver();
            // Service is being killed, so make sure we release our resources
            mPlaybackManager.handleStopRequest(null);
            _mediaNotificationManager.StopNotifications();

            /*
            if (mCastSessionManager != null)
            {
                mCastSessionManager.removeSessionManagerListener(mCastSessionManagerListener,
                        CastSession.class);
            }
*/
            _delayedStopHandler.RemoveCallbacksAndMessages(null);
            _mediaSession.Release();
        }

        public override BrowserRoot OnGetRoot(string clientPackageName, int clientUid, Bundle rootHints)
        {
            return new BrowserRoot(nameof(ApplicationContext.ApplicationInfo.Name), // Name visible in Android Auto
                null);
        }

        public override void OnLoadChildren(string parentId, Result result)
        {
            //var test = (Result<List<MediaBrowserCompat.MediaItem>>)result;
            result.SendResult(null);
        }

        public void onPlaybackStart()
        {
            if (!_mediaSession.Active)
            {
                _mediaSession.Active = true;
            }

            _delayedStopHandler.RemoveCallbacksAndMessages(null);

            // The service needs to continue running even after the bound client (usually a
            // MediaController) disconnects, otherwise the music playback will stop.
            // Calling startService(Intent) will keep the service running until it is explicitly killed.
            StartService(new Intent(ApplicationContext, typeof(MusicService)));
        }

        public void onNotificationRequired()
        {
            _mediaNotificationManager.StartNotification();
        }

        public void onPlaybackStop()
        {
            // Reset the delayed stop handler, so after STOP_DELAY it will be executed again,
            // potentially stopping the service.
            _delayedStopHandler.RemoveCallbacksAndMessages(null);
            _delayedStopHandler.SendEmptyMessageDelayed(0, STOP_DELAY);
            StopForeground(true);
        }

        public void onPlaybackStateUpdated(PlaybackStateCompat newState)
        {
            _mediaSession.SetPlaybackState(newState);
        }

        /**
     * A simple handler that stops the service if playback is not active (playing)
     */
        public class DelayedStopHandler : Handler
        {
            private WeakReference<MusicService> _weakReference;

            public DelayedStopHandler(MusicService service)
            {
                _weakReference = new WeakReference<MusicService>(service);
            }

            public override void HandleMessage(Message msg)
            {
                MusicService service;
                if (_weakReference.TryGetTarget(out service))
                {
                    if (service != null && service.mPlaybackManager.getPlayback() != null)
                    {
                        if (service.mPlaybackManager.getPlayback().isPlaying())
                        {
                            return;
                        }
                        service.StopSelf();
                    }
                }
            }
        }
    }
}
