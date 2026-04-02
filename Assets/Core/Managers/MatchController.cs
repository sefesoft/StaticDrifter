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
        [SerializeField] private float _firstWaveDurationSeconds = 60f;
        [SerializeField] private float _waveDurationSeconds = 25f;

        private float _matchTime;
        private bool _running;
        private int _score;
        private int _currentWave = 1;
        private float _nextWaveAt;
        private PlayerHealth _playerHealth;
        private bool _isGameOver;
        private bool _isInterlude;
        private int _pendingWave;
        private int _waveStartScore;
        private float _waveStartTime;

        /// <summary>
        /// Elapsed match time in seconds. Used by GameplayHUD for the timer display.
        /// </summary>
        public float MatchTime => _matchTime;
        public int Score => _score;
        public int CurrentWave => _currentWave;
        public bool IsGameOver => _isGameOver;

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
            SpawnPlayer();
            SpawnGameplayHUD();
            _matchTime = 0f;
            _score = 0;
            _currentWave = 1;
            _waveStartScore = 0;
            _waveStartTime = 0f;
            _nextWaveAt = _firstWaveDurationSeconds;
            _running = true;

            if (_waveSpawner == null)
            {
                _waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
            }
            if (_waveSpawner != null)
            {
                _waveSpawner.ConfigureForWave(_currentWave);
                _waveSpawner.SetSpawningEnabled(true);
            }

            Asteroid.AsteroidDestroyed += OnAsteroidDestroyed;
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }
            if (_pauseTimerWhenPaused && Time.timeScale < 0.001f)
            {
                return;
            }
            _matchTime += Time.deltaTime;

            if (_matchTime >= _nextWaveAt)
            {
                BeginWaveInterlude(_currentWave + 1);
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
            }
            else if (size == Asteroid.AsteroidSize.Medium)
            {
                _score += 60;
            }
            else
            {
                _score += 30;
            }
        }

        private void TriggerGameOver()
        {
            _isGameOver = true;
            _isInterlude = false;
            _running = false;
            Time.timeScale = 0f;

            List<int> topScores = SaveAndGetTopScores(_score);
            CreateGameOverOverlay(topScores);
        }

        private void BeginWaveInterlude(int nextWave)
        {
            if (_isInterlude || _isGameOver)
            {
                return;
            }

            _isInterlude = true;
            _pendingWave = Mathf.Max(nextWave, _currentWave + 1);
            _running = false;

            if (_waveSpawner != null)
            {
                _waveSpawner.SetSpawningEnabled(false);
                _waveSpawner.ClearActiveAsteroids();
            }

            ClearActiveProjectiles();

            int scoreDelta = _score - _waveStartScore;
            float waveDuration = _matchTime - _waveStartTime;

            Time.timeScale = 0f;
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
            _nextWaveAt = _matchTime + _waveDurationSeconds;
            _isInterlude = false;
            _running = true;

            if (_waveSpawner != null)
            {
                _waveSpawner.ConfigureForWave(_currentWave);
                _waveSpawner.SetSpawningEnabled(true);
            }

            Time.timeScale = 1f;
        }

        private void CreateGameOverOverlay(List<int> topScores)
        {
            GameObject canvasGo = new GameObject("GameOverCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
            panelImage.color = new Color(0f, 0f, 0f, 0.78f);

            CreateText(panel.transform, "GameOverTitle", "GAME OVER", new Vector2(0.5f, 0.78f), 86f, true);
            CreateText(panel.transform, "FinalScore", "Score: " + _score, new Vector2(0.5f, 0.67f), 52f, true);

            string scoreList = BuildTopScoreText(topScores);
            CreateText(panel.transform, "TopScores", scoreList, new Vector2(0.5f, 0.45f), 34f, false);

            Button retryButton = CreateButton(panel.transform, "RetryButton", "Retry", new Vector2(0.42f, 0.2f), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene("Gameplay");
            });

            CreateButton(panel.transform, "TitleButton", "Title", new Vector2(0.58f, 0.2f), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene("TitleScreen");
            });

            SetSelectedButton(retryButton);
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

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TMP_Text t = textGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 36f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.85f, 0.95f, 1f, 1f);
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

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);

            CreateText(panel.transform, "WaveComplete", "WAVE " + completedWave + " COMPLETE", new Vector2(0.5f, 0.72f), 72f, true);
            string body = "Wave Score: " + scoreDelta + "\nWave Time: " + duration.ToString("0.0") + "s\nTotal Score: " + _score;
            CreateText(panel.transform, "WaveStats", body, new Vector2(0.5f, 0.52f), 40f, true);
            CreateText(panel.transform, "UpgradePlaceholder", "Upgrade menu will live here soon.", new Vector2(0.5f, 0.36f), 30f, true);

            Button continueButton = CreateButton(panel.transform, "ContinueButton", "Continue", new Vector2(0.5f, 0.2f), () =>
            {
                Destroy(canvasGo);
                ContinueFromInterlude();
            });
            SetSelectedButton(continueButton);
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

        private static List<int> SaveAndGetTopScores(int newScore)
        {
            const string key = "TopScores";
            string raw = PlayerPrefs.GetString(key, string.Empty);
            List<int> scores = new List<int>(12);
            if (!string.IsNullOrEmpty(raw))
            {
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
            }

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

            eventSystem.SetSelectedGameObject(button.gameObject);
        }
    }
}
