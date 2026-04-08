using System.Collections.Generic;
using UnityEngine;
using StaticDrift.Managers;
using StaticDrift.Player;

namespace StaticDrift.Environment
{
    /// <summary>
    /// Randomly activates laser barriers on screen edges when MatchController rolls ScreenEdgeLaser for that wave
    /// (after the first boss). Blocks screen-wrap and applies damage over time on overlap.
    /// Difficulty scales with how many times the laser hazard has been rolled (see MatchController.GetHazardDifficultyTier), not the match wave number.
    /// </summary>
    public class ScreenEdgeBarrierController : MonoBehaviour
    {
        [System.Flags]
        private enum EdgeMask
        {
            None = 0,
            Left = 1,
            Right = 2,
            Top = 4,
            Bottom = 8
        }

        public static ScreenEdgeBarrierController Instance { get; private set; }

        [Header("Activation")]
        [SerializeField] private float _barrierThickness = 0.42f;
        [SerializeField] private float _innerClampPadding = 0.18f;

        [Header("Combat")]
        [Tooltip("Damage per second while the player stays inside an active barrier collider.")]
        [SerializeField] private float _damagePerSecond = 32f;
        [Tooltip("How often damage is applied (smaller = smoother DOT, slightly more expensive).")]
        [SerializeField] private float _damageTickInterval = 0.12f;

        [Header("Difficulty (laser hazard tier)")]
        [Tooltip("Active/idle laser phase lengths interpolate from min to max as the laser hazard tier reaches this count (tier = times laser was rolled for a wave).")]
        [SerializeField] private float _minActiveDuration = 2.1f;
        [SerializeField] private float _maxActiveDuration = 7.5f;
        [SerializeField] private float _minIdleDuration = 11f;
        [SerializeField] private float _maxIdleDuration = 4.5f;
        [SerializeField] private int _laserTierSpanForMaxDifficulty = 22;

        [Header("Visual")]
        [SerializeField] private float _lineWidth = 0.14f;
        [SerializeField] private Color _laserColor = new Color(1f, 0.25f, 0.15f, 0.92f);
        [SerializeField] private int _lineSortingOrder = 18;
        [Header("Laser feedback")]
        [SerializeField] private float _laserPulseFrequency = 2.8f;
        [SerializeField] private float _laserPulseWidthAmplitude = 0.22f;
        [SerializeField] [Range(0f, 0.5f)] private float _laserPulseAlphaAmplitude = 0.18f;
        [SerializeField] private float _muzzleSparkEmissionRate = 55f;
        [Tooltip("Optional when this object is placed in the scene. For runtime-created ScreenEdgeBarriers, assign the prefab on MatchController → Screen Edge Corner Gun Prefab instead.")]
        [SerializeField] private GameObject _cornerGunPrefab;
        [Tooltip("Extra world units to push corners inward after bounds clamp (fine-tune).")]
        [SerializeField] private float _cornerExtraInset = 0.04f;
        [Tooltip("Used to inset corners when no SpriteRenderer is found on the prefab root.")]
        [SerializeField] private float _cornerInsetFallbackWorld = 0.95f;

        [Header("Laser endpoints (corner prefab children)")]
        [Tooltip("Vertical edges (left/right): same child on both corners — CornerGun uses Laser1 on the up/down barrel line.")]
        [SerializeField] private string _leftEdgeStartLaser = "Laser1";
        [SerializeField] private string _leftEdgeEndLaser = "Laser1";
        [Tooltip("Horizontal edges (bottom/top): same child on both corners — CornerGun uses Laser2 on the left/right barrel line.")]
        [SerializeField] private string _bottomEdgeStartLaser = "Laser2";
        [SerializeField] private string _bottomEdgeEndLaser = "Laser2";
        [Tooltip("Vertical edges: Laser1 on up/down barrels.")]
        [SerializeField] private string _rightEdgeStartLaser = "Laser1";
        [SerializeField] private string _rightEdgeEndLaser = "Laser1";
        [Tooltip("Horizontal edges: Laser2 on left/right barrels.")]
        [SerializeField] private string _topEdgeStartLaser = "Laser2";
        [SerializeField] private string _topEdgeEndLaser = "Laser2";

        private const int CornerBottomLeft = 0;
        private const int CornerBottomRight = 1;
        private const int CornerTopRight = 2;
        private const int CornerTopLeft = 3;

        private Camera _camera;
        private EdgeSegment[] _segments;
        private Transform[] _cornerRoots;
        private float _phaseTimer;
        private bool _barriersLit;
        private EdgeMask _activeMask;
        private readonly Dictionary<PlayerHealth, float> _nextBarrierDamageTime = new Dictionary<PlayerHealth, float>();
        private ParticleSystem[] _muzzleSparkLaser1;
        private ParticleSystem[] _muzzleSparkLaser2;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _camera = Camera.main;
            BuildSegments();
            _phaseTimer = Random.Range(3.5f, 7f);
        }

        private void Start()
        {
            if (_cornerGunPrefab != null && _cornerRoots == null)
            {
                BuildCornerGuns();
            }
        }

        /// <summary>
        /// Called from MatchController after spawning/finding this component so corner guns work when this object is created at runtime (no saved Inspector data).
        /// </summary>
        public void ApplyCornerGunPrefabFromMatch(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            DestroyCornerGunInstances();
            _cornerGunPrefab = prefab;
            BuildCornerGuns();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            _camera = _camera != null ? _camera : Camera.main;
            UpdateLayout();
            UpdateCycle();
            UpdateVisuals();
            UpdateLaserFeedback();
        }

        /// <summary>Call when advancing to a new wave (e.g. after interlude). Respects MatchController hazard roll.</summary>
        public void NotifyWaveChanged(int wave)
        {
            _phaseTimer = Mathf.Min(_phaseTimer, 0.5f);
            if (!CanRunCycle())
            {
                SetAllInactive();
            }
        }

        public bool IsLeftBarrierBlockingWrap(float posX, float left, float wrapMargin) =>
            _barriersLit && (_activeMask & EdgeMask.Left) != 0 && posX < left - wrapMargin;

        public bool IsRightBarrierBlockingWrap(float posX, float right, float wrapMargin) =>
            _barriersLit && (_activeMask & EdgeMask.Right) != 0 && posX > right + wrapMargin;

        public bool IsBottomBarrierBlockingWrap(float posY, float bottom, float wrapMargin) =>
            _barriersLit && (_activeMask & EdgeMask.Bottom) != 0 && posY < bottom - wrapMargin;

        public bool IsTopBarrierBlockingWrap(float posY, float top, float wrapMargin) =>
            _barriersLit && (_activeMask & EdgeMask.Top) != 0 && posY > top + wrapMargin;

        public void ApplyLeftWrapBlock(ref Vector2 pos, Rigidbody2D rb, float leftWorld)
        {
            pos.x = leftWorld + _innerClampPadding;
            BounceVelocityX(rb, 1f);
        }

        public void ApplyRightWrapBlock(ref Vector2 pos, Rigidbody2D rb, float rightWorld)
        {
            pos.x = rightWorld - _innerClampPadding;
            BounceVelocityX(rb, -1f);
        }

        public void ApplyBottomWrapBlock(ref Vector2 pos, Rigidbody2D rb, float bottomWorld)
        {
            pos.y = bottomWorld + _innerClampPadding;
            BounceVelocityY(rb, 1f);
        }

        public void ApplyTopWrapBlock(ref Vector2 pos, Rigidbody2D rb, float topWorld)
        {
            pos.y = topWorld - _innerClampPadding;
            BounceVelocityY(rb, -1f);
        }

        private void BounceVelocityX(Rigidbody2D rb, float inwardSign)
        {
            Vector2 v = rb.linearVelocity;
            if (Mathf.Sign(v.x) != inwardSign && Mathf.Abs(v.x) > 0.05f)
            {
                v.x = -v.x * 0.22f;
            }

            v.x += inwardSign * 2.2f;
            rb.linearVelocity = v;
        }

        private void BounceVelocityY(Rigidbody2D rb, float inwardSign)
        {
            Vector2 v = rb.linearVelocity;
            if (Mathf.Sign(v.y) != inwardSign && Mathf.Abs(v.y) > 0.05f)
            {
                v.y = -v.y * 0.22f;
            }

            v.y += inwardSign * 2.2f;
            rb.linearVelocity = v;
        }

        internal void TryBarrierDamageOverTime(Collider2D other)
        {
            if (!CanRunCycle() || !_barriersLit)
            {
                return;
            }

            PlayerHealth health = other.GetComponent<PlayerHealth>() ?? other.GetComponentInParent<PlayerHealth>();
            if (health == null || health.IsDead)
            {
                return;
            }

            float now = Time.time;
            if (_nextBarrierDamageTime.TryGetValue(health, out float nextAllowed) && now < nextAllowed)
            {
                return;
            }

            float tick = Mathf.Max(0.02f, _damageTickInterval);
            health.TakeDamage(_damagePerSecond * tick, environmentalDamageOverTime: true);
            if (health.IsDead)
            {
                _nextBarrierDamageTime.Remove(health);
                return;
            }

            _nextBarrierDamageTime[health] = now + tick;
        }

        private bool CanRunCycle()
        {
            MatchController mc = MatchController.Instance;
            if (mc == null || mc.IsGameOver || mc.IsPaused)
            {
                return false;
            }

            if (!mc.EnvironmentHazardsActive)
            {
                return false;
            }

            return mc.IsScreenEdgeLaserHazardActiveThisWave;
        }

        private void UpdateCycle()
        {
            if (!CanRunCycle())
            {
                SetAllInactive();
                return;
            }

            MatchController mc = MatchController.Instance;
            int laserTier = mc != null
                ? mc.GetHazardDifficultyTier(MatchController.EnvironmentHazardKind.ScreenEdgeLaser)
                : 1;

            _phaseTimer -= Time.deltaTime;
            if (_barriersLit)
            {
                if (_phaseTimer <= 0f)
                {
                    SetAllInactive();
                    _phaseTimer = GetIdleDurationForLaserTier(laserTier);
                }
            }
            else
            {
                if (_phaseTimer <= 0f)
                {
                    StartPulse(laserTier);
                }
            }
        }

        private void StartPulse(int laserTier)
        {
            int sides = GetSideCountForLaserTier(laserTier);
            _activeMask = PickRandomEdges(sides);
            _barriersLit = _activeMask != EdgeMask.None;
            _phaseTimer = GetActiveDurationForLaserTier(laserTier);
            _nextBarrierDamageTime.Clear();
            if (_barriersLit)
            {
                AudioManager.EnsureExists().PlayEdgeBarrierActivate();
            }
        }

        private void SetAllInactive()
        {
            _barriersLit = false;
            _activeMask = EdgeMask.None;
        }

        private static int GetSideCountForLaserTier(int tier)
        {
            int t = Mathf.Max(1, tier);
            return Mathf.Clamp(1 + (t - 1) / 4, 1, 4);
        }

        private float GetActiveDurationForLaserTier(int tier)
        {
            int span = Mathf.Max(1, _laserTierSpanForMaxDifficulty);
            float t = Mathf.InverseLerp(1, span, Mathf.Clamp(tier, 1, span));
            return Mathf.Lerp(_minActiveDuration, _maxActiveDuration, t);
        }

        private float GetIdleDurationForLaserTier(int tier)
        {
            int span = Mathf.Max(1, _laserTierSpanForMaxDifficulty);
            float t = Mathf.InverseLerp(1, span, Mathf.Clamp(tier, 1, span));
            return Mathf.Lerp(_minIdleDuration, _maxIdleDuration, t);
        }

        private static EdgeMask PickRandomEdges(int count)
        {
            var pool = new List<EdgeMask>
            {
                EdgeMask.Left,
                EdgeMask.Right,
                EdgeMask.Top,
                EdgeMask.Bottom
            };

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            EdgeMask m = EdgeMask.None;
            for (int i = 0; i < count && i < pool.Count; i++)
            {
                m |= pool[i];
            }

            return m;
        }

        private void BuildSegments()
        {
            _segments = new EdgeSegment[4];
            _segments[0] = new EdgeSegment(this, EdgeMask.Left, "BarrierLeft");
            _segments[1] = new EdgeSegment(this, EdgeMask.Right, "BarrierRight");
            _segments[2] = new EdgeSegment(this, EdgeMask.Top, "BarrierTop");
            _segments[3] = new EdgeSegment(this, EdgeMask.Bottom, "BarrierBottom");
        }

        private void UpdateLayout()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            Vector3 cp = _camera.transform.position;
            float hh = _camera.orthographicSize;
            float hw = hh * _camera.aspect;
            float left = cp.x - hw;
            float right = cp.x + hw;
            float bottom = cp.y - hh;
            float top = cp.y + hh;
            float t = _barrierThickness;

            if (_cornerRoots != null)
            {
                PlaceCornerGuns(left, right, bottom, top);
                Vector3 leftA = GetCornerLaserWorld(CornerBottomLeft, _leftEdgeStartLaser);
                Vector3 leftB = GetCornerLaserWorld(CornerTopLeft, _leftEdgeEndLaser);
                Vector3 rightA = GetCornerLaserWorld(CornerBottomRight, _rightEdgeStartLaser);
                Vector3 rightB = GetCornerLaserWorld(CornerTopRight, _rightEdgeEndLaser);
                Vector3 topA = GetCornerLaserWorld(CornerTopLeft, _topEdgeStartLaser);
                Vector3 topB = GetCornerLaserWorld(CornerTopRight, _topEdgeEndLaser);
                Vector3 bottomA = GetCornerLaserWorld(CornerBottomLeft, _bottomEdgeStartLaser);
                Vector3 bottomB = GetCornerLaserWorld(CornerBottomRight, _bottomEdgeEndLaser);

                // Straighten to screen axes on the correct barrel row (Laser1 = vertical barrels, Laser2 = horizontal on CornerGun prefab).
                SnapVerticalToBarrelColumnX(ref leftA, ref leftB, isLeftEdge: true);
                SnapVerticalToBarrelColumnX(ref rightA, ref rightB, isLeftEdge: false);
                SnapHorizontalToBarrelRowY(ref topA, ref topB, isTopEdge: true);
                SnapHorizontalToBarrelRowY(ref bottomA, ref bottomB, isTopEdge: false);

                _segments[0].LayoutVertical(left + t * 0.5f, cp.y, t, hh * 2f, leftA, leftB);
                _segments[1].LayoutVertical(right - t * 0.5f, cp.y, t, hh * 2f, rightA, rightB);
                _segments[2].LayoutHorizontal(cp.x, top - t * 0.5f, hw * 2f, t, topA, topB);
                _segments[3].LayoutHorizontal(cp.x, bottom + t * 0.5f, hw * 2f, t, bottomA, bottomB);
            }
            else
            {
                _segments[0].LayoutVertical(left + t * 0.5f, cp.y, t, hh * 2f);
                _segments[1].LayoutVertical(right - t * 0.5f, cp.y, t, hh * 2f);
                _segments[2].LayoutHorizontal(cp.x, top - t * 0.5f, hw * 2f, t);
                _segments[3].LayoutHorizontal(cp.x, bottom + t * 0.5f, hw * 2f, t);
            }
        }

        private void UpdateVisuals()
        {
            bool showLasers = _barriersLit && CanRunCycle();
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].SetVisible(showLasers && ((_activeMask & _segments[i].Mask) != 0));
            }

            SetCornerGunRenderersVisible(ShouldShowCornerGuns());
        }

        private void UpdateLaserFeedback()
        {
            bool showLasers = _barriersLit && CanRunCycle();
            float wMul = 1f;
            float aMul = 1f;
            if (showLasers)
            {
                float ph = Time.time * _laserPulseFrequency * Mathf.PI * 2f;
                wMul = 1f + _laserPulseWidthAmplitude * Mathf.Sin(ph);
                float alphaSwing = Mathf.Sin(ph + 0.55f);
                aMul = 1f - _laserPulseAlphaAmplitude * (0.5f - 0.5f * alphaSwing);
            }

            for (int i = 0; i < _segments.Length; i++)
            {
                bool segOn = showLasers && ((_activeMask & _segments[i].Mask) != 0);
                if (segOn)
                {
                    _segments[i].ApplyPulseVisuals(wMul, aMul);
                }
            }

            UpdateMuzzleSparks(showLasers);
        }

        private void UpdateMuzzleSparks(bool showLasers)
        {
            if (_muzzleSparkLaser1 == null || _muzzleSparkLaser2 == null)
            {
                return;
            }

            bool lit = showLasers;
            SetCornerSparkEmission(CornerBottomLeft, lit && (_activeMask & EdgeMask.Left) != 0, lit && (_activeMask & EdgeMask.Bottom) != 0);
            SetCornerSparkEmission(CornerBottomRight, lit && (_activeMask & EdgeMask.Right) != 0, lit && (_activeMask & EdgeMask.Bottom) != 0);
            SetCornerSparkEmission(CornerTopRight, lit && (_activeMask & EdgeMask.Right) != 0, lit && (_activeMask & EdgeMask.Top) != 0);
            SetCornerSparkEmission(CornerTopLeft, lit && (_activeMask & EdgeMask.Left) != 0, lit && (_activeMask & EdgeMask.Top) != 0);
        }

        private void SetCornerSparkEmission(int cornerIndex, bool verticalEdgeActive, bool horizontalEdgeActive)
        {
            SetSparkEmission(_muzzleSparkLaser1[cornerIndex], verticalEdgeActive);
            SetSparkEmission(_muzzleSparkLaser2[cornerIndex], horizontalEdgeActive);
        }

        private void SetSparkEmission(ParticleSystem ps, bool active)
        {
            if (ps == null)
            {
                return;
            }

            ParticleSystem.EmissionModule em = ps.emission;
            if (active)
            {
                em.rateOverTime = _muzzleSparkEmissionRate;
                if (!ps.isPlaying)
                {
                    ps.Play();
                }
            }
            else
            {
                em.rateOverTime = 0f;
            }
        }

        private void BuildMuzzleSparkParticles()
        {
            _muzzleSparkLaser1 = null;
            _muzzleSparkLaser2 = null;
            if (_cornerRoots == null)
            {
                return;
            }

            _muzzleSparkLaser1 = new ParticleSystem[4];
            _muzzleSparkLaser2 = new ParticleSystem[4];
            for (int i = 0; i < 4; i++)
            {
                _muzzleSparkLaser1[i] = CreateMuzzleSparkOnChild(_cornerRoots[i], "Laser1");
                _muzzleSparkLaser2[i] = CreateMuzzleSparkOnChild(_cornerRoots[i], "Laser2");
            }
        }

        private ParticleSystem CreateMuzzleSparkOnChild(Transform cornerRoot, string childName)
        {
            Transform t = cornerRoot != null ? cornerRoot.Find(childName) : null;
            if (t == null)
            {
                t = cornerRoot;
            }

            GameObject go = new GameObject("MuzzleSpark");
            go.SetActive(false);
            go.transform.SetParent(t, false);
            go.transform.localPosition = Vector3.zero;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ConfigureMuzzleSparkParticle(ps);
            ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sortingOrder = _lineSortingOrder + 2;
            go.SetActive(true);
            return ps;
        }

        private void ConfigureMuzzleSparkParticle(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.36f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.11f);
            Color bright = Color.Lerp(_laserColor, Color.white, 0.35f);
            bright.a = Mathf.Min(1f, _laserColor.a + 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(bright, _laserColor);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 200;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.055f;
            shape.radiusThickness = 0.35f;
            shape.randomDirectionAmount = 0.78f;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.45f),
                new Keyframe(0.22f, 1f),
                new Keyframe(1f, 0.2f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
            {
                sh = Shader.Find("Particles/Standard Unlit");
            }

            if (sh == null)
            {
                sh = Shader.Find("Sprites/Default");
            }

            if (sh != null)
            {
                rend.material = new Material(sh);
            }

            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>Corner turrets only when this wave’s hazard is the screen-edge laser (not during boss or interlude).</summary>
        private bool ShouldShowCornerGuns()
        {
            MatchController mc = MatchController.Instance;
            if (mc == null || mc.IsGameOver || mc.IsPaused)
            {
                return false;
            }

            return mc.EnvironmentHazardsActive && mc.IsScreenEdgeLaserHazardActiveThisWave;
        }

        private void DestroyCornerGunInstances()
        {
            if (_cornerRoots == null)
            {
                return;
            }

            for (int i = 0; i < _cornerRoots.Length; i++)
            {
                if (_cornerRoots[i] != null)
                {
                    Destroy(_cornerRoots[i].gameObject);
                }
            }

            _cornerRoots = null;
            _muzzleSparkLaser1 = null;
            _muzzleSparkLaser2 = null;
        }

        private void BuildCornerGuns()
        {
            if (_cornerGunPrefab == null)
            {
                return;
            }

            DestroyCornerGunInstances();
            _cornerRoots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                GameObject instance = Instantiate(_cornerGunPrefab, transform);
                instance.name = "CornerGun_" + i;
                foreach (Collider2D col in instance.GetComponentsInChildren<Collider2D>(true))
                {
                    col.enabled = false;
                }

                _cornerRoots[i] = instance.transform;
            }

            BuildMuzzleSparkParticles();
        }

        private void PlaceCornerGuns(float left, float right, float bottom, float top)
        {
            if (_cornerRoots == null)
            {
                return;
            }

            _cornerRoots[CornerBottomLeft].SetPositionAndRotation(new Vector3(left, bottom, 0f), Quaternion.identity);
            _cornerRoots[CornerBottomLeft].localScale = Vector3.one;

            _cornerRoots[CornerBottomRight].SetPositionAndRotation(new Vector3(right, bottom, 0f), Quaternion.identity);
            _cornerRoots[CornerBottomRight].localScale = new Vector3(-1f, 1f, 1f);

            _cornerRoots[CornerTopRight].SetPositionAndRotation(new Vector3(right, top, 0f), Quaternion.identity);
            _cornerRoots[CornerTopRight].localScale = new Vector3(-1f, -1f, 1f);

            _cornerRoots[CornerTopLeft].SetPositionAndRotation(new Vector3(left, top, 0f), Quaternion.identity);
            _cornerRoots[CornerTopLeft].localScale = new Vector3(1f, -1f, 1f);

            for (int i = 0; i < _cornerRoots.Length; i++)
            {
                InsetCornerInsideScreen(i, left, right, bottom, top);
            }
        }

        private void InsetCornerInsideScreen(int index, float left, float right, float bottom, float top)
        {
            Transform tr = _cornerRoots[index];
            if (tr == null)
            {
                return;
            }

            float extraInset = _cornerExtraInset;
            SpriteRenderer sr = tr.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = tr.GetComponentInChildren<SpriteRenderer>();
            }

            if (sr != null)
            {
                Bounds b = sr.bounds;
                Vector3 c = tr.position;
                if (b.min.x < left)
                {
                    c.x += left - b.min.x;
                }

                if (b.max.x > right)
                {
                    c.x -= b.max.x - right;
                }

                if (b.min.y < bottom)
                {
                    c.y += bottom - b.min.y;
                }

                if (b.max.y > top)
                {
                    c.y -= b.max.y - top;
                }

                switch (index)
                {
                    case CornerBottomLeft:
                        c.x += extraInset;
                        c.y += extraInset;
                        break;
                    case CornerBottomRight:
                        c.x -= extraInset;
                        c.y += extraInset;
                        break;
                    case CornerTopRight:
                        c.x -= extraInset;
                        c.y -= extraInset;
                        break;
                    case CornerTopLeft:
                        c.x += extraInset;
                        c.y -= extraInset;
                        break;
                }

                tr.position = c;
                return;
            }

            float f = _cornerInsetFallbackWorld;
            Vector3 p = tr.position;
            switch (index)
            {
                case CornerBottomLeft:
                    p.x = left + f + extraInset;
                    p.y = bottom + f + extraInset;
                    break;
                case CornerBottomRight:
                    p.x = right - f - extraInset;
                    p.y = bottom + f + extraInset;
                    break;
                case CornerTopRight:
                    p.x = right - f - extraInset;
                    p.y = top - f - extraInset;
                    break;
                case CornerTopLeft:
                    p.x = left + f + extraInset;
                    p.y = top - f - extraInset;
                    break;
            }

            tr.position = p;
        }

        private Vector3 GetCornerLaserWorld(int cornerIndex, string childName)
        {
            if (_cornerRoots == null || cornerIndex < 0 || cornerIndex >= _cornerRoots.Length)
            {
                return Vector3.zero;
            }

            Transform root = _cornerRoots[cornerIndex];
            if (root == null)
            {
                return Vector3.zero;
            }

            Transform laser = root.Find(childName);
            return laser != null ? laser.position : root.position;
        }

        /// <summary>Vertical edge beams: column along up/down barrels (screen-left = min X, screen-right = max X).</summary>
        private static void SnapVerticalToBarrelColumnX(ref Vector3 a, ref Vector3 b, bool isLeftEdge)
        {
            float x = isLeftEdge ? Mathf.Min(a.x, b.x) : Mathf.Max(a.x, b.x);
            a.x = x;
            b.x = x;
        }

        /// <summary>Horizontal edge beams: row along left/right barrels (bottom = lower Y, top = higher Y on CornerGun).</summary>
        private static void SnapHorizontalToBarrelRowY(ref Vector3 a, ref Vector3 b, bool isTopEdge)
        {
            float y = isTopEdge ? Mathf.Max(a.y, b.y) : Mathf.Min(a.y, b.y);
            a.y = y;
            b.y = y;
        }

        private void SetCornerGunRenderersVisible(bool visible)
        {
            if (_cornerRoots == null)
            {
                return;
            }

            for (int i = 0; i < _cornerRoots.Length; i++)
            {
                if (_cornerRoots[i] == null)
                {
                    continue;
                }

                foreach (SpriteRenderer sr in _cornerRoots[i].GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.enabled = visible;
                }
            }
        }

        private sealed class EdgeSegment
        {
            private readonly ScreenEdgeBarrierController _owner;
            public readonly EdgeMask Mask;
            private readonly GameObject _go;
            private readonly BoxCollider2D _collider;
            private readonly LineRenderer _line;
            private readonly float _baseLineWidth;
            private readonly Color _baseStartColor;
            private readonly Color _baseEndColor;

            public EdgeSegment(ScreenEdgeBarrierController owner, EdgeMask mask, string name)
            {
                _owner = owner;
                Mask = mask;
                _go = new GameObject(name);
                _go.transform.SetParent(owner.transform, false);

                _collider = _go.AddComponent<BoxCollider2D>();
                _collider.isTrigger = true;

                BarrierHitRelay relay = _go.AddComponent<BarrierHitRelay>();
                relay.Initialize(owner);

                _line = _go.AddComponent<LineRenderer>();
                _line.positionCount = 2;
                _line.useWorldSpace = true;
                _baseLineWidth = owner._lineWidth;
                _line.startWidth = _baseLineWidth;
                _line.endWidth = _baseLineWidth;
                _line.numCapVertices = 4;
                _line.numCornerVertices = 2;
                _line.sortingOrder = owner._lineSortingOrder;
                Shader sh = Shader.Find("Sprites/Default");
                if (sh != null)
                {
                    _line.material = new Material(sh);
                }

                _baseStartColor = owner._laserColor;
                _baseEndColor = owner._laserColor;
                _line.startColor = _baseStartColor;
                _line.endColor = _baseEndColor;
                _line.enabled = false;
                _collider.enabled = false;
            }

            public void ApplyPulseVisuals(float widthMultiplier, float alphaMultiplier)
            {
                float w = _baseLineWidth * widthMultiplier;
                _line.startWidth = w;
                _line.endWidth = w;
                Color c0 = _baseStartColor;
                Color c1 = _baseEndColor;
                c0.a *= alphaMultiplier;
                c1.a *= alphaMultiplier;
                _line.startColor = c0;
                _line.endColor = c1;
            }

            private void ResetLineVisuals()
            {
                _line.startWidth = _baseLineWidth;
                _line.endWidth = _baseLineWidth;
                _line.startColor = _baseStartColor;
                _line.endColor = _baseEndColor;
            }

            public void LayoutVertical(float centerX, float centerY, float w, float h)
            {
                float hh = h * 0.5f;
                LayoutVertical(
                    centerX,
                    centerY,
                    w,
                    h,
                    new Vector3(centerX, centerY - hh, 0f),
                    new Vector3(centerX, centerY + hh, 0f));
            }

            public void LayoutVertical(float centerX, float centerY, float w, float h, Vector3 lineWorldStart, Vector3 lineWorldEnd)
            {
                _go.transform.position = new Vector3(centerX, centerY, 0f);
                _collider.size = new Vector2(w, h);
                _line.SetPosition(0, lineWorldStart);
                _line.SetPosition(1, lineWorldEnd);
            }

            public void LayoutHorizontal(float centerX, float centerY, float w, float h)
            {
                float hw = w * 0.5f;
                LayoutHorizontal(
                    centerX,
                    centerY,
                    w,
                    h,
                    new Vector3(centerX - hw, centerY, 0f),
                    new Vector3(centerX + hw, centerY, 0f));
            }

            public void LayoutHorizontal(float centerX, float centerY, float w, float h, Vector3 lineWorldStart, Vector3 lineWorldEnd)
            {
                _go.transform.position = new Vector3(centerX, centerY, 0f);
                _collider.size = new Vector2(w, h);
                _line.SetPosition(0, lineWorldStart);
                _line.SetPosition(1, lineWorldEnd);
            }

            public void SetVisible(bool on)
            {
                _line.enabled = on;
                _collider.enabled = on;
                if (!on)
                {
                    ResetLineVisuals();
                }
            }
        }

        private sealed class BarrierHitRelay : MonoBehaviour
        {
            private ScreenEdgeBarrierController _owner;

            public void Initialize(ScreenEdgeBarrierController owner)
            {
                _owner = owner;
            }

            private void OnTriggerEnter2D(Collider2D other)
            {
                _owner?.TryBarrierDamageOverTime(other);
            }

            private void OnTriggerStay2D(Collider2D other)
            {
                _owner?.TryBarrierDamageOverTime(other);
            }
        }
    }
}
