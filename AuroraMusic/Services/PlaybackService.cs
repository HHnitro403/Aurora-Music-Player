
using AuroraMusic.Models;
using LibVLCSharp.Shared;
using System;

namespace AuroraMusic.Services
{
    public class PlaybackService : IDisposable
    {
        private readonly LibVLC _libVLC;
        private readonly MediaPlayer _mediaPlayer;

        public event EventHandler<MediaPlayerTimeChangedEventArgs>? TimeChanged;
        public event EventHandler<MediaPlayerLengthChangedEventArgs>? LengthChanged;
        public event EventHandler? EndReached;

        public PlaybackService()
        {
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            _mediaPlayer.TimeChanged += (s, e) => TimeChanged?.Invoke(s, e);
            _mediaPlayer.LengthChanged += (s, e) => LengthChanged?.Invoke(s, e);
            _mediaPlayer.EndReached += (s, e) => EndReached?.Invoke(s, e);
        }

        public bool IsPlaying => _mediaPlayer.IsPlaying;
        public long Time
        {
            get => _mediaPlayer.Time;
            set => _mediaPlayer.Time = value;
        }

        public int Volume
        {
            get => _mediaPlayer.Volume;
            set => _mediaPlayer.Volume = value;
        }

        public void Play(PlaylistItem item)
        {
            if (item == null) return;

            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Stop();
            }

            var media = new Media(_libVLC, new Uri(item.FilePath));
            _mediaPlayer.Media = media;
            _mediaPlayer.Play();
            media.Dispose();
        }

        public void Play() => _mediaPlayer.Play();
        public void Pause() => _mediaPlayer.Pause();
        public void Stop() => _mediaPlayer.Stop();

        public void Dispose()
        {
            _mediaPlayer.Dispose();
            _libVLC.Dispose();
        }
    }
}
