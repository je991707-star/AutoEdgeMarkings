using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TownRoadLane.Systems.Prefabs
{
    /// <summary>
    /// Generates self-contained, tileable procedural <see cref="Texture2D"/> assets for road
    /// marking lines, curb strips and intersection area fills so the mod has zero external EAI or
    /// G87 asset dependencies. All created textures are retained for the process lifetime so
    /// Unity's GC never reclaims them while referenced by runtime materials.
    /// </summary>
    internal static class ProceduralTextureFactory
    {
        private static readonly List<Object> s_Pinned = new List<Object>();

        // Road-paint palette tuned to match CS2's daylight/HDRP exposure without blowing out.
        public static readonly Color32 kPaintWhite = new Color32(236, 238, 240, 255);
        public static readonly Color32 kPaintYellow = new Color32(232, 186, 46, 255);
        public static readonly Color32 kPaintBlue = new Color32(58, 128, 214, 255);
        public static readonly Color32 kBikeGreen = new Color32(58, 138, 76, 255);
        public static readonly Color32 kBusRed = new Color32(166, 62, 52, 255);
        public static readonly Color32 kAsphaltDark = new Color32(58, 61, 66, 255);

        private static Texture2D s_FlatNormal;
        private static Texture2D s_PaintMask;

        public static T Pin<T>(T obj) where T : Object
        {
            if (obj != null)
            {
                obj.hideFlags = HideFlags.HideAndDontSave;
                s_Pinned.Add(obj);
            }
            return obj;
        }

        /// <summary>Neutral tangent-space normal map (0.5, 0.5, 1.0) so patterned road markings
        /// do not imprint concrete surface relief onto transparent asphalt areas.</summary>
        public static Texture2D GetFlatNormalTexture()
        {
            if (s_FlatNormal != null) return s_FlatNormal;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "TRL Flat Normal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(128, 128, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            s_FlatNormal = Pin(tex);
            return s_FlatNormal;
        }

        /// <summary>Matte road-paint HDRP MaskMap (R=metallic 0, G=AO 1, B=detail 0, A=smoothness).</summary>
        public static Texture2D GetPaintMaskTexture()
        {
            if (s_PaintMask != null) return s_PaintMask;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "TRL Paint Mask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[16];
            // Low smoothness (52/255 ≈ 0.20) for realistic matte thermoplastic road paint.
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 255, 0, 52);
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            s_PaintMask = Pin(tex);
            return s_PaintMask;
        }

        /// <summary>Tileable 45-degree yellow crosshatch waffle grid on a transparent background
        /// (junction box / keep-clear zone).</summary>
        public static Texture2D CreateJunctionBoxTexture(int size = 512)
        {
            var pixels = new Color32[size * size];
            const float cycles = 2f;
            const float barHalfWidth = 0.085f;
            const float aa = 0.012f;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float d1 = StripeDist(u + v, cycles);
                    float d2 = StripeDist(u - v, cycles);
                    float cov1 = 1f - Smoothstep(barHalfWidth - aa, barHalfWidth + aa, d1);
                    float cov2 = 1f - Smoothstep(barHalfWidth - aa, barHalfWidth + aa, d2);
                    float cov = math.max(cov1, cov2);

                    if (cov <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(kPaintYellow.r, kPaintYellow.g, kPaintYellow.b, 0);
                        continue;
                    }

                    float grain = 0.94f + 0.12f * ValueNoise(x, y, 71);
                    pixels[y * size + x] = ScaleColor(kPaintYellow, grain, cov * 0.94f);
                }
            }

            return BuildTexture("TRL Junction Box Texture", size, size, pixels);
        }

        /// <summary>Tileable 45-degree diagonal hatching stripes on a transparent background.</summary>
        /// <param name="paintDuty">Fraction of each stripe period that is painted (e.g. 0.50 for 1:1 dense, 0.33 for 1:2 sparse).</param>
        /// <param name="cycles">Number of stripe periods across one tile (integer for seamless tiling).</param>
        public static Texture2D CreateDiagonalStripesTexture(string name, Color32 color, float paintDuty, int cycles, int size = 512)
        {
            var pixels = new Color32[size * size];
            float halfDuty = paintDuty * 0.5f;
            const float aa = 0.015f;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float d = StripeDist(u + v, cycles);
                    float cov = 1f - Smoothstep(halfDuty - aa, halfDuty + aa, d);

                    if (cov <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(color.r, color.g, color.b, 0);
                        continue;
                    }

                    float grain = 0.93f + 0.13f * ValueNoise(x, y, 137);
                    pixels[y * size + x] = ScaleColor(color, grain, cov * 0.92f);
                }
            }

            return BuildTexture(name, size, size, pixels);
        }

        /// <summary>Tileable solid road-surface paint or asphalt texture with subtle mineral grain
        /// so colored bus/bike lanes and asphalt patches blend naturally into CS2 roads.</summary>
        public static Texture2D CreateTintedPavementTexture(string name, Color32 baseColor, float opacity, float grainAmplitude, int size = 512)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nFine = ValueNoise(x, y, 19);
                    float nMid = ValueNoise(x >> 2, y >> 2, 53);
                    float nCoarse = ValueNoise(x >> 4, y >> 4, 97);
                    float combined = (nFine * 0.45f + nMid * 0.35f + nCoarse * 0.20f) - 0.5f;
                    float shade = 1f + combined * grainAmplitude;
                    pixels[y * size + x] = ScaleColor(baseColor, shade, opacity);
                }
            }
            return BuildTexture(name, size, size, pixels);
        }

        /// <summary>Longitudinal solid road-marking decal texture (U = lateral across lane width,
        /// V = longitudinal along curve).</summary>
        public static Texture2D CreateSolidLineTexture(string name, Color32 color, float widthFraction = 0.72f, int size = 256)
        {
            var pixels = new Color32[size * size];
            float halfW = widthFraction * 0.5f;
            const float aa = 0.03f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float du = math.abs(u - 0.5f);
                    float cov = 1f - Smoothstep(halfW - aa, halfW + aa, du);
                    if (cov <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(color.r, color.g, color.b, 0);
                        continue;
                    }
                    float grain = 0.94f + 0.10f * ValueNoise(x, y, 29);
                    pixels[y * size + x] = ScaleColor(color, grain, cov * 0.97f);
                }
            }
            return BuildTexture(name, size, size, pixels);
        }

        /// <summary>Longitudinal dashed road-marking decal texture with anti-aliased dash ends.</summary>
        public static Texture2D CreateDashedLineTexture(string name, Color32 color, float widthFraction = 0.72f, float dashFraction = 0.52f, int size = 256)
        {
            var pixels = new Color32[size * size];
            float halfW = widthFraction * 0.5f;
            float halfD = dashFraction * 0.5f;
            const float aaU = 0.03f;
            const float aaV = 0.015f;

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                float dv = math.abs(v - 0.5f);
                float covV = 1f - Smoothstep(halfD - aaV, halfD + aaV, dv);
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
                    float grain = 0.94f + 0.10f * ValueNoise(x, y, 43);
                    pixels[y * size + x] = ScaleColor(color, grain, cov * 0.97f);
                }
            }
            return BuildTexture(name, size, size, pixels);
        }

        /// <summary>Flat concrete curb strip texture for traffic islands and medians: light
        /// concrete crown with subtle grain, expansion seams and dark gutter bevel shadow.</summary>
        public static Texture2D CreateCurbTexture(int size = 256)
        {
            var pixels = new Color32[size * size];
            var crown = new Color32(182, 187, 192, 255);
            var bevelLight = new Color32(204, 208, 212, 255);
            var gutterShadow = new Color32(84, 90, 98, 255);

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                // Two subtle transverse expansion joints per tile.
                float seamDist = math.min(math.abs(v - 0.25f), math.abs(v - 0.75f));
                float seamDarken = 1f - 0.22f * (1f - Smoothstep(0.004f, 0.014f, seamDist));

                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float du = math.abs(u - 0.5f);
                    float edgeAlpha = 1f - Smoothstep(0.42f, 0.48f, du);
                    if (edgeAlpha <= 0.001f)
                    {
                        pixels[y * size + x] = new Color32(crown.r, crown.g, crown.b, 0);
                        continue;
                    }

                    // Profile across curb width: bright lip near left edge, crown in middle, dark
                    // shadow strip along right gutter edge.
                    Color32 col;
                    if (u < 0.24f)
                        col = LerpColor(gutterShadow, bevelLight, Smoothstep(0.08f, 0.24f, u));
                    else if (u < 0.72f)
                        col = LerpColor(bevelLight, crown, Smoothstep(0.24f, 0.45f, u));
                    else
                        col = LerpColor(crown, gutterShadow, Smoothstep(0.72f, 0.90f, u));

                    float grain = (0.92f + 0.16f * ValueNoise(x, y, 83)) * seamDarken;
                    pixels[y * size + x] = ScaleColor(col, grain, edgeAlpha);
                }
            }
            return BuildTexture("TRL Curb Texture", size, size, pixels);
        }

        private static Texture2D BuildTexture(string name, int width, int height, Color32[] pixels)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: true, linear: false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
            };
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return Pin(tex);
        }

        private static float StripeDist(float coord, float cycles)
        {
            float p = coord * cycles;
            float frac = p - math.floor(p);
            return math.abs(frac - 0.5f);
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

        private static Color32 LerpColor(Color32 a, Color32 b, float t)
        {
            t = math.saturate(t);
            return new Color32(
                (byte)math.round(math.lerp(a.r, b.r, t)),
                (byte)math.round(math.lerp(a.g, b.g, t)),
                (byte)math.round(math.lerp(a.b, b.b, t)),
                255);
        }
    }
}
