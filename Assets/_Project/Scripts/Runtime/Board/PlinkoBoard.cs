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
        private Coroutine _dispenserPulseCoroutine;
        private Sprite _fallbackBarSprite;

        private void OnDestroy()
        {
            if (pegs != null)
            {
                for (int i = 0; i < pegs.Length; i++)
                {
                    if (pegs[i] != null)
                    {
                        pegs[i].OnPegHit -= HandlePegHit;
                    }
                }
            }

            if (_fallbackBarSprite != null)
            {
                if (_fallbackBarSprite.texture != null)
                {
                    Destroy(_fallbackBarSprite.texture);
                }

                Destroy(_fallbackBarSprite);
                _fallbackBarSprite = null;
            }
        }

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

                    var pegView = pegGo.GetComponent<PegView>();
                    if (pegView != null)
                    {
                        pegView.Initialize(row, col, cfg);
                        builtPegs.Add(pegView);
                    }
                }
            }

            pegs = builtPegs.ToArray();

            var builtSlots = new List<SlotView>(cfg.slotCount);
            for (int i = 0; i < cfg.slotCount; i++)
            {
                Vector2 pos = PlinkoMath.GetSlotPosition(i, cfg);
                GameObject slotGo = Instantiate(slotPrefab, pos, Quaternion.identity, slotsContainer);
                slotGo.name = $"Slot_{i + 1}";

                var slotView = slotGo.GetComponent<SlotView>();
                if (slotView != null)
                {
                    slotView.Initialize(i, cfg, null, null);
                    builtSlots.Add(slotView);
                }
            }

            slots = builtSlots.ToArray();

            BuildSpawnMarker(cfg, slotSprite);
            BuildContainmentWalls(cfg, slotSprite, pegMat);
        }

        public void BindRuntimeCallbacks(
            PlinkoConfig cfg,
            System.Action<int> onSlotSelected,
            System.Action<int, PlinkoBall> onBallEntered,
            System.Action onPegHit)
        {
            _onPegHitCallback = onPegHit;
            EnsureDispenser(cfg);

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
                    if (slot != null)
                    {
                        slot.Initialize(slot.SlotIndex, cfg, onSlotSelected, onBallEntered);
                    }
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
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
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

        public void PulseDispenser()
        {
            if (spawnMarker == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (_dispenserPulseCoroutine != null)
            {
                StopCoroutine(_dispenserPulseCoroutine);
            }

            _dispenserPulseCoroutine = StartCoroutine(DispenserPulseRoutine());
        }

        private System.Collections.IEnumerator DispenserPulseRoutine()
        {
            const float duration = 0.14f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float bump = Mathf.Sin(t * Mathf.PI) * 0.12f;
                spawnMarker.localScale = new Vector3(1f + bump, 1f - bump * 0.5f, 1f);
                yield return null;
            }

            spawnMarker.localScale = Vector3.one;
            _dispenserPulseCoroutine = null;
        }

        private void EnsureDispenser(PlinkoConfig cfg)
        {
            if (cfg == null)
            {
                return;
            }

            if (spawnMarker != null && spawnMarker.childCount >= 3)
            {
                return;
            }

            if (spawnMarker != null)
            {
                DestroyObjectSmart(spawnMarker.gameObject);
                spawnMarker = null;
            }

            BuildSpawnMarker(cfg, GetOrCreateFallbackBarSprite());
        }

        private Sprite GetOrCreateFallbackBarSprite()
        {
            if (_fallbackBarSprite == null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                _fallbackBarSprite = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            }

            return _fallbackBarSprite;
        }

        private void BuildSpawnMarker(PlinkoConfig cfg, Sprite barSprite)
        {
            if (barSprite == null)
            {
                barSprite = GetOrCreateFallbackBarSprite();
            }

            var dispenserGo = new GameObject("SpawnDispenser");
            dispenserGo.transform.SetParent(transform, false);
            Vector2 spawnPos = PlinkoMath.GetSpawnPosition(cfg);
            dispenserGo.transform.position = new Vector3(spawnPos.x, spawnPos.y + 0.18f, 0f);
            spawnMarker = dispenserGo.transform;

            float mouthHalfWidth = Mathf.Max(cfg.ballRadius + cfg.classicDropJitter + 0.08f, 0.42f);
            float topHalfWidth = mouthHalfWidth + 0.36f;

            Color bodyColor = cfg.slotNormalColor;
            Color rimColor = cfg.slotBorderColor;
            Color accentColor = cfg.ballColor;

            // Back plate inside the chute so balls appear to emerge from inside the funnel
            CreateDispenserPart("ChuteBack", dispenserGo.transform, barSprite,
                new Vector2(0f, 0.24f), new Vector2(mouthHalfWidth * 2.1f, 0.52f), 0f,
                new Color(bodyColor.r * 0.45f, bodyColor.g * 0.45f, bodyColor.b * 0.55f, 0.92f), 2);

            // Left & right angled funnel wings
            CreateDispenserPart("FunnelLeft", dispenserGo.transform, barSprite,
                new Vector2(-(mouthHalfWidth + topHalfWidth) * 0.5f, 0.36f), new Vector2(0.11f, 0.62f), 32f,
                rimColor, 8);
            CreateDispenserPart("FunnelRight", dispenserGo.transform, barSprite,
                new Vector2((mouthHalfWidth + topHalfWidth) * 0.5f, 0.36f), new Vector2(0.11f, 0.62f), -32f,
                rimColor, 8);

            // Nozzle collar & glowing exit rim
            CreateDispenserPart("NozzleCollar", dispenserGo.transform, barSprite,
                new Vector2(0f, 0.06f), new Vector2(mouthHalfWidth * 2.15f, 0.14f), 0f,
                bodyColor, 8);
            CreateDispenserPart("NozzleRim", dispenserGo.transform, barSprite,
                new Vector2(0f, -0.02f), new Vector2(mouthHalfWidth * 2.25f, 0.07f), 0f,
                accentColor, 9);
        }

        private static void CreateDispenserPart(
            string partName,
            Transform parent,
            Sprite sprite,
            Vector2 localPos,
            Vector2 worldSize,
            float angleDeg,
            Color color,
            int sortingOrder)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;

            Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            float sx = spriteSize.x > 0.0001f ? worldSize.x / spriteSize.x : worldSize.x;
            float sy = spriteSize.y > 0.0001f ? worldSize.y / spriteSize.y : worldSize.y;
            go.transform.localScale = new Vector3(sx, sy, 1f);
        }

        private void BuildContainmentWalls(PlinkoConfig cfg, Sprite wallSprite, PhysicsMaterial2D wallMat)
        {
            float topY = cfg.topPegY + cfg.spawnOffsetY + 0.5f;
            float slotY = PlinkoMath.GetSlotPosition(0, cfg).y;
            float bottomY = slotY - 0.65f;

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
            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            CreateBoxWall(wallName, mid, new Vector2(delta.magnitude, thickness), angleDeg, sprite, mat, color);
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
                color.a = 0.45f;
                sr.color = color;
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
