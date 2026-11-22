using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Plinko
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class PlinkoBall : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private CircleCollider2D circleCollider;
        [SerializeField] private SpriteRenderer bodyRenderer;

        private PlinkoConfig _config;
        private System.Action<PlinkoBall, int> _onLanded;
        private System.Action<PlinkoBall> _onDespawnReady;
        private Coroutine _guidedCoroutine;
        private Coroutine _despawnCoroutine;
        private bool _isClassicActive;
        private bool _arrivalConsumed;
        private float _stuckTimer;
        private float _slotY;
        private Vector3 _baseScale = Vector3.one;

        public int TargetSlot { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (circleCollider == null)
            {
                circleCollider = GetComponent<CircleCollider2D>();
            }

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponent<SpriteRenderer>();
            }
        }

        public void ApplyConfig(PlinkoConfig cfg)
        {
            CacheComponents();
            _config = cfg;
            _slotY = PlinkoMath.GetSlotPosition(0, cfg).y;

            float spriteRadius = 0.58f;
            if (bodyRenderer != null && bodyRenderer.sprite != null)
            {
                float candidate = bodyRenderer.sprite.bounds.extents.x * PlinkoConfig.CircleFillFraction;
                if (candidate > 0.001f)
                {
                    spriteRadius = candidate;
                }
            }

            float uniformScale = cfg.ballRadius / spriteRadius;
            _baseScale = new Vector3(uniformScale, uniformScale, 1f);
            transform.localScale = _baseScale;

            if (circleCollider != null)
            {
                circleCollider.radius = spriteRadius;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = cfg.ballColor;
            }
        }

        public bool TryConsumeClassicArrival()
        {
            if (!_isClassicActive || _arrivalConsumed)
            {
                return false;
            }

            _arrivalConsumed = true;
            _isClassicActive = false;
            return true;
        }

        public void LaunchClassic(
            PlinkoConfig cfg,
            int targetSlot,
            System.Action<PlinkoBall, int> onLanded,
            System.Action<PlinkoBall> onDespawnReady = null)
        {
            StopActiveDrop();
            ApplyConfig(cfg);

            TargetSlot = targetSlot;
            _onLanded = onLanded;
            _onDespawnReady = onDespawnReady;
            _arrivalConsumed = false;
            _isClassicActive = true;

            Vector2 spawn = PlinkoMath.GetSpawnPosition(cfg);
            float xOffset = Random.Range(-cfg.classicDropJitter, cfg.classicDropJitter);
            transform.position = new Vector3(spawn.x + xOffset, spawn.y, 0f);
            gameObject.SetActive(true);

            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = 1.8f;
            rb.linearDamping = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            float initialPush = Random.Range(-0.45f, 0.45f);
            if (Mathf.Abs(initialPush) < 0.08f)
            {
                initialPush = initialPush >= 0f ? 0.14f : -0.14f;
            }

            rb.AddForce(new Vector2(initialPush, -0.25f), ForceMode2D.Impulse);
        }

        public void LaunchGuided(
            List<PlinkoMath.GuidedStep> path,
            PlinkoConfig cfg,
            int targetSlot,
            System.Func<int, int, PegView> pegLookup,
            System.Action onPegBounce,
            System.Action<PlinkoBall, int> onLanded,
            System.Action<PlinkoBall> onDespawnReady = null)
        {
            StopActiveDrop();
            ApplyConfig(cfg);

            TargetSlot = targetSlot;
            _onLanded = onLanded;
            _onDespawnReady = onDespawnReady;
            _arrivalConsumed = false;
            _isClassicActive = false;

            if (path == null || path.Count < 2)
            {
                return;
            }

            Vector2 startPos = path[0].position;
            transform.position = new Vector3(startPos.x, startPos.y, 0f);
            gameObject.SetActive(true);

            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            _guidedCoroutine = StartCoroutine(GuidedRoutine(path, cfg, pegLookup, onPegBounce));
        }

        public void NotifyLandedExternally()
        {
            _isClassicActive = false;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity *= 0.25f;
                rb.linearDamping = 4f;
            }

            StartDespawn(0.45f);
        }

        private void StartDespawn(float settleDelay)
        {
            if (_despawnCoroutine != null || !gameObject.activeInHierarchy)
            {
                return;
            }

            _despawnCoroutine = StartCoroutine(DespawnRoutine(settleDelay));
        }

        private IEnumerator DespawnRoutine(float settleDelay)
        {
            if (settleDelay > 0f)
            {
                yield return new WaitForSeconds(settleDelay);
            }

            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }

            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            const float fadeDuration = 0.16f;
            float elapsed = 0f;
            Color baseColor = _config != null ? _config.ballColor : Color.white;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                transform.localScale = _baseScale * Mathf.Lerp(1f, 0.35f, t);

                if (bodyRenderer != null)
                {
                    Color c = baseColor;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    bodyRenderer.color = c;
                }

                yield return null;
            }

            _despawnCoroutine = null;
            _onDespawnReady?.Invoke(this);
        }

        private void StopActiveDrop()
        {
            if (_guidedCoroutine != null)
            {
                StopCoroutine(_guidedCoroutine);
                _guidedCoroutine = null;
            }

            if (_despawnCoroutine != null)
            {
                StopCoroutine(_despawnCoroutine);
                _despawnCoroutine = null;
            }

            _isClassicActive = false;
            _stuckTimer = 0f;
        }

        private void FixedUpdate()
        {
            if (!_isClassicActive || _arrivalConsumed || _config == null)
            {
                return;
            }

            Vector2 pos = transform.position;
            if (pos.y < _slotY - 2f)
            {
                if (TryConsumeClassicArrival())
                {
                    float rawIndex = pos.x / _config.horizontalSpacing + (_config.slotCount - 1) * 0.5f;
                    int nearestSlot = Mathf.Clamp(Mathf.RoundToInt(rawIndex), 0, _config.slotCount - 1);
                    _onLanded?.Invoke(this, nearestSlot);
                    StartDespawn(0.25f);
                }

                return;
            }

            if (pos.y > _slotY + 0.35f && rb.linearVelocity.sqrMagnitude < 0.0064f)
            {
                _stuckTimer += Time.fixedDeltaTime;
                if (_stuckTimer > 0.55f)
                {
                    float nudgeX = Random.value < 0.5f ? -0.6f : 0.6f;
                    rb.AddForce(new Vector2(nudgeX, 0.25f), ForceMode2D.Impulse);
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        private static Vector2 GetGuidedWaypointPos(PlinkoMath.GuidedStep step, PlinkoConfig cfg)
        {
            return step.isPegHit
                ? step.position + new Vector2(0f, cfg.pegRadius + cfg.ballRadius * 0.85f)
                : step.position;
        }

        private IEnumerator GuidedRoutine(
            List<PlinkoMath.GuidedStep> path,
            PlinkoConfig cfg,
            System.Func<int, int, PegView> pegLookup,
            System.Action onPegBounce)
        {
            float stepDuration = Mathf.Max(0.08f, cfg.guidedStepDuration);

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 fromPos = GetGuidedWaypointPos(path[i], cfg);
                PlinkoMath.GuidedStep to = path[i + 1];
                Vector2 toPos = GetGuidedWaypointPos(to, cfg);
                float arcHeight = i == 0 ? cfg.guidedArcHeight * 0.15f : cfg.guidedArcHeight;

                float elapsed = 0f;
                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / stepDuration);
                    float yEase = i == 0 ? t * t : Mathf.Lerp(t, t * t, 0.35f);

                    float x = Mathf.Lerp(fromPos.x, toPos.x, t);
                    float y = Mathf.Lerp(fromPos.y, toPos.y, yEase) + Mathf.Sin(t * Mathf.PI) * arcHeight;
                    transform.position = new Vector3(x, y, 0f);
                    yield return null;
                }

                transform.position = new Vector3(toPos.x, toPos.y, 0f);

                if (to.isPegHit)
                {
                    PegView peg = pegLookup?.Invoke(to.row, to.col);
                    if (peg != null)
                    {
                        peg.Pulse();
                    }

                    onPegBounce?.Invoke();
                }
            }

            _guidedCoroutine = null;
            _arrivalConsumed = true;
            _onLanded?.Invoke(this, path[path.Count - 1].col);
            StartDespawn(0.25f);
        }
    }
}
