using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Plinko
{
    public class PlinkoGameController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private PlinkoConfig config;
        [SerializeField] private PlinkoBoard board;
        [SerializeField] private PlinkoBall ballPrefab;
        [SerializeField] private PlinkoHudController hud;
        [SerializeField] private ProceduralSfx sfx;
        [SerializeField] private Camera targetCamera;

        [Header("Game Feel")]
        [SerializeField] private float shakeDecay = 1.8f;
        [SerializeField] private Vector2 maxShakeOffset = new Vector2(0.14f, 0.10f);

        private PlinkoBall _activeBall;
        private Vector3 _cameraRestPos;
        private float _trauma;
        private float _shakeTime;
        private bool _isDropping;

        public int SelectedSlot { get; private set; } = 3;
        public GameMode CurrentMode { get; private set; } = GameMode.ClassicRandom;
        public int Hits { get; private set; }
        public int Attempts { get; private set; }
        public int Streak { get; private set; }

        public void SetReferences(
            PlinkoConfig cfg,
            PlinkoBoard plinkoBoard,
            PlinkoBall prefab,
            PlinkoHudController hudController,
            ProceduralSfx proceduralSfx,
            Camera cam)
        {
            config = cfg;
            board = plinkoBoard;
            ballPrefab = prefab;
            hud = hudController;
            sfx = proceduralSfx;
            targetCamera = cam;
        }

        private void Awake()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<PlinkoBoard>();
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<PlinkoHudController>();
            }

            if (sfx == null)
            {
                sfx = FindAnyObjectByType<ProceduralSfx>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (targetCamera != null)
            {
                _cameraRestPos = targetCamera.transform.position;
                if (config != null)
                {
                    targetCamera.backgroundColor = config.backgroundColor;
                }
            }

            if (config == null)
            {
                Debug.LogError("PlinkoGameController requires a PlinkoConfig reference.");
                return;
            }

            SelectedSlot = Mathf.Clamp(SelectedSlot, 0, config.slotCount - 1);

            if (board != null)
            {
                board.BindRuntimeCallbacks(config, SelectSlot, OnBallEnteredSlot, HandlePegBounce);
                board.HighlightSelectedSlot(SelectedSlot);
            }

            if (hud != null)
            {
                hud.BindCallbacks(
                    () => SetMode(GameMode.ClassicRandom),
                    () => SetMode(GameMode.GuidedTarget),
                    SelectSlot,
                    DropBall
                );
                hud.SetMode(CurrentMode);
                hud.SetSelectedSlot(SelectedSlot);
                hud.UpdateStats(Hits, Attempts, Streak);
                hud.SetDropInteractable(true);
                hud.ClearResultBanner();
            }

            EnsureBallInstance();
            ParkBallAtSpawn();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        private void LateUpdate()
        {
            UpdateCameraShake();
        }

        public void SelectSlot(int index)
        {
            if (config == null || _isDropping)
            {
                return;
            }

            int clamped = Mathf.Clamp(index, 0, config.slotCount - 1);
            if (SelectedSlot == clamped)
            {
                if (sfx != null)
                {
                    sfx.PlaySelect();
                }

                return;
            }

            SelectedSlot = clamped;

            if (board != null)
            {
                board.HighlightSelectedSlot(SelectedSlot);
            }

            if (hud != null)
            {
                hud.SetSelectedSlot(SelectedSlot);
            }

            if (sfx != null)
            {
                sfx.PlaySelect();
            }
        }

        public void SetMode(GameMode mode)
        {
            if (_isDropping || CurrentMode == mode)
            {
                return;
            }

            CurrentMode = mode;

            if (hud != null)
            {
                hud.SetMode(CurrentMode);
            }

            if (sfx != null)
            {
                sfx.PlaySelect();
            }
        }

        public void ToggleMode()
        {
            SetMode(CurrentMode == GameMode.ClassicRandom
                ? GameMode.GuidedTarget
                : GameMode.ClassicRandom);
        }

        public void DropBall()
        {
            if (_isDropping || config == null)
            {
                return;
            }

            EnsureBallInstance();
            if (_activeBall == null)
            {
                return;
            }

            _isDropping = true;
            if (hud != null)
            {
                hud.SetDropInteractable(false);
                hud.ClearResultBanner();
            }

            if (CurrentMode == GameMode.ClassicRandom)
            {
                _activeBall.LaunchClassic(config, OnBallLanded, null);
            }
            else
            {
                List<PlinkoMath.GuidedStep> path = PlinkoMath.GenerateGuidedPath(SelectedSlot, config);
                System.Func<int, int, PegView> pegLookup = board != null ? board.GetPeg : null;
                _activeBall.LaunchGuided(path, config, pegLookup, HandlePegBounce, OnBallLanded);
            }
        }

        private void HandleKeyboardShortcuts()
        {
            for (int i = 0; i < 7; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectSlot(i);
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                DropBall();
                return;
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                ToggleMode();
            }
        }

        private void HandlePegBounce()
        {
            if (sfx != null)
            {
                sfx.PlayPegTick();
            }

            AddTrauma(0.12f);
        }

        private void OnBallEnteredSlot(int slotIndex)
        {
            if (_activeBall != null)
            {
                _activeBall.NotifyLandedExternally();
            }

            OnBallLanded(slotIndex);
        }

        private void OnBallLanded(int landedSlot)
        {
            _isDropping = false;
            Attempts++;

            bool isWin = landedSlot == SelectedSlot;
            if (isWin)
            {
                Hits++;
                Streak++;
                if (sfx != null)
                {
                    sfx.PlayWin();
                }

                AddTrauma(0.38f);
            }
            else
            {
                Streak = 0;
                if (sfx != null)
                {
                    sfx.PlayMiss();
                }

                AddTrauma(0.16f);
            }

            if (board != null)
            {
                SlotView slotView = board.GetSlot(landedSlot);
                if (slotView != null)
                {
                    slotView.PlayResultFlash(isWin);
                }
            }

            if (hud != null)
            {
                hud.UpdateStats(Hits, Attempts, Streak);
                hud.ShowResultBanner(isWin, landedSlot, SelectedSlot);
                hud.SetDropInteractable(true);
            }
        }

        private void EnsureBallInstance()
        {
            if (_activeBall != null)
            {
                return;
            }

            _activeBall = FindAnyObjectByType<PlinkoBall>();
            if (_activeBall == null && ballPrefab != null)
            {
                _activeBall = Instantiate(ballPrefab);
                _activeBall.name = "PlinkoBall";
            }
        }

        private void ParkBallAtSpawn()
        {
            if (_activeBall == null || config == null)
            {
                return;
            }

            _activeBall.ApplyConfig(config);
            _activeBall.ClearTrail();

            var rb = _activeBall.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            Vector2 spawn = PlinkoMath.GetSpawnPosition(config);
            _activeBall.transform.position = new Vector3(spawn.x, spawn.y, 0f);
        }

        private void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        private void UpdateCameraShake()
        {
            if (targetCamera == null || _trauma <= 0f)
            {
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - shakeDecay * Time.unscaledDeltaTime);
            float shake = _trauma * _trauma;
            _shakeTime += Time.unscaledDeltaTime * 30f;

            float offsetX = maxShakeOffset.x * shake * Mathf.Sin(_shakeTime * 1.7f);
            float offsetY = maxShakeOffset.y * shake * Mathf.Sin(_shakeTime * 2.3f);
            targetCamera.transform.position = _cameraRestPos + new Vector3(offsetX, offsetY, 0f);

            if (_trauma <= 0f)
            {
                targetCamera.transform.position = _cameraRestPos;
            }
        }
    }
}
