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

        private int _selectedSlot = 3;
        private GameMode _currentMode = GameMode.ClassicRandom;
        private int _hits;
        private int _attempts;
        private int _streak;

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

            _selectedSlot = Mathf.Clamp(_selectedSlot, 0, config.slotCount - 1);

            if (board != null)
            {
                board.BindRuntimeCallbacks(config, SelectSlot, OnBallEnteredSlot, HandlePegBounce);
                board.HighlightSelectedSlot(_selectedSlot);
            }

            if (hud != null)
            {
                hud.BindCallbacks(
                    () => SetMode(GameMode.ClassicRandom),
                    () => SetMode(GameMode.GuidedTarget),
                    SelectSlot,
                    DropBall
                );
                hud.SetMode(_currentMode);
                hud.SetSelectedSlot(_selectedSlot);
                hud.UpdateStats(_hits, _attempts, _streak);
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
            if (_selectedSlot != clamped)
            {
                _selectedSlot = clamped;

                if (board != null)
                {
                    board.HighlightSelectedSlot(_selectedSlot);
                }

                if (hud != null)
                {
                    hud.SetSelectedSlot(_selectedSlot);
                }
            }

            if (sfx != null)
            {
                sfx.PlaySelect();
            }
        }

        public void SetMode(GameMode mode)
        {
            if (_isDropping || _currentMode == mode)
            {
                return;
            }

            _currentMode = mode;

            if (hud != null)
            {
                hud.SetMode(_currentMode);
            }

            if (sfx != null)
            {
                sfx.PlaySelect();
            }
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

            if (_currentMode == GameMode.ClassicRandom)
            {
                _activeBall.LaunchClassic(config, OnBallLanded);
            }
            else
            {
                List<PlinkoMath.GuidedStep> path = PlinkoMath.GenerateGuidedPath(_selectedSlot, config);
                System.Func<int, int, PegView> pegLookup = board != null ? board.GetPeg : null;
                _activeBall.LaunchGuided(path, config, pegLookup, HandlePegBounce, OnBallLanded);
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (config == null)
            {
                return;
            }

            int keyCount = Mathf.Min(config.slotCount, 9);
            for (int i = 0; i < keyCount; i++)
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
                SetMode(_currentMode == GameMode.ClassicRandom
                    ? GameMode.GuidedTarget
                    : GameMode.ClassicRandom);
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
            _attempts++;

            bool isWin = landedSlot == _selectedSlot;
            if (isWin)
            {
                _hits++;
                _streak++;
                if (sfx != null)
                {
                    sfx.PlayWin();
                }

                AddTrauma(0.38f);
            }
            else
            {
                _streak = 0;
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
                hud.UpdateStats(_hits, _attempts, _streak);
                hud.ShowResultBanner(isWin, landedSlot, _selectedSlot);
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
