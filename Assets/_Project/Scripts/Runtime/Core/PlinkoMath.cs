using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Plinko
{
    public static class PlinkoMath
    {
        public struct GuidedStep
        {
            public Vector2 position;
            public bool isPegHit;
            public int row;
            public int col;
        }

        public static int GetPegCountInRow(int row, PlinkoConfig cfg)
        {
            return cfg.topRowPegs + row;
        }

        public static Vector2 GetSpawnPosition(PlinkoConfig cfg)
        {
            return new Vector2(0f, cfg.topPegY + cfg.spawnOffsetY);
        }

        public static Vector2 GetPegPosition(int row, int col, PlinkoConfig cfg)
        {
            float x = (col - (cfg.topRowPegs + row - 1) * 0.5f) * cfg.horizontalSpacing;
            float y = cfg.topPegY - row * cfg.verticalSpacing;
            return new Vector2(x, y);
        }

        public static Vector2 GetSlotPosition(int slotIndex, PlinkoConfig cfg)
        {
            float x = (slotIndex - (cfg.slotCount - 1) * 0.5f) * cfg.horizontalSpacing;
            float y = cfg.topPegY - (cfg.pegRows - 1) * cfg.verticalSpacing - cfg.slotOffsetY;
            return new Vector2(x, y);
        }

        public static List<GuidedStep> GenerateGuidedPath(int targetSlot, PlinkoConfig cfg, System.Random rng = null)
        {
            int clampedSlot = Mathf.Clamp(targetSlot, 0, cfg.slotCount - 1);
            int steps = cfg.pegRows + 1;
            int rightMoves = Mathf.Clamp(clampedSlot, 0, steps);

            int[] moves = new int[steps];
            for (int i = 0; i < rightMoves; i++)
            {
                moves[i] = 1;
            }

            System.Random random = rng ?? new System.Random();
            for (int i = steps - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = moves[i];
                moves[i] = moves[j];
                moves[j] = temp;
            }

            var path = new List<GuidedStep>(steps + 1);
            path.Add(new GuidedStep
            {
                position = GetSpawnPosition(cfg),
                isPegHit = false,
                row = -1,
                col = -1
            });

            int col = 0;
            for (int row = 0; row < cfg.pegRows; row++)
            {
                col += moves[row];
                path.Add(new GuidedStep
                {
                    position = GetPegPosition(row, col, cfg),
                    isPegHit = true,
                    row = row,
                    col = col
                });
            }

            path.Add(new GuidedStep
            {
                position = GetSlotPosition(clampedSlot, cfg),
                isPegHit = false,
                row = cfg.pegRows,
                col = clampedSlot
            });

            return path;
        }
    }
}
