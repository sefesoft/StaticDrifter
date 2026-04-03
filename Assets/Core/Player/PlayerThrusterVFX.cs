using UnityEngine;

namespace StaticDrift.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerThrusterVFX : MonoBehaviour
    {
        [SerializeField] private Transform _rocketTransform;
        [SerializeField] private SpriteRenderer _flameRenderer;
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] private SpriteRenderer _coreRenderer;

        [SerializeField] private Vector3 _baseScale = new Vector3(0.32f, 0.58f, 1f);
        [SerializeField] private Vector3 _activeScale = new Vector3(0.44f, 1.05f, 1f);
        [SerializeField] private float _glowScaleMultiplier = 2.15f;
        [SerializeField] private float _coreScaleMultiplier = 0.38f;

        [SerializeField] private Color _flameColor = new Color(1f, 0.48f, 0.12f, 0.95f);
        [SerializeField] private Color _glowColor = new Color(1f, 0.35f, 0.08f, 0.42f);
        [SerializeField] private Color _coreColor = new Color(1f, 0.98f, 0.72f, 0.98f);
        [SerializeField] private float _flickerSpeed = 28f;
        [SerializeField] private float _flashSpeed = 17f;

        private PlayerController _controller;
        private bool _forceActive;
        private float _manualIntensity = 1f;
        private static Sprite _sharedFlameSprite;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            EnsureRocketTransform();
            EnsureFlameLayers();
            SetFlameActive(false);
        }

        private void Update()
        {
            if (_flameRenderer == null || _controller == null)
            {
                return;
            }

            bool active = _forceActive || _controller.IsAccelerating;
            SetFlameActive(active);
            if (!active)
            {
                return;
            }

            float t = Time.time;
            float flicker = 0.68f + 0.32f * Mathf.Abs(Mathf.Sin(t * _flickerSpeed));
            float flash = 0.82f + 0.18f * Mathf.Sin(t * _flashSpeed);
            float combined = flicker * flash * Mathf.Max(0.5f, _manualIntensity);

            Vector3 mainScale = Vector3.Lerp(_baseScale, _activeScale, combined);
            _flameRenderer.transform.localScale = mainScale;

            Color fc = _flameColor;
            fc.a = 0.55f + 0.45f * combined;
            _flameRenderer.color = fc;

            if (_glowRenderer != null)
            {
                float glowPulse = 0.75f + 0.25f * Mathf.Sin(t * (_flickerSpeed * 0.65f));
                _glowRenderer.transform.localScale = mainScale * _glowScaleMultiplier * glowPulse;
                Color gc = _glowColor;
                gc.a = _glowColor.a * (0.55f + 0.45f * combined);
                _glowRenderer.color = gc;
            }

            if (_coreRenderer != null)
            {
                float corePulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * (_flickerSpeed * 1.2f)));
                _coreRenderer.transform.localScale = mainScale * _coreScaleMultiplier * corePulse;
                Color cc = _coreColor;
                cc.a = 0.75f + 0.25f * combined;
                _coreRenderer.color = cc;
            }
        }

        public void SetManualThrusterOverride(bool active, float intensity = 1f)
        {
            _forceActive = active;
            _manualIntensity = Mathf.Max(0.5f, intensity);
            if (!active)
            {
                SetFlameActive(false);
            }
        }

        private void SetFlameActive(bool active)
        {
            if (_flameRenderer != null)
            {
                _flameRenderer.enabled = active;
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.enabled = active;
            }

            if (_coreRenderer != null)
            {
                _coreRenderer.enabled = active;
            }
        }

        private void EnsureRocketTransform()
        {
            if (_rocketTransform != null)
            {
                return;
            }

            Transform found = transform.Find("Rocket");
            if (found != null)
            {
                _rocketTransform = found;
                return;
            }

            GameObject rocket = new GameObject("Rocket");
            _rocketTransform = rocket.transform;
            _rocketTransform.SetParent(transform, false);
            _rocketTransform.localPosition = new Vector3(0f, -0.72f, 0f);
        }

        private void EnsureFlameLayers()
        {
            if (_flameRenderer == null)
            {
                Transform existing = _rocketTransform.Find("Flame");
                if (existing != null)
                {
                    _flameRenderer = existing.GetComponent<SpriteRenderer>();
                }
            }

            if (_glowRenderer == null)
            {
                Transform g = _rocketTransform.Find("FlameGlow");
                if (g != null)
                {
                    _glowRenderer = g.GetComponent<SpriteRenderer>();
                }
            }

            if (_coreRenderer == null)
            {
                Transform c = _rocketTransform.Find("FlameCore");
                if (c != null)
                {
                    _coreRenderer = c.GetComponent<SpriteRenderer>();
                }
            }

            if (_glowRenderer == null)
            {
                GameObject glow = new GameObject("FlameGlow");
                glow.transform.SetParent(_rocketTransform, false);
                glow.transform.localPosition = Vector3.zero;
                glow.transform.SetAsFirstSibling();
                _glowRenderer = glow.AddComponent<SpriteRenderer>();
                _glowRenderer.sprite = GetSharedFlameSprite();
                _glowRenderer.color = _glowColor;
                _glowRenderer.sortingOrder = 10;
            }

            if (_flameRenderer == null)
            {
                GameObject flame = new GameObject("Flame");
                flame.transform.SetParent(_rocketTransform, false);
                flame.transform.localPosition = Vector3.zero;
                _flameRenderer = flame.AddComponent<SpriteRenderer>();
                _flameRenderer.sprite = GetSharedFlameSprite();
                _flameRenderer.color = _flameColor;
                _flameRenderer.sortingOrder = 12;
            }

            if (_coreRenderer == null)
            {
                GameObject core = new GameObject("FlameCore");
                core.transform.SetParent(_rocketTransform, false);
                core.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                _coreRenderer = core.AddComponent<SpriteRenderer>();
                _coreRenderer.sprite = GetSharedFlameSprite();
                _coreRenderer.color = _coreColor;
                _coreRenderer.sortingOrder = 14;
            }

            _flameRenderer.transform.localScale = _baseScale;
            _glowRenderer.transform.localScale = _baseScale * _glowScaleMultiplier;
            _coreRenderer.transform.localScale = _baseScale * _coreScaleMultiplier;
        }

        private static Sprite GetSharedFlameSprite()
        {
            if (_sharedFlameSprite != null)
            {
                return _sharedFlameSprite;
            }

            _sharedFlameSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 1f),
                1f);
            return _sharedFlameSprite;
        }
    }
}
