using System.Collections;
using UnityEngine;

namespace FunFlow.Plinko
{
    public class PegView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer glowRenderer;

        [SerializeField] private int row;
        [SerializeField] private int col;
        [SerializeField] private Vector3 baseScale = Vector3.one;
        [SerializeField] private Color normalColor = new Color(0x9D / 255f, 0x8F / 255f, 0xF0 / 255f, 1f);
        [SerializeField] private Color pulseColor = new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f);

        private Coroutine _pulseCoroutine;

        public int Row => row;
        public int Col => col;

        public event System.Action<PegView> OnPegHit;

        private void Awake()
        {
            CacheBodyRenderer();
        }

        public void Initialize(int pegRow, int pegCol, PlinkoConfig cfg)
        {
            row = pegRow;
            col = pegCol;
            CacheBodyRenderer();

            normalColor = cfg.pegColor;
            pulseColor = cfg.pegPulseColor;

            float spriteRadius = 0.58f;
            if (bodyRenderer != null && bodyRenderer.sprite != null)
            {
                float candidate = bodyRenderer.sprite.bounds.extents.x * PlinkoConfig.CircleFillFraction;
                if (candidate > 0.001f)
                {
                    spriteRadius = candidate;
                }
            }

            float uniformScale = cfg.pegRadius / spriteRadius;
            baseScale = new Vector3(uniformScale, uniformScale, 1f);
            transform.localScale = baseScale;

            var circleCol = GetComponent<CircleCollider2D>();
            if (circleCol != null)
            {
                circleCol.radius = spriteRadius;
            }

            ApplyVisual(1f);
        }

        public void Pulse()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }

            _pulseCoroutine = StartCoroutine(PulseRoutine());
        }

        private IEnumerator PulseRoutine()
        {
            const float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);
                ApplyVisual(easeOut);
                yield return null;
            }

            ApplyVisual(1f);
            _pulseCoroutine = null;
        }

        private void ApplyVisual(float easeOut)
        {
            transform.localScale = baseScale * Mathf.Lerp(1.38f, 1f, easeOut);

            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(pulseColor, normalColor, easeOut);
            }

            if (glowRenderer != null)
            {
                Color g = pulseColor;
                g.a = Mathf.Lerp(0.75f, 0f, easeOut);
                glowRenderer.color = g;
            }
        }

        private void CacheBodyRenderer()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider != null && collision.collider.GetComponent<PlinkoBall>() != null)
            {
                Pulse();
                OnPegHit?.Invoke(this);
            }
        }
    }
}
