using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StaticDrift.Items
{
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] private int _poolSize = 5;
        [SerializeField] private float _minSpawnInterval = 18f;
        [SerializeField] private float _maxSpawnInterval = 34f;
        [SerializeField] private float _spawnChancePerRoll = 0.45f;
        [SerializeField] private int _maxActiveItems = 1;
        [Tooltip("Extra world units inset from the camera orthographic bounds (keeps pickups off the playfield rim).")]
        [SerializeField] private float _worldEdgeInset = 0.35f;
        [Tooltip("Viewport X range excluding side HUD (normalized 0–1).")]
        [SerializeField] [Range(0.02f, 0.45f)] private float _safeViewportInsetX = 0.07f;
        [Tooltip("Viewport Y range excluding bottom touch bar and top status bar (normalized 0–1).")]
        [SerializeField] [Range(0.12f, 0.45f)] private float _safeViewportInsetBottom = 0.28f;
        [SerializeField] [Range(0.08f, 0.35f)] private float _safeViewportInsetTop = 0.13f;
        [SerializeField] private Key _debugSpawnKey = Key.I;

        private static readonly Vector3 ItemPickupBaseScale = new Vector3(0.42f, 0.42f, 1f);

        private readonly List<ItemPickup> _pool = new List<ItemPickup>(8);
        private float _nextSpawnIn;
        private Camera _camera;

        private void Awake()
        {
            _camera = Camera.main;
            BuildPool();
            ScheduleNextRoll();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[_debugSpawnKey].wasPressedThisFrame)
            {
                SpawnRandomItem();
            }

            _nextSpawnIn -= Time.deltaTime;
            if (_nextSpawnIn > 0f)
            {
                return;
            }

            ScheduleNextRoll();
            if (GetActiveItemCount() >= _maxActiveItems)
            {
                return;
            }

            if (Random.value > _spawnChancePerRoll)
            {
                return;
            }

            SpawnRandomItem();
        }

        public void Despawn(ItemPickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            pickup.gameObject.SetActive(false);
            pickup.transform.SetParent(transform, false);
        }

        /// <summary>Hide or show active pickups during wave hyperspace transitions.</summary>
        public void SetActivePickupVisualsVisible(bool visible)
        {
            int count = _pool.Count;
            for (int i = 0; i < count; i++)
            {
                ItemPickup pickup = _pool[i];
                if (pickup == null || !pickup.gameObject.activeSelf)
                {
                    continue;
                }

                pickup.SetVisualAndColliderEnabled(visible);
            }
        }

        private void BuildPool()
        {
            int count = Mathf.Max(1, _poolSize);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("ItemPickup_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localScale = ItemPickupBaseScale;

                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = ItemTypeSprites.Get(ItemType.ContactShield);
                renderer.sortingOrder = 12;

                CircleCollider2D collider2D = go.AddComponent<CircleCollider2D>();
                collider2D.isTrigger = true;
                collider2D.radius = 0.6f;

                ItemPickup pickup = go.AddComponent<ItemPickup>();
                pickup.Initialize(this);

                go.SetActive(false);
                _pool.Add(pickup);
            }
        }

        private void SpawnRandomItem()
        {
            ItemPickup pickup = GetAvailablePickup();
            if (pickup == null)
            {
                return;
            }

            pickup.Configure(GetRandomType(), ItemPickupBaseScale);
            pickup.transform.position = GetSpawnPosition();
            pickup.transform.rotation = Quaternion.identity;
            pickup.gameObject.SetActive(true);
        }

        private ItemPickup GetAvailablePickup()
        {
            int count = _pool.Count;
            for (int i = 0; i < count; i++)
            {
                ItemPickup pickup = _pool[i];
                if (pickup != null && !pickup.gameObject.activeSelf)
                {
                    return pickup;
                }
            }

            return null;
        }

        private int GetActiveItemCount()
        {
            int active = 0;
            int count = _pool.Count;
            for (int i = 0; i < count; i++)
            {
                ItemPickup pickup = _pool[i];
                if (pickup != null && pickup.gameObject.activeSelf)
                {
                    active++;
                }
            }

            return active;
        }

        private Vector3 GetSpawnPosition()
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return Vector3.zero;
            }

            float minVx = Mathf.Clamp01(_safeViewportInsetX);
            float maxVx = Mathf.Clamp01(1f - _safeViewportInsetX);
            float minVy = Mathf.Clamp01(_safeViewportInsetBottom);
            float maxVy = Mathf.Clamp01(1f - _safeViewportInsetTop);
            if (minVx >= maxVx || minVy >= maxVy)
            {
                minVx = 0.1f;
                maxVx = 0.9f;
                minVy = 0.2f;
                maxVy = 0.85f;
            }

            float vx = Random.Range(minVx, maxVx);
            float vy = Random.Range(minVy, maxVy);

            float dist = Mathf.Abs(cam.transform.position.z);
            if (dist < 0.01f)
            {
                dist = 10f;
            }

            Vector3 world = cam.ViewportToWorldPoint(new Vector3(vx, vy, dist));
            world.z = 0f;

            Vector3 camPos = cam.transform.position;
            float halfH = Mathf.Max(0.05f, cam.orthographicSize - _worldEdgeInset);
            float halfW = halfH * cam.aspect;
            world.x = Mathf.Clamp(world.x, camPos.x - halfW, camPos.x + halfW);
            world.y = Mathf.Clamp(world.y, camPos.y - halfH, camPos.y + halfH);
            return world;
        }

        private static ItemType GetRandomType()
        {
            int roll = Random.Range(0, 5);
            if (roll == 0)
            {
                return ItemType.ContactShield;
            }
            if (roll == 1)
            {
                return ItemType.PiercingLaser;
            }
            if (roll == 2)
            {
                return ItemType.Overdrive;
            }
            if (roll == 3)
            {
                return ItemType.TimeWarp;
            }

            return ItemType.HealthPack;
        }

        private void ScheduleNextRoll()
        {
            _nextSpawnIn = Random.Range(_minSpawnInterval, _maxSpawnInterval);
        }
    }
}
