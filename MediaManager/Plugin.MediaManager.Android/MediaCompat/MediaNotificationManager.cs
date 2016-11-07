using System;
using Android.Content;
using Android.Support.V4.App;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Implementations;

namespace Plugin.MediaManager.MediaCompat
{
    public class MediaNotificationManager : IMediaNotificationManager
    {
        private Context _appliactionContext;
        private MediaSessionCompat _mediaSession;

        public MediaNotificationManager(Context context, MediaSessionCompat mediaSession)
        {
            _appliactionContext = context;
            _mediaSession = mediaSession;
        }

        public void StartNotification(IMediaFile mediaFile)
        {
            MediaControllerCompat controller = _mediaSession.Controller;
            MediaMetadataCompat mediaMetadata = controller.Metadata;
            MediaDescriptionCompat description = mediaMetadata.Description;

            NotificationCompat.Builder builder = new NotificationCompat.Builder(_appliactionContext);
            builder
                    .SetContentTitle(description.Title)
                    .SetContentText(description.Subtitle)
                    .SetSubText(description.Description)
                    .SetLargeIcon(description.IconBitmap)
                    .SetContentIntent(controller.SessionActivity)
                    .SetDeleteIntent(
                        MediaButtonReceiver.BuildMediaButtonPendingIntent(_appliactionContext, PlaybackStateCompat.ActionStop))
                    .SetVisibility(Android.Support.V4.App.NotificationCompat.VisibilityPublic);

            Android.Support.V4.App.NotificationManagerCompat.From(_appliactionContext)
                .Notify(MediaServiceBase.NotificationId, builder.Build());
        }

        public void StopNotifications()
        {
            throw new NotImplementedException();
        }

        public void UpdateNotifications(IMediaFile mediaFile, MediaPlayerStatus status)
        {
            throw new NotImplementedException();
        }
    }
}
