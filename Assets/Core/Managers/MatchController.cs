using UnityEngine;
using StaticDrift.UI;
using StaticDrift.Player;
using StaticDrift.Enemies;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using StaticDrift.Projectiles;
using StaticDrift.Pooling;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using StaticDrift.Items;
using StaticDrift.Cards;
using StaticDrift.Achievements;
using System.Collections;

namespace StaticDrift.Managers
{
    /// <summary>
    /// Runs when Gameplay scene loads. Owns match timer, spawns Player and GameplayHUD.
    /// </summary>
    public class MatchController : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _gameplayHUDPrefab;
        [SerializeField] private Vector3 _playerSpawnPosition = Vector3.zero;
        [SerializeField] private bool _pauseTimerWhenPaused = true;
        [SerializeField] private EnemyWaveSpawner _waveSpawner;
        [SerializeField] private float _firstWaveDurationSeconds = 30f;
        [Tooltip("Each wave after the first lasts this much longer as a fraction of the first wave (e.g. 0.1 = +10% of 1 min per wave).")]
        [SerializeField] [Range(0f, 0.5f)] private float _extraWaveDurationFractionPerWave = 0.1f;
        [SerializeField] private int _bossEveryWaves = 5;
        [SerializeField] private float _bossBaseHealth = 220f;
        [SerializeField] private float _bossHealthPerCycle = 90f;
        [SerializeField] private RunUpgradeController _runUpgradeController;
        [SerializeField] private ItemSpawner _itemSpawner;

        private float _matchTime;
        private bool _running;
        private int _score;
        private int _currentWave = 1;
        private float _waveElapsedTime;
        private float _currentWaveDuration;
        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private PlayerAutoFire _playerAutoFire;
        private PlayerThrusterVFX _playerThrusterVfx;
        private Rigidbody2D _playerBody;
        private Transform _playerTransform;
        private GameObject _gameplayHudCanvas;
        private bool _isGameOver;
        private bool _isInterlude;
        private bool _isWaveTransitionAnimating;
        private int _pendingWave;
        private int _waveStartScore;
        private float _waveStartTime;
        private int _runScrap;
        private bool _waveDamageTakenThisWave;
        private readonly List<float> _chainAsteroidTimes = new List<float>(12);
        private BossShip _bossShip;
        private bool _isBossFight;
        private bool _isPaused;
        private float _bossFightElapsed;
        private StarfieldBackground _starfieldBackground;
        private Vector3 _defaultCameraPosition;
        private float _defaultCameraSize = 10f;
        private Vector3 _playerBaseScale = Vector3.one;
        [Header("Boss Presentation")]
        [SerializeField] private float _bossWarningDurationSeconds = 1.6f;
        [SerializeField] private float _bossExplosionDurationSeconds = 1.1f;
        [Tooltip("Scales particle count/size/speed for the boss death orange explosion (same VFX as game over).")]
        [SerializeField] private float _bossDeathExplosionIntensity = 1.9f;
        [Header("Game Over Presentation")]
        [Tooltip("Wall-clock seconds to ramp Time.timeScale from 1 down to the target below.")]
        [SerializeField] private float _gameOverSlowMoRampDuration = 0.28f;
        [Tooltip("Time scale while the camera zooms and the explosion plays (before full pause).")]
        [SerializeField] [Range(0.02f, 0.5f)] private float _gameOverPresentationTimeScale = 0.07f;
        [Tooltip("Wall-clock seconds for camera move + orthographic zoom toward the ship.")]
        [SerializeField] private float _gameOverCameraZoomDuration = 1.28f;
        [Tooltip("Multiply orthographic size by this (smaller = tighter zoom on the ship).")]
        [SerializeField] [Range(0.25f, 0.95f)] private float _gameOverOrthographicZoomMultiplier = 0.5f;
        [Tooltip("Wall-clock seconds from sequence start until the orange burst spawns.")]
        [SerializeField] private float _gameOverExplosionDelay = 0.52f;
        [Tooltip("Extra wall-clock hold after the zoom finishes before freezing time and showing the UI.")]
        [SerializeField] private float _gameOverPostZoomHold = 0.42f;
        [SerializeField] private float _gameOverExplosionVisualDuration = 0.95f;
        [Header("Wave Transition")]
        [SerializeField] private float _waveExitCenterDuration = 0.6f;
        [SerializeField] private float _waveExitRotateDuration = 0.55f;
        [SerializeField] private float _waveExitHyperDuration = 0.85f;
        [SerializeField] private float _waveEntryDuration = 0.95f;
        [SerializeField] private float _waveEntryOvershootDistance = 0.2f;
        [SerializeField] private float _waveTransitionOffscreenPadding = 6f;
        [SerializeField] private float _waveHyperStretchY = 1.65f;
        [SerializeField] private float _waveHyperStretchX = 0.72f;
        [SerializeField] private float _waveStarWarpAmount = 1f;
        [SerializeField] private float _waveHyperspaceZoomFactor = 0.72f;

        /// <summary>
        /// Elapsed match time in seconds. Used by GameplayHUD for the timer display.
        /// </summary>
        public float MatchTime => _matchTime;
        public float WaveElapsedTime => _waveElapsedTime;
        public float WaveDuration => _currentWaveDuration;
        /// <summary>True while a timed wave is active (not during boss).</summary>
        public bool IsWaveTimerActive => _running && !_isInterlude && !_isGameOver && !_isBossFight && _currentWaveDuration > 0.01f;
        public int Score => _score;
        public int CurrentWave => _currentWave;
        public bool IsGameOver => _isGameOver;
        public bool IsBossFight => _isBossFight;
        public bool IsPaused => _isPaused;
        public float BossHealthNormalized => _bossShip != null && _bossShip.IsActiveBoss ? _bossShip.Health01 : 0f;
        public int BossCurrentHealth => _bossShip != null && _bossShip.IsActiveBoss ? Mathf.CeilToInt(_bossShip.CurrentHealth) : 0;
        public int BossMaxHealth => _bossShip != null && _bossShip.IsActiveBoss ? Mathf.CeilToInt(_bossShip.MaxHealth) : 0;
        public string BuildSummary => _runUpgradeController != null ? _runUpgradeController.GetSynergySummary() : "V0 K0 T0 S0";

        public static MatchController Instance { get; private set; }
        private static Sprite _pauseInfoItemSpriteSolid;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            GameSettings.Load();
            EnsureTransitionTimings();
            EnsureStarfieldBackground();
            CacheDefaultCameraPosition();
            AudioManager.EnsureExists().PlayWaveMusicForWave(_currentWave, _bossEveryWaves);
            SpawnPlayer();
            SpawnGameplayHUD();
            EnsureItemSpawner();
            EnsureBossShip();
            _matchTime = 0f;
            _score = 0;
            _runScrap = 0;
            _currentWave = 1;
            _waveStartScore = 0;
            _waveStartTime = 0f;
            _waveElapsedTime = 0f;
            _currentWaveDuration = GetWaveDurationSeconds(_currentWave);
            _running = true;

            if (_waveSpawner == null)
            {
                _waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
            }

            if (_runUpgradeController == null)
            {
                _runUpgradeController = FindFirstObjectByType<RunUpgradeController>();
                if (_runUpgradeController == null)
                {
                    _runUpgradeController = gameObject.AddComponent<RunUpgradeController>();
                }
            }

            _runUpgradeController.ResetRun();
            ApplyRunModifiers();

            if (_waveSpawner != null)
            {
                _waveSpawner.ConfigureForWave(_currentWave);
                _waveSpawner.SetSpawningEnabled(true);
            }

            Asteroid.AsteroidDestroyed += OnAsteroidDestroyed;
            PlayerHealth.PlayerTookDamage += OnPlayerTookDamageForAchievements;
            Enemy.HostileDestroyed += OnHostileDestroyedForAchievements;
            _waveDamageTakenThisWave = false;
            _chainAsteroidTimes.Clear();
        }

        private void EnsureTransitionTimings()
        {
            _waveExitCenterDuration = Mathf.Max(_waveExitCenterDuration, 0.6f);
            _waveExitRotateDuration = Mathf.Max(_waveExitRotateDuration, 0.55f);
            _waveExitHyperDuration = Mathf.Max(_waveExitHyperDuration, 0.85f);
            _waveEntryDuration = Mathf.Max(_waveEntryDuration, 0.95f);
            _waveTransitionOffscreenPadding = Mathf.Max(_waveTransitionOffscreenPadding, 6f);
            _waveStarWarpAmount = Mathf.Max(_waveStarWarpAmount, 1f);
            _waveHyperspaceZoomFactor = Mathf.Clamp(_waveHyperspaceZoomFactor, 0.45f, 0.95f);
        }

        private void Update()
        {
            if (HandlePauseToggleInput())
            {
                return;
            }

            if (_isPaused)
            {
                return;
            }

            if (!_running)
            {
                return;
            }
            if (_pauseTimerWhenPaused && Time.timeScale < 0.001f)
            {
                return;
            }
            _matchTime += Time.deltaTime;
            if (_isBossFight)
            {
                _bossFightElapsed += Time.deltaTime;
                if (_playerHealth != null && _playerHealth.IsDead && !_isGameOver)
                {
                    TriggerGameOver();
                }

                return;
            }

            _waveElapsedTime += Time.deltaTime;
            if (_waveElapsedTime >= _currentWaveDuration)
            {
                float remainingOnClock = _currentWaveDuration - _waveElapsedTime;
                NotifyTimedWaveAchievements(_currentWave, remainingOnClock);
                if (ShouldStartBossFight(_currentWave))
                {
                    StartBossFight();
                }
                else
                {
                    BeginWaveInterlude(_currentWave + 1, -1f);
                }
                return;
            }

            if (_playerHealth != null && _playerHealth.IsDead && !_isGameOver)
            {
                TriggerGameOver();
            }
        }

        private void OnDestroy()
        {
            Asteroid.AsteroidDestroyed -= OnAsteroidDestroyed;
            PlayerHealth.PlayerTookDamage -= OnPlayerTookDamageForAchievements;
            Enemy.HostileDestroyed -= OnHostileDestroyedForAchievements;
            if (_bossShip != null)
            {
                _bossShip.Defeated -= HandleBossDefeated;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnPlayerTookDamageForAchievements()
        {
            _waveDamageTakenThisWave = true;
        }

        private static void OnHostileDestroyedForAchievements()
        {
            AchievementProgress.RecordHostileDestroyed();
        }

        private void NotifyTimedWaveAchievements(int completedWave, float remainingSecondsOnClock)
        {
            if (!_waveDamageTakenThisWave)
            {
                AchievementProgress.Unlock(AchievementId.CleanSector);
            }

            if (remainingSecondsOnClock <= 3f)
            {
                AchievementProgress.Unlock(AchievementId.CuttingClose);
            }

            if (completedWave >= 10)
            {
                AchievementProgress.Unlock(AchievementId.DriftSurvivor);
            }

            if (completedWave >= 15)
            {
                AchievementProgress.Unlock(AchievementId.DeepRun);
            }

            if (_waveSpawner != null && _waveSpawner.IsEliteWaveNumber(completedWave))
            {
                AchievementProgress.RecordEliteWaveSurvived();
            }
        }

        private void SpawnPlayer()
        {
            if (_playerPrefab == null)
            {
                return;
            }
            GameObject player = Instantiate(_playerPrefab, _playerSpawnPosition, Quaternion.identity);
            player.name = _playerPrefab.name;

            _playerHealth = player.GetComponent<PlayerHealth>();
            if (_playerHealth == null)
            {
                _playerHealth = player.GetComponentInChildren<PlayerHealth>();
            }

            _playerTransform = player.transform;
            _playerBaseScale = _playerTransform.localScale;
            _playerController = player.GetComponent<PlayerController>();
            _playerAutoFire = player.GetComponent<PlayerAutoFire>();
            _playerThrusterVfx = player.GetComponent<PlayerThrusterVFX>();
            _playerBody = player.GetComponent<Rigidbody2D>();

            PlayerThrusterVFX thrusterVfx = player.GetComponent<PlayerThrusterVFX>();
            if (thrusterVfx == null)
            {
                thrusterVfx = player.AddComponent<PlayerThrusterVFX>();
            }
            _playerThrusterVfx = thrusterVfx;

            PlayerPowerupController powerupController = player.GetComponent<PlayerPowerupController>();
            if (powerupController == null)
            {
                player.AddComponent<PlayerPowerupController>();
            }
        }

        private void EnsureItemSpawner()
        {
            if (_itemSpawner != null)
            {
                return;
            }

            _itemSpawner = FindFirstObjectByType<ItemSpawner>();
            if (_itemSpawner == null)
            {
                GameObject go = new GameObject("ItemSpawner");
                _itemSpawner = go.AddComponent<ItemSpawner>();
            }
        }

        private void EnsureBossShip()
        {
            if (_bossShip != null)
            {
                return;
            }

            _bossShip = FindFirstObjectByType<BossShip>();
            if (_bossShip == null)
            {
                GameObject go = new GameObject("BossShip");
                _bossShip = go.AddComponent<BossShip>();
            }

            _bossShip.Defeated -= HandleBossDefeated;
            _bossShip.Defeated += HandleBossDefeated;
            _bossShip.Deactivate();
        }

        private void EnsureStarfieldBackground()
        {
            if (_starfieldBackground != null)
            {
                return;
            }

            _starfieldBackground = FindFirstObjectByType<StarfieldBackground>();
            if (_starfieldBackground != null)
            {
                _starfieldBackground.Initialize(Camera.main);
                return;
            }

            GameObject go = new GameObject("StarfieldBackground");
            _starfieldBackground = go.AddComponent<StarfieldBackground>();
            _starfieldBackground.Initialize(Camera.main);
        }

        private void CacheDefaultCameraPosition()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                _defaultCameraPosition = cam.transform.position;
                _defaultCameraSize = cam.orthographicSize;
            }
        }

        private void SpawnGameplayHUD()
        {
            if (_gameplayHUDPrefab == null)
            {
                return;
            }
            Transform parent = null;
            GameObject canvasGo = GameObject.Find("GameplayHUD_Canvas");
            if (canvasGo != null)
            {
                _gameplayHudCanvas = canvasGo;
                parent = canvasGo.transform;
            }
            GameObject hud = Instantiate(_gameplayHUDPrefab, parent);
            hud.name = _gameplayHUDPrefab.name;
            if (parent != null)
            {
                var rect = hud.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            }
        }

        private void OnAsteroidDestroyed(Asteroid.AsteroidSize size)
        {
            if (_isGameOver)
            {
                return;
            }

            if (size == Asteroid.AsteroidSize.Large)
            {
                _score += 100;
                _runScrap += 4;
            }
            else if (size == Asteroid.AsteroidSize.Medium)
            {
                _score += 60;
                _runScrap += 2;
            }
            else
            {
                _score += 30;
                _runScrap += 1;
            }

            if (_waveSpawner != null && _waveSpawner.IsEliteWave)
            {
                _score += 15;
            }

            bool nearEdge = _playerController != null && _playerController.IsNearWrapEdgeBand(2.2f);
            AchievementProgress.RecordAsteroidDestroyed(nearEdge);
            AchievementProgress.TryUnlockChainReaction(Time.unscaledTime, _chainAsteroidTimes);
        }

        private void TriggerGameOver()
        {
            _isGameOver = true;
            _isInterlude = false;
            _running = false;
            _isPaused = false;
            _isBossFight = false;
            DestroyPauseOverlay();
            if (_bossShip != null)
            {
                _bossShip.Deactivate();
            }

            SetGameplayHudVisible(false);
            SetPlayerTransitionControlEnabled(false);

            AudioManager.EnsureExists().PlayGameOver();

            StartCoroutine(GameOverPresentationRoutine());
        }

        private IEnumerator GameOverPresentationRoutine()
        {
            CacheDefaultCameraPosition();

            Camera cam = Camera.main;
            Vector3 camStartPos = cam != null ? cam.transform.position : _defaultCameraPosition;
            float camStartSize = cam != null ? cam.orthographicSize : _defaultCameraSize;
            Vector3 playerPos = _playerTransform != null ? _playerTransform.position : camStartPos;
            playerPos.z = camStartPos.z;
            float camEndSize = Mathf.Max(0.5f, camStartSize * _gameOverOrthographicZoomMultiplier);

            float rampDuration = Mathf.Max(0.01f, _gameOverSlowMoRampDuration);
            float zoomDuration = Mathf.Max(0.01f, _gameOverCameraZoomDuration);
            float explosionDelay = Mathf.Max(0f, _gameOverExplosionDelay);
            float postHold = Mathf.Max(0f, _gameOverPostZoomHold);
            float totalDuration = zoomDuration + postHold;
            float targetScale = Mathf.Clamp(_gameOverPresentationTimeScale, 0.02f, 0.5f);

            GameObject explosionRoot = null;
            bool explosionSpawned = false;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (elapsed < rampDuration)
                {
                    float u = Mathf.Clamp01(elapsed / rampDuration);
                    Time.timeScale = Mathf.Lerp(1f, targetScale, EaseInOut(u));
                }
                else
                {
                    Time.timeScale = targetScale;
                }

                if (!explosionSpawned && elapsed >= explosionDelay)
                {
                    explosionSpawned = true;
                    if (_playerTransform != null)
                    {
                        _playerTransform.gameObject.SetActive(false);
                    }

                    explosionRoot = CreateGameOverExplosionVfx(playerPos);
                }

                if (cam != null)
                {
                    float zt = Mathf.Clamp01(elapsed / zoomDuration);
                    float ze = EaseInOut(zt);
                    cam.transform.position = Vector3.Lerp(camStartPos, playerPos, ze);
                    cam.orthographicSize = Mathf.Lerp(camStartSize, camEndSize, ze);
                }

                yield return null;
            }

            Time.timeScale = 0f;

            List<int> topScores = SaveAndGetTopScores(_score);
            AchievementProgress.OnGameOverScore(_score, topScores);
            int totalScrap = SaveAndGetTotalScrap(_runScrap);
            CreateGameOverOverlay(topScores, totalScrap);

            if (explosionRoot != null)
            {
                Destroy(explosionRoot, _gameOverExplosionVisualDuration + 0.35f);
            }
        }

        private void BeginWaveInterlude(int nextWave, float segmentDurationOverride = -1f)
        {
            if (_isInterlude || _isGameOver || _isWaveTransitionAnimating)
            {
                return;
            }

            _isInterlude = true;
            _pendingWave = Mathf.Max(nextWave, _currentWave + 1);
            _running = false;
            _isPaused = false;
            DestroyPauseOverlay();

            if (_waveSpawner != null)
            {
                _waveSpawner.SetSpawningEnabled(false);
                _waveSpawner.ClearActiveAsteroids();
            }

            ClearActiveProjectiles();

            int scoreDelta = _score - _waveStartScore;
            float waveDuration = segmentDurationOverride >= 0f ? segmentDurationOverride : _waveElapsedTime;

            Time.timeScale = 0f;
            SetGameplayHudVisible(false);
            StartCoroutine(BeginWaveInterludeSequence(_currentWave, scoreDelta, waveDuration));
        }

        private void ContinueFromInterlude()
        {
            if (_isGameOver || _isWaveTransitionAnimating)
            {
                return;
            }

            StartCoroutine(ContinueFromInterludeSequence());
        }

        private void PrepareNextWaveFromInterlude()
        {
            _currentWave = _pendingWave;
            _waveStartScore = _score;
            _waveStartTime = _matchTime;
            _waveElapsedTime = 0f;
            _currentWaveDuration = GetWaveDurationSeconds(_currentWave);
            _isBossFight = false;
            _isPaused = false;

            if (_waveSpawner != null)
            {
                _waveSpawner.ConfigureForWave(_currentWave);
            }

            ApplyRunModifiers();
            _waveDamageTakenThisWave = false;
        }

        private bool HandlePauseToggleInput()
        {
            if (_isGameOver || _isInterlude)
            {
                return false;
            }

            bool pausePressed = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                pausePressed = true;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame)
            {
                pausePressed = true;
            }

            if (!pausePressed)
            {
                return false;
            }

            TogglePause();
            return true;
        }

        public void TogglePause()
        {
            if (_isGameOver || _isInterlude)
            {
                return;
            }

            if (_isPaused)
            {
                ResumeFromPause();
            }
            else if (_running)
            {
                PauseMatch();
            }
        }

        private void PauseMatch()
        {
            if (_isPaused || !_running || _isGameOver || _isInterlude)
            {
                return;
            }

            _isPaused = true;
            Time.timeScale = 0f;
            CreatePauseOverlay();
        }

        private void ResumeFromPause()
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            DestroyPauseOverlay();
            Time.timeScale = 1f;
            SetGameplayHudPauseButtonVisible(true);
        }

        private float GetWaveDurationSeconds(int waveNumber)
        {
            int wave = Mathf.Max(1, waveNumber);
            float first = Mathf.Max(1f, _firstWaveDurationSeconds);
            float extra = Mathf.Max(0f, _extraWaveDurationFractionPerWave);
            return first * (1f + extra * (wave - 1));
        }

        private bool ShouldStartBossFight(int completedWave)
        {
            int every = Mathf.Max(1, _bossEveryWaves);
            return completedWave > 0 && completedWave % every == 0;
        }

        private void StartBossFight()
        {
            if (_isGameOver)
            {
                return;
            }

            // Prevent MatchController from advancing waves while we show the warning.
            _isInterlude = false;
            _running = false;
            _currentWaveDuration = 0f;

            if (_waveSpawner != null)
            {
                _waveSpawner.SetSpawningEnabled(false);
                _waveSpawner.ClearActiveAsteroids();
            }

            ClearActiveProjectiles();

            Vector3 spawnPos = _playerSpawnPosition + new Vector3(0f, 5f, 0f);
            Camera cam = Camera.main;
            if (cam != null)
            {
                spawnPos = cam.transform.position + new Vector3(0f, cam.orthographicSize * 0.7f, 0f);
                spawnPos.z = 0f;
            }

            float bossHealth = CalculateBossHealth(_currentWave);
            Transform playerTarget = _playerHealth != null ? _playerHealth.transform : null;

            GameObject warningGo = CreateBossWarningOverlay();
            StartCoroutine(BeginBossFightAfterWarning(warningGo, spawnPos, bossHealth, playerTarget));
        }

        private IEnumerator BeginBossFightAfterWarning(GameObject warningGo, Vector3 spawnPos, float bossHealth, Transform playerTarget)
        {
            float wait = Mathf.Max(0f, _bossWarningDurationSeconds);
            if (wait > 0f)
            {
                yield return new WaitForSecondsRealtime(wait);
            }

            if (warningGo != null)
            {
                Destroy(warningGo);
            }

            EnsureBossShip();
            _isBossFight = true;
            _running = true;
            _bossFightElapsed = 0f;

            _bossShip.Activate(playerTarget, bossHealth, spawnPos);
            AudioManager.EnsureExists().PlayBossMusic();
        }

        private GameObject CreateBossWarningOverlay()
        {
            GameObject canvasGo = new GameObject("BossWarningCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 650;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.45f);

            CreateGameOverLine(
                panel.transform,
                "BossWarningTitle",
                "Warning!!\nA Huge battleship is approaching fast",
                new Vector2(0f, 120f),
                new Vector2(1200f, 200f),
                58f,
                TextAlignmentOptions.Center);

            return canvasGo;
        }

        private float CalculateBossHealth(int currentWave)
        {
            int cycle = Mathf.Max(0, (currentWave / Mathf.Max(1, _bossEveryWaves)) - 1);
            return _bossBaseHealth + _bossHealthPerCycle * cycle;
        }

        private void HandleBossDefeated()
        {
            if (_isGameOver)
            {
                return;
            }

            // Freeze match logic immediately so the next Update tick can't start another boss
            // while _currentWaveDuration is still 0f (boss fight state).
            _isBossFight = false;
            _running = false;
            _isPaused = false;
            Time.timeScale = 0f;
            DestroyPauseOverlay();

            _score += 1000 + (_currentWave * 100);
            _runScrap += 30;
            float bossDuration = _bossFightElapsed;
            int nextWave = _currentWave + 1;

            Vector3 bossPos = _bossShip != null ? _bossShip.transform.position : Vector3.zero;
            if (_bossShip != null)
            {
                _bossShip.gameObject.SetActive(false);
            }

            float wait = Mathf.Max(0.1f, _bossExplosionDurationSeconds);
            float intensity = Mathf.Max(0.5f, _bossDeathExplosionIntensity);
            GameObject bossExplosionVfx = SpawnOrangeDeathExplosion(bossPos, wait, intensity, "BossOrangeExplosion");
            StartCoroutine(FinishBossDeathAfterExplosionVfx(nextWave, bossDuration, bossExplosionVfx, wait));
        }

        private IEnumerator FinishBossDeathAfterExplosionVfx(int nextWave, float bossDuration, GameObject vfxGo, float wait)
        {
            yield return new WaitForSecondsRealtime(wait + 0.05f);
            if (vfxGo != null)
            {
                Destroy(vfxGo);
            }

            BeginWaveInterlude(nextWave, bossDuration);
        }

        private static void ConfigureAdditiveMaterial(ParticleSystemRenderer renderer, out Material material)
        {
            material = null;
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Particles/Additive");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader != null)
            {
                material = new Material(shader);
                renderer.material = material;
            }
        }

        private GameObject CreateGameOverExplosionVfx(Vector3 position, float visualDuration = -1f)
        {
            float dur = visualDuration > 0f ? visualDuration : _gameOverExplosionVisualDuration;
            return SpawnOrangeDeathExplosion(position, dur, 1f, "OrangeExplosionVFX");
        }

        /// <summary>Shared orange burst + embers used for game over and boss death.</summary>
        private GameObject SpawnOrangeDeathExplosion(Vector3 position, float visualDuration, float intensity, string rootObjectName)
        {
            float i = Mathf.Max(0.5f, intensity);
            GameObject root = new GameObject(string.IsNullOrEmpty(rootObjectName) ? "OrangeExplosionVFX" : rootObjectName);
            root.transform.position = position;

            CreateGameOverOrangeBurst(root, visualDuration, i);
            CreateGameOverOrangeEmbers(root, visualDuration, i);

            return root;
        }

        private static void CreateGameOverOrangeBurst(GameObject root, float visualDuration, float intensity = 1f)
        {
            float i = Mathf.Max(0.5f, intensity);
            GameObject flashGo = new GameObject("OrangeBurst");
            flashGo.transform.SetParent(root.transform, false);

            ParticleSystem ps = flashGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.08f;
            main.startLifetime = Mathf.Clamp(visualDuration * 0.5f, 0.22f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f * i, 7.5f * i);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * i, 0.38f * i);
            main.gravityModifier = 0.15f;
            main.maxParticles = Mathf.Min(8000, Mathf.RoundToInt(2200 * i));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.35f, 0.08f * i);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.35f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.08f, 1f), 0.22f),
                    new GradientColorKey(new Color(1f, 0.22f, 0.02f, 0.85f), 0.55f),
                    new GradientColorKey(new Color(0.2f, 0.04f, 0f, 0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.35f),
                    new GradientAlphaKey(0f, 1f),
                }
            };

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0.12f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            ConfigureAdditiveMaterial(renderer, out _);

            ps.Play();
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(520 * i), 200, 4000));
        }

        private static void CreateGameOverOrangeEmbers(GameObject root, float visualDuration, float intensity = 1f)
        {
            float i = Mathf.Max(0.5f, intensity);
            GameObject emberGo = new GameObject("OrangeEmbers");
            emberGo.transform.SetParent(root.transform, false);

            ParticleSystem ps = emberGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.06f;
            main.startLifetime = Mathf.Clamp(visualDuration * 0.85f, 0.35f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f * i, 2.2f * i);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f * i, 0.22f * i);
            main.gravityModifier = -0.05f;
            main.maxParticles = Mathf.Min(5000, Mathf.RoundToInt(1400 * i));
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.45f, 0.18f * i);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.72f, 0.18f, 0.95f), 0f),
                    new GradientColorKey(new Color(1f, 0.38f, 0.05f, 0.65f), 0.45f),
                    new GradientColorKey(new Color(0.35f, 0.08f, 0.02f, 0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0f, 1f),
                }
            };

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.05f, 2.1f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            ConfigureAdditiveMaterial(renderer, out _);

            ps.Play();
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(220 * i), 80, 2000));
        }

        private void CreateGameOverOverlay(List<int> topScores, int totalScrap)
        {
            GameObject canvasGo = new GameObject("GameOverCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            UiSelectionKeepAlive gameOverKeepAlive = canvasGo.AddComponent<UiSelectionKeepAlive>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.02f, 0.04f, 0.94f);
            MenuPanelPointerGuard gameOverPointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

            CreateGameOverLine(panel.transform, "GameOverTitle", "GAME OVER", new Vector2(0f, 248f), new Vector2(1000f, 100f), 80f, TextAlignmentOptions.Center);
            CreateGameOverLine(panel.transform, "FinalScore", "Score: " + _score, new Vector2(0f, 148f), new Vector2(920f, 64f), 48f, TextAlignmentOptions.Center);
            CreateGameOverLine(panel.transform, "ScrapLine", "Scrap Gained: +" + _runScrap + " (Total " + totalScrap + ")", new Vector2(0f, 78f), new Vector2(920f, 48f), 30f, TextAlignmentOptions.Center);

            string scoreList = BuildTopScoreText(topScores);
            TMP_Text topScoresText = CreateGameOverLine(
                panel.transform,
                "TopScores",
                scoreList,
                new Vector2(0f, 48f),
                new Vector2(820f, 300f),
                28f,
                TextAlignmentOptions.Top,
                new Vector2(0.5f, 1f));

            int highlightRow = GetTopScoreHighlightRowIndex(topScores, _score);
            if (highlightRow >= 0 && topScoresText != null)
            {
                GameOverTopScoreLineHighlight pulse = topScoresText.gameObject.AddComponent<GameOverTopScoreLineHighlight>();
                pulse.Configure(topScoresText, highlightRow);
            }

            Button retryButton = CreateButton(panel.transform, "RetryButton", "Retry", new Vector2(0.42f, 0.2f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                Time.timeScale = 1f;
                SceneManager.LoadScene("Gameplay");
            });

            CreateButton(panel.transform, "TitleButton", "Title", new Vector2(0.58f, 0.2f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                Time.timeScale = 1f;
                SceneManager.LoadScene("TitleScreen");
            });

            SetSelectedButton(retryButton);
            gameOverKeepAlive.DefaultSelection = retryButton.gameObject;
            gameOverPointerGuard.DefaultSelection = retryButton.gameObject;
        }

        private void CreatePauseOverlay()
        {
            DestroyPauseOverlay();

            GameObject canvasGo = new GameObject("PauseCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 520;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            UiSelectionKeepAlive pauseKeepAlive = canvasGo.AddComponent<UiSelectionKeepAlive>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.03f, 0.06f, 0.86f);
            MenuPanelPointerGuard pausePointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

            GameObject pauseMenuRoot = new GameObject("PauseMenuRoot");
            pauseMenuRoot.transform.SetParent(panel.transform, false);
            RectTransform pauseMenuRect = pauseMenuRoot.AddComponent<RectTransform>();
            pauseMenuRect.anchorMin = Vector2.zero;
            pauseMenuRect.anchorMax = Vector2.one;
            pauseMenuRect.offsetMin = Vector2.zero;
            pauseMenuRect.offsetMax = Vector2.zero;
            pauseMenuRect.anchoredPosition = UseMobileUpgradeLayout() ? new Vector2(0f, 150f) : new Vector2(0f, 90f);

            bool useMobilePauseLayout = UseMobileUpgradeLayout();
            CreateGameOverLine(
                pauseMenuRoot.transform,
                "PauseTitle",
                "PAUSED",
                new Vector2(0f, useMobilePauseLayout ? 210f : 170f),
                new Vector2(useMobilePauseLayout ? 1100f : 900f, useMobilePauseLayout ? 120f : 100f),
                useMobilePauseLayout ? 92f : 76f,
                TextAlignmentOptions.Center);
            CreateGameOverLine(
                pauseMenuRoot.transform,
                "PauseHint",
                "Take a breath, then resume when ready.",
                new Vector2(0f, useMobilePauseLayout ? 120f : 90f),
                new Vector2(useMobilePauseLayout ? 1240f : 1000f, useMobilePauseLayout ? 70f : 50f),
                useMobilePauseLayout ? 40f : 30f,
                TextAlignmentOptions.Center);

            Vector2 pauseBtnSize = useMobilePauseLayout ? new Vector2(320f, 80f) : new Vector2(260f, 72f);
            Button resumeButton = CreateButton(pauseMenuRoot.transform, "ResumeButton", "Resume", new Vector2(0.5f, 0.505f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ResumeFromPause();
            });
            SetRectSize(resumeButton, pauseBtnSize);
            ConfigureButtonText(
                resumeButton,
                useMobilePauseLayout ? 42f : 36f,
                useMobilePauseLayout ? 26f : 18f,
                useMobilePauseLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobilePauseLayout ? -6f : -10f);

            Button infoButton = null;
            Button infoCloseButton = null;
            GameObject infoWindow = CreatePauseInfoWindow(
                panel.transform,
                pauseMenuRoot,
                pauseKeepAlive,
                pausePointerGuard,
                () => infoButton,
                () => SetGameplayHudPauseButtonVisible(true),
                out infoCloseButton);

            Button achievementsButton = null;
            Button achievementsCloseButton = null;
            TMP_Text achievementsBodyText;
            GameObject achievementsWindow = CreatePauseAchievementsWindow(
                panel.transform,
                pauseMenuRoot,
                pauseKeepAlive,
                pausePointerGuard,
                () => achievementsButton,
                () => SetGameplayHudPauseButtonVisible(true),
                out achievementsBodyText,
                out achievementsCloseButton);

            achievementsButton = CreateButton(pauseMenuRoot.transform, "AchievementsFromPauseButton", "Achievements", new Vector2(0.5f, 0.385f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                AchievementListPanel.RefreshBodyText(achievementsBodyText);
                SetGameplayHudPauseButtonVisible(false);
                pauseMenuRoot.SetActive(false);
                if (infoWindow != null)
                {
                    infoWindow.SetActive(false);
                }

                if (achievementsWindow != null)
                {
                    achievementsWindow.SetActive(true);
                    achievementsWindow.transform.SetAsLastSibling();
                }

                if (achievementsCloseButton != null)
                {
                    SetSelectedButton(achievementsCloseButton);
                    pauseKeepAlive.DefaultSelection = achievementsCloseButton.gameObject;
                    pausePointerGuard.DefaultSelection = achievementsCloseButton.gameObject;
                }
            });
            SetRectSize(achievementsButton, pauseBtnSize);
            ConfigureButtonText(
                achievementsButton,
                useMobilePauseLayout ? 42f : 36f,
                useMobilePauseLayout ? 26f : 18f,
                useMobilePauseLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobilePauseLayout ? -6f : -10f);

            infoButton = CreateButton(pauseMenuRoot.transform, "InfoFromPauseButton", "Help", new Vector2(0.5f, 0.155f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                SetGameplayHudPauseButtonVisible(false);
                pauseMenuRoot.SetActive(false);
                if (achievementsWindow != null)
                {
                    achievementsWindow.SetActive(false);
                }

                if (infoWindow != null)
                {
                    infoWindow.SetActive(true);
                }

                if (infoCloseButton != null)
                {
                    SetSelectedButton(infoCloseButton);
                    pauseKeepAlive.DefaultSelection = infoCloseButton.gameObject;
                    pausePointerGuard.DefaultSelection = infoCloseButton.gameObject;
                }
            });
            SetRectSize(infoButton, pauseBtnSize);
            ConfigureButtonText(
                infoButton,
                useMobilePauseLayout ? 42f : 36f,
                useMobilePauseLayout ? 26f : 18f,
                useMobilePauseLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobilePauseLayout ? -6f : -10f);

            Button retryButton = CreateButton(pauseMenuRoot.transform, "RetryFromPauseButton", "Retry", new Vector2(0.5f, 0.27f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                _isPaused = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("Gameplay");
            });
            SetRectSize(retryButton, pauseBtnSize);
            ConfigureButtonText(
                retryButton,
                useMobilePauseLayout ? 42f : 36f,
                useMobilePauseLayout ? 26f : 18f,
                useMobilePauseLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobilePauseLayout ? -6f : -10f);

            Button titleButton = CreateButton(pauseMenuRoot.transform, "TitleFromPauseButton", "Exit to Title", new Vector2(0.5f, 0.045f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                _isPaused = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("TitleScreen");
            });
            SetRectSize(titleButton, pauseBtnSize);
            ConfigureButtonText(
                titleButton,
                useMobilePauseLayout ? 42f : 36f,
                useMobilePauseLayout ? 26f : 18f,
                useMobilePauseLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobilePauseLayout ? -6f : -10f);

            SetSelectedButton(resumeButton);
            pauseKeepAlive.DefaultSelection = resumeButton.gameObject;
            pausePointerGuard.DefaultSelection = resumeButton.gameObject;
        }

        private static void DestroyPauseOverlay()
        {
            SetGameplayHudPauseButtonVisible(true);
            GameObject pauseCanvas = GameObject.Find("PauseCanvas");
            if (pauseCanvas != null)
            {
                Destroy(pauseCanvas);
            }
        }

        private void SetGameplayHudVisible(bool visible)
        {
            if (_gameplayHudCanvas == null)
            {
                _gameplayHudCanvas = GameObject.Find("GameplayHUD_Canvas");
            }

            if (_gameplayHudCanvas != null)
            {
                _gameplayHudCanvas.SetActive(visible);
            }
        }

        private static void SetGameplayHudPauseButtonVisible(bool visible)
        {
            GameplayHUD hud = UnityEngine.Object.FindFirstObjectByType<GameplayHUD>();
            if (hud != null)
            {
                hud.SetPauseButtonHidden(!visible);
            }
        }

        private static TMP_Text CreateGameOverLine(
            Transform parent,
            string objectName,
            string text,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            float fontSize,
            TextAlignmentOptions alignment,
            Vector2? pivot = null)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            GameFontLibrary.Apply(tmp);
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.color = new Color(0.93f, 0.97f, 1f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            GameFontLibrary.ApplyOutline(tmp, 0.18f, new Color(0.04f, 0.05f, 0.08f, 1f));

            tmp.raycastTarget = false;
            return tmp;
        }

        private static TMP_Text CreateText(Transform parent, string objectName, string text, Vector2 anchor, float fontSize, bool centered)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 300f);

            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            GameFontLibrary.Apply(tmp);
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Top;
            tmp.color = new Color(0.93f, 0.97f, 1f, 1f);
            GameFontLibrary.ApplyOutline(tmp, 0.18f, new Color(0.04f, 0.05f, 0.08f, 1f));
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform CreatePauseHelpSafeAreaHost(Transform parent)
        {
            Rect safe = Screen.safeArea;
            GameObject host = new GameObject("PauseHelpSafeArea");
            host.transform.SetParent(parent, false);
            host.transform.SetAsLastSibling();
            RectTransform rt = host.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void ApplyPauseHelpWindowFitInsideHost(RectTransform windowRt, RectTransform hostRt, Vector2 intendedSize)
        {
            if (windowRt == null || hostRt == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            const float pad = 28f;
            float availW = hostRt.rect.width - pad;
            float availH = hostRt.rect.height - pad;
            if (availW < 32f || availH < 32f || hostRt.rect.width < 2f || hostRt.rect.height < 2f)
            {
                RectTransform panelRt = hostRt.parent as RectTransform;
                if (panelRt != null)
                {
                    float nx = Screen.safeArea.width / Mathf.Max(1f, Screen.width);
                    float ny = Screen.safeArea.height / Mathf.Max(1f, Screen.height);
                    availW = Mathf.Max(80f, panelRt.rect.width * nx - pad);
                    availH = Mathf.Max(80f, panelRt.rect.height * ny - pad);
                }
                else
                {
                    availW = Mathf.Max(80f, availW);
                    availH = Mathf.Max(80f, availH);
                }
            }

            float f = Mathf.Min(1f, availW / Mathf.Max(1f, intendedSize.x), availH / Mathf.Max(1f, intendedSize.y));
            windowRt.localScale = new Vector3(f, f, 1f);
        }

        private static GameObject CreatePauseAchievementsWindow(
            Transform parent,
            GameObject pauseMenuRoot,
            UiSelectionKeepAlive pauseKeepAlive,
            MenuPanelPointerGuard pausePointerGuard,
            System.Func<Button> reopenButtonProvider,
            System.Action onAchievementsClosed,
            out TMP_Text achievementsBodyText,
            out Button closeButton)
        {
            bool useMobileLayout = UseMobileUpgradeLayout();
            Vector2 intendedSize = useMobileLayout ? new Vector2(1500f, 1040f) : new Vector2(1180f, 900f);
            RectTransform safeHost = CreatePauseHelpSafeAreaHost(parent);

            GameObject window = new GameObject("PauseAchievementsWindow");
            window.transform.SetParent(safeHost, false);
            window.transform.SetAsLastSibling();
            RectTransform windowRect = window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = intendedSize;
            windowRect.localScale = Vector3.one;

            Image windowImage = window.AddComponent<Image>();
            windowImage.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);
            windowImage.raycastTarget = true;
            Outline outline = window.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.86f, 1f, 0.45f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;

            CreateGameOverLine(
                window.transform,
                "PauseAchievementsTitle",
                "ACHIEVEMENTS",
                new Vector2(0f, useMobileLayout ? 448f : 392f),
                new Vector2(useMobileLayout ? 1200f : 900f, useMobileLayout ? 92f : 72f),
                useMobileLayout ? 78f : 62f,
                TextAlignmentOptions.Center);

            closeButton = CreateButton(window.transform, "CloseAchievementsButton", "X", new Vector2(0.5f, 0.5f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                window.SetActive(false);
                if (pauseMenuRoot != null)
                {
                    pauseMenuRoot.SetActive(true);
                }

                onAchievementsClosed?.Invoke();
                Button reopen = reopenButtonProvider != null ? reopenButtonProvider() : null;
                if (reopen != null)
                {
                    SetSelectedButton(reopen);
                    pauseKeepAlive.DefaultSelection = reopen.gameObject;
                    pausePointerGuard.DefaultSelection = reopen.gameObject;
                }
            });
            SetRectSize(closeButton, useMobileLayout ? new Vector2(100f, 100f) : new Vector2(88f, 88f));
            RectTransform closeRt = closeButton.GetComponent<RectTransform>();
            if (closeRt != null)
            {
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                float inset = useMobileLayout ? 14f : 12f;
                closeRt.anchoredPosition = new Vector2(-inset, -inset);
            }

            ApplyPauseHelpCloseButtonStyle(closeButton, useMobileLayout);

            AchievementListPanel.Layout scrollLayout = new AchievementListPanel.Layout(
                new Vector2(0.02f, 0.045f),
                new Vector2(0.98f, 0.855f),
                Vector2.zero,
                Vector2.zero);
            float bodyFont = useMobileLayout ? 42f : 36f;
            int descRich = useMobileLayout ? 34 : 30;
            float sbw = useMobileLayout ? 44f : 38f;
            AchievementListPanel.Style achStyle = new AchievementListPanel.Style(bodyFont, descRich, sbw);
            achievementsBodyText = AchievementListPanel.CreateScrollingBody(window.transform, scrollLayout, achStyle);

            ApplyPauseHelpWindowFitInsideHost(windowRect, safeHost, intendedSize);
            window.SetActive(false);
            return window;
        }

        private static GameObject CreatePauseInfoWindow(
            Transform parent,
            GameObject pauseMenuRoot,
            UiSelectionKeepAlive pauseKeepAlive,
            MenuPanelPointerGuard pausePointerGuard,
            System.Func<Button> reopenButtonProvider,
            System.Action onHelpScreenClosed,
            out Button closeButton)
        {
            bool useMobileLayout = UseMobileUpgradeLayout();
            Vector2 intendedSize = useMobileLayout ? new Vector2(1500f, 1040f) : new Vector2(1180f, 900f);
            RectTransform safeHost = CreatePauseHelpSafeAreaHost(parent);

            GameObject window = new GameObject("PauseInfoWindow");
            window.transform.SetParent(safeHost, false);
            window.transform.SetAsLastSibling();
            RectTransform windowRect = window.AddComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = intendedSize;
            windowRect.localScale = Vector3.one;

            Image windowImage = window.AddComponent<Image>();
            windowImage.color = new Color(0.06f, 0.09f, 0.14f, 0.97f);
            windowImage.raycastTarget = true;
            Outline outline = window.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.86f, 1f, 0.45f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;

            CreateGameOverLine(
                window.transform,
                "InfoTitle",
                "HELP & REFERENCE",
                new Vector2(0f, useMobileLayout ? 384f : 330f),
                new Vector2(useMobileLayout ? 1200f : 900f, useMobileLayout ? 92f : 72f),
                useMobileLayout ? 78f : 62f,
                TextAlignmentOptions.Center);

            closeButton = CreateButton(window.transform, "CloseInfoButton", "X", new Vector2(0.5f, 0.5f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                window.SetActive(false);
                if (pauseMenuRoot != null)
                {
                    pauseMenuRoot.SetActive(true);
                }

                onHelpScreenClosed?.Invoke();
                Button reopenButton = reopenButtonProvider != null ? reopenButtonProvider() : null;
                if (reopenButton != null)
                {
                    SetSelectedButton(reopenButton);
                    pauseKeepAlive.DefaultSelection = reopenButton.gameObject;
                    pausePointerGuard.DefaultSelection = reopenButton.gameObject;
                }
            });
            SetRectSize(closeButton, useMobileLayout ? new Vector2(100f, 100f) : new Vector2(88f, 88f));
            RectTransform closeRt = closeButton.GetComponent<RectTransform>();
            if (closeRt != null)
            {
                closeRt.anchorMin = new Vector2(1f, 1f);
                closeRt.anchorMax = new Vector2(1f, 1f);
                closeRt.pivot = new Vector2(1f, 1f);
                float inset = useMobileLayout ? 14f : 12f;
                closeRt.anchoredPosition = new Vector2(-inset, -inset);
            }

            ApplyPauseHelpCloseButtonStyle(closeButton, useMobileLayout);

            float tabCenterY = useMobileLayout ? 288f : 248f;
            float tabOffsetX = useMobileLayout ? 178f : 142f;
            Button itemsTabButton = CreateButton(window.transform, "ItemsTabButton", "Items", new Vector2(0.5f, 0.5f), () => { });
            Button upgradesTabButton = CreateButton(window.transform, "UpgradesTabButton", "Upgrades", new Vector2(0.5f, 0.5f), () => { });
            SetRectSize(itemsTabButton, useMobileLayout ? new Vector2(280f, 96f) : new Vector2(220f, 82f));
            SetRectSize(upgradesTabButton, useMobileLayout ? new Vector2(320f, 96f) : new Vector2(240f, 82f));
            ConfigureButtonText(
                itemsTabButton,
                useMobileLayout ? 38f : 30f,
                useMobileLayout ? 24f : 18f,
                useMobileLayout ? 38f : 30f,
                new Vector2(18f, 12f),
                new Vector2(-18f, -10f),
                useMobileLayout ? -4f : -8f);
            ConfigureButtonText(
                upgradesTabButton,
                useMobileLayout ? 38f : 30f,
                useMobileLayout ? 24f : 18f,
                useMobileLayout ? 38f : 30f,
                new Vector2(18f, 12f),
                new Vector2(-18f, -10f),
                useMobileLayout ? -4f : -8f);
            RectTransform itemsTabRt = itemsTabButton.GetComponent<RectTransform>();
            RectTransform upgradesTabRt = upgradesTabButton.GetComponent<RectTransform>();
            if (itemsTabRt != null)
            {
                itemsTabRt.anchorMin = itemsTabRt.anchorMax = new Vector2(0.5f, 0.5f);
                itemsTabRt.pivot = new Vector2(0.5f, 0.5f);
                itemsTabRt.anchoredPosition = new Vector2(-tabOffsetX, tabCenterY);
            }

            if (upgradesTabRt != null)
            {
                upgradesTabRt.anchorMin = upgradesTabRt.anchorMax = new Vector2(0.5f, 0.5f);
                upgradesTabRt.pivot = new Vector2(0.5f, 0.5f);
                upgradesTabRt.anchoredPosition = new Vector2(tabOffsetX, tabCenterY);
            }

            float scrollPanelW = useMobileLayout ? 1320f : 1040f;
            float scrollViewH = useMobileLayout ? 580f : 470f;
            float itemRowSpacing = useMobileLayout ? 148f : 102f;
            float upgradeRowSpacing = useMobileLayout ? 168f : 118f;
            float listTopPad = useMobileLayout ? 64f : 52f;
            float listBottomPad = useMobileLayout ? 72f : 60f;
            float itemContentH = listTopPad + (5f * itemRowSpacing) + listBottomPad;
            float upgradeContentH = listTopPad + (8f * upgradeRowSpacing) + listBottomPad;
            Vector2 scrollPanelPos = new Vector2(0f, useMobileLayout ? -88f : -82f);

            ScrollRect itemsScroll = CreatePauseInfoScrollArea(
                window.transform,
                "ItemsScroll",
                scrollPanelW,
                scrollViewH,
                itemContentH,
                scrollPanelPos,
                useMobileLayout,
                out RectTransform itemsContentRect);
            ScrollRect upgradesScroll = CreatePauseInfoScrollArea(
                window.transform,
                "UpgradesScroll",
                scrollPanelW,
                scrollViewH,
                upgradeContentH,
                scrollPanelPos,
                useMobileLayout,
                out RectTransform upgradesContentRect);
            upgradesScroll.gameObject.SetActive(false);

            PauseHelpScrollInput helpScrollInput = window.AddComponent<PauseHelpScrollInput>();
            helpScrollInput.SetScrollRects(itemsScroll, upgradesScroll);

            itemsTabButton.onClick.RemoveAllListeners();
            itemsTabButton.onClick.AddListener(() =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                SetPauseInfoTab(itemsScroll.gameObject, upgradesScroll.gameObject, itemsTabButton, upgradesTabButton, true);
                itemsScroll.verticalNormalizedPosition = 1f;
            });
            upgradesTabButton.onClick.RemoveAllListeners();
            upgradesTabButton.onClick.AddListener(() =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                SetPauseInfoTab(itemsScroll.gameObject, upgradesScroll.gameObject, itemsTabButton, upgradesTabButton, false);
                upgradesScroll.verticalNormalizedPosition = 1f;
            });

            Transform itemsContent = itemsContentRect.transform;
            CreatePauseInfoRow(itemsContent, "ShieldInfo", GetPauseInfoItemSprite(), ItemVisualColors.Get(ItemType.ContactShield), "C", "Contact Shield", "Blocks collision damage for a short time.", new Vector2(0f, -(listTopPad + (0f * itemRowSpacing))), true);
            CreatePauseInfoRow(itemsContent, "LaserInfo", GetPauseInfoItemSprite(), ItemVisualColors.Get(ItemType.PiercingLaser), "L", "Piercing Laser", "Shots become laser-like and pierce through multiple targets for a limited time.", new Vector2(0f, -(listTopPad + (1f * itemRowSpacing))), true);
            CreatePauseInfoRow(itemsContent, "OverdriveInfo", GetPauseInfoItemSprite(), ItemVisualColors.Get(ItemType.Overdrive), "O", "Overdrive", "Boosts fire rate and projectile speed while the effect lasts.", new Vector2(0f, -(listTopPad + (2f * itemRowSpacing))), true);
            CreatePauseInfoRow(itemsContent, "TimeWarpInfo", GetPauseInfoItemSprite(), ItemVisualColors.Get(ItemType.TimeWarp), "T", "Time Warp", "Slows enemy movement temporarily to create breathing room.", new Vector2(0f, -(listTopPad + (3f * itemRowSpacing))), true);
            CreatePauseInfoRow(itemsContent, "HealthPackInfo", GetPauseInfoItemSprite(), ItemVisualColors.Get(ItemType.HealthPack), "H", "Health Pack", "Instantly restores part of your HP.", new Vector2(0f, -(listTopPad + (4f * itemRowSpacing))), true);

            float leftX = useMobileLayout ? -338f : -272f;
            float rightX = useMobileLayout ? 338f : 272f;
            Transform upgradesContent = upgradesContentRect.transform;
            CreatePauseInfoRow(upgradesContent, "UpgradeVolt1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.VoltOverclock), "V", "Volt Overclock", "-8% fire interval.", new Vector2(leftX, -(listTopPad + (0f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeVolt2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.VoltChainCharge), "V", "Volt Accelerator", "+12% projectile speed.", new Vector2(rightX, -(listTopPad + (0f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeKin1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.KineticPayload), "K", "Kinetic Payload", "+12% projectile damage.", new Vector2(leftX, -(listTopPad + (1f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeKin2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.KineticSlinger), "K", "Kinetic Slinger", "+10% projectile speed, +4% damage.", new Vector2(rightX, -(listTopPad + (1f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeTherm1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.ThermalFlux), "T", "Thermal Shrapnel", "+0.30 splash radius and enables AOE shots.", new Vector2(leftX, -(listTopPad + (2f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeTherm2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.ThermalCore), "T", "Thermal Reactor", "+8% damage, +0.38 splash, +6% splash damage.", new Vector2(rightX, -(listTopPad + (2f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeStatic1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.StaticPlating), "S", "Static Plating", "-8% incoming damage.", new Vector2(leftX, -(listTopPad + (3f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeStatic2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.StaticField), "S", "Static Field", "Heal 12 HP, -3% fire interval, -4% incoming damage.", new Vector2(rightX, -(listTopPad + (3f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeRepair1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.RepairNanites), "R", "Nanite Swarm", "+0.09 HP/sec regeneration.", new Vector2(leftX, -(listTopPad + (4f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeRepair2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.RepairWeave), "R", "Biosuture Weave", "+0.07 HP/sec regeneration.", new Vector2(rightX, -(listTopPad + (4f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeReach1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.ReachExtender), "E", "Coil Extender", "+9% projectile travel distance.", new Vector2(leftX, -(listTopPad + (5f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeReach2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.ReachCalibrator), "E", "Harmonic Lens", "+7% reach, +3% damage.", new Vector2(rightX, -(listTopPad + (5f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeVolley1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.VolleySpread), "C", "Scatter Matrix", "+1 spread shot (up to 4, fan pattern).", new Vector2(leftX, -(listTopPad + (6f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeVit1", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.BackupCell), "L", "Backup Cell", "+1 extra life; revive at 50% HP on lethal hit.", new Vector2(rightX, -(listTopPad + (6f * upgradeRowSpacing))), true);
            CreatePauseInfoRow(upgradesContent, "UpgradeVit2", UpgradeHudVisuals.GetDiamondSprite(), UpgradeHudVisuals.GetUpgradeColor(RunUpgradeController.UpgradeId.ReserveHarness), "L", "Reserve Harness", "+1 extra life, heal 15 HP; revive at 50% HP.", new Vector2(leftX, -(listTopPad + (7f * upgradeRowSpacing))), true);

            SetPauseInfoTab(itemsScroll.gameObject, upgradesScroll.gameObject, itemsTabButton, upgradesTabButton, true);

            if (closeButton != null)
            {
                closeButton.transform.SetAsLastSibling();
            }

            ApplyPauseHelpWindowFitInsideHost(windowRect, safeHost, intendedSize);

            window.SetActive(false);
            return window;
        }

        private static void ApplyPauseHelpCloseButtonStyle(Button closeButton, bool useMobileLayout)
        {
            if (closeButton == null)
            {
                return;
            }

            Image img = closeButton.targetGraphic as Image;
            if (img != null)
            {
                img.color = new Color(0.95f, 0.32f, 0.38f, 1f);
            }

            TMP_Text label = GetButtonLabel(closeButton);
            if (label != null)
            {
                label.text = "X";
                label.color = Color.white;
                label.enableAutoSizing = false;
                label.fontSize = useMobileLayout ? 52f : 46f;
                label.fontStyle = FontStyles.Bold;
            }

            Outline outline = closeButton.gameObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = closeButton.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.02f, 0.04f, 0.08f, 0.92f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            outline.useGraphicAlpha = false;
        }

        private static Scrollbar CreatePauseHelpVerticalScrollbar(Transform parent, float widthPx, float verticalInset)
        {
            GameObject sbGo = new GameObject("ScrollbarVertical");
            sbGo.transform.SetParent(parent, false);
            RectTransform sbRt = sbGo.AddComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(widthPx, -verticalInset * 2f);
            sbRt.anchoredPosition = new Vector2(0f, 0f);

            Scrollbar sb = sbGo.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.interactable = true;
            sb.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = sb.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.82f, 0.95f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.55f, 0.58f, 0.62f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            sb.colors = colors;

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sbGo.transform, false);
            RectTransform bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.1f, 0.16f, 0.92f);
            bgImg.raycastTarget = true;

            GameObject sliding = new GameObject("SlidingArea");
            sliding.transform.SetParent(sbGo.transform, false);
            RectTransform slidingRt = sliding.AddComponent<RectTransform>();
            slidingRt.anchorMin = new Vector2(0.12f, 0.03f);
            slidingRt.anchorMax = new Vector2(0.88f, 0.97f);
            slidingRt.offsetMin = Vector2.zero;
            slidingRt.offsetMax = Vector2.zero;
            slidingRt.anchoredPosition = Vector2.zero;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(sliding.transform, false);
            RectTransform handleRt = handle.AddComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.sizeDelta = Vector2.zero;
            handleRt.anchoredPosition = Vector2.zero;
            Image handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.42f, 0.72f, 1f, 0.95f);
            handleImg.raycastTarget = true;

            sb.targetGraphic = handleImg;
            sb.handleRect = handleRt;
            return sb;
        }

        private static ScrollRect CreatePauseInfoScrollArea(
            Transform parent,
            string rootName,
            float width,
            float viewportHeight,
            float contentHeight,
            Vector2 anchoredPosition,
            bool useMobileLayout,
            out RectTransform contentRect)
        {
            float scrollbarWidth = useMobileLayout ? 22f : 18f;
            float scrollbarInsetV = 6f;

            GameObject root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(width, viewportHeight);
            rootRt.anchoredPosition = anchoredPosition;

            ScrollRect sr = root.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 36f;
            sr.inertia = true;
            sr.decelerationRate = 0.2f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            RectTransform vpRt = viewport.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = new Vector2(-scrollbarWidth, 0f);
            vpRt.anchoredPosition = Vector2.zero;
            Image vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0.03f, 0.06f, 0.12f, 0.4f);
            vpImg.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            Scrollbar vScroll = CreatePauseHelpVerticalScrollbar(root.transform, scrollbarWidth, scrollbarInsetV);
            sr.verticalScrollbar = vScroll;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform cRt = content.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.sizeDelta = new Vector2(0f, contentHeight);

            sr.viewport = vpRt;
            sr.content = cRt;
            contentRect = cRt;
            return sr;
        }

        private static void SetPauseInfoTab(GameObject itemsScrollRoot, GameObject upgradesScrollRoot, Button itemsTabButton, Button upgradesTabButton, bool showItems)
        {
            if (itemsScrollRoot != null)
            {
                itemsScrollRoot.SetActive(showItems);
            }

            if (upgradesScrollRoot != null)
            {
                upgradesScrollRoot.SetActive(!showItems);
            }

            SetPauseInfoTabButtonState(itemsTabButton, showItems);
            SetPauseInfoTabButtonState(upgradesTabButton, !showItems);
        }

        private static void SetPauseInfoTabButtonState(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = active ? new Color(0.28f, 0.54f, 1f, 1f) : new Color(0.18f, 0.28f, 0.58f, 0.95f);
            }
        }

        private static void CreatePauseInfoRow(
            Transform parent,
            string objectName,
            Sprite iconSprite,
            Color iconColor,
            string iconLabel,
            string title,
            string description,
            Vector2 anchoredPosition,
            bool anchorFromScrollContentTop = false)
        {
            bool useMobileLayout = UseMobileUpgradeLayout();
            GameObject row = new GameObject(objectName);
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            if (anchorFromScrollContentTop)
            {
                rowRect.anchorMin = new Vector2(0.5f, 1f);
                rowRect.anchorMax = new Vector2(0.5f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rowRect.anchorMin = new Vector2(0.5f, 0.5f);
                rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                rowRect.pivot = new Vector2(0.5f, 0.5f);
            }

            rowRect.anchoredPosition = anchoredPosition;
            rowRect.sizeDelta = useMobileLayout ? new Vector2(640f, 176f) : new Vector2(520f, 118f);

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(useMobileLayout ? 62f : 54f, 0f);
            iconRect.sizeDelta = useMobileLayout ? new Vector2(92f, 92f) : new Vector2(70f, 70f);
            Image iconImage = iconGo.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.color = iconColor;
            iconImage.raycastTarget = false;

            if (!string.IsNullOrEmpty(iconLabel))
            {
                GameObject iconLabelGo = new GameObject("IconLabel");
                iconLabelGo.transform.SetParent(iconGo.transform, false);
                RectTransform iconLabelRect = iconLabelGo.AddComponent<RectTransform>();
                iconLabelRect.anchorMin = Vector2.zero;
                iconLabelRect.anchorMax = Vector2.one;
                iconLabelRect.offsetMin = Vector2.zero;
                iconLabelRect.offsetMax = Vector2.zero;
                TMP_Text iconLabelText = iconLabelGo.AddComponent<TextMeshProUGUI>();
                GameFontLibrary.Apply(iconLabelText);
                iconLabelText.fontSize = useMobileLayout ? 56f : 40f;
                iconLabelText.fontStyle = FontStyles.Bold;
                iconLabelText.text = iconLabel;
                iconLabelText.color = new Color(0.04f, 0.05f, 0.08f, 1f);
                iconLabelText.alignment = TextAlignmentOptions.Center;
                iconLabelText.raycastTarget = false;
            }

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(row.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(0f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(useMobileLayout ? 128f : 108f, useMobileLayout ? 34f : 22f);
            titleRect.sizeDelta = new Vector2(useMobileLayout ? 480f : 360f, useMobileLayout ? 62f : 44f);
            TMP_Text titleText = titleGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(titleText);
            titleText.fontSize = useMobileLayout ? 46f : 32f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.text = title;
            titleText.color = new Color(0.96f, 0.98f, 1f, 1f);
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.raycastTarget = false;

            GameObject descGo = new GameObject("Description");
            descGo.transform.SetParent(row.transform, false);
            RectTransform descRect = descGo.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0.5f);
            descRect.anchorMax = new Vector2(0f, 0.5f);
            descRect.pivot = new Vector2(0f, 0.5f);
            descRect.anchoredPosition = new Vector2(useMobileLayout ? 128f : 108f, useMobileLayout ? -26f : -16f);
            descRect.sizeDelta = new Vector2(useMobileLayout ? 500f : 380f, useMobileLayout ? 102f : 64f);
            TMP_Text descText = descGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(descText);
            descText.fontSize = useMobileLayout ? 36f : 24f;
            descText.text = description;
            descText.color = new Color(0.8f, 0.9f, 1f, 0.96f);
            descText.alignment = TextAlignmentOptions.Left;
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode = TextOverflowModes.Overflow;
            descText.raycastTarget = false;
        }

        private static Sprite GetPauseInfoItemSprite()
        {
            if (_pauseInfoItemSpriteSolid != null)
            {
                return _pauseInfoItemSpriteSolid;
            }

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = 30f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = d <= outer ? 1f : 0f;
                    if (d > outer && d <= outer + 1.35f)
                    {
                        alpha = 1f - Mathf.InverseLerp(outer, outer + 1.35f, d);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _pauseInfoItemSpriteSolid = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _pauseInfoItemSpriteSolid;
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(parent, false);
            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 86f);

            Image image = buttonGo.AddComponent<Image>();
            Button btn = buttonGo.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);
            buttonGo.AddComponent<UiSelectOnPointerEnter>();

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 12f);
            textRect.offsetMax = new Vector2(-18f, -10f);
            TMP_Text t = textGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = label.Contains("\n") ? 26f : 36f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.85f, 0.95f, 1f, 1f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.enableAutoSizing = true;
            t.fontSizeMin = label.Contains("\n") ? 14f : 18f;
            t.fontSizeMax = label.Contains("\n") ? 26f : 36f;
            t.lineSpacing = -10f;
            t.raycastTarget = false;
            PixelArtUiSkin.ApplyButtonStyle(btn, image, t);
            return btn;
        }

        private static bool UseMobileUpgradeLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return Application.isMobilePlatform || shortSide <= 1080;
        }

        private static void SetRectSize(Component component, Vector2 size)
        {
            if (component == null)
            {
                return;
            }

            RectTransform rect = component.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = size;
            }
        }

        private static RectTransform CreateContainer(Transform parent, string objectName, Vector2 anchor, Vector2 size, Color fillColor)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = fillColor;
            image.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.9f, 1f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            return rect;
        }

        private static TMP_Text GetButtonLabel(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform label = button.transform.Find("Label");
            if (label == null)
            {
                return null;
            }

            return label.GetComponent<TMP_Text>();
        }

        private static void ConfigureButtonText(Button button, float baseSize, float minSize, float maxSize, Vector2 paddingMin, Vector2 paddingMax, float lineSpacing = -10f)
        {
            TMP_Text label = GetButtonLabel(button);
            if (label == null)
            {
                return;
            }

            label.fontSize = baseSize;
            label.fontSizeMin = minSize;
            label.fontSizeMax = maxSize;
            label.lineSpacing = lineSpacing;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform labelRect = label.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.offsetMin = paddingMin;
                labelRect.offsetMax = paddingMax;
            }
        }

        private void CreateInterludeBuildSummaryRow(RectTransform buildContainer, bool useMobileLayout)
        {
            GameObject rowGo = new GameObject("BuildLineRow", typeof(RectTransform));
            rowGo.transform.SetParent(buildContainer, false);
            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = Vector2.zero;
            rowRt.anchorMax = Vector2.one;
            rowRt.offsetMin = new Vector2(6f, 4f);
            rowRt.offsetMax = new Vector2(-6f, -4f);

            HorizontalLayoutGroup hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = useMobileLayout ? 12f : 8f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(4, 4, 2, 2);

            CreateInterludeBuildPrefixLabel(
                rowGo.transform,
                "Current Build",
                useMobileLayout ? 34f : 28f,
                useMobileLayout ? 220f : 180f);

            for (int i = 0; i < RunUpgradeController.LoadoutSlotCount; i++)
            {
                AddInterludeLoadoutSlotPip(rowGo.transform, i, _runUpgradeController, useMobileLayout);
            }
        }

        private static void CreateInterludeBuildPrefixLabel(Transform parent, string text, float fontSize, float preferredWidth)
        {
            GameObject go = new GameObject("BuildPrefix", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.minWidth = 96f;
            le.flexibleWidth = 0f;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(preferredWidth, 40f);
            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(tmp);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = new Color(0.93f, 0.97f, 1f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            GameFontLibrary.ApplyOutline(tmp, 0.16f, new Color(0.04f, 0.05f, 0.08f, 1f));
        }

        private static void AddInterludeLoadoutSlotPip(Transform parent, int slotIndex, RunUpgradeController run, bool useMobileLayout)
        {
            CardTag tag = run != null ? run.GetLoadoutSlotTag(slotIndex) : CardTag.None;
            string letter = tag == CardTag.None ? "?" : GetCardTagLetter(tag);
            AddInterludeBuildTagPip(parent, tag, letter, run, useMobileLayout, slotIndex);
        }

        private static void AddInterludeBuildTagPip(Transform parent, CardTag tag, string letter, RunUpgradeController run, bool useMobileLayout, int slotIndex = -1)
        {
            float iconSize = useMobileLayout ? 42f : 36f;
            string colName = slotIndex >= 0 ? "LoadoutSlot_" + slotIndex : "BuildPip_" + tag;
            GameObject col = new GameObject(colName, typeof(RectTransform));
            col.transform.SetParent(parent, false);
            VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            LayoutElement colLe = col.AddComponent<LayoutElement>();
            colLe.preferredWidth = iconSize + 18f;

            GameObject diamondGo = new GameObject("Diamond", typeof(RectTransform));
            diamondGo.transform.SetParent(col.transform, false);
            LayoutElement dLe = diamondGo.AddComponent<LayoutElement>();
            dLe.preferredWidth = iconSize;
            dLe.preferredHeight = iconSize;
            RectTransform dRt = diamondGo.GetComponent<RectTransform>();
            dRt.sizeDelta = new Vector2(iconSize, iconSize);
            Image img = diamondGo.AddComponent<Image>();
            img.sprite = UpgradeHudVisuals.GetDiamondSprite();
            img.color = tag == CardTag.None
                ? new Color(0.32f, 0.36f, 0.42f, 0.55f)
                : UpgradeHudVisuals.GetTagColor(tag);
            img.preserveAspect = true;
            img.raycastTarget = false;

            GameObject letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(diamondGo.transform, false);
            RectTransform letterRt = letterGo.GetComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero;
            letterRt.anchorMax = Vector2.one;
            letterRt.offsetMin = Vector2.zero;
            letterRt.offsetMax = Vector2.zero;
            TMP_Text letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(letterTmp);
            letterTmp.text = letter;
            letterTmp.fontSize = useMobileLayout ? 30f : 26f;
            letterTmp.fontStyle = FontStyles.Bold;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.color = tag == CardTag.None
                ? new Color(0.82f, 0.86f, 0.92f, 1f)
                : new Color(0.04f, 0.05f, 0.08f, 1f);
            letterTmp.raycastTarget = false;

            GameObject countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(col.transform, false);
            LayoutElement cLe = countGo.AddComponent<LayoutElement>();
            cLe.preferredHeight = 26f;
            RectTransform cRt = countGo.GetComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(iconSize + 14f, 26f);
            TMP_Text countTmp = countGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(countTmp);
            countTmp.text = GetInterludeSlotCountString(run, slotIndex);
            countTmp.fontSize = useMobileLayout ? 24f : 20f;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.alignment = TextAlignmentOptions.Center;
            countTmp.color = new Color(0.84f, 0.92f, 1f, 0.96f);
            countTmp.raycastTarget = false;
            GameFontLibrary.ApplyOutline(countTmp, 0.12f, new Color(0.04f, 0.05f, 0.08f, 0.9f));
        }

        private static string GetInterludeSlotCountString(RunUpgradeController run, int slotIndex)
        {
            if (run == null || slotIndex < 0)
            {
                return "--";
            }

            return run.GetLoadoutSlotStacksText(slotIndex);
        }

        private static string GetCardTagLetter(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt:
                    return "V";
                case CardTag.Kinetic:
                    return "K";
                case CardTag.Thermal:
                    return "T";
                case CardTag.Static:
                    return "S";
                case CardTag.Repair:
                    return "R";
                case CardTag.Reach:
                    return "E";
                case CardTag.Volley:
                    return "C";
                case CardTag.Vitality:
                    return "L";
                default:
                    return "?";
            }
        }

        private static void AddDraftUpgradeButtonIcon(Button button, CardTag tag, bool useMobileLayout)
        {
            float iconCol = useMobileLayout ? 108f : 90f;
            Transform labelTf = button.transform.Find("Label");
            if (labelTf != null)
            {
                RectTransform lr = labelTf.GetComponent<RectTransform>();
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                float padL = iconCol + (useMobileLayout ? 10f : 6f);
                float padR = useMobileLayout ? 26f : 18f;
                float padB = useMobileLayout ? 22f : 14f;
                float padT = useMobileLayout ? 20f : 12f;
                lr.offsetMin = new Vector2(padL, padB);
                lr.offsetMax = new Vector2(-padR, -padT);
            }

            GameObject iconArea = new GameObject("UpgradeTagIcon", typeof(RectTransform));
            iconArea.transform.SetParent(button.transform, false);
            iconArea.transform.SetAsFirstSibling();
            RectTransform areaRt = iconArea.GetComponent<RectTransform>();
            areaRt.anchorMin = new Vector2(0f, 0.5f);
            areaRt.anchorMax = new Vector2(0f, 0.5f);
            areaRt.pivot = new Vector2(0f, 0.5f);
            areaRt.anchoredPosition = new Vector2(useMobileLayout ? 12f : 8f, 0f);
            float diamondSize = useMobileLayout ? 76f : 64f;
            areaRt.sizeDelta = new Vector2(iconCol - 8f, diamondSize + 8f);

            GameObject diamondGo = new GameObject("Diamond", typeof(RectTransform));
            diamondGo.transform.SetParent(iconArea.transform, false);
            RectTransform dRt = diamondGo.GetComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.5f, 0.5f);
            dRt.anchorMax = new Vector2(0.5f, 0.5f);
            dRt.pivot = new Vector2(0.5f, 0.5f);
            dRt.sizeDelta = new Vector2(diamondSize, diamondSize);
            dRt.anchoredPosition = Vector2.zero;
            Image dImg = diamondGo.AddComponent<Image>();
            dImg.sprite = UpgradeHudVisuals.GetDiamondSprite();
            dImg.color = UpgradeHudVisuals.GetTagColor(tag);
            dImg.preserveAspect = true;
            dImg.raycastTarget = false;

            GameObject letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(diamondGo.transform, false);
            RectTransform letterRt = letterGo.GetComponent<RectTransform>();
            letterRt.anchorMin = Vector2.zero;
            letterRt.anchorMax = Vector2.one;
            letterRt.offsetMin = Vector2.zero;
            letterRt.offsetMax = Vector2.zero;
            TMP_Text letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(letterTmp);
            letterTmp.text = GetCardTagLetter(tag);
            letterTmp.fontSize = useMobileLayout ? 40f : 34f;
            letterTmp.fontStyle = FontStyles.Bold;
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            letterTmp.raycastTarget = false;
        }

        private void CreateWaveInterludeOverlay(int completedWave, int scoreDelta, float duration)
        {
            bool useMobileLayout = UseMobileUpgradeLayout();
            GameObject canvasGo = new GameObject("WaveInterludeCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            UiSelectionKeepAlive interludeKeepAlive = canvasGo.AddComponent<UiSelectionKeepAlive>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            MenuPanelPointerGuard interludePointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

            string waveTitle = "WAVE " + completedWave + " COMPLETE";
            if (_waveSpawner != null && _waveSpawner.IsEliteWave)
            {
                waveTitle += "\n<size=32><color=#FFCF66>ELITE SURVIVED</color></size>";
            }

            float yTitle = useMobileLayout ? 0.82f : 0.79f;
            float yStats = useMobileLayout ? 0.605f : 0.575f;
            float yBuild = useMobileLayout ? 0.455f : 0.435f;
            float yDraftTitle = useMobileLayout ? 0.335f : 0.325f;
            float yDraftCards = useMobileLayout ? 0.255f : 0.265f;
            float yContinue = useMobileLayout ? 0.055f : 0.065f;

            TMP_Text waveCompleteText = CreateText(panel.transform, "WaveComplete", waveTitle, new Vector2(0.5f, yTitle), useMobileLayout ? 84f : 72f, true);
            SetRectSize(waveCompleteText, useMobileLayout ? new Vector2(1280f, 280f) : new Vector2(1000f, 250f));
            string body = "Wave Score: " + scoreDelta + "\nWave Time: " + duration.ToString("0.0") + "s\nTotal Score: " + _score;
            TMP_Text waveStatsText = CreateText(panel.transform, "WaveStats", body, new Vector2(0.5f, yStats), useMobileLayout ? 50f : 40f, true);
            SetRectSize(waveStatsText, useMobileLayout ? new Vector2(1280f, 160f) : new Vector2(900f, 140f));
            RectTransform buildContainer = CreateContainer(
                panel.transform,
                "BuildLineContainer",
                new Vector2(0.5f, yBuild),
                useMobileLayout ? new Vector2(1420f, 112f) : new Vector2(1080f, 96f),
                new Color(0.05f, 0.09f, 0.14f, 0.82f));
            CreateInterludeBuildSummaryRow(buildContainer, useMobileLayout);

            RunUpgradeController.UpgradeOption[] draft = _runUpgradeController != null
                ? _runUpgradeController.BuildDraftOptions(3)
                : new RunUpgradeController.UpgradeOption[0];

            TMP_Text draftTitleText = CreateText(panel.transform, "DraftTitle", "Choose 1 Upgrade", new Vector2(0.5f, yDraftTitle), useMobileLayout ? 50f : 40f, true);
            SetRectSize(draftTitleText, useMobileLayout ? new Vector2(1200f, 72f) : new Vector2(900f, 64f));
            bool picked = false;
            Button continueButton = CreateButton(panel.transform, "ContinueButton", "Continue", new Vector2(0.5f, yContinue), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                Destroy(canvasGo);
                ContinueFromInterlude();
            });
            SetRectSize(continueButton, useMobileLayout ? new Vector2(320f, 108f) : new Vector2(250f, 92f));
            ConfigureButtonText(
                continueButton,
                useMobileLayout ? 42f : 36f,
                useMobileLayout ? 24f : 18f,
                useMobileLayout ? 42f : 36f,
                new Vector2(22f, 16f),
                new Vector2(-22f, -14f),
                useMobileLayout ? -6f : -10f);
            continueButton.interactable = false;

            if (draft.Length == 0)
            {
                string capText = "Upgrade limits reached.";
                TMP_Text capTextLabel = CreateText(panel.transform, "DraftCapText", capText, new Vector2(0.5f, yDraftCards), useMobileLayout ? 40f : 30f, true);
                SetRectSize(capTextLabel, useMobileLayout ? new Vector2(1200f, 140f) : new Vector2(900f, 120f));
                continueButton.interactable = true;
                SetSelectedButton(continueButton);
                interludeKeepAlive.DefaultSelection = continueButton.gameObject;
                interludePointerGuard.DefaultSelection = continueButton.gameObject;
                return;
            }

            float[] xPositions = new float[] { 0.24f, 0.5f, 0.76f };
            int optionCount = draft.Length;
            Button firstDraftButton = null;
            List<Button> draftButtons = new List<Button>(optionCount);
            for (int i = 0; i < optionCount; i++)
            {
                RunUpgradeController.UpgradeOption option = draft[i];
                int selectedIndex = i;
                string descriptionSize = useMobileLayout ? "36" : "26";
                string label = option.Title + "\n<size=" + descriptionSize + ">" + option.Description + "</size>";
                float x = i < xPositions.Length ? xPositions[i] : 0.5f;
                Button pickButton = CreateButton(panel.transform, "DraftOption_" + i, label, new Vector2(x, yDraftCards), () =>
                {
                    if (picked)
                    {
                        return;
                    }

                    picked = true;
                    bool applied = false;
                    if (_runUpgradeController != null)
                    {
                        applied = _runUpgradeController.ApplyUpgrade(option.Id, _playerHealth);
                    }

                    if (!applied)
                    {
                        picked = false;
                        return;
                    }

                    ApplyRunModifiers();
                    AchievementProgress.EvaluateRunUpgradeAchievements(_runUpgradeController);
                    int buttonCount = draftButtons.Count;
                    for (int b = 0; b < buttonCount; b++)
                    {
                        Button draftButton = draftButtons[b];
                        if (draftButton == null)
                        {
                            continue;
                        }

                        if (b == selectedIndex)
                        {
                            HighlightChosenDraftButton(draftButton);
                        }
                        else
                        {
                            draftButton.gameObject.SetActive(false);
                        }
                    }

                    continueButton.interactable = true;
                    AudioManager.EnsureExists().PlayUiConfirm();
                    SetSelectedButton(continueButton);
                    interludeKeepAlive.DefaultSelection = continueButton.gameObject;
                    interludePointerGuard.DefaultSelection = continueButton.gameObject;
                });

                RectTransform pickRect = pickButton.GetComponent<RectTransform>();
                if (pickRect != null)
                {
                    pickRect.sizeDelta = useMobileLayout ? new Vector2(430f, 200f) : new Vector2(340f, 150f);
                }
                ConfigureButtonText(
                    pickButton,
                    useMobileLayout ? 40f : 30f,
                    useMobileLayout ? 28f : 18f,
                    useMobileLayout ? 40f : 30f,
                    useMobileLayout ? new Vector2(28f, 24f) : new Vector2(20f, 14f),
                    useMobileLayout ? new Vector2(-28f, -22f) : new Vector2(-20f, -12f),
                    useMobileLayout ? -2f : -6f);
                AddDraftUpgradeButtonIcon(pickButton, option.Tag, useMobileLayout);
                draftButtons.Add(pickButton);

                if (firstDraftButton == null)
                {
                    firstDraftButton = pickButton;
                }
            }
            Button defaultBtn = firstDraftButton != null ? firstDraftButton : continueButton;
            SetSelectedButton(defaultBtn);
            interludeKeepAlive.DefaultSelection = defaultBtn != null ? defaultBtn.gameObject : null;
            interludePointerGuard.DefaultSelection = defaultBtn != null ? defaultBtn.gameObject : null;
        }

        private static void HighlightChosenDraftButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = new Vector3(1.06f, 1.06f, 1f);
            }

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.color = new Color(0.95f, 0.72f, 0.28f, 1f);
            }
        }

        private static string BuildTopScoreText(List<int> topScores)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Top Scores\n");
            int count = topScores != null ? topScores.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sb.Append(i + 1);
                sb.Append(". ");
                sb.Append(topScores[i]);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>0-based index of the score row to highlight (first place = 0), or -1 if not in the list.</summary>
        private static int GetTopScoreHighlightRowIndex(List<int> topScores, int playerScore)
        {
            if (topScores == null)
            {
                return -1;
            }

            int count = topScores.Count;
            for (int i = 0; i < count; i++)
            {
                if (topScores[i] == playerScore)
                {
                    return i;
                }
            }

            return -1;
        }

        public static List<int> GetSavedTopScores(int maxCount = 10)
        {
            List<int> scores = LoadTopScores();
            int count = Mathf.Clamp(maxCount, 0, scores.Count);
            if (count == scores.Count)
            {
                return scores;
            }

            return scores.GetRange(0, count);
        }

        private static List<int> SaveAndGetTopScores(int newScore)
        {
            const string key = "TopScores";
            List<int> scores = LoadTopScores();

            scores.Add(newScore);
            scores.Sort((a, b) => b.CompareTo(a));
            if (scores.Count > 10)
            {
                scores.RemoveRange(10, scores.Count - 10);
            }

            string save = string.Join(",", scores);
            PlayerPrefs.SetString(key, save);
            PlayerPrefs.Save();
            return scores;
        }

        private static List<int> LoadTopScores()
        {
            const string key = "TopScores";
            string raw = PlayerPrefs.GetString(key, string.Empty);
            List<int> scores = new List<int>(12);
            if (string.IsNullOrEmpty(raw))
            {
                return scores;
            }

            string[] parts = raw.Split(',');
            int pCount = parts.Length;
            for (int i = 0; i < pCount; i++)
            {
                int parsed;
                if (int.TryParse(parts[i], out parsed))
                {
                    scores.Add(parsed);
                }
            }

            scores.Sort((a, b) => b.CompareTo(a));
            return scores;
        }

        private static int SaveAndGetTotalScrap(int earned)
        {
            const string key = "MetaScrapTotal";
            int total = PlayerPrefs.GetInt(key, 0);
            total += Mathf.Max(0, earned);
            PlayerPrefs.SetInt(key, total);
            PlayerPrefs.Save();
            return total;
        }

        private void ApplyRunModifiers()
        {
            if (_runUpgradeController == null || _playerHealth == null)
            {
                return;
            }

            _playerHealth.SetIncomingDamageMultiplier(_runUpgradeController.IncomingDamageMultiplier);
        }

        private void ClearActiveProjectiles()
        {
            Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            int count = projectiles != null ? projectiles.Length : 0;
            if (count == 0)
            {
                return;
            }

            ObjectPooler pooler = FindFirstObjectByType<ObjectPooler>();
            for (int i = 0; i < count; i++)
            {
                Projectile projectile = projectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                if (pooler != null)
                {
                    pooler.Despawn(projectile.gameObject);
                }
                else
                {
                    projectile.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator BeginWaveInterludeSequence(int completedWave, int scoreDelta, float waveDuration)
        {
            _isWaveTransitionAnimating = true;
            yield return PlayShipExitTransition();
            AudioManager.EnsureExists().PlayWaveInterlude();
            CreateWaveInterludeOverlay(completedWave, scoreDelta, waveDuration);
            _isWaveTransitionAnimating = false;
        }

        private IEnumerator ContinueFromInterludeSequence()
        {
            _isWaveTransitionAnimating = true;
            PrepareNextWaveFromInterlude();
            yield return PlayShipEntryTransition();

            _isInterlude = false;
            _isPaused = false;
            _running = true;
            Time.timeScale = 1f;
            SetPlayerTransitionControlEnabled(true);
            SetGameplayHudVisible(true);
            if (_waveSpawner != null)
            {
                _waveSpawner.SetSpawningEnabled(true);
            }

            AudioManager.EnsureExists().PlayWaveMusicForWave(_currentWave, _bossEveryWaves);
            _isWaveTransitionAnimating = false;
        }

        private IEnumerator PlayShipExitTransition()
        {
            if (_playerTransform == null)
            {
                yield break;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                yield break;
            }

            CacheDefaultCameraPosition();
            SetPlayerTransitionControlEnabled(false);

            Vector3 playerPos = _playerTransform.position;
            Vector3 startCameraPos = cam.transform.position;
            Vector3 centeredCameraPos = new Vector3(playerPos.x, playerPos.y, startCameraPos.z);
            yield return MoveTransformRealtime(cam.transform, startCameraPos, centeredCameraPos, _waveExitCenterDuration);

            float startAngle = _playerTransform.eulerAngles.z;
            float rotateDuration = Mathf.Max(0.01f, _waveExitRotateDuration);
            float rotateElapsed = 0f;
            float zoomedInSize = _defaultCameraSize * _waveHyperspaceZoomFactor;
            while (rotateElapsed < rotateDuration)
            {
                rotateElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(rotateElapsed / rotateDuration);
                float angle = Mathf.LerpAngle(startAngle, 0f, EaseInOut(t));
                _playerTransform.rotation = Quaternion.Euler(0f, 0f, angle);
                cam.orthographicSize = Mathf.Lerp(_defaultCameraSize, zoomedInSize, EaseInOut(t));
                yield return null;
            }
            _playerTransform.rotation = Quaternion.identity;
            cam.orthographicSize = zoomedInSize;

            Vector3 exitStart = _playerTransform.position;
            Vector3 exitEnd = new Vector3(
                centeredCameraPos.x,
                centeredCameraPos.y + cam.orthographicSize + _waveTransitionOffscreenPadding,
                exitStart.z);
            Vector3 stretchedScale = new Vector3(
                _playerBaseScale.x * _waveHyperStretchX,
                _playerBaseScale.y * _waveHyperStretchY,
                _playerBaseScale.z);
            float exitDuration = Mathf.Max(0.01f, _waveExitHyperDuration);
            float exitElapsed = 0f;
            SetHyperTravelVisuals(true, 1.2f);
            while (exitElapsed < exitDuration)
            {
                exitElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(exitElapsed / exitDuration);
                float eased = EaseIn(t);
                _playerTransform.position = Vector3.Lerp(exitStart, exitEnd, eased);
                _playerTransform.localScale = Vector3.Lerp(_playerBaseScale, stretchedScale, eased);
                if (_starfieldBackground != null)
                {
                    _starfieldBackground.SetWarpAmount(eased * _waveStarWarpAmount);
                }
                yield return null;
            }

            if (_starfieldBackground != null)
            {
                _starfieldBackground.SetWarpAmount(_waveStarWarpAmount);
            }
            cam.orthographicSize = zoomedInSize;
            SetHyperTravelVisuals(false);
            _playerTransform.localScale = _playerBaseScale;
            _playerTransform.gameObject.SetActive(false);
        }

        private IEnumerator PlayShipEntryTransition()
        {
            if (_playerTransform == null)
            {
                yield break;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                yield break;
            }

            float zoomedInSize = _defaultCameraSize * _waveHyperspaceZoomFactor;
            cam.transform.position = _defaultCameraPosition;
            cam.orthographicSize = zoomedInSize;
            _playerTransform.gameObject.SetActive(true);
            SetPlayerTransitionControlEnabled(false);

            Vector3 centerTarget = new Vector3(_defaultCameraPosition.x, _defaultCameraPosition.y, _playerTransform.position.z);
            Vector3 entryStart = centerTarget - new Vector3(0f, cam.orthographicSize + _waveTransitionOffscreenPadding, 0f);
            Vector3 overshootTarget = centerTarget + new Vector3(0f, _waveEntryOvershootDistance, 0f);
            Vector3 stretchedScale = new Vector3(
                _playerBaseScale.x * _waveHyperStretchX,
                _playerBaseScale.y * _waveHyperStretchY,
                _playerBaseScale.z);

            _playerTransform.position = entryStart;
            _playerTransform.rotation = Quaternion.identity;
            _playerTransform.localScale = stretchedScale;
            if (_starfieldBackground != null)
            {
                _starfieldBackground.SetWarpAmount(_waveStarWarpAmount);
            }
            SetHyperTravelVisuals(true, 1.05f);

            float entryDuration = Mathf.Max(0.01f, _waveEntryDuration);
            float entryElapsed = 0f;
            while (entryElapsed < entryDuration)
            {
                entryElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(entryElapsed / entryDuration);
                float eased = EaseOut(t);
                _playerTransform.position = Vector3.Lerp(entryStart, overshootTarget, eased);
                _playerTransform.localScale = Vector3.Lerp(stretchedScale, _playerBaseScale, eased);
                cam.orthographicSize = Mathf.Lerp(zoomedInSize, _defaultCameraSize, eased);
                if (_starfieldBackground != null)
                {
                    _starfieldBackground.SetWarpAmount((1f - eased) * _waveStarWarpAmount);
                }
                yield return null;
            }

            float settleDuration = 0.12f;
            float settleElapsed = 0f;
            Vector3 settleStart = _playerTransform.position;
            while (settleElapsed < settleDuration)
            {
                settleElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(settleElapsed / settleDuration);
                _playerTransform.position = Vector3.Lerp(settleStart, centerTarget, EaseInOut(t));
                yield return null;
            }

            _playerTransform.position = centerTarget;
            _playerTransform.localScale = _playerBaseScale;
            cam.orthographicSize = _defaultCameraSize;
            if (_starfieldBackground != null)
            {
                _starfieldBackground.SetWarpAmount(0f);
            }
            SetHyperTravelVisuals(false);
        }

        private void SetPlayerTransitionControlEnabled(bool enabled)
        {
            AudioManager manager = AudioManager.Instance;
            if (manager != null)
            {
                manager.SetThrustLoopActive(false);
            }

            if (_playerController != null)
            {
                _playerController.enabled = enabled;
            }

            if (_playerAutoFire != null)
            {
                _playerAutoFire.enabled = enabled;
            }

            if (_playerThrusterVfx != null)
            {
                _playerThrusterVfx.SetManualThrusterOverride(false);
            }

            if (_playerBody != null)
            {
                _playerBody.linearVelocity = Vector2.zero;
                _playerBody.angularVelocity = 0f;
                _playerBody.simulated = enabled;
            }
        }

        private void SetHyperTravelVisuals(bool active, float thrusterIntensity = 1f)
        {
            AudioManager manager = AudioManager.Instance;
            if (manager != null)
            {
                manager.SetThrustLoopActive(active);
            }

            if (_playerThrusterVfx != null)
            {
                _playerThrusterVfx.SetManualThrusterOverride(active, thrusterIntensity);
            }
        }

        private static IEnumerator MoveTransformRealtime(Transform target, Vector3 start, Vector3 end, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                target.position = Vector3.Lerp(start, end, EaseInOut(t));
                yield return null;
            }

            target.position = end;
        }

        private static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - (inv * inv);
        }

        private static void SetSelectedButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.firstSelectedGameObject = button.gameObject;
            eventSystem.SetSelectedGameObject(button.gameObject);
        }
    }

    /// <summary>
    /// Creates a simple layered starfield behind the gameplay area.
    /// </summary>
    public class StarfieldBackground : MonoBehaviour
    {
        [SerializeField] private int _starCount = 180;
        [SerializeField] private float _starSizeMultiplier = 5f;
        [SerializeField] private float _edgePadding = 2f;
        [SerializeField] private Vector2 _starScaleRange = new Vector2(0.06f, 0.18f);
        [SerializeField] private int _sortingOrder = -50;
        [SerializeField] private float _twinkleSpeed = 1.8f;
        [SerializeField] private float _warpStretchMultiplier = 18f;
        [SerializeField] private float _warpMinWidthMultiplier = 0.22f;
        [SerializeField] private Color _nearStarColor = new Color(0.95f, 0.98f, 1f, 0.9f);
        [SerializeField] private Color _farStarColor = new Color(0.45f, 0.62f, 0.95f, 0.55f);

        private Camera _targetCamera;
        private SpriteRenderer[] _stars;
        private float[] _twinkleOffsets;
        private float[] _baseAlphas;
        private float[] _baseScales;
        private float _warpAmount;
        private bool _initialized;

        private static Sprite _starSprite;

        public void Initialize(Camera targetCamera)
        {
            _targetCamera = targetCamera != null ? targetCamera : Camera.main;
            if (_targetCamera == null)
            {
                return;
            }

            transform.SetParent(_targetCamera.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 10f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            if (_initialized)
            {
                return;
            }

            BuildStarfield();
            _initialized = true;
        }

        public void SetWarpAmount(float amount)
        {
            _warpAmount = Mathf.Clamp01(amount);
        }

        private void LateUpdate()
        {
            if (!_initialized && (_targetCamera != null || Camera.main != null))
            {
                Initialize(_targetCamera != null ? _targetCamera : Camera.main);
            }

            if (_stars == null)
            {
                return;
            }

            float time = Time.unscaledTime * _twinkleSpeed;
            int count = _stars.Length;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer star = _stars[i];
                if (star == null)
                {
                    continue;
                }

                Color color = star.color;
                float pulse = 0.72f + 0.28f * Mathf.Sin(time + _twinkleOffsets[i]);
                color.a = Mathf.Clamp01(_baseAlphas[i] * pulse);
                star.color = color;

                float baseScale = _baseScales != null && i < _baseScales.Length ? _baseScales[i] : 1f;
                float width = baseScale * Mathf.Lerp(1f, _warpMinWidthMultiplier, _warpAmount);
                float height = baseScale * Mathf.Lerp(1f, _warpStretchMultiplier, _warpAmount);
                star.transform.localScale = new Vector3(width, height, 1f);
            }
        }

        private void BuildStarfield()
        {
            EnsureStarSprite();

            int count = Mathf.Max(24, _starCount);
            _stars = new SpriteRenderer[count];
            _twinkleOffsets = new float[count];
            _baseAlphas = new float[count];
            _baseScales = new float[count];

            float cameraHeight = _targetCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * _targetCamera.aspect;
            float minX = -(cameraWidth * 0.5f) - _edgePadding;
            float maxX = (cameraWidth * 0.5f) + _edgePadding;
            float minY = -(cameraHeight * 0.5f) - _edgePadding;
            float maxY = (cameraHeight * 0.5f) + _edgePadding;

            for (int i = 0; i < count; i++)
            {
                GameObject starGo = new GameObject("Star_" + i);
                starGo.transform.SetParent(transform, false);
                starGo.transform.localPosition = new Vector3(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY),
                    0f);

                float scale = Random.Range(_starScaleRange.x, _starScaleRange.y);
                starGo.transform.localScale = Vector3.one * scale * Mathf.Max(0.1f, _starSizeMultiplier);

                SpriteRenderer renderer = starGo.AddComponent<SpriteRenderer>();
                renderer.sprite = _starSprite;
                renderer.sortingOrder = _sortingOrder;
                renderer.color = Color.Lerp(_farStarColor, _nearStarColor, Random.value);

                _stars[i] = renderer;
                _twinkleOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
                _baseAlphas[i] = renderer.color.a;
                _baseScales[i] = scale * Mathf.Max(0.1f, _starSizeMultiplier);
            }
        }

        private static void EnsureStarSprite()
        {
            if (_starSprite != null)
            {
                return;
            }

            const int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / (size * 0.35f)));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _starSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>
    /// Drives pause help ScrollRects with keyboard (Page Up/Down), gamepad left stick; touch drag and mouse wheel use Unity ScrollRect.
    /// </summary>
    public class PauseHelpScrollInput : MonoBehaviour
    {
        [SerializeField] private ScrollRect _itemsScroll;
        [SerializeField] private ScrollRect _upgradesScroll;

        private void Update()
        {
            ScrollRect active = GetActiveScroll();
            if (active == null || !active.vertical)
            {
                return;
            }

            float delta = 0f;
            float dt = Time.unscaledDeltaTime;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.pageDownKey.isPressed)
                {
                    delta -= 1.8f * dt;
                }

                if (kb.pageUpKey.isPressed)
                {
                    delta += 1.8f * dt;
                }
            }

            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                float y = gp.leftStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.18f)
                {
                    delta += y * 2.2f * dt;
                }
            }

            if (Mathf.Abs(delta) < 0.00001f)
            {
                return;
            }

            active.verticalNormalizedPosition = Mathf.Clamp01(active.verticalNormalizedPosition + delta);
        }

        private ScrollRect GetActiveScroll()
        {
            if (_upgradesScroll != null && _upgradesScroll.gameObject.activeInHierarchy)
            {
                return _upgradesScroll;
            }

            if (_itemsScroll != null && _itemsScroll.gameObject.activeInHierarchy)
            {
                return _itemsScroll;
            }

            return null;
        }

        public void SetScrollRects(ScrollRect items, ScrollRect upgrades)
        {
            _itemsScroll = items;
            _upgradesScroll = upgrades;
        }
    }
}
