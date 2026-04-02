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
        private float _spinSpeed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider2D = GetComponent<CircleCollider2D>();
            _collider2D.isTrigger = true;
            _spinSpeed = Random.Range(50f, 110f) * (Random.value < 0.5f ? -1f : 1f);
        }

        private void Update()
        {
            transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);
        }

        public void Initialize(ItemSpawner spawner)
        {
            _spawner = spawner;
        }

        public void Configure(ItemType itemType)
        {
            _itemType = itemType;
            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.color = ItemVisualColors.Get(_itemType);
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
