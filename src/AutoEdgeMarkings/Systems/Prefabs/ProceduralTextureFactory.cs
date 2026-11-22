using Unity.Mathematics;
using UnityEngine;

namespace AutoEdgeMarkings.Systems.Prefabs
{
    /// <summary>
    /// Generates self-contained, tileable procedural <see cref="Texture2D"/> line textures for the
    /// built-in edge-line and parking styles, so the mod has no external asset dependency.
    /// </summary>
    internal static class ProceduralTextureFactory
    {

        // Road-paint palette tuned to match CS2's daylight/HDRP exposure without blowing out.
        public static readonly Color32 kPaintWhite = new Color32(236, 238, 240, 255);
        public static readonly Color32 kPaintYellow = new Color32(232, 186, 46, 255);
        public static readonly Color32 kPaintBlue = new Color32(58, 128, 214, 255);

        /// <summary>Longitudinal road-marking decal texture (U = lateral across lane width,
        /// V = longitudinal along curve). <paramref name="dashFraction"/> null = solid line;
        /// otherwise one anti-aliased dash covering that fraction of each tile.</summary>
        public static Texture2D CreateLineTexture(string name, Color32 color, float widthFraction, float? dashFraction = null)
        {
            const int size = 256;
            var pixels = new Color32[size * size];
            float halfW = widthFraction * 0.5f;
            const float aaU = 0.03f;
            const float aaV = 0.015f;
            int seed = dashFraction == null ? 29 : 43;

            for (int y = 0; y < size; y++)
            {
                float covV = 1f;
                if (dashFraction is float dash)
                {
                    float dv = math.abs((y + 0.5f) / size - 0.5f);
                    covV = 1f - Smoothstep(dash * 0.5f - aaV, dash * 0.5f + aaV, dv);
                }
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float du = math.abs(u - 0.5f);
                    float covU = 1f - Smoothstep(halfW - aaU, halfW + aaU, du);
                    float cov = covU * covV;
                    if (cov <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(color.r, color.g, color.b, 0);
                        continue;
                    }
                    float grain = 0.94f + 0.10f * ValueNoise(x, y, seed);
                    pixels[y * size + x] = ScaleColor(color, grain, cov * 0.97f);
                }
            }

            // HideAndDontSave includes DontUnloadUnusedAsset, so Unity never unloads it.
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return tex;
        }

        private static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = math.saturate((x - edge0) / math.max(edge1 - edge0, 1e-6f));
            return t * t * (3f - 2f * t);
        }

        private static float ValueNoise(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFu) / 65535f;
            }
        }

        private static Color32 ScaleColor(Color32 c, float scale, float alpha01)
        {
            byte r = (byte)math.clamp((int)math.round(c.r * scale), 0, 255);
            byte g = (byte)math.clamp((int)math.round(c.g * scale), 0, 255);
            byte b = (byte)math.clamp((int)math.round(c.b * scale), 0, 255);
            byte a = (byte)math.clamp((int)math.round(alpha01 * 255f), 0, 255);
            return new Color32(r, g, b, a);
        }
    }
}
