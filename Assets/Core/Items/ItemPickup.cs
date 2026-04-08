using UnityEngine;
using StaticDrift.Player;

namespace StaticDrift.Items
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class ItemPickup : MonoBehaviour
    {
        private ItemSpawner _spawner;
        private ItemType _itemType;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider2D;
        private Vector3 _baseLocalScale = Vector3.one;
        private float _pulsePhase;
        [SerializeField] private float _pulseSpeed = 2.6f;
        [SerializeField] private float _pulseAmplitude = 0.055f;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D = GetComponent<CircleCollider2D>();
            _collider2D.isTrigger = true;
        }

        private void Update()
        {
            float pulse = 1f + _pulseAmplitude * Mathf.Sin(Time.time * _pulseSpeed + _pulsePhase);
            transform.localScale = _baseLocalScale * pulse;
        }

        public void Initialize(ItemSpawner spawner)
        {
            _spawner = spawner;
        }

        /// <summary>Used during wave hyperspace transitions to hide pickups without despawning them.</summary>
        public void SetVisualAndColliderEnabled(bool enabled)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = enabled;
            }

            if (_collider2D != null)
            {
                _collider2D.enabled = enabled;
            }
        }

        public void Configure(ItemType itemType, Vector3 baseLocalScale)
        {
            _itemType = itemType;
            _baseLocalScale = baseLocalScale;
            _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = _baseLocalScale;

            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.sprite = ItemTypeSprites.Get(_itemType);
            _spriteRenderer.color = Color.white;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            PlayerPowerupController powerups = other.GetComponent<PlayerPowerupController>();
            if (powerups == null)
            {
                powerups = other.GetComponentInParent<PlayerPowerupController>();
            }

            if (powerups != null)
            {
                powerups.ApplyItem(_itemType);
            }

            if (_spawner != null)
            {
                _spawner.Despawn(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
