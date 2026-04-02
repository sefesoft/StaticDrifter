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
        private bool _isGameOver;
        private bool _isInterlude;
        private int _pendingWave;
        private int _waveStartScore;
        private float _waveStartTime;
        private int _runScrap;
        private BossShip _bossShip;
        private bool _isBossFight;
        private bool _isPaused;
        private float _bossFightElapsed;
        [Header("Boss Presentation")]
        [SerializeField] private float _bossWarningDurationSeconds = 1.6f;
        [SerializeField] private float _bossExplosionDurationSeconds = 1.1f;

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
            if (_bossShip != null)
            {
                _bossShip.Defeated -= HandleBossDefeated;
            }

            if (Instance == this)
            {
                Instance = null;
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

            PlayerThrusterVFX thrusterVfx = player.GetComponent<PlayerThrusterVFX>();
            if (thrusterVfx == null)
            {
                player.AddComponent<PlayerThrusterVFX>();
            }

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
        }

        private void TriggerGameOver()
        {
            _isGameOver = true;
            _isInterlude = false;
            _running = false;
            _isPaused = false;
            Time.timeScale = 0f;
            _isBossFight = false;
            DestroyPauseOverlay();
            if (_bossShip != null)
            {
                _bossShip.Deactivate();
            }

            SetGameplayHudVisible(false);

            AudioManager.EnsureExists().PlayGameOver();

            List<int> topScores = SaveAndGetTopScores(_score);
            int totalScrap = SaveAndGetTotalScrap(_runScrap);
            CreateGameOverOverlay(topScores, totalScrap);
        }

        private void BeginWaveInterlude(int nextWave, float segmentDurationOverride = -1f)
        {
            if (_isInterlude || _isGameOver)
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
            AudioManager.EnsureExists().PlayWaveInterlude();
            CreateWaveInterludeOverlay(_currentWave, scoreDelta, waveDuration);
        }

        private void ContinueFromInterlude()
        {
            if (_isGameOver)
            {
                return;
            }

            _currentWave = _pendingWave;
            _waveStartScore = _score;
            _waveStartTime = _matchTime;
            _waveElapsedTime = 0f;
            _currentWaveDuration = GetWaveDurationSeconds(_currentWave);
            _isBossFight = false;
            _isInterlude = false;
            _isPaused = false;
            _running = true;

            if (_waveSpawner != null)
            {
                _waveSpawner.ConfigureForWave(_currentWave);
                _waveSpawner.SetSpawningEnabled(true);
            }

            ApplyRunModifiers();
            Time.timeScale = 1f;
            SetGameplayHudVisible(true);
            AudioManager.EnsureExists().PlayWaveMusicForWave(_currentWave, _bossEveryWaves);
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
            StartCoroutine(BeginUpgradeAfterBossExplosion(nextWave, bossDuration, bossPos));
        }

        private IEnumerator BeginUpgradeAfterBossExplosion(int nextWave, float bossDuration, Vector3 bossPos)
        {
            GameObject vfxGo = CreateExplosionVfx(bossPos);
            float wait = Mathf.Max(0.1f, _bossExplosionDurationSeconds);
            yield return new WaitForSecondsRealtime(wait + 0.05f);
            if (vfxGo != null)
            {
                Destroy(vfxGo);
            }
            BeginWaveInterlude(nextWave, bossDuration);
        }

        private GameObject CreateExplosionVfx(Vector3 position)
        {
            GameObject root = new GameObject("BossExplosionVFX");
            root.transform.position = position;

            CreateExplosionFlash(root, _bossExplosionDurationSeconds);
            CreateExplosionSmoke(root, _bossExplosionDurationSeconds);

            return root;
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

        private void CreateExplosionFlash(GameObject root, float visualDuration)
        {
            GameObject flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(root.transform, false);

            ParticleSystem ps = flashGo.AddComponent<ParticleSystem>();
            // Some Unity versions may start the system on AddComponent for a frame.
            // Stop it immediately so duration/lifetime edits are allowed.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.06f;
            main.startLifetime = Mathf.Clamp(visualDuration * 0.45f, 0.18f, 0.55f);
            main.startSpeed = 4.8f;
            main.startSize = 0.22f;
            main.gravityModifier = 0f;
            main.maxParticles = 1600;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.7f, 0.22f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.15f, 0.06f, 0.9f), 0.5f),
                    new GradientColorKey(new Color(0.12f, 0.02f, 0.02f, 0.0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                }
            };

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(0.95f, 0.25f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            ConfigureAdditiveMaterial(renderer, out _);

            ps.Play();
            ps.Emit(320);
        }

        private void CreateExplosionSmoke(GameObject root, float visualDuration)
        {
            GameObject smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(root.transform, false);

            ParticleSystem ps = smokeGo.AddComponent<ParticleSystem>();
            // Avoid duration/lifetime edit errors if the system auto-starts for a frame.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.05f;
            main.startLifetime = Mathf.Clamp(visualDuration * 0.7f, 0.25f, 1.1f);
            main.startSpeed = 0.75f;
            main.startSize = 0.95f;
            main.gravityModifier = -0.08f;
            main.maxParticles = 900;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new Gradient
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.55f, 0.2f, 0.8f), 0f),
                    new GradientColorKey(new Color(0.75f, 0.15f, 0.08f, 0.35f), 0.55f),
                    new GradientColorKey(new Color(0.12f, 0.02f, 0.02f, 0.0f), 1f),
                },
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f),
                }
            };

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.15f, 2.4f);

            ps.Play();
            ps.Emit(150);
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
            CreateGameOverLine(
                panel.transform,
                "TopScores",
                scoreList,
                new Vector2(0f, 48f),
                new Vector2(820f, 300f),
                28f,
                TextAlignmentOptions.Top,
                new Vector2(0.5f, 1f));

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

            CreateGameOverLine(panel.transform, "PauseTitle", "PAUSED", new Vector2(0f, 170f), new Vector2(900f, 100f), 76f, TextAlignmentOptions.Center);
            CreateGameOverLine(panel.transform, "PauseHint", "Take a breath, then resume when ready.", new Vector2(0f, 90f), new Vector2(1000f, 50f), 30f, TextAlignmentOptions.Center);

            Button resumeButton = CreateButton(panel.transform, "ResumeButton", "Resume", new Vector2(0.5f, 0.40f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ResumeFromPause();
            });

            CreateButton(panel.transform, "RetryFromPauseButton", "Retry", new Vector2(0.5f, 0.29f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                _isPaused = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("Gameplay");
            });

            CreateButton(panel.transform, "TitleFromPauseButton", "Title", new Vector2(0.5f, 0.18f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                _isPaused = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene("TitleScreen");
            });

            SetSelectedButton(resumeButton);
            pauseKeepAlive.DefaultSelection = resumeButton.gameObject;
            pausePointerGuard.DefaultSelection = resumeButton.gameObject;
        }

        private static void DestroyPauseOverlay()
        {
            GameObject pauseCanvas = GameObject.Find("PauseCanvas");
            if (pauseCanvas != null)
            {
                Destroy(pauseCanvas);
            }
        }

        private static void SetGameplayHudVisible(bool visible)
        {
            GameObject gameplayHudCanvas = GameObject.Find("GameplayHUD_Canvas");
            if (gameplayHudCanvas != null)
            {
                gameplayHudCanvas.SetActive(visible);
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
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.color = new Color(0.93f, 0.97f, 1f, 1f);
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (tmp.fontSharedMaterial != null)
            {
                tmp.outlineWidth = 0.18f;
                tmp.outlineColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            }

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
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Top;
            tmp.color = new Color(0.93f, 0.97f, 1f, 1f);
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            tmp.raycastTarget = false;
            return tmp;
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
            image.color = new Color(0.15f, 0.22f, 0.38f, 0.92f);
            Button btn = buttonGo.AddComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(onClick);
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.18f, 0.25f, 0.42f, 0.95f);
            colors.highlightedColor = new Color(0.36f, 0.62f, 0.95f, 1f);
            colors.selectedColor = new Color(0.45f, 0.76f, 1f, 1f);
            colors.pressedColor = new Color(0.95f, 0.72f, 0.30f, 1f);
            colors.disabledColor = new Color(0.16f, 0.18f, 0.24f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            buttonGo.AddComponent<UiSelectOnPointerEnter>();

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TMP_Text t = textGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = label.Contains("\n") ? 26f : 36f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.85f, 0.95f, 1f, 1f);
            t.raycastTarget = false;
            return btn;
        }

        private void CreateWaveInterludeOverlay(int completedWave, int scoreDelta, float duration)
        {
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

            CreateText(panel.transform, "WaveComplete", waveTitle, new Vector2(0.5f, 0.72f), 72f, true);
            string body = "Wave Score: " + scoreDelta + "\nWave Time: " + duration.ToString("0.0") + "s\nTotal Score: " + _score;
            CreateText(panel.transform, "WaveStats", body, new Vector2(0.5f, 0.52f), 40f, true);
            CreateText(panel.transform, "BuildLine", "Current Build: " + BuildSummary, new Vector2(0.5f, 0.44f), 28f, true);

            RunUpgradeController.UpgradeOption[] draft = _runUpgradeController != null
                ? _runUpgradeController.BuildDraftOptions(3)
                : new RunUpgradeController.UpgradeOption[0];

            CreateText(panel.transform, "DraftTitle", "Choose 1 Upgrade", new Vector2(0.5f, 0.40f), 38f, true);
            bool picked = false;
            Button continueButton = CreateButton(panel.transform, "ContinueButton", "Continue", new Vector2(0.5f, 0.14f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                Destroy(canvasGo);
                ContinueFromInterlude();
            });
            continueButton.interactable = false;

            if (draft.Length == 0)
            {
                string capText = "Upgrade limits reached.";
                CreateText(panel.transform, "DraftCapText", capText, new Vector2(0.5f, 0.30f), 30f, true);
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
                string label = option.Title + "\n<size=22>" + option.Description + "</size>";
                float x = i < xPositions.Length ? xPositions[i] : 0.5f;
                Button pickButton = CreateButton(panel.transform, "DraftOption_" + i, label, new Vector2(x, 0.28f), () =>
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
                    pickRect.sizeDelta = new Vector2(300f, 134f);
                }
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
                rect.sizeDelta = new Vector2(340f, 160f);
                rect.localScale = new Vector3(1.08f, 1.08f, 1f);
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
}
