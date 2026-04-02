using UnityEngine;
using StaticDrift.UI;

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

        private float _matchTime;
        private bool _running;

        /// <summary>
        /// Elapsed match time in seconds. Used by GameplayHUD for the timer display.
        /// </summary>
        public float MatchTime => _matchTime;

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
            SpawnPlayer();
            SpawnGameplayHUD();
            _matchTime = 0f;
            _running = true;
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
        }

        private void OnDestroy()
        {
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
    }
}
