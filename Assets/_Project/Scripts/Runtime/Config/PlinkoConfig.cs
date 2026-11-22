using UnityEngine;

namespace FunFlow.Plinko
{
    [CreateAssetMenu(fileName = "PlinkoConfig", menuName = "FunFlow/Plinko Config")]
    public class PlinkoConfig : ScriptableObject
    {
        [Header("Board Layout")]
        public int pegRows = 5;
        public int topRowPegs = 2;
        public int slotCount = 7;
        public float horizontalSpacing = 1.15f;
        public float verticalSpacing = 0.95f;
        public float topPegY = 2.4f;
        public float spawnOffsetY = 1.25f;
        public float slotOffsetY = 1.15f;

        [Header("Radii & Motion")]
        public float pegRadius = 0.14f;
        public float ballRadius = 0.22f;
        public float classicDropJitter = 0.18f;
        public float guidedStepDuration = 0.28f;
        public float guidedArcHeight = 0.24f;

        [Header("Colors")]
        public Color backgroundColor = new Color(0x21 / 255f, 0x1E / 255f, 0x57 / 255f, 1f);
        public Color pegColor = new Color(0x9D / 255f, 0x8F / 255f, 0xF0 / 255f, 1f);
        public Color pegPulseColor = new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f);
        public Color slotNormalColor = new Color(0x5B / 255f, 0x52 / 255f, 0xA8 / 255f, 1f);
        public Color slotBorderColor = new Color(0x7C / 255f, 0x72 / 255f, 0xCC / 255f, 1f);
        public Color slotSelectedColor = new Color(0xF5 / 255f, 0xA6 / 255f, 0x23 / 255f, 1f);
        public Color ballColor = new Color(0xFF / 255f, 0x9F / 255f, 0x2E / 255f, 1f);
        public Color trailDotColor = new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f);
    }
}
