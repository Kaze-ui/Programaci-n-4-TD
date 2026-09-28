using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    public float speed = 300f;
    public int damage = 1;
    public Vector3 direction = Vector3.down;
    public float lifeTime = 5f;

    [Header("Seguimiento / Homing")]
    public bool isHoming = false;
    public float homingTurnSpeed = 120f; // grados por segundo
    private Transform targetPlayer;

    public void EnableHoming(Transform target, float turnSpeed = 120f)
    {
        isHoming = true;
        targetPlayer = target;
        homingTurnSpeed = turnSpeed;
    }

    void Start()
    {
        Destroy(gameObject, lifeTime);
        AlignRotationWithDirection();
    }

    public void AlignRotationWithDirection()
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        if (isHoming)
        {
            if (targetPlayer == null)
            {
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) targetPlayer = pc.transform;
            }

            if (targetPlayer != null)
            {
                Vector3 desiredDir = (targetPlayer.position - transform.position).normalized;
                direction = Vector3.RotateTowards(direction, desiredDir, homingTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
                AlignRotationWithDirection();
            }
        }

        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.timeScale == 0f) return;

        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }

            Destroy(gameObject);
        }
    }
}