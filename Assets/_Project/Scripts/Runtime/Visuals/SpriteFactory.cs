using UnityEngine;

namespace FunFlow.Plinko
{
    public static class SpriteFactory
    {
        public const float DefaultPixelsPerUnit = 100f;
        public const float CircleFillFraction = 0.92f;

        public static Texture2D CreateCircleTexture(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float radius = size * 0.5f * CircleFillFraction;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 0.75f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateGlowTexture(int size = 128)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float maxDist = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                    float falloff = 1f - t;
                    float alpha = falloff * falloff * (1f - 0.15f * t);
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Texture2D CreateRoundedSquareTexture(int size = 128, float cornerRadiusFraction = 0.22f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RoundedSquare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float halfBox = size * 0.5f * 0.94f;
            float cornerRadius = halfBox * Mathf.Clamp01(cornerRadiusFraction);
            float innerHalf = halfBox - cornerRadius;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x - half) - innerHalf;
                    float py = Mathf.Abs(y - half) - innerHalf;
                    float qx = Mathf.Max(px, 0f);
                    float qy = Mathf.Max(py, 0f);
                    float dist = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px, py), 0f) - cornerRadius;
                    float alpha = Mathf.Clamp01(0.75f - dist);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public static Sprite CreateSpriteFromTexture(Texture2D texture, float pixelsPerUnit = DefaultPixelsPerUnit)
        {
            if (texture == null)
            {
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
        }
    }
}
