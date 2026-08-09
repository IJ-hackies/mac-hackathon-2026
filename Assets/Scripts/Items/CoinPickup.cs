using System.Collections;
using Audio;
using Player.UI.Progression;
using UnityEngine;

namespace Items
{
    /// <summary>
    /// Purely cosmetic coin payoff spawned when an enemy dies - the real gold amount is already
    /// added instantly by WaveDirector.AwardGold (see WaveDirector.HandleEnemyKilled), so this
    /// never gates or double-counts the economy. Three phases: Burst (launches outward with a
    /// random impulse and bounces on the ground like fireworks/a coin fountain), Settle (a short
    /// bob-in-place beat so the burst reads before it flies off), then Home (accelerates toward
    /// the player and self-destructs on arrival, pinging ProgressionGoldHud's collect pop/confetti).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoinPickup : MonoBehaviour
    {
        [Header("Burst")]
        [SerializeField] private float minBurstSpeed = 3.5f;
        [SerializeField] private float maxBurstSpeed = 6.5f;
        [SerializeField] private float minUpwardSpeed = 4.5f;
        [SerializeField] private float maxUpwardSpeed = 7f;
        [SerializeField] private float gravity = 16f;
        [SerializeField] private float bounceDamping = 0.42f;
        [SerializeField, Range(0, 3)] private int maxBounces = 2;

        [Header("Settle / Home")]
        [SerializeField] private float settleDuration = 0.3f;
        [SerializeField] private float homingDelay = 0.15f;
        [SerializeField] private float homingAcceleration = 24f;
        [SerializeField] private float homingMaxSpeed = 26f;
        [SerializeField] private float collectDistance = 0.7f;
        [SerializeField] private float spinSpeed = 260f;
        [SerializeField] private float bobHeight = 0.06f;
        [SerializeField] private float bobSpeed = 7f;
        [Tooltip("Absolute safety timeout - guarantees this always eventually collects even if " +
                 "the player reference is lost mid-flight.")]
        [SerializeField] private float maxLifetime = 8f;

        private Transform _player;
        private Vector3 _velocity;
        private float _groundY;
        private int _bouncesLeft;

        /// Launches this coin from groundPosition, bursting outward before homing to player.
        public void Launch(Vector3 groundPosition, Transform player)
        {
            transform.position = groundPosition;
            _player = player;
            _groundY = groundPosition.y;
            _bouncesLeft = maxBounces;

            Vector2 planar = Random.insideUnitCircle.normalized;
            _velocity = new Vector3(planar.x, 0f, planar.y) * Random.Range(minBurstSpeed, maxBurstSpeed)
                + Vector3.up * Random.Range(minUpwardSpeed, maxUpwardSpeed);

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float deadline = Time.time + maxLifetime;

            yield return Burst();
            yield return Settle();
            yield return Home(deadline);

            if (ProgressionGoldHud.Instance != null) ProgressionGoldHud.Instance.PlayCollectPop();
            AudioManager.Instance.PlaySfx(SfxId.CoinCollect, transform.position);
            Destroy(gameObject);
        }

        private IEnumerator Burst()
        {
            while (_bouncesLeft >= 0)
            {
                _velocity += Vector3.down * gravity * Time.deltaTime;
                transform.position += _velocity * Time.deltaTime;
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

                if (transform.position.y <= _groundY)
                {
                    Vector3 pos = transform.position;
                    pos.y = _groundY;
                    transform.position = pos;

                    if (_bouncesLeft == 0 || _velocity.y > -0.5f)
                    {
                        yield break;
                    }

                    _velocity = new Vector3(
                        _velocity.x * bounceDamping,
                        -_velocity.y * bounceDamping,
                        _velocity.z * bounceDamping);
                    _bouncesLeft--;
                }

                yield return null;
            }
        }

        private IEnumerator Settle()
        {
            float elapsed = 0f;
            Vector3 basePosition = transform.position;
            while (elapsed < settleDuration + homingDelay)
            {
                elapsed += Time.deltaTime;
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
                transform.position = basePosition + Vector3.up * (Mathf.Sin(elapsed * bobSpeed) * bobHeight);
                yield return null;
            }
        }

        private IEnumerator Home(float deadline)
        {
            float speed = 0f;
            while (Time.time < deadline)
            {
                if (_player == null)
                {
                    yield return null;
                    continue;
                }

                Vector3 target = _player.position + Vector3.up * 1f;
                if (Vector3.Distance(transform.position, target) <= collectDistance) yield break;

                speed = Mathf.Min(homingMaxSpeed, speed + homingAcceleration * Time.deltaTime);
                Vector3 toPlayer = (target - transform.position).normalized;
                transform.position += toPlayer * speed * Time.deltaTime;
                transform.Rotate(Vector3.up, spinSpeed * 2f * Time.deltaTime, Space.World);
                yield return null;
            }
        }
    }
}
