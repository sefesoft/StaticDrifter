using UnityEngine;

namespace StaticDrift.Player
{
    /// <summary>
    /// Temporary freeze state (ice asteroids). Stops movement via <see cref="PlayerController"/> and firing via
    /// <see cref="PlayerAutoFire"/> while active; optional tint on the ship sprite.
    /// </summary>
    public class PlayerFreezeController : MonoBehaviour
    {
        [SerializeField] private float _defaultFreezeSeconds = 1.75f;

        private float _frozenUntil;
        private SpriteRenderer _shipRenderer;
        private Color _baseColor;

        public bool IsFrozen => Time.time < _frozenUntil;

        private void Awake()
        {
            CacheRenderer();
        }

        private void CacheRenderer()
        {
            if (_shipRenderer != null)
            {
                return;
            }

            _shipRenderer = GetComponent<SpriteRenderer>();
            if (_shipRenderer == null)
            {
                _shipRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_shipRenderer != null)
            {
                _baseColor = _shipRenderer.color;
            }
        }

        public void ApplyFreeze(float durationSeconds = -1f)
        {
            if (durationSeconds <= 0f)
            {
                durationSeconds = _defaultFreezeSeconds;
            }

            float until = Time.time + durationSeconds;
            if (until > _frozenUntil)
            {
                _frozenUntil = until;
            }

            CacheRenderer();
            if (_shipRenderer != null)
            {
                Color c = new Color(0.65f, 0.88f, 1f, _baseColor.a);
                _shipRenderer.color = c;
            }
        }

        private void LateUpdate()
        {
            if (_shipRenderer == null) return;
            if (!IsFrozen && _shipRenderer.color != _baseColor)
            {
                _shipRenderer.color = _baseColor;
            }
        }
    }
}
