using System;
using MvvmCross.Core.ViewModels;
using Plugin.MediaManager;
using Plugin.MediaManager.Abstractions;
using Plugin.MediaManager.Abstractions.Implementations;

namespace MyMediaPlayer.Core.ViewModels
{
    public class FirstViewModel 
        : MvxViewModel
    {
        private string _hello = "Hello MvvmCross";
        public string Hello
        { 
            get { return _hello; }
            set { SetProperty (ref _hello, value); }
        }

        public IMediaManager MediaPlayer { get; }

        public IMediaQueue Queue => MediaPlayer.MediaQueue;

        public IMediaFile CurrentTrack => Queue.Current;

        public int Duration => MediaPlayer != null && MediaPlayer.Duration.TotalSeconds > 0 ? Convert.ToInt32(MediaPlayer.Duration.TotalSeconds) : 0;

        private bool _isSeeking = false;

        public bool IsSeeking
        {
            get
            {
                return _isSeeking;
            }
            set
            {
                // Put into an action so we can await the seek-command before we update the value. Prevents jumping of the progress-bar.
                var a = new Action(async () =>
                {
                    // When disable user-seeking, update the position with the position-value
                    if (value == false)
                    {
                        await MediaPlayer.Seek(TimeSpan.FromSeconds(Position));
                    }

                    _isSeeking = value;
                });
                a.Invoke();
            }
        }

        private int _position;

        public FirstViewModel()
        {
            MediaPlayer = CrossMediaManager.Current;

            Queue.Add(new MediaFile() {Type = MediaFileType.AudioUrl, Url = "http://www.montemagno.com/sample.mp3" });
        }

        public int Position
        {
            get
            {
                if (IsSeeking)
                    return _position;

                return MediaPlayer.Position.TotalSeconds > 0 ? Convert.ToInt32(MediaPlayer.Position.TotalSeconds) : 0;
            }
            set
            {
                _position = value;
                RaisePropertyChanged(nameof(Position));
            }
        }

        public int Downloaded => Convert.ToInt32(MediaPlayer.Buffered.TotalSeconds);

        public bool IsPlaying => MediaPlayer.Status == MediaPlayerStatus.Playing || MediaPlayer.Status == MediaPlayerStatus.Buffering;

        public MediaPlayerStatus Status => MediaPlayer.Status;

        public object Cover => MediaPlayer.MediaQueue.Current.Metadata.Cover;

        public string PlayingText => $"Playing: {(Queue.Index + 1).ToString()} of {Queue.Count.ToString()}";

        public IMvxCommand PlayFileCommand => new MvxCommand(OnPlayFileCommand);

        public void OnPlayFileCommand()
        {
            MediaPlayer.PlayPause();
        }
    }
}
