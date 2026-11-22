using UnityEngine;

namespace FunFlow.Plinko
{
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralSfx : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private AudioSource audioSource;

        private AudioClip _pegTickClip;
        private AudioClip _selectClip;
        private AudioClip _winClip;
        private AudioClip _missClip;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            _pegTickClip = CreateToneClip("PegTick", 0.065f, t =>
            {
                float env = Mathf.Exp(-t * 58f);
                float body = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.35f;
                return body * env * 0.35f;
            });

            _selectClip = CreateToneClip("SelectPop", 0.045f, t =>
            {
                float env = Mathf.Exp(-t * 72f);
                float freq = Mathf.Lerp(520f, 760f, t / 0.045f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.28f;
            });

            _winClip = CreateToneClip("WinChime", 0.38f, t =>
                NoteBurst(t, 0.00f, 523.25f, 12f) * 0.32f +
                NoteBurst(t, 0.09f, 659.25f, 12f) * 0.32f +
                NoteBurst(t, 0.18f, 783.99f, 10f) * 0.36f);

            _missClip = CreateToneClip("MissThud", 0.22f, t =>
                NoteBurst(t, 0.00f, 260f, 16f) * 0.3f +
                NoteBurst(t, 0.08f, 195f, 14f) * 0.32f);
        }

        private void OnDestroy()
        {
            DestroyClip(_pegTickClip);
            DestroyClip(_selectClip);
            DestroyClip(_winClip);
            DestroyClip(_missClip);
        }

        public void PlayPegTick() => Play(_pegTickClip, 0.7f, Random.Range(0.94f, 1.08f));
        public void PlaySelect() => Play(_selectClip, 0.75f);
        public void PlayWin() => Play(_winClip, 0.9f);
        public void PlayMiss() => Play(_missClip, 0.8f);

        private void Play(AudioClip clip, float volume, float pitch = 1f)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }

        private static float NoteBurst(float t, float start, float freq, float decay)
        {
            if (t < start)
            {
                return 0f;
            }

            float localT = t - start;
            float env = Mathf.Exp(-localT * decay) * Mathf.Clamp01(localT * 120f);
            return (Mathf.Sin(2f * Mathf.PI * freq * localT) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * localT)) * env;
        }

        private static AudioClip CreateToneClip(string clipName, float durationSeconds, System.Func<float, float> sampleFunc)
        {
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * SampleRate));
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Clamp(sampleFunc((float)i / SampleRate), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null)
            {
                Destroy(clip);
            }
        }
    }
}
