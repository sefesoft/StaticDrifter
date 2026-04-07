using UnityEngine;

namespace StaticDrift.Player
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerThrusterVFX : MonoBehaviour
    {
        [SerializeField] private Transform _rocketTransform;
        [SerializeField] private SpriteRenderer _outerBloomRenderer;
        [SerializeField] private SpriteRenderer _flameRenderer;
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] private SpriteRenderer _coreRenderer;

        [Header("Shape")]
        [SerializeField] private Vector3 _baseScale = new Vector3(0.32f, 0.58f, 1f);
        [SerializeField] private Vector3 _activeScale = new Vector3(0.44f, 1.05f, 1f);
        [Tooltip("Slightly larger than main flame only; keep low to match pre-VFX footprint.")]
        [SerializeField] private float _outerBloomScaleMultiplier = 2.2f;
        [SerializeField] private float _glowScaleMultiplier = 2.15f;
        [SerializeField] private float _coreScaleMultiplier = 0.38f;

        [Header("Ramp")]
        [SerializeField] private float _rampUpSeconds = 0.11f;
        [SerializeField] private float _rampDownSeconds = 0.26f;

        [Header("Colors")]
        [SerializeField] private Color _outerBloomColor = new Color(1f, 0.22f, 0.02f, 0.28f);
        [SerializeField] private Color _flameColor = new Color(1f, 0.42f, 0.08f, 0.98f);
        [SerializeField] private Color _glowColor = new Color(1f, 0.38f, 0.06f, 0.5f);
        [SerializeField] private Color _coreColor = new Color(1f, 0.98f, 0.78f, 0.98f);

        [Header("Motion")]
        [SerializeField] private float _flickerSpeed = 30f;
        [SerializeField] private float _flashSpeed = 19f;
        [SerializeField] private float _wobbleDegrees = 4f;
        [SerializeField] private float _wobbleSpeed = 21f;

        private PlayerController _controller;
        private bool _forceActive;
        private float _manualIntensity = 1f;
        private float _blend;

        private static Sprite _gradientFlameSprite;
        private static Material _additiveParticleMat;
        private static int _gradientSpriteRecipe;
        private const int GradientSpriteRecipe = 2;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            EnsureRocketTransform();
            EnsureFlameLayers();
            SetRenderersVisible(false);
        }

        private void Update()
        {
            if (_flameRenderer == null || _controller == null)
            {
                return;
            }

            bool wantOn = _forceActive || _controller.IsAccelerating;
            float ramp = wantOn ? _rampUpSeconds : _rampDownSeconds;
            float rate = 1f / Mathf.Max(0.02f, ramp);
            // Wave interlude / hyperspace runs at Time.timeScale == 0; ramp must use unscaled delta.
            _blend = Mathf.MoveTowards(_blend, wantOn ? 1f : 0f, Time.unscaledDeltaTime * rate);

            if (_blend < 0.002f)
            {
                SetRenderersVisible(false);
                return;
            }

            SetRenderersVisible(true);

            float envelope = Mathf.SmoothStep(0f, 1f, _blend);
            float t = Time.unscaledTime;
            float flicker = 0.68f + 0.32f * Mathf.Abs(Mathf.Sin(t * _flickerSpeed));
            float flash = 0.82f + 0.18f * Mathf.Sin(t * _flashSpeed);
            float combined = flicker * flash * Mathf.Max(0.5f, _manualIntensity);
            float scaleT = Mathf.Clamp01(combined * envelope);
            Vector3 mainScale = Vector3.Lerp(_baseScale, _activeScale, scaleT);

            float wobble = Mathf.Sin(t * _wobbleSpeed) * _wobbleDegrees * envelope;
            Quaternion wobbleRot = Quaternion.Euler(0f, 0f, wobble);

            _flameRenderer.transform.localScale = mainScale;
            _flameRenderer.transform.localRotation = wobbleRot;

            Color fc = _flameColor;
            fc.a = (0.55f + 0.45f * combined) * envelope;
            _flameRenderer.color = fc;

            if (_glowRenderer != null)
            {
                float glowPulse = 0.75f + 0.25f * Mathf.Sin(t * (_flickerSpeed * 0.65f));
                _glowRenderer.transform.localScale = mainScale * _glowScaleMultiplier * glowPulse;
                _glowRenderer.transform.localRotation = wobbleRot;
                Color gc = _glowColor;
                gc.a = _glowColor.a * (0.55f + 0.45f * combined) * envelope;
                _glowRenderer.color = gc;
            }

            if (_coreRenderer != null)
            {
                float corePulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * (_flickerSpeed * 1.2f)));
                _coreRenderer.transform.localScale = mainScale * _coreScaleMultiplier * corePulse;
                _coreRenderer.transform.localRotation = wobbleRot;
                Color cc = _coreColor;
                cc.a = (0.75f + 0.25f * combined) * envelope;
                _coreRenderer.color = cc;
            }

            if (_outerBloomRenderer != null)
            {
                float bloomPulse = 0.8f + 0.2f * Mathf.Sin(t * (_flickerSpeed * 0.42f));
                _outerBloomRenderer.transform.localScale = mainScale * _outerBloomScaleMultiplier * bloomPulse;
                _outerBloomRenderer.transform.localRotation = wobbleRot;
                Color bc = _outerBloomColor;
                bc.a = _outerBloomColor.a * envelope * bloomPulse;
                _outerBloomRenderer.color = bc;
            }
        }

        public void SetManualThrusterOverride(bool active, float intensity = 1f)
        {
            _forceActive = active;
            _manualIntensity = Mathf.Max(0.5f, intensity);
        }

        private void SetRenderersVisible(bool visible)
        {
            if (_outerBloomRenderer != null)
            {
                _outerBloomRenderer.enabled = visible;
            }

            if (_flameRenderer != null)
            {
                _flameRenderer.enabled = visible;
            }

            if (_glowRenderer != null)
            {
                _glowRenderer.enabled = visible;
            }

            if (_coreRenderer != null)
            {
                _coreRenderer.enabled = visible;
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
            Sprite gradient = GetGradientFlameSprite();

            if (_outerBloomRenderer == null)
            {
                Transform o = _rocketTransform.Find("FlameOuterBloom");
                if (o != null)
                {
                    _outerBloomRenderer = o.GetComponent<SpriteRenderer>();
                }
            }

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

            if (_outerBloomRenderer == null)
            {
                GameObject bloom = new GameObject("FlameOuterBloom");
                bloom.transform.SetParent(_rocketTransform, false);
                bloom.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                bloom.transform.SetAsFirstSibling();
                _outerBloomRenderer = bloom.AddComponent<SpriteRenderer>();
                _outerBloomRenderer.sprite = gradient;
                _outerBloomRenderer.color = _outerBloomColor;
                _outerBloomRenderer.sortingOrder = 6;
                TrySetAdditiveMaterial(_outerBloomRenderer);
            }

            if (_glowRenderer == null)
            {
                GameObject glow = new GameObject("FlameGlow");
                glow.transform.SetParent(_rocketTransform, false);
                glow.transform.localPosition = Vector3.zero;
                glow.transform.SetAsFirstSibling();
                _glowRenderer = glow.AddComponent<SpriteRenderer>();
                _glowRenderer.sprite = gradient;
                _glowRenderer.color = _glowColor;
                _glowRenderer.sortingOrder = 10;
            }

            if (_flameRenderer == null)
            {
                GameObject flame = new GameObject("Flame");
                flame.transform.SetParent(_rocketTransform, false);
                flame.transform.localPosition = Vector3.zero;
                _flameRenderer = flame.AddComponent<SpriteRenderer>();
                _flameRenderer.sprite = gradient;
                _flameRenderer.color = _flameColor;
                _flameRenderer.sortingOrder = 12;
            }

            if (_coreRenderer == null)
            {
                GameObject core = new GameObject("FlameCore");
                core.transform.SetParent(_rocketTransform, false);
                core.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                _coreRenderer = core.AddComponent<SpriteRenderer>();
                _coreRenderer.sprite = gradient;
                _coreRenderer.color = _coreColor;
                _coreRenderer.sortingOrder = 14;
            }

            WirePrefabFlameSpritesAndMaterials(gradient);
        }

        /// <summary>Prefab layers can omit the procedural sprite; assign it at runtime. Keeps any sprite you set in the prefab.</summary>
        private void WirePrefabFlameSpritesAndMaterials(Sprite gradient)
        {
            if (_outerBloomRenderer != null)
            {
                if (_outerBloomRenderer.sprite == null)
                {
                    _outerBloomRenderer.sprite = gradient;
                    TrySetAdditiveMaterial(_outerBloomRenderer);
                }
            }

            if (_glowRenderer != null && _glowRenderer.sprite == null)
            {
                _glowRenderer.sprite = gradient;
            }

            if (_flameRenderer != null && _flameRenderer.sprite == null)
            {
                _flameRenderer.sprite = gradient;
            }

            if (_coreRenderer != null && _coreRenderer.sprite == null)
            {
                _coreRenderer.sprite = gradient;
            }
        }

        private static Sprite GetGradientFlameSprite()
        {
            if (_gradientFlameSprite != null && _gradientSpriteRecipe == GradientSpriteRecipe)
            {
                return _gradientFlameSprite;
            }

            if (_gradientFlameSprite != null)
            {
                if (Application.isPlaying)
                {
                    Texture2D oldTex = _gradientFlameSprite.texture;
                    Object.Destroy(_gradientFlameSprite);
                    if (oldTex != null)
                    {
                        Object.Destroy(oldTex);
                    }
                }
#if UNITY_EDITOR
                else
                {
                    Texture2D oldTex = _gradientFlameSprite.texture;
                    Object.DestroyImmediate(_gradientFlameSprite);
                    if (oldTex != null)
                    {
                        Object.DestroyImmediate(oldTex);
                    }
                }
#endif
                _gradientFlameSprite = null;
            }

            const int height = 56;
            const int width = 1;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float edge = Mathf.Pow(t, 1.35f);
                float alpha = Mathf.Clamp01(Mathf.Pow(t, 0.55f)) * (0.15f + 0.85f * edge);
                float cool = 1f - t;
                Color c = new Color(
                    Mathf.Lerp(0.95f, 1f, edge),
                    Mathf.Lerp(0.15f, 0.55f, t) + 0.25f * cool,
                    Mathf.Lerp(0.02f, 0.12f, 1f - t),
                    alpha);
                tex.SetPixel(0, y, c);
            }

            tex.Apply();
            // Match the old 1×1 white quad: PPU 1 → ~1 world unit. Here height is 56px so PPU=56 keeps
            // base flame length ~1 unit before _baseScale / _activeScale (PPU=1 made this 56 units tall).
            const float pixelsPerUnit = height;
            _gradientFlameSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 1f),
                pixelsPerUnit);
            _gradientSpriteRecipe = GradientSpriteRecipe;
            return _gradientFlameSprite;
        }

        private static void TrySetAdditiveMaterial(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            if (_additiveParticleMat != null)
            {
                renderer.material = _additiveParticleMat;
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Additive");
            }

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader == null)
            {
                return;
            }

            _additiveParticleMat = new Material(shader);
            renderer.material = _additiveParticleMat;
        }
    }
}
