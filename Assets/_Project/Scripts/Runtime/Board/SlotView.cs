using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FunFlow.Plinko
{
    public class SlotView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;
        [SerializeField] private SpriteRenderer glowRenderer;
        [SerializeField] private Text labelText;

        [SerializeField] private int slotIndex;
        [SerializeField] private Color normalColor = new Color(0x5B / 255f, 0x52 / 255f, 0xA8 / 255f, 1f);
        [SerializeField] private Color borderColor = new Color(0x7C / 255f, 0x72 / 255f, 0xCC / 255f, 1f);
        [SerializeField] private Color selectedColor = new Color(0xF5 / 255f, 0xA6 / 255f, 0x23 / 255f, 1f);

        private System.Action<int> _onSelected;
        private System.Action<int> _onBallEntered;
        private bool _isSelected;
        private Vector3 _baseScale = Vector3.one;
        private Coroutine _flashCoroutine;

        public int SlotIndex => slotIndex;

        private void Awake()
        {
            if (fillRenderer == null)
            {
                fillRenderer = GetComponent<SpriteRenderer>();
            }

            _baseScale = transform.localScale;
        }

        public void Initialize(int index, PlinkoConfig cfg, System.Action<int> onSelected, System.Action<int> onBallEntered)
        {
            slotIndex = index;
            _onSelected = onSelected;
            _onBallEntered = onBallEntered;

            normalColor = cfg.slotNormalColor;
            borderColor = cfg.slotBorderColor;
            selectedColor = cfg.slotSelectedColor;

            if (fillRenderer == null)
            {
                fillRenderer = GetComponent<SpriteRenderer>();
            }

            _baseScale = transform.localScale;

            if (labelText != null)
            {
                labelText.text = (index + 1).ToString();
                labelText.raycastTarget = false;
            }

            ApplyVisualState();
        }

        public void BindCallbacks(System.Action<int> onSelected, System.Action<int> onBallEntered)
        {
            _onSelected = onSelected;
            _onBallEntered = onBallEntered;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_flashCoroutine == null)
            {
                ApplyVisualState();
            }
        }

        public void PlayResultFlash(bool isWin)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
            }

            _flashCoroutine = StartCoroutine(ResultFlashRoutine(isWin));
        }

        private IEnumerator ResultFlashRoutine(bool isWin)
        {
            Color flashColor = isWin
                ? new Color(1f, 0.85f, 0.28f, 1f)
                : new Color(0.92f, 0.36f, 0.44f, 1f);

            const float duration = 0.45f;
            float elapsed = 0f;
            Color targetColor = _isSelected ? selectedColor : normalColor;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                float scalePop = Mathf.Lerp(1.16f, 1f, easeOut);
                transform.localScale = _baseScale * scalePop;

                if (fillRenderer != null)
                {
                    fillRenderer.color = Color.Lerp(flashColor, targetColor, easeOut);
                }

                if (glowRenderer != null)
                {
                    Color g = flashColor;
                    g.a = Mathf.Lerp(0.85f, _isSelected ? 0.35f : 0f, easeOut);
                    glowRenderer.color = g;
                }

                yield return null;
            }

            transform.localScale = _baseScale;
            _flashCoroutine = null;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (fillRenderer != null)
            {
                fillRenderer.color = _isSelected ? selectedColor : normalColor;
            }

            if (borderRenderer != null)
            {
                borderRenderer.color = _isSelected
                    ? new Color(1f, 0.88f, 0.55f, 1f)
                    : borderColor;
            }

            if (glowRenderer != null)
            {
                Color g = selectedColor;
                g.a = _isSelected ? 0.35f : 0f;
                glowRenderer.color = g;
            }
        }

        private void OnMouseDown()
        {
            _onSelected?.Invoke(slotIndex);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            var ball = other.GetComponent<PlinkoBall>();
            if (ball != null && ball.TryConsumeClassicArrival())
            {
                _onBallEntered?.Invoke(slotIndex);
            }
        }
    }
}
