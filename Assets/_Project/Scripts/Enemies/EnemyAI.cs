using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TPSShooter.Combat;
using TPSShooter.Player;

namespace TPSShooter.Enemies
{
    public enum EnemyState { Patrol, Chase, Attack, Dead }

    /// <summary>
    /// IA básica de un bot enemigo:
    ///   Patrol → deambula alrededor de su punto de aparición.
    ///   Chase  → ha visto u oído al jugador y va hacia su última posición conocida.
    ///   Attack → ve al jugador dentro del rango de disparo: se gira hacia él y dispara cada X segundos.
    ///   Dead   → cae al suelo y se destruye.
    ///
    /// Movimiento:
    ///   - Si el bot tiene un NavMeshAgent sobre un NavMesh horneado, lo usa (esquiva obstáculos).
    ///   - Si no, se mueve en línea recta con un CharacterController (funciona sin hornear nada).
    ///
    /// La vida del bot la gestiona el componente Health (obligatorio), así las balas del jugador le hacen daño.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Inspector
        // ------------------------------------------------------------------
        [Header("Objetivo")]
        [Tooltip("Transform del jugador (el objeto con el CharacterController). Si está vacío se busca por la etiqueta 'Player'.")]
        [SerializeField] private Transform player;
        [Tooltip("Altura (desde los pies del jugador) a la que apunta y mira el bot.")]
        [SerializeField] private float targetHeight = 1.2f;

        [Header("Detección")]
        [SerializeField] private float detectionRange = 25f;
        [Tooltip("Ángulo de visión total en grados.")]
        [SerializeField, Range(10f, 360f)] private float fieldOfView = 120f;
        [Tooltip("A esta distancia te detecta aunque estés a su espalda.")]
        [SerializeField] private float closeDetectionRange = 5f;
        [Tooltip("Segundos que sigue persiguiendo tras perderte de vista.")]
        [SerializeField] private float loseTargetTime = 5f;
        [SerializeField] private float eyeHeight = 1.6f;
        [Tooltip("Capas que bloquean la visión. Debe incluir la capa del jugador ('Ignore Raycast' por defecto).")]
        [SerializeField] private LayerMask visionMask = ~0;

        [Header("Movimiento")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float chaseSpeed = 4.5f;
        [Tooltip("Grados por segundo.")]
        [SerializeField] private float turnSpeed = 360f;
        [Tooltip("Radio de patrulla alrededor del punto de aparición (0 = quedarse quieto).")]
        [SerializeField] private float patrolRadius = 8f;
        [SerializeField] private float patrolWaitTime = 2f;
        [SerializeField] private float gravity = -20f;

        [Header("Combate")]
        [Tooltip("Distancia máxima a la que empieza a disparar.")]
        [SerializeField] private float attackRange = 15f;
        [Tooltip("Distancia a la que deja de acercarse mientras dispara.")]
        [SerializeField] private float preferredDistance = 8f;
        [Tooltip("Segundos entre disparos.")]
        [SerializeField] private float fireInterval = 0.7f;
        [Tooltip("Retraso antes del primer disparo al detectarte (tiempo de reacción).")]
        [SerializeField] private float reactionTime = 0.5f;
        [SerializeField] private float damage = 8f;
        [Tooltip("Dispersión en grados (más alto = peor puntería).")]
        [SerializeField] private float spread = 3f;
        [Tooltip("Sólo dispara si está mirando al jugador con menos de este ángulo.")]
        [SerializeField] private float maxShootAngle = 15f;
        [SerializeField] private float impactForce = 4f;
        [Tooltip("Punto de salida de las balas. Si está vacío se usan los 'ojos'.")]
        [SerializeField] private Transform muzzle;
        [Tooltip("Capas que pueden recibir los disparos. Debe incluir la capa del jugador.")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Efectos (opcionales)")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private Light muzzleFlash;
        [SerializeField] private float effectDuration = 0.05f;

        [Header("Muerte")]
        [SerializeField] private float destroyDelay = 4f;

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------
        public EnemyState State { get; private set; } = EnemyState.Patrol;

        private Health health;
        private NavMeshAgent agent;
        private CharacterController characterController;
        private PlayerHealth playerHealth;

        private Vector3 spawnPosition;
        private Vector3 patrolPoint;
        private float patrolWaitUntil;
        private Vector3 lastKnownPlayerPos;
        private float lastSeenTime = -999f;
        private float nextFireTime;
        private float verticalVelocity;
        private float effectsOffTime;

        private readonly RaycastHit[] rayHits = new RaycastHit[16];

        private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;
        private Vector3 PlayerAimPoint => player.position + Vector3.up * targetHeight;
        private bool UseAgent => agent != null && agent.enabled && agent.isOnNavMesh;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------
        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            characterController = GetComponent<CharacterController>();

            spawnPosition = transform.position;
            patrolPoint = spawnPosition;
            SetEffects(false);
            FindPlayer();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void Update()
        {
            if (Time.time >= effectsOffTime) SetEffects(false);
            if (State == EnemyState.Dead) return;

            if (player == null && !FindPlayer())
            {
                UpdatePatrol();
                return;
            }

            bool playerAlive = playerHealth == null || !playerHealth.IsDead;
            bool alerted = playerAlive && Time.time - lastSeenTime <= loseTargetTime;

            // --- Percepción ---
            bool canSee = playerAlive && CanSeePlayer(alerted);
            if (canSee)
            {
                lastSeenTime = Time.time;
                lastKnownPlayerPos = player.position;
                alerted = true;
            }

            // --- Decisión de estado ---
            EnemyState newState;
            if (!alerted) newState = EnemyState.Patrol;
            else if (canSee && FlatDistance(transform.position, player.position) <= attackRange) newState = EnemyState.Attack;
            else newState = EnemyState.Chase;

            if (newState == EnemyState.Attack && State != EnemyState.Attack)
                nextFireTime = Mathf.Max(nextFireTime, Time.time + reactionTime); // tiempo de reacción
            State = newState;

            // --- Ejecución ---
            switch (State)
            {
                case EnemyState.Patrol: UpdatePatrol(); break;
                case EnemyState.Chase: UpdateChase(); break;
                case EnemyState.Attack: UpdateAttack(); break;
            }
        }

        // ------------------------------------------------------------------
        // Estados
        // ------------------------------------------------------------------
        private void UpdatePatrol()
        {
            if (patrolRadius <= 0f)
            {
                Stop();
                return;
            }

            if (Time.time < patrolWaitUntil)
            {
                Stop();
                return;
            }

            if (HasReached(patrolPoint))
            {
                patrolWaitUntil = Time.time + patrolWaitTime;
                patrolPoint = PickPatrolPoint();
                Stop();
                return;
            }

            MoveTo(patrolPoint, patrolSpeed, faceMovement: true);
        }

        private void UpdateChase()
        {
            if (HasReached(lastKnownPlayerPos))
            {
                // Llegó a donde te vio por última vez: se queda mirando alrededor
                Stop();
                transform.Rotate(0f, turnSpeed * 0.25f * Time.deltaTime, 0f);
                return;
            }
            MoveTo(lastKnownPlayerPos, chaseSpeed, faceMovement: true);
        }

        private void UpdateAttack()
        {
            // Se acerca hasta su distancia preferida, después se planta
            if (FlatDistance(transform.position, player.position) > preferredDistance)
                MoveTo(player.position, chaseSpeed, faceMovement: false);
            else
                Stop();

            FaceTowards(player.position);

            if (Time.time >= nextFireTime && AngleToPlayer() <= maxShootAngle)
            {
                Shoot();
                nextFireTime = Time.time + fireInterval * Random.Range(0.85f, 1.15f);
            }
        }

        // ------------------------------------------------------------------
        // Percepción
        // ------------------------------------------------------------------
        private bool CanSeePlayer(bool alreadyAlerted)
        {
            Vector3 eye = EyePosition;
            Vector3 target = PlayerAimPoint;
            float distance = Vector3.Distance(eye, target);
            if (distance > detectionRange) return false;

            // Si ya te tenía localizado no necesita tenerte dentro del cono de visión
            if (!alreadyAlerted && distance > closeDetectionRange)
            {
                Vector3 flatDir = target - eye;
                flatDir.y = 0f;
                if (Vector3.Angle(transform.forward, flatDir) > fieldOfView * 0.5f) return false;
            }

            return HasLineOfSight(eye, target);
        }

        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;

            // Sin obstáculos => visible. Si lo primero que golpea es el jugador => visible.
            if (!RaycastIgnoringSelf(from, dir / dist, dist, visionMask, out RaycastHit hit)) return true;
            return IsPlayerCollider(hit.collider);
        }

        private bool IsPlayerCollider(Collider col)
        {
            Transform t = col.transform;
            return t.IsChildOf(player) || (playerHealth != null && t.IsChildOf(playerHealth.transform));
        }

        /// <summary>Raycast que ignora los colliders del propio bot y devuelve el impacto más cercano.</summary>
        private bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float distance, LayerMask mask, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.RaycastNonAlloc(origin, dir, rayHits, distance, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                if (rayHits[i].collider.transform.IsChildOf(transform)) continue;
                if (rayHits[i].distance < best)
                {
                    best = rayHits[i].distance;
                    closest = rayHits[i];
                    found = true;
                }
            }
            return found;
        }

        private void OnDamaged(Vector3 point, Vector3 normal, float amount)
        {
            // Si le disparan, se alerta y va hacia el jugador aunque no lo viera
            if (player == null) return;
            lastSeenTime = Time.time;
            lastKnownPlayerPos = player.position;
        }

        // ------------------------------------------------------------------
        // Disparo
        // ------------------------------------------------------------------
        private void Shoot()
        {
            Vector3 origin = muzzle != null ? muzzle.position : EyePosition;
            Vector3 dir = (PlayerAimPoint - origin).normalized;

            // Dispersión aleatoria dentro de un cono
            Vector2 r = Random.insideUnitCircle * spread;
            dir = Quaternion.LookRotation(dir) * Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward;

            float range = attackRange * 1.5f;
            Vector3 end = origin + dir * range;

            if (RaycastIgnoringSelf(origin, dir, range, hitMask, out RaycastHit hit))
            {
                end = hit.point;

                // Sin fuego amigo entre bots
                bool hitAnotherEnemy = hit.collider.GetComponentInParent<EnemyAI>() != null;
                if (!hitAnotherEnemy)
                {
                    var damageable = hit.collider.GetComponentInParent<IDamageable>();
                    damageable?.TakeDamage(damage, hit.point, hit.normal);
                }

                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(dir * impactForce, hit.point, ForceMode.Impulse);
            }

            ShowEffects(origin, end);
        }

        private void ShowEffects(Vector3 from, Vector3 to)
        {
            if (tracer != null)
            {
                tracer.useWorldSpace = true;
                tracer.positionCount = 2;
                tracer.SetPosition(0, from);
                tracer.SetPosition(1, to);
            }
            SetEffects(true);
            effectsOffTime = Time.time + effectDuration;
        }

        private void SetEffects(bool on)
        {
            if (tracer != null) tracer.enabled = on;
            if (muzzleFlash != null) muzzleFlash.enabled = on;
        }

        // ------------------------------------------------------------------
        // Movimiento (NavMeshAgent o CharacterController)
        // ------------------------------------------------------------------
        private void MoveTo(Vector3 destination, float speed, bool faceMovement)
        {
            if (UseAgent)
            {
                agent.isStopped = false;
                agent.speed = speed;
                agent.updateRotation = faceMovement;
                if ((agent.destination - destination).sqrMagnitude > 0.25f) agent.SetDestination(destination);
                return;
            }

            Vector3 toTarget = destination - transform.position;
            toTarget.y = 0f;
            Vector3 velocity = toTarget.magnitude > 0.3f ? toTarget.normalized * speed : Vector3.zero;
            ManualMove(velocity);
            if (faceMovement && velocity.sqrMagnitude > 0.01f) FaceTowards(transform.position + velocity);
        }

        private void Stop()
        {
            if (UseAgent)
            {
                agent.isStopped = true;
                agent.updateRotation = false;
            }
            else
            {
                ManualMove(Vector3.zero); // sigue aplicando gravedad
            }
        }

        private void ManualMove(Vector3 planarVelocity)
        {
            float dt = Time.deltaTime;
            if (characterController != null && characterController.enabled)
            {
                if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
                verticalVelocity += gravity * dt;
                characterController.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            }
            else
            {
                transform.position += planarVelocity * dt;
            }
        }

        private void FaceTowards(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private bool HasReached(Vector3 destination)
        {
            if (UseAgent)
                return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f;
            return FlatDistance(transform.position, destination) < 0.5f;
        }

        private Vector3 PickPatrolPoint()
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = spawnPosition + new Vector3(offset.x, 0f, offset.y);

            if (UseAgent && NavMesh.SamplePosition(candidate, out NavMeshHit navHit, patrolRadius, NavMesh.AllAreas))
                return navHit.position;
            return candidate;
        }

        private float AngleToPlayer()
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            return Vector3.Angle(transform.forward, dir);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private bool FindPlayer()
        {
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }
            if (player != null && playerHealth == null)
            {
                playerHealth = player.GetComponentInParent<PlayerHealth>();
                if (playerHealth == null) playerHealth = player.GetComponentInChildren<PlayerHealth>();
            }
            return player != null;
        }

        // ------------------------------------------------------------------
        // Muerte
        // ------------------------------------------------------------------
        private void OnDied(Health h)
        {
            State = EnemyState.Dead;
            SetEffects(false);

            if (UseAgent) agent.isStopped = true;
            if (agent != null) agent.enabled = false;
            if (characterController != null) characterController.enabled = false; // ya no recibe balas

            StartCoroutine(FallOver());
            Destroy(gameObject, destroyDelay);
        }

        private IEnumerator FallOver()
        {
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }
            transform.rotation = end;
        }

        // ------------------------------------------------------------------
        // Gizmos (selecciona el bot en la escena para ver sus rangos)
        // ------------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            Vector3 pos = transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, attackRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pos, preferredDistance);

            Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pos + Vector3.up * eyeHeight, pos + Vector3.up * eyeHeight + left * detectionRange);
            Gizmos.DrawLine(pos + Vector3.up * eyeHeight, pos + Vector3.up * eyeHeight + right * detectionRange);
        }
    }
}
