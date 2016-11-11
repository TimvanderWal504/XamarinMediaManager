using System.Collections.Generic;
using Android.App;
using Android.OS;
using MvvmCross.Droid.Views;
using Plugin.MediaManager;
using Plugin.MediaManager.ExoPlayer;

namespace MyMediaPlayer.Droid.Views
{
    [Activity(Label = "View for FirstViewModel")]
    public class FirstView : MvxActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.FirstView);

            var exoPlayer =
                new ExoPlayerAudioImplementation(
                    ((MediaManagerImplementation) CrossMediaManager.Current).MediaSessionManager);
            exoPlayer.RequestProperties = new Dictionary<string, string> { {"Test", "1234"} };
            CrossMediaManager.Current.AudioPlayer = exoPlayer;

            //new AudioImp
        }
    }
}
