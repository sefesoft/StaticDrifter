using UnityEngine;

namespace StaticDrift.VFX
{
    /// <summary>
    /// Single shared materials for runtime VFX so shaders stay referenced in builds (avoids stripped URP particle shaders → pink on device)
    /// and SpriteRenderer layers use the URP 2D sprite shader (correct UVs; avoids flat quads in Editor vs GLES).
    /// </summary>
    public static class SharedVfxMaterials
    {
        private static Material _urpParticlesUnlit;
        private static Material _urpSpriteUnlitDefault;
        private static Texture2D _softRadialParticleTex;

        public static void ApplyUrpParticlesUnlit(ParticleSystemRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material mat = GetUrpParticlesUnlit();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        public static void ApplyUrpSpriteUnlitDefault(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            Material mat = GetUrpSpriteUnlitDefault();
            if (mat != null)
            {
                renderer.sharedMaterial = mat;
            }
        }

        public static Material GetUrpParticlesUnlit()
        {
            if (_urpParticlesUnlit != null)
            {
                return _urpParticlesUnlit;
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
                return null;
            }

            _urpParticlesUnlit = new Material(shader);
            _urpParticlesUnlit.name = "SharedURPParticlesUnlit";
            EnsureSoftRadialBaseMap(_urpParticlesUnlit);
            return _urpParticlesUnlit;
        }

        /// <summary>
        /// URP particle default is a solid white quad; assign a soft radial alpha so billboards read as round puffs, not big squares.
        /// </summary>
        private static void EnsureSoftRadialBaseMap(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            if (_softRadialParticleTex == null)
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.name = "SoftRadialParticle";
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                float half = (size - 1) * 0.5f;
                float maxR = half * 0.99f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - half;
                        float dy = y - half;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                        float a = 1f - Mathf.SmoothStep(0f, 1f, d);
                        a = Mathf.Pow(Mathf.Clamp01(a), 1.2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                tex.Apply();
                _softRadialParticleTex = tex;
            }

            mat.SetTexture("_BaseMap", _softRadialParticleTex);
            mat.SetColor("_BaseColor", Color.white);
        }

        public static Material GetUrpSpriteUnlitDefault()
        {
            if (_urpSpriteUnlitDefault != null)
            {
                return _urpSpriteUnlitDefault;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            _urpSpriteUnlitDefault = new Material(shader);
            _urpSpriteUnlitDefault.name = "SharedURPSpriteUnlitDefault";
            return _urpSpriteUnlitDefault;
        }
    }
}
