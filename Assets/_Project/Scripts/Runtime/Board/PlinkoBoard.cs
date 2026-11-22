using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Plinko
{
    public class PlinkoBoard : MonoBehaviour
    {
        [SerializeField] private PegView[] pegs = System.Array.Empty<PegView>();
        [SerializeField] private SlotView[] slots = System.Array.Empty<SlotView>();
        [SerializeField] private Transform spawnMarker;
        [SerializeField] private Transform pegsContainer;
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private Transform wallsContainer;

        private System.Action _onPegHitCallback;

        public int PegCount => pegs != null ? pegs.Length : 0;
        public int SlotCount => slots != null ? slots.Length : 0;

        public void ClearBoard()
        {
            DestroyChildren(pegsContainer);
            DestroyChildren(slotsContainer);
            DestroyChildren(wallsContainer);

            if (spawnMarker != null)
            {
                DestroyObjectSmart(spawnMarker.gameObject);
                spawnMarker = null;
            }

            pegs = System.Array.Empty<PegView>();
            slots = System.Array.Empty<SlotView>();
        }

        public void BuildBoard(
            PlinkoConfig cfg,
            Sprite circleSprite,
            Sprite glowSprite,
            Sprite slotSprite,
            PhysicsMaterial2D pegMat,
            GameObject pegPrefab,
            GameObject slotPrefab)
        {
            ClearBoard();

            pegsContainer = EnsureContainer("Pegs", pegsContainer);
            slotsContainer = EnsureContainer("Slots", slotsContainer);
            wallsContainer = EnsureContainer("Walls", wallsContainer);

            var builtPegs = new List<PegView>();
            for (int row = 0; row < cfg.pegRows; row++)
            {
                int countInRow = PlinkoMath.GetPegCountInRow(row, cfg);
                for (int col = 0; col < countInRow; col++)
                {
                    Vector2 pos = PlinkoMath.GetPegPosition(row, col, cfg);
                    GameObject pegGo = Instantiate(pegPrefab, pos, Quaternion.identity, pegsContainer);
                    pegGo.name = $"Peg_R{row}_C{col}";

                    var circleCol = pegGo.GetComponent<CircleCollider2D>();
                    if (circleCol != null && pegMat != null)
                    {
                        circleCol.sharedMaterial = pegMat;
                    }

                    var sr = pegGo.GetComponent<SpriteRenderer>();
                    if (sr != null && circleSprite != null && sr.sprite == null)
                    {
                        sr.sprite = circleSprite;
                    }

                    var pegView = pegGo.GetComponent<PegView>();
                    if (pegView != null)
                    {
                        pegView.Initialize(row, col, cfg);
                        builtPegs.Add(pegView);
                    }
                }
            }

            pegs = builtPegs.ToArray();

            var builtSlots = new List<SlotView>();
            for (int i = 0; i < cfg.slotCount; i++)
            {
                Vector2 pos = PlinkoMath.GetSlotPosition(i, cfg);
                GameObject slotGo = Instantiate(slotPrefab, pos, Quaternion.identity, slotsContainer);
                slotGo.name = $"Slot_{i + 1}";

                var sr = slotGo.GetComponent<SpriteRenderer>();
                if (sr != null && slotSprite != null && sr.sprite == null)
                {
                    sr.sprite = slotSprite;
                }

                var slotView = slotGo.GetComponent<SlotView>();
                if (slotView != null)
                {
                    slotView.Initialize(i, cfg, null, null);
                    builtSlots.Add(slotView);
                }
            }

            slots = builtSlots.ToArray();

            BuildSpawnMarker(cfg, circleSprite);
            BuildContainmentWalls(cfg, slotSprite, pegMat);
        }

        public void BindRuntimeCallbacks(
            PlinkoConfig cfg,
            System.Action<int> onSlotSelected,
            System.Action<int> onBallEntered,
            System.Action onPegHit)
        {
            _onPegHitCallback = onPegHit;

            if (pegs != null)
            {
                for (int i = 0; i < pegs.Length; i++)
                {
                    PegView peg = pegs[i];
                    if (peg == null)
                    {
                        continue;
                    }

                    peg.OnPegHit -= HandlePegHit;
                    peg.OnPegHit += HandlePegHit;
                }
            }

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    SlotView slot = slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    slot.Initialize(slot.SlotIndex, cfg, onSlotSelected, onBallEntered);
                }
            }
        }

        public PegView GetPeg(int row, int col)
        {
            if (pegs == null)
            {
                return null;
            }

            for (int i = 0; i < pegs.Length; i++)
            {
                PegView peg = pegs[i];
                if (peg != null && peg.Row == row && peg.Col == col)
                {
                    return peg;
                }
            }

            return null;
        }

        public SlotView GetSlot(int index)
        {
            if (slots == null || index < 0 || index >= slots.Length)
            {
                return null;
            }

            return slots[index];
        }

        public void HighlightSelectedSlot(int index)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].SetSelected(slots[i].SlotIndex == index);
                }
            }
        }

        private void HandlePegHit(PegView peg)
        {
            _onPegHitCallback?.Invoke();
        }

        private void BuildSpawnMarker(PlinkoConfig cfg, Sprite circleSprite)
        {
            var markerGo = new GameObject("SpawnMarker");
            markerGo.transform.SetParent(transform, false);
            markerGo.transform.position = PlinkoMath.GetSpawnPosition(cfg);

            var sr = markerGo.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            Color c = cfg.ballColor;
            c.a = 0.35f;
            sr.color = c;
            sr.sortingOrder = 1;
            markerGo.transform.localScale = Vector3.one * (cfg.ballRadius * 1.45f);
            spawnMarker = markerGo.transform;
        }

        private void BuildContainmentWalls(PlinkoConfig cfg, Sprite wallSprite, PhysicsMaterial2D wallMat)
        {
            float topY = cfg.topPegY + cfg.spawnOffsetY + 0.5f;
            float bottomY = PlinkoMath.GetSlotPosition(0, cfg).y - 0.65f;

            float topHalfWidth = (cfg.topRowPegs - 1) * 0.5f * cfg.horizontalSpacing + 1.25f;
            float bottomHalfWidth = (cfg.slotCount - 1) * 0.5f * cfg.horizontalSpacing + 0.95f;

            CreateAngledWall(
                "LeftFunnelWall",
                new Vector2(-topHalfWidth, topY),
                new Vector2(-bottomHalfWidth, bottomY),
                0.12f,
                wallSprite,
                wallMat,
                cfg.slotBorderColor
            );

            CreateAngledWall(
                "RightFunnelWall",
                new Vector2(topHalfWidth, topY),
                new Vector2(bottomHalfWidth, bottomY),
                0.12f,
                wallSprite,
                wallMat,
                cfg.slotBorderColor
            );

            float slotY = PlinkoMath.GetSlotPosition(0, cfg).y;
            float floorWidth = bottomHalfWidth * 2f + 0.25f;
            CreateBoxWall(
                "BottomFloor",
                new Vector2(0f, slotY - 0.52f),
                new Vector2(floorWidth, 0.14f),
                0f,
                wallSprite,
                wallMat,
                cfg.slotBorderColor
            );

            // Thin dividers between slots so dynamic Classic balls settle cleanly into one bucket
            for (int i = 0; i < cfg.slotCount - 1; i++)
            {
                float x = (i - (cfg.slotCount - 1) * 0.5f + 0.5f) * cfg.horizontalSpacing;
                CreateBoxWall(
                    $"SlotDivider_{i}",
                    new Vector2(x, slotY - 0.05f),
                    new Vector2(0.08f, 0.85f),
                    0f,
                    wallSprite,
                    wallMat,
                    cfg.slotBorderColor
                );
            }
        }

        private void CreateAngledWall(
            string wallName,
            Vector2 start,
            Vector2 end,
            float thickness,
            Sprite sprite,
            PhysicsMaterial2D mat,
            Color color)
        {
            Vector2 mid = (start + end) * 0.5f;
            Vector2 delta = end - start;
            float length = delta.magnitude;
            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            CreateBoxWall(wallName, mid, new Vector2(length, thickness), angleDeg, sprite, mat, color);
        }

        private void CreateBoxWall(
            string wallName,
            Vector2 position,
            Vector2 size,
            float angleDeg,
            Sprite sprite,
            PhysicsMaterial2D mat,
            Color color)
        {
            var go = new GameObject(wallName);
            go.transform.SetParent(wallsContainer, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            if (mat != null)
            {
                col.sharedMaterial = mat;
            }

            if (sprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                Color railColor = color;
                railColor.a = 0.45f;
                sr.color = railColor;
                sr.sortingOrder = 0;
                Vector2 spriteSize = sprite.bounds.size;
                if (spriteSize.x > 0.001f && spriteSize.y > 0.001f)
                {
                    go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
                    col.size = spriteSize;
                }
            }
        }

        private Transform EnsureContainer(string containerName, Transform existing)
        {
            if (existing != null)
            {
                return existing;
            }

            Transform found = transform.Find(containerName);
            if (found != null)
            {
                return found;
            }

            var go = new GameObject(containerName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyObjectSmart(parent.GetChild(i).gameObject);
            }
        }

        private static void DestroyObjectSmart(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
    }
}
