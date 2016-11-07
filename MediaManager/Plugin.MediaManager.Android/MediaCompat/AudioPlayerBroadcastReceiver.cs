using Android.Content;
using Android.Media;
using Android.Support.V4.Media.Session;
using Plugin.MediaManager.Audio;

namespace Plugin.MediaManager.MediaCompat
{
    /// <summary>
    /// This is a simple intent receiver that is used to stop playback
    /// when audio become noisy, such as the user unplugged headphones
    /// </summary>
    [BroadcastReceiver]
    [Android.App.IntentFilter(new[] { AudioManager.ActionAudioBecomingNoisy })]
    public class AudioPlayerBroadcastReceiver : BroadcastReceiver
    {
        LocalPlayback _playback;
        Context _context;

        public AudioPlayerBroadcastReceiver(Context context, LocalPlayback playback)
        {
            _playback = playback;
            _context = context;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            if (AudioManager.ActionAudioBecomingNoisy.Equals(intent.Action))
            {
                if (_playback.isPlaying())
                {
                    Intent i = new Intent(context, typeof(MusicService));
                    i.SetAction(MusicService.ACTION_CMD);
                    i.PutExtra(MusicService.CMD_NAME, MusicService.CMD_PAUSE);
                    _context.StartService(i);
                }
            }
        }
    }
}