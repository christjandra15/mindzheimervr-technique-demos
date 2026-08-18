using System.Collections.Generic;
using UnityEngine;

namespace Mindzheimer.Portfolio
{
    /// <summary>
    /// Generates rounded-rect / circle / soft-glow sprites at runtime so the
    /// calibration UI needs zero imported art assets. Sprites are cached by
    /// their generation parameters so repeated calls are cheap.
    /// </summary>
    public static class UIShapeFactory
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedRect(int width, int height, int radius, Color color)
        {
            width  = Mathf.Max(2, width);
            height = Mathf.Max(2, height);
            radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);

            string key = $"rr_{width}_{height}_{radius}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = 1f;
                    bool cornerX = x < radius || x >= width  - radius;
                    bool cornerY = y < radius || y >= height - radius;

                    if (cornerX && cornerY && radius > 0)
                    {
                        float cx = x < radius ? radius : width  - radius;
                        float cy = y < radius ? radius : height - radius;
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }

                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(
                tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = key;

            cache[key] = sprite;
            return sprite;
        }

        public static Sprite Circle(int diameter, Color color) =>
            RoundedRect(diameter, diameter, diameter / 2, color);

        /// <summary>Radial falloff sprite, used behind tracker dots for a soft glow.</summary>
        public static Sprite SoftGlowCircle(int diameter, Color color)
        {
            diameter = Mathf.Max(2, diameter);
            string key = $"glow_{diameter}_{ColorUtility.ToHtmlStringRGBA(color)}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 c = new Vector2(diameter / 2f, diameter / 2f);
            float r = diameter / 2f;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / r;
                    float alpha = Mathf.Clamp01(1f - d);
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = key;

            cache[key] = sprite;
            return sprite;
        }

        public static void ClearCache() => cache.Clear();
    }
}
