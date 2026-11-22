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
        [SerializeField] private DottedPathRenderer pathRenderer;

        private PlinkoConfig _config;
        private System.Action<int> _onLanded;
        private System.Action _onPegBounce;
        private Coroutine _guidedCoroutine;
        private bool _isClassicActive;
        private bool _arrivalConsumed;
        private float _stuckTimer;
        private float _slotY;

        public bool IsDropping => (_isClassicActive || _guidedCoroutine != null) && !_arrivalConsumed;

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

            if (pathRenderer == null)
            {
                pathRenderer = GetComponent<DottedPathRenderer>();
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
                spriteRadius = bodyRenderer.sprite.bounds.extents.x * SpriteFactory.CircleFillFraction;
                if (spriteRadius <= 0.001f)
                {
                    spriteRadius = 0.58f;
                }
            }

            float uniformScale = cfg.ballRadius / spriteRadius;
            transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

            if (circleCollider != null)
            {
                circleCollider.radius = spriteRadius;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = cfg.ballColor;
            }
        }

        public bool TryConsumeArrival()
        {
            if (_arrivalConsumed)
            {
                return false;
            }

            _arrivalConsumed = true;
            _isClassicActive = false;
            return true;
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

        public void ClearTrail()
        {
            if (pathRenderer != null)
            {
                pathRenderer.Clear();
            }
        }

        public void LaunchClassic(PlinkoConfig cfg, System.Action<int> onLanded, System.Action onPegBounce)
        {
            StopActiveDrop();
            ApplyConfig(cfg);

            _onLanded = onLanded;
            _onPegBounce = onPegBounce;
            _arrivalConsumed = false;
            _isClassicActive = true;
            _stuckTimer = 0f;

            Vector2 spawn = PlinkoMath.GetSpawnPosition(cfg);
            float xOffset = Random.Range(-cfg.classicDropJitter, cfg.classicDropJitter);
            Vector2 startPos = new Vector2(spawn.x + xOffset, spawn.y);

            transform.position = new Vector3(startPos.x, startPos.y, 0f);
            gameObject.SetActive(true);

            rb.bodyType = RigidbodyType2D.Dynamic;
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

            if (pathRenderer != null)
            {
                pathRenderer.BeginPath(startPos, cfg.trailDotColor);
            }
        }

        public void LaunchGuided(
            List<PlinkoMath.GuidedStep> path,
            PlinkoConfig cfg,
            System.Func<int, int, PegView> pegLookup,
            System.Action onPegBounce,
            System.Action<int> onLanded)
        {
            StopActiveDrop();
            ApplyConfig(cfg);

            _onLanded = onLanded;
            _onPegBounce = onPegBounce;
            _arrivalConsumed = false;
            _isClassicActive = false;

            if (path == null || path.Count < 2)
            {
                return;
            }

            Vector2 startPos = path[0].position;
            transform.position = new Vector3(startPos.x, startPos.y, 0f);
            gameObject.SetActive(true);

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (pathRenderer != null)
            {
                pathRenderer.BeginPath(startPos, cfg.trailDotColor);
            }

            _guidedCoroutine = StartCoroutine(GuidedRoutine(path, cfg, pegLookup));
        }

        public void NotifyLandedExternally()
        {
            _isClassicActive = false;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity *= 0.25f;
                rb.linearDamping = 4f;
            }
        }

        private void StopActiveDrop()
        {
            if (_guidedCoroutine != null)
            {
                StopCoroutine(_guidedCoroutine);
                _guidedCoroutine = null;
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
            if (pathRenderer != null)
            {
                pathRenderer.RecordPosition(pos);
            }

            if (pos.y < _slotY - 2f)
            {
                if (TryConsumeArrival())
                {
                    float rawIndex = pos.x / _config.horizontalSpacing + (_config.slotCount - 1) * 0.5f;
                    int nearestSlot = Mathf.Clamp(Mathf.RoundToInt(rawIndex), 0, _config.slotCount - 1);
                    _onLanded?.Invoke(nearestSlot);
                }

                return;
            }

            if (pos.y > _slotY + 0.35f && rb.linearVelocity.magnitude < 0.08f)
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

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_isClassicActive || _arrivalConsumed || collision.collider == null)
            {
                return;
            }

            if (collision.collider.GetComponent<PegView>() != null)
            {
                _onPegBounce?.Invoke();
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
            System.Func<int, int, PegView> pegLookup)
        {
            float stepDuration = Mathf.Max(0.08f, cfg.guidedStepDuration);

            for (int i = 0; i < path.Count - 1; i++)
            {
                PlinkoMath.GuidedStep from = path[i];
                PlinkoMath.GuidedStep to = path[i + 1];
                Vector2 fromPos = GetGuidedWaypointPos(from, cfg);
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
                    Vector2 currentPos = new Vector2(x, y);

                    transform.position = new Vector3(currentPos.x, currentPos.y, 0f);
                    if (pathRenderer != null)
                    {
                        pathRenderer.RecordPosition(currentPos);
                    }

                    yield return null;
                }

                transform.position = new Vector3(toPos.x, toPos.y, 0f);
                if (pathRenderer != null)
                {
                    pathRenderer.RecordPosition(toPos);
                }

                if (to.isPegHit)
                {
                    PegView peg = pegLookup?.Invoke(to.row, to.col);
                    if (peg != null)
                    {
                        peg.Pulse();
                    }

                    _onPegBounce?.Invoke();
                }
            }

            _guidedCoroutine = null;

            if (TryConsumeArrival())
            {
                int landedSlot = path[path.Count - 1].col;
                _onLanded?.Invoke(landedSlot);
            }
        }
    }
}
