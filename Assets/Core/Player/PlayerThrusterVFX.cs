using UnityEngine;

namespace StaticDrift.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerThrusterVFX : MonoBehaviour
    {
        [SerializeField] private Transform _rocketTransform;
        [SerializeField] private SpriteRenderer _flameRenderer;
        [SerializeField] private Vector3 _baseScale = new Vector3(0.18f, 0.34f, 1f);
        [SerializeField] private Vector3 _activeScale = new Vector3(0.22f, 0.6f, 1f);
        [SerializeField] private Color _flameColor = new Color(1f, 0.62f, 0.2f, 0.9f);
        [SerializeField] private float _flickerSpeed = 24f;

        private PlayerController _controller;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            EnsureRocketTransform();
            EnsureFlameRenderer();
            if (_flameRenderer != null)
            {
                _flameRenderer.enabled = false;
            }
        }

        private void Update()
        {
            if (_flameRenderer == null || _controller == null)
            {
                return;
            }

            bool active = _controller.IsAccelerating;
            _flameRenderer.enabled = active;
            if (!active)
            {
                return;
            }

            float flicker = 0.75f + Mathf.Abs(Mathf.Sin(Time.time * _flickerSpeed)) * 0.35f;
            _flameRenderer.transform.localScale = Vector3.Lerp(_baseScale, _activeScale, flicker);

            Color c = _flameColor;
            c.a = 0.65f + 0.35f * flicker;
            _flameRenderer.color = c;
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

        private void EnsureFlameRenderer()
        {
            if (_flameRenderer != null)
            {
                return;
            }

            Transform existing = _rocketTransform.Find("Flame");
            if (existing != null)
            {
                _flameRenderer = existing.GetComponent<SpriteRenderer>();
                if (_flameRenderer != null)
                {
                    return;
                }
            }

            GameObject flame = new GameObject("Flame");
            flame.transform.SetParent(_rocketTransform, false);
            flame.transform.localPosition = Vector3.zero;
            flame.transform.localScale = _baseScale;

            _flameRenderer = flame.AddComponent<SpriteRenderer>();
            _flameRenderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 1f), 1f);
            _flameRenderer.color = _flameColor;
            _flameRenderer.sortingOrder = 1;
        }
    }
}
