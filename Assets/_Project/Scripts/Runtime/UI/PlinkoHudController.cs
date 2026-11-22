using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FunFlow.Plinko
{
    public class PlinkoHudController : MonoBehaviour
    {
        private static readonly Color ActiveGold = new Color(0xF5 / 255f, 0xA6 / 255f, 0x23 / 255f, 1f);
        private static readonly Color InactivePurple = new Color(0x47 / 255f, 0x3E / 255f, 0x91 / 255f, 1f);
        private static readonly Color SlotButtonNormal = new Color(0x5B / 255f, 0x52 / 255f, 0xA8 / 255f, 1f);
        private static readonly Color WinBannerColor = new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f);
        private static readonly Color MissBannerColor = new Color(0xFF / 255f, 0x7B / 255f, 0x89 / 255f, 1f);

        [Header("Mode Controls")]
        [SerializeField] private Button classicModeButton;
        [SerializeField] private Image classicModeBackground;
        [SerializeField] private Button guidedModeButton;
        [SerializeField] private Image guidedModeBackground;

        [Header("Slot Quick Buttons (1..7)")]
        [SerializeField] private Button[] slotButtons = System.Array.Empty<Button>();
        [SerializeField] private Image[] slotButtonBackgrounds = System.Array.Empty<Image>();

        [Header("Action & Status")]
        [SerializeField] private Button dropButton;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text attemptsText;
        [SerializeField] private Text streakText;
        [SerializeField] private Text modeDescriptionText;
        [SerializeField] private Text resultBannerText;

        private Coroutine _bannerCoroutine;
        private Vector3 _bannerBaseScale = Vector3.one;

        public static Font GetBuiltinFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void SetReferences(
            Button classicBtn,
            Image classicBg,
            Button guidedBtn,
            Image guidedBg,
            Button[] slotBtns,
            Image[] slotBgs,
            Button dropBtn,
            Text scoreTxt,
            Text attemptsTxt,
            Text streakTxt,
            Text modeDescTxt,
            Text resultTxt)
        {
            classicModeButton = classicBtn;
            classicModeBackground = classicBg;
            guidedModeButton = guidedBtn;
            guidedModeBackground = guidedBg;
            slotButtons = slotBtns;
            slotButtonBackgrounds = slotBgs;
            dropButton = dropBtn;
            scoreText = scoreTxt;
            attemptsText = attemptsTxt;
            streakText = streakTxt;
            modeDescriptionText = modeDescTxt;
            resultBannerText = resultTxt;
        }

        private void Awake()
        {
            if (resultBannerText != null)
            {
                _bannerBaseScale = resultBannerText.transform.localScale;
            }
        }

        public void BindCallbacks(
            System.Action onClassicSelected,
            System.Action onGuidedSelected,
            System.Action<int> onSlotSelected,
            System.Action onDropClicked)
        {
            if (classicModeButton != null)
            {
                classicModeButton.onClick.RemoveAllListeners();
                classicModeButton.onClick.AddListener(() => onClassicSelected?.Invoke());
            }

            if (guidedModeButton != null)
            {
                guidedModeButton.onClick.RemoveAllListeners();
                guidedModeButton.onClick.AddListener(() => onGuidedSelected?.Invoke());
            }

            if (slotButtons != null)
            {
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int index = i;
                    Button btn = slotButtons[i];
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => onSlotSelected?.Invoke(index));
                    }
                }
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveAllListeners();
                dropButton.onClick.AddListener(() => onDropClicked?.Invoke());
            }
        }

        public void SetMode(GameMode mode)
        {
            bool isClassic = mode == GameMode.ClassicRandom;

            if (classicModeBackground != null)
            {
                classicModeBackground.color = isClassic ? ActiveGold : InactivePurple;
            }

            if (guidedModeBackground != null)
            {
                guidedModeBackground.color = isClassic ? InactivePurple : ActiveGold;
            }

            if (modeDescriptionText != null)
            {
                modeDescriptionText.text = isClassic
                    ? "Режим: Классика (физика 2D) — угадай ячейку [1-7], [Space] бросить, [M] режим"
                    : "Режим: Довести — случайный путь по колышкам в выбранную ячейку [1-7], [Space] бросить";
            }
        }

        public void SetSelectedSlot(int slotIndex)
        {
            if (slotButtonBackgrounds == null)
            {
                return;
            }

            for (int i = 0; i < slotButtonBackgrounds.Length; i++)
            {
                if (slotButtonBackgrounds[i] != null)
                {
                    slotButtonBackgrounds[i].color = i == slotIndex ? ActiveGold : SlotButtonNormal;
                }
            }
        }

        public void UpdateStats(int hits, int attempts, int streak)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Попадания: {hits}";
            }

            if (attemptsText != null)
            {
                attemptsText.text = $"Попытки: {attempts}";
            }

            if (streakText != null)
            {
                streakText.text = $"Серия: {streak}";
            }
        }

        public void SetDropInteractable(bool interactable)
        {
            if (dropButton != null)
            {
                dropButton.interactable = interactable;
            }
        }

        public void ClearResultBanner()
        {
            if (_bannerCoroutine != null)
            {
                StopCoroutine(_bannerCoroutine);
                _bannerCoroutine = null;
            }

            if (resultBannerText != null)
            {
                resultBannerText.text = string.Empty;
                resultBannerText.transform.localScale = _bannerBaseScale;
            }
        }

        public void ShowResultBanner(bool isWin, int landedSlot, int targetSlot)
        {
            if (resultBannerText == null)
            {
                return;
            }

            resultBannerText.text = isWin
                ? $"ПОПАДАНИЕ! Ячейка #{landedSlot + 1}"
                : $"Мимо: выпала #{landedSlot + 1} (цель #{targetSlot + 1})";

            Color baseColor = isWin ? WinBannerColor : MissBannerColor;
            resultBannerText.color = baseColor;

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_bannerCoroutine != null)
            {
                StopCoroutine(_bannerCoroutine);
            }

            _bannerCoroutine = StartCoroutine(PopBannerRoutine(baseColor));
        }

        private IEnumerator PopBannerRoutine(Color baseColor)
        {
            const float popDuration = 0.24f;
            float elapsed = 0f;

            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.24f;
                resultBannerText.transform.localScale = _bannerBaseScale * overshoot;

                Color c = baseColor;
                c.a = Mathf.Lerp(0.35f, 1f, t);
                resultBannerText.color = c;
                yield return null;
            }

            resultBannerText.transform.localScale = _bannerBaseScale;
            resultBannerText.color = baseColor;
            _bannerCoroutine = null;
        }
    }
}
