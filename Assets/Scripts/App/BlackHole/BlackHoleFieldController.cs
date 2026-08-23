using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    /// <summary>Owns a pooled stream of rocks and progressively increases pressure as rocks are swallowed.</summary>
    public sealed class BlackHoleFieldController : MonoBehaviour
    {
        private readonly Queue<BlackHoleRock> pool = new();
        private readonly HashSet<BlackHoleRock> active = new();
        private RectTransform rockLayer;
        private Vector2 center;
        private Vector2 spawnHalfExtents;
        private float captureRadius;
        private RectTransform pulseRoot;
        private Image magentaRim;
        private Image cyanRim;
        private int swallowed;
        private int targetActive = 10;
        private float spawnTimer;
        private bool configured;

        private const int PoolSize = 48;
        private const int MaxActive = 40;

        public void Configure(RectTransform layer, Vector2 holeCenter, float horizonRadius,
            Vector2 halfExtents, RectTransform corePulseRoot, Image outerRim, Image innerRim)
        {
            rockLayer = layer;
            center = holeCenter;
            captureRadius = horizonRadius;
            spawnHalfExtents = halfExtents;
            pulseRoot = corePulseRoot;
            magentaRim = outerRim;
            cyanRim = innerRim;

            for (var i = 0; i < PoolSize; i++)
            {
                var rock = BlackHoleRockVisual.Create(rockLayer, i);
                rock.gameObject.SetActive(false);
                pool.Enqueue(rock);
            }
            configured = true;

            // Fill the field immediately so the home screen never starts empty.
            for (var i = 0; i < targetActive && pool.Count > 0; i++)
                SpawnRock();
        }

        private void Update()
        {
            if (!configured)
                return;

            spawnTimer -= Time.unscaledDeltaTime;
            if (spawnTimer > 0f || active.Count >= targetActive || pool.Count == 0)
                return;

            SpawnRock();
            spawnTimer = Mathf.Max(0.18f, 0.62f - swallowed * 0.0085f);
        }

        private void SpawnRock()
        {
            var rock = pool.Dequeue();
            active.Add(rock);

            var side = Random.Range(0, 4);
            Vector2 start;
            switch (side)
            {
                case 0: start = new Vector2(-spawnHalfExtents.x, Random.Range(-spawnHalfExtents.y, spawnHalfExtents.y)); break;
                case 1: start = new Vector2(spawnHalfExtents.x, Random.Range(-spawnHalfExtents.y, spawnHalfExtents.y)); break;
                case 2: start = new Vector2(Random.Range(-spawnHalfExtents.x, spawnHalfExtents.x), spawnHalfExtents.y); break;
                default: start = new Vector2(Random.Range(-spawnHalfExtents.x, spawnHalfExtents.x), -spawnHalfExtents.y); break;
            }

            var radius = Mathf.Max(360f, Vector2.Distance(start, center));
            var startAngle = Mathf.Atan2((start.y - center.y) / 0.68f, start.x - center.x) * Mathf.Rad2Deg;
            rock.Launch(this, center, captureRadius, radius, startAngle,
                Random.Range(0.9f, 1.65f), Random.Range(4.5f, 7.8f),
                Random.Range(0.60f, 0.76f), Random.value > 0.5f, Random.Range(0.72f, 1.25f));
        }

        internal void RockWasSwallowed(BlackHoleRock rock)
        {
            if (!active.Remove(rock))
                return;

            swallowed++;
            targetActive = Mathf.Min(MaxActive, 10 + swallowed / 4);
            rock.gameObject.SetActive(false);
            pool.Enqueue(rock);
            StartCoroutine(PulseHole());
        }

        private IEnumerator PulseHole()
        {
            if (pulseRoot == null)
                yield break;

            var originalScale = Vector3.one;
            var outerBase = magentaRim != null ? magentaRim.color : Color.white;
            var innerBase = cyanRim != null ? cyanRim.color : Color.white;
            const float duration = 0.18f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = time / duration;
                var punch = Mathf.Sin(t * Mathf.PI);
                pulseRoot.localScale = originalScale * (1f + punch * 0.09f);
                if (magentaRim != null)
                    magentaRim.color = Color.Lerp(outerBase, Color.white, punch * 0.42f);
                if (cyanRim != null)
                    cyanRim.color = Color.Lerp(innerBase, Color.white, punch * 0.28f);
                yield return null;
            }
            pulseRoot.localScale = originalScale;
            if (magentaRim != null) magentaRim.color = outerBase;
            if (cyanRim != null) cyanRim.color = innerBase;
        }
    }
}
