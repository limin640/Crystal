using Crystal.Audio;

namespace Client.MirSounds
{
    /// <summary>
    /// Windows GameScene-facing sound index API. Device playback goes through
    /// <see cref="IAudio"/> (NAudio backend). Client.Linux uses Null / OpenAL
    /// via <see cref="AudioFactory.Create"/> and never references this type.
    /// </summary>
    public static class SoundManager
    {
        private static Dictionary<int, string> _indexList => SoundList.Indexes;
        private static List<KeyValuePair<long, int>> _delayList = new List<KeyValuePair<long, int>>();
        private static IAudio _device;
        private static readonly MusicFacade _music = new MusicFacade();

        private static int _vol;
        private static int _musicVol;

        public static readonly List<string> SupportedFileTypes;
        private static long _checkSoundTime;
        public static ISoundLibrary Music => _music;
        public static IAudio Device => _device;

        public static int Vol
        {
            get { return _vol; }
            set
            {
                if (_vol == value) return;
                _vol = value;
                _device?.SetSfxVolume(ScaleVolume(_vol));
            }
        }

        public static int MusicVol
        {
            get { return _musicVol; }
            set
            {
                if (_musicVol == value) return;
                _musicVol = value;
                _device?.SetMusicVolume(ScaleVolume(_musicVol));
            }
        }

        static SoundManager()
        {
            _checkSoundTime = CMain.Time + 30 * 1000;

            SupportedFileTypes = new List<string>
            {
                ".wav",
                ".mp3"
            };

            SoundList.LoadSoundList();
        }

        public static void Create()
        {
            _device?.Dispose();
            _device = AudioFactory.CreateNAudio();
            _device.SetSfxVolume(ScaleVolume(_vol));
            _device.SetMusicVolume(ScaleVolume(_musicVol));
        }

        static void EnsureDevice()
        {
            if (_device == null)
                Create();
        }

        public static void PlaySound(int index, bool loop = false, int delay = 0)
        {
            CheckSoundTimeOut();

            if (delay > 0)
            {
                _delayList.Add(new KeyValuePair<long, int>(CMain.Time + delay, index));
                return;
            }

            if (!_indexList.ContainsKey(index))
            {
                string filename = index > 20000 ?
                                    string.Format("M{0:0}-{1:0}", (index - 20000) / 10, index % 10) :
                                    string.Format("{0:000}-{1:0}", index / 10, index % 10);

                _indexList.Add(index, filename);
            }

            string path = ResolveSoundPath(_indexList[index]);
            if (path == null) return;

            EnsureDevice();
            if (!loop)
                _device.PlayOneShot(path);
            else
                _device.PlayLoop(index, path, ScaleVolume(Vol));
        }

        public static void StopSound(int index)
        {
            _device?.StopLoop(index);
        }

        public static void PlayMusic(int index, bool loop = false)
        {
            StopMusic();

            if (!_indexList.TryGetValue(index, out string value))
                return;

            string path = ResolveSoundPath(value);
            if (path == null) return;

            EnsureDevice();
            _device.PlayMusic(path, loop, ScaleVolume(MusicVol));
        }

        public static void StopMusic()
        {
            _device?.StopMusic();
        }

        public static void ProcessDelayedSounds()
        {
            if (_delayList.Count == 0) return;

            var sounds = _delayList.Where(x => x.Key <= CMain.Time).ToList();

            foreach (var sound in sounds)
            {
                _delayList.Remove(sound);

                PlaySound(sound.Value);
            }
        }

        private static string ResolveSoundPath(string fileName)
        {
            string path = Path.Combine(Settings.SoundPath, fileName);
            string fileType = Path.GetExtension(path);

            if (string.IsNullOrEmpty(fileType))
            {
                foreach (string ext in SupportedFileTypes)
                {
                    if (File.Exists(path + ext))
                        return path + ext;
                }

                return null;
            }

            return File.Exists(path) ? path : null;
        }

        private static float ScaleVolume(int volume)
        {
            float scaled = 0.0f + (float)(volume - 0) / (100 - 0) * (1.0f - 0.0f);
            return scaled;
        }

        private static void CheckSoundTimeOut()
        {
            if (CMain.Time >= _checkSoundTime)
            {
                _checkSoundTime = CMain.Time + 30 * 1000;
                _device?.PumpExpired(CMain.Time, Settings.SoundCleanMinutes * 60L * 1000);
            }
        }

        public static void Dispose()
        {
            _device?.Dispose();
            _device = null;
        }

        sealed class MusicFacade : ISoundLibrary
        {
            public int Index { get; set; }
            public long ExpireTime { get; set; }

            public bool IsPlaying() => false;

            public void Play(int volume)
            {
                _ = volume;
            }

            public void Stop() => StopMusic();

            public void SetVolume(int vol) => MusicVol = vol;

            public void Dispose() => StopMusic();
        }
    }
}
