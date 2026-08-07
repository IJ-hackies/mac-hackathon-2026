using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(Collider))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 5f;

        private Vector3 _direction;
        private float _speed;

        private void Awake()
        {
            var ownCollider = GetComponent<Collider>();
            ownCollider.isTrigger = true;
        }

        public void Launch(Vector3 direction, float speed)
        {
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _speed = speed;
            Destroy(gameObject, lifetime);
        }

        public void IgnoreCollisionWith(Collider other)
        {
            if (other == null) return;
            Physics.IgnoreCollision(GetComponent<Collider>(), other);
        }

        private void Update()
        {
            transform.position += _direction * (_speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            Destroy(gameObject);
        }
    }
}
