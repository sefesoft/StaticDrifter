using UnityEngine;

namespace StaticDrift.Managers
{
    public class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private const float BaseMusicVolume = 0.18f;
        private const float BaseSfxVolume = 0.72f;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _thrustLoopSource;

        private AudioClip _titleMusic;
        private AudioClip[] _waveMusicClips = new AudioClip[4];
        private AudioClip _bossMusic;
        private AudioClip _gameplayFallbackMusic;
        private AudioClip _interludeMusic;
        private AudioClip _gameOverMusic;
        private AudioClip _uiConfirm;
        private AudioClip _uiMove;
        private AudioClip _shoot;
        private AudioClip _asteroidHit;
        private AudioClip _asteroidBreak;
        private AudioClip _playerHit;
        private AudioClip _waveInterlude;
        private AudioClip _gameOver;
        private AudioClip _thrustLoopClip;

        private float _nextShootSfxAt;
        private float _nextAsteroidHitSfxAt;

        public static AudioManager Instance { get; private set; }

        public static AudioManager EnsureExists()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AudioManager existing = FindFirstObjectByType<AudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSources();
            BuildPlaceholderLibrary();
            ApplyVolumeFromSettings();
        }

        public void ApplyVolumeFromSettings()
        {
            if (_musicSource != null)
            {
                _musicSource.volume = BaseMusicVolume * GameSettings.MusicVolume;
            }

            if (_sfxSource != null)
            {
                _sfxSource.volume = BaseSfxVolume * GameSettings.SfxVolume;
            }

            if (_thrustLoopSource != null)
            {
                _thrustLoopSource.volume = BaseSfxVolume * GameSettings.SfxVolume * 0.55f;
            }
        }

        public void PlayMusicForScene(string sceneName)
        {
            if (_musicSource == null)
            {
                return;
            }

            if (sceneName == "TitleScreen")
            {
                PlayMusicClip(_titleMusic, loop: true);
            }
        }

        /// <summary>
        /// Looping wave combat music: blocks of <paramref name="wavesPerBlock"/> waves share one track; cycles the four wave songs.
        /// </summary>
        public void PlayWaveMusicForWave(int waveNumber, int wavesPerBlock)
        {
            int w = Mathf.Max(1, waveNumber);
            int block = (w - 1) / Mathf.Max(1, wavesPerBlock);
            int idx = block % _waveMusicClips.Length;
            AudioClip clip = _waveMusicClips[idx];
            if (clip == null)
            {
                clip = _gameplayFallbackMusic;
            }

            PlayMusicClip(clip, loop: true);
        }

        public void PlayBossMusic()
        {
            if (_bossMusic != null)
            {
                PlayMusicClip(_bossMusic, loop: true);
            }
            else if (_waveMusicClips[0] != null)
            {
                PlayMusicClip(_waveMusicClips[0], loop: true);
            }
            else
            {
                PlayMusicClip(_gameplayFallbackMusic, loop: true);
            }
        }

        /// <summary>Looping engine layer while acceleration is held.</summary>
        public void SetThrustLoopActive(bool active)
        {
            if (_thrustLoopSource == null || _thrustLoopClip == null)
            {
                return;
            }

            if (active)
            {
                if (!_thrustLoopSource.isPlaying)
                {
                    _thrustLoopSource.clip = _thrustLoopClip;
                    _thrustLoopSource.Play();
                }
            }
            else if (_thrustLoopSource.isPlaying)
            {
                _thrustLoopSource.Stop();
            }
        }

        public void PlayUiConfirm()
        {
            PlaySfx(_uiConfirm, 0.42f, 1f);
        }

        public void PlayUiMove()
        {
            PlaySfx(_uiMove, 0.24f, 1f);
        }

        public void PlayShoot()
        {
            float now = Time.unscaledTime;
            if (now < _nextShootSfxAt)
            {
                return;
            }

            _nextShootSfxAt = now + 0.055f;
            PlaySfx(_shoot, 0.22f, Random.Range(0.96f, 1.05f));
        }

        public void PlayAsteroidHit()
        {
            float now = Time.unscaledTime;
            if (now < _nextAsteroidHitSfxAt)
            {
                return;
            }

            _nextAsteroidHitSfxAt = now + 0.035f;
            PlaySfx(_asteroidHit, 0.2f, Random.Range(0.92f, 1.08f));
        }

        public void PlayAsteroidBreak()
        {
            PlaySfx(_asteroidBreak, 0.35f, Random.Range(0.95f, 1.05f));
        }

        public void PlayPlayerHit()
        {
            PlaySfx(_playerHit, 0.38f, 1f);
        }

        public void PlayWaveInterlude()
        {
            if (_interludeMusic != null)
            {
                PlayMusicClip(_interludeMusic, loop: true);
            }
            else
            {
                PlaySfx(_waveInterlude, 0.4f, 1f);
            }
        }

        public void PlayGameOver()
        {
            if (_gameOverMusic != null)
            {
                PlayMusicClip(_gameOverMusic, loop: false);
            }
            else
            {
                PlaySfx(_gameOver, 0.48f, 1f);
            }
        }

        private void InitializeSources()
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;

            _thrustLoopSource = gameObject.AddComponent<AudioSource>();
            _thrustLoopSource.playOnAwake = false;
            _thrustLoopSource.loop = true;
            _thrustLoopSource.spatialBlend = 0f;
            _thrustLoopSource.priority = 200;
        }

        private void BuildPlaceholderLibrary()
        {
            AssignMusicFromResources();
            _uiConfirm = CreateToneSweep("ui_confirm", 880f, 580f, 0.12f, 0.25f);
            _uiMove = CreateToneSweep("ui_move", 460f, 520f, 0.06f, 0.18f);
            _shoot = CreateToneSweep("shoot", 900f, 260f, 0.08f, 0.22f);
            _asteroidHit = CreateNoiseBurst("asteroid_hit", 0.06f, 0.32f);
            _asteroidBreak = CreateNoiseBurst("asteroid_break", 0.16f, 0.52f);
            _playerHit = CreateToneSweep("player_hit", 210f, 120f, 0.18f, 0.35f);
            _waveInterlude = CreateToneSweep("wave_interlude", 520f, 920f, 0.22f, 0.30f);
            _gameOver = CreateToneSweep("game_over", 280f, 70f, 0.45f, 0.38f);
            _thrustLoopClip = Resources.Load<AudioClip>("SFX/thust_sfx") ?? BuildThrustLoopSfx();
        }

        private void AssignMusicFromResources()
        {
            _titleMusic = Resources.Load<AudioClip>("Music/SkyFire (Title Screen)") ?? BuildTitleMusic();
            _waveMusicClips[0] = Resources.Load<AudioClip>("Music/Alone Against Enemy");
            _waveMusicClips[1] = Resources.Load<AudioClip>("Music/Battle in the Stars");
            _waveMusicClips[2] = Resources.Load<AudioClip>("Music/Rain of Lasers");
            _waveMusicClips[3] = Resources.Load<AudioClip>("Music/Without Fear");
            _bossMusic = Resources.Load<AudioClip>("Music/Epic End");
            _gameplayFallbackMusic = BuildGameplayMusic();
            _interludeMusic = Resources.Load<AudioClip>("Music/Brave Pilots (Menu Screen)");
            _gameOverMusic = Resources.Load<AudioClip>("Music/Defeated (Game Over Tune)");
        }

        private void PlayMusicClip(AudioClip clip, bool loop)
        {
            if (_musicSource == null || clip == null)
            {
                return;
            }

            if (_musicSource.clip == clip && _musicSource.isPlaying)
            {
                return;
            }

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.Play();
        }

        private void PlaySfx(AudioClip clip, float volume, float pitch)
        {
            if (_sfxSource == null || clip == null)
            {
                return;
            }

            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private static AudioClip CreateToneSweep(string name, float startHz, float endHz, float seconds, float amplitude)
        {
            int sampleCount = Mathf.Max(32, Mathf.FloorToInt(seconds * SampleRate));
            float[] data = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float freq = Mathf.Lerp(startHz, endHz, t);
                phase += (freq * Mathf.PI * 2f) / SampleRate;
                float env = 1f - t;
                data[i] = Mathf.Sin(phase) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Seamless 0.5s loop (100 Hz × 50 cycles @ 44.1 kHz) — rumble + harmonics + slow AM for engine texture.
        /// </summary>
        private static AudioClip BuildThrustLoopSfx()
        {
            const int rate = 44100;
            const int sampleCount = 22050;
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)rate;
                float am = 0.68f + 0.32f * Mathf.Sin(2f * Mathf.PI * 2f * t);
                float a = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.38f;
                float b = Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.16f;
                float c = Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.1f;
                float d = Mathf.Sin(2f * Mathf.PI * 100f * 13f * t) * 0.08f;
                float sample = (a + b + c + d) * am * 0.72f;
                data[i] = Mathf.Clamp(sample, -0.88f, 0.88f);
            }

            AudioClip clip = AudioClip.Create("thrust_loop", sampleCount, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoiseBurst(string name, float seconds, float amplitude)
        {
            int sampleCount = Mathf.Max(32, Mathf.FloorToInt(seconds * SampleRate));
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float env = 1f - t;
                data[i] = (Random.value * 2f - 1f) * amplitude * env;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildTitleMusic()
        {
            float seconds = 6f;
            int sampleCount = Mathf.FloorToInt(seconds * SampleRate);
            float[] data = new float[sampleCount];
            float phaseA = 0f;
            float phaseB = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 2f * 0.5f);
                float freqA = 110f + Mathf.Sin(t * Mathf.PI * 2f * 0.12f) * 14f;
                float freqB = 220f + Mathf.Sin(t * Mathf.PI * 2f * 0.07f) * 10f;
                phaseA += (freqA * Mathf.PI * 2f) / SampleRate;
                phaseB += (freqB * Mathf.PI * 2f) / SampleRate;
                float s = (Mathf.Sin(phaseA) * 0.36f + Mathf.Sin(phaseB) * 0.18f) * pulse;
                data[i] = s * 0.25f;
            }

            AudioClip clip = AudioClip.Create("music_title", sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildGameplayMusic()
        {
            float seconds = 6f;
            int sampleCount = Mathf.FloorToInt(seconds * SampleRate);
            float[] data = new float[sampleCount];
            float phaseA = 0f;
            float phaseB = 0f;
            float phaseC = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float bpmPulse = 0.65f + 0.35f * Mathf.Sign(Mathf.Sin(t * Mathf.PI * 2f * 2.2f));
                float freqA = 140f;
                float freqB = 280f + Mathf.Sin(t * Mathf.PI * 2f * 1.1f) * 18f;
                float freqC = 560f + Mathf.Sin(t * Mathf.PI * 2f * 0.3f) * 24f;
                phaseA += (freqA * Mathf.PI * 2f) / SampleRate;
                phaseB += (freqB * Mathf.PI * 2f) / SampleRate;
                phaseC += (freqC * Mathf.PI * 2f) / SampleRate;
                float s = Mathf.Sin(phaseA) * 0.22f + Mathf.Sin(phaseB) * 0.12f + Mathf.Sin(phaseC) * 0.08f;
                data[i] = s * bpmPulse * 0.34f;
            }

            AudioClip clip = AudioClip.Create("music_gameplay", sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
