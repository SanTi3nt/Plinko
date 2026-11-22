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

        [Header("Pooling")]
        [SerializeField] private int maxActiveBalls = 35;

        private readonly Queue<PlinkoBall> _ballPool = new Queue<PlinkoBall>();
        private readonly HashSet<PlinkoBall> _activeBalls = new HashSet<PlinkoBall>();
        private Transform _ballsContainer;
        private PlinkoBall _templateBall;

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
            QualitySettings.vSyncCount = 1;

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
            if (targetCamera != null && config != null)
            {
                targetCamera.backgroundColor = config.backgroundColor;
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

            InitializeBallPool();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        public void SelectSlot(int index)
        {
            if (config == null)
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
            if (_currentMode == mode)
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
            if (config == null || _activeBalls.Count >= maxActiveBalls)
            {
                return;
            }

            PlinkoBall ball = AcquireBall();
            if (ball == null)
            {
                return;
            }

            _activeBalls.Add(ball);

            if (board != null)
            {
                board.PulseDispenser();
            }

            int targetSlotAtDrop = _selectedSlot;

            if (_currentMode == GameMode.ClassicRandom)
            {
                ball.LaunchClassic(config, targetSlotAtDrop, OnBallLanded, ReturnBallToPool);
            }
            else
            {
                List<PlinkoMath.GuidedStep> path = PlinkoMath.GenerateGuidedPath(targetSlotAtDrop, config);
                System.Func<int, int, PegView> pegLookup = board != null ? board.GetPeg : null;
                ball.LaunchGuided(path, config, targetSlotAtDrop, pegLookup, HandlePegBounce, OnBallLanded, ReturnBallToPool);
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
        }

        private void OnBallEnteredSlot(int slotIndex, PlinkoBall ball)
        {
            if (ball != null)
            {
                ball.NotifyLandedExternally();
            }

            OnBallLanded(ball, slotIndex);
        }

        private void OnBallLanded(PlinkoBall ball, int landedSlot)
        {
            int targetSlot = ball != null ? ball.TargetSlot : _selectedSlot;
            _attempts++;

            bool isWin = landedSlot == targetSlot;
            if (isWin)
            {
                _hits++;
                _streak++;
                if (sfx != null)
                {
                    sfx.PlayWin();
                }
            }
            else
            {
                _streak = 0;
                if (sfx != null)
                {
                    sfx.PlayMiss();
                }
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
                hud.ShowResultBanner(isWin, landedSlot, targetSlot);
            }
        }

        private void InitializeBallPool()
        {
            if (_ballsContainer == null)
            {
                var containerGo = new GameObject("BallsPool");
                _ballsContainer = containerGo.transform;
            }

            PlinkoBall sceneBall = FindAnyObjectByType<PlinkoBall>();
            if (sceneBall != null)
            {
                _templateBall = sceneBall;
                sceneBall.transform.SetParent(_ballsContainer, true);
                sceneBall.gameObject.SetActive(false);
                _ballPool.Enqueue(sceneBall);
            }
        }

        private PlinkoBall AcquireBall()
        {
            while (_ballPool.Count > 0)
            {
                PlinkoBall pooled = _ballPool.Dequeue();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            PlinkoBall source = ballPrefab != null ? ballPrefab : _templateBall;
            if (source == null)
            {
                return null;
            }

            PlinkoBall created = Instantiate(source, _ballsContainer);
            created.name = $"PlinkoBall_{_activeBalls.Count + _ballPool.Count + 1}";
            return created;
        }

        private void ReturnBallToPool(PlinkoBall ball)
        {
            if (ball == null)
            {
                return;
            }

            _activeBalls.Remove(ball);
            ball.gameObject.SetActive(false);
            _ballPool.Enqueue(ball);
        }
    }
}
