using System.Collections.Generic;
using UnityEngine;

namespace FunFlow.Plinko
{
    public class DottedPathRenderer : MonoBehaviour
    {
        [SerializeField] private Sprite dotSprite;
        [SerializeField] private float dotSpacing = 0.18f;
        [SerializeField] private float dotScale = 0.11f;
        [SerializeField] private int sortingOrder = 3;
        [SerializeField] private Color dotColor = new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 0.9f);

        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private Transform _container;
        private int _activeCount;
        private Vector2 _lastDotPos;
        private bool _hasStarted;
        private Sprite _fallbackSprite;

        public void Configure(Sprite sprite, Color color)
        {
            if (sprite != null)
            {
                dotSprite = sprite;
            }

            dotColor = color;
        }

        public void BeginPath(Vector2 startPos, Color color)
        {
            Clear();
            dotColor = color;
            _lastDotPos = startPos;
            _hasStarted = true;
            SpawnDot(startPos);
        }

        public void RecordPosition(Vector2 pos)
        {
            if (!_hasStarted)
            {
                BeginPath(pos, dotColor);
                return;
            }

            float dist = Vector2.Distance(_lastDotPos, pos);
            if (dist < dotSpacing)
            {
                return;
            }

            Vector2 dir = (pos - _lastDotPos) / dist;
            while (dist >= dotSpacing)
            {
                _lastDotPos += dir * dotSpacing;
                SpawnDot(_lastDotPos);
                dist -= dotSpacing;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _activeCount && i < _pool.Count; i++)
            {
                if (_pool[i] != null)
                {
                    _pool[i].gameObject.SetActive(false);
                }
            }

            _activeCount = 0;
            _hasStarted = false;
        }

        private void OnDestroy()
        {
            if (_container != null)
            {
                Destroy(_container.gameObject);
            }

            if (_fallbackSprite != null)
            {
                Destroy(_fallbackSprite.texture);
                Destroy(_fallbackSprite);
            }
        }

        private void EnsureContainer()
        {
            if (_container != null)
            {
                return;
            }

            var go = new GameObject("DottedTrailContainer");
            _container = go.transform;
        }

        private Sprite GetDotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }

            if (_fallbackSprite == null)
            {
                Texture2D tex = SpriteFactory.CreateCircleTexture(64);
                _fallbackSprite = SpriteFactory.CreateSpriteFromTexture(tex, SpriteFactory.DefaultPixelsPerUnit);
            }

            return _fallbackSprite;
        }

        private void SpawnDot(Vector2 pos)
        {
            EnsureContainer();

            SpriteRenderer sr;
            if (_activeCount < _pool.Count)
            {
                sr = _pool[_activeCount];
            }
            else
            {
                var dotGo = new GameObject($"TrailDot_{_pool.Count}");
                dotGo.transform.SetParent(_container, false);
                sr = dotGo.AddComponent<SpriteRenderer>();
                sr.sprite = GetDotSprite();
                sr.sortingOrder = sortingOrder;
                _pool.Add(sr);
            }

            _activeCount++;
            sr.gameObject.SetActive(true);
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            sr.transform.localScale = new Vector3(dotScale, dotScale, 1f);
            sr.color = dotColor;
        }
    }
}
