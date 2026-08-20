using System.Collections.Generic;
using UnityEngine;

namespace Craigles
{
    public class CraigleSimulation : MonoBehaviour
    {
        [SerializeField] private SwarmConfig config;

        private readonly List<CraigleData> craigles = new();
        private readonly List<int> neighborCountCache = new();
        private readonly List<int> pendingMitosisParents = new();
        private readonly List<int> pendingDeaths = new();
        private readonly HashSet<int> toRemoveScratch = new();
        private readonly List<CraigleData> nextGenerationBuffer = new();
        private float spawnTimer;
        public IReadOnlyList<CraigleData> Craigles => craigles;
        public SwarmConfig Config => config;

        private Vector3 SpawnCorner => Vector3.zero;
        private Vector3 CubeCenter => Vector3.one * (config.BoundsSize * 0.5f);

        private void Update()
        {
            float d = Time.deltaTime;
            HandleSpawning(d);
            HandleMovement(d);
            HandleGrowth(d);
            HandleMitosisAndCrowding();
            PopulationCleaner();
        }

        private void HandleSpawning(float d)
        {
            if (config.SpawnRate <= 0f) return;

            spawnTimer += d;
            float interval = 1f / config.SpawnRate;
            // In case of lag or when spawn rate is lower than referesh rate, so we don't throttled it.
            while (spawnTimer >= interval)
            {
                spawnTimer -= interval;
                SpawnCraigle();
            }
        }

        private void SpawnCraigle()
        {            
            // We get the direction of the corner we are using for spawning and use it for ref when making the random direction.
            Vector3 axis = (CubeCenter - SpawnCorner).normalized;
            Vector3 direction = RandomDirectionInCone(axis, config.SpawnConeHalfAngle);
            // Random range starting velocity
            float speed = Random.Range(config.SpawnSpeedMin, config.SpawnSpeedMax);

            craigles.Add(new CraigleData
            {
                Position = SpawnCorner,
                Velocity = direction * speed,
                Size = config.InitialSize,
                Hue = Random.Range(0f, 360f),
                Age = 0f,
            });
        }

        /// <summary>
        /// Returns a random direction using the axis and the amount of angle degree.
        /// </summary>
        private static Vector3 RandomDirectionInCone(Vector3 axis, float halfAngleDegrees)
        {
            float halfAngleRad = halfAngleDegrees * Mathf.Deg2Rad;
            float cosHalfAngle = Mathf.Cos(halfAngleRad);
            float z = Random.Range(cosHalfAngle, 1f);
            float phi = Random.Range(0f, Mathf.PI * 2f);
            float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));

            Vector3 local = new(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), z);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, axis);
            return rotation * local;
        }

        private void HandleMovement(float d)
        {
            int count = craigles.Count;
            neighborCountCache.Clear();

            for (int i = 0; i < count; i++)
            {
                CraigleData c = craigles[i];

                // Pretty unoptimized considering cuz O(n) * O(n) :(
                GetSwarm(i, out int neighborCount, out Vector3 centroidSum,
                    out Vector3 velocitySum, out Vector3 separationSum);
                neighborCountCache.Add(neighborCount);

                if (neighborCount > 0)
                {
                    Vector3 centroid = centroidSum / neighborCount;
                    ApplyCohesion(ref c, centroid, d);
                    ApplyAlignment(ref c, velocitySum, centroid, d);
                }
                ApplySeparation(ref c, separationSum, d);

                c.Position += c.Velocity * d;
                BounceOffWalls(ref c);

                craigles[i] = c;
            }
        }

        /// <summary>
        /// Builds the array for neighborCounts per Craigle.
        /// </summary>
        private void GetSwarm(int index, out int neighborCount, out Vector3 centroidSum,
            out Vector3 velocitySum, out Vector3 separationSum)
        {
            neighborCount = 0;
            centroidSum = Vector3.zero;
            velocitySum = Vector3.zero;
            separationSum = Vector3.zero;

            CraigleData self = craigles[index];
            float perceptionSqr = config.PerceptionRadius * config.PerceptionRadius;
            float separationDistance = config.SeparationDistanceFactor * self.Size;
            float separationDistanceSqr = separationDistance * separationDistance;

            int count = craigles.Count;
            for (int j = 0; j < count; j++)
            {
                if (j == index) continue;

                CraigleData other = craigles[j];
                Vector3 offset = other.Position - self.Position;
                float sqrDist = offset.sqrMagnitude;
                if (sqrDist > perceptionSqr) continue;

                neighborCount++;
                centroidSum += other.Position;
                velocitySum += other.Velocity;

                if (sqrDist < separationDistanceSqr && sqrDist > 0.0001f)
                {
                    float dist = Mathf.Sqrt(sqrDist);
                    float attenuation = 1f - (dist / separationDistance);
                    separationSum += -offset / dist * attenuation;
                }
            }
        }

        private void ApplyCohesion(ref CraigleData c, Vector3 centroid, float d)
        {
            Vector3 toCenter = centroid - c.Position;
            float distance = toCenter.magnitude;
            if (distance < 0.0001f) return;

            // The farther it is from the center of the swarm the weaker it is.
            float attenuation = 1f - Mathf.Clamp01(distance / config.PerceptionRadius);
            Vector3 accel = toCenter / distance * (config.CohesionMaxAcceleration * attenuation);
            c.Velocity += accel * d;
        }

        private void ApplySeparation(ref CraigleData c, Vector3 separationSum, float d)
        {
            if (separationSum.sqrMagnitude < 0.0001f) return;

            // Clamp to length 1 so neighbors pushing the same way can't stack past the max force.
            Vector3 direction = Vector3.ClampMagnitude(separationSum, 1f);
            c.Velocity += direction * (config.SeparationMaxAcceleration * d);
        }

        private void ApplyAlignment(ref CraigleData c, Vector3 velocitySum, Vector3 centroid, float d)
        {
            if (velocitySum.sqrMagnitude < 0.0001f || c.Velocity.sqrMagnitude < 0.0001f) return;

            Vector3 currentDirection = c.Velocity.normalized;
            Vector3 targetDirection = velocitySum.normalized;

            float distanceToCenter = Vector3.Distance(c.Position, centroid);
            float distanceAttenuation = 1f - Mathf.Clamp01(distanceToCenter / config.PerceptionRadius);
            float angleDifference = Vector3.Angle(currentDirection, targetDirection);
            float angleAttenuation = Mathf.Clamp01(angleDifference / 180f);

            float maxTurnRadiansThisStep = config.AlignmentMaxTurnSpeed * distanceAttenuation
                * angleAttenuation * d * Mathf.Deg2Rad;
            Vector3 newDirection = Vector3.RotateTowards(currentDirection, targetDirection,
                maxTurnRadiansThisStep, 0f);

            // Alignment only ever changes heading
            float speed = c.Velocity.magnitude;
            c.Velocity = newDirection * speed;
        }

        private void BounceOffWalls(ref CraigleData c)
        {
            BounceAxis(ref c.Position.x, ref c.Velocity.x);
            BounceAxis(ref c.Position.y, ref c.Velocity.y);
            BounceAxis(ref c.Position.z, ref c.Velocity.z);
        }

        private void BounceAxis(ref float position, ref float velocity)
        {
            if (position < 0f)
            {
                position = -position;
                velocity = Mathf.Abs(velocity);
            }
            else if (position > config.BoundsSize)
            {
                position = config.BoundsSize - (position - config.BoundsSize);
                velocity = -Mathf.Abs(velocity);
            }
        }

        private void HandleGrowth(float d)
        {
            for (int i = 0; i < craigles.Count; i++)
            {
                CraigleData c = craigles[i];
                c.Size += config.InitialSize * config.GrowthRatePerSecond * d;
                c.Age += d;
                craigles[i] = c;
            }
        }

        private void HandleMitosisAndCrowding()
        {
            pendingMitosisParents.Clear();
            pendingDeaths.Clear();

            // Check if we need to fullfil such request.
            // We only need to do ONE of them, with some priority involved.
            // Priortiy: Mitosis -> Death
            int count = craigles.Count;
            for (int i = 0; i < count; i++)
            {
                CraigleData c = craigles[i];

                if (c.Age >= config.MitosisMinAge && Random.value < config.MitosisChancePerUpdate)
                {
                    pendingMitosisParents.Add(i);
                    continue;
                }

                if (neighborCountCache[i] > config.CrowdingNeighborThreshold && Random.value < config.CrowdingDeathChance)
                {
                    pendingDeaths.Add(i);
                }
            }

            if (pendingMitosisParents.Count == 0 && pendingDeaths.Count == 0) return;

            toRemoveScratch.Clear();
            foreach (int idx in pendingMitosisParents) toRemoveScratch.Add(idx);
            foreach (int idx in pendingDeaths) toRemoveScratch.Add(idx);

            nextGenerationBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                if (toRemoveScratch.Contains(i)) continue;
                nextGenerationBuffer.Add(craigles[i]);
            }

            foreach (int parentIndex in pendingMitosisParents)
            {
                CraigleData parent = craigles[parentIndex];
                nextGenerationBuffer.Add(MakeChild(parent, config.MitosisHueShift, config.MitosisVelocityBend));
                nextGenerationBuffer.Add(MakeChild(parent, -config.MitosisHueShift, -config.MitosisVelocityBend));
            }

            craigles.Clear();
            craigles.AddRange(nextGenerationBuffer);
        }

        private CraigleData MakeChild(CraigleData parent, float hueShift, float velocityBendDegrees)
        {
            Vector3 forward = parent.Velocity.sqrMagnitude > 0.0001f
                ? parent.Velocity.normalized
                : Vector3.forward;

            Vector3 axis = Vector3.Cross(forward, Vector3.up);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.Cross(forward, Vector3.right);
            axis.Normalize();

            Quaternion bend = Quaternion.AngleAxis(velocityBendDegrees, axis);

            return new CraigleData
            {
                Position = parent.Position,
                Velocity = bend * parent.Velocity,
                Size = parent.Size * config.MitosisSizeMultiplier,
                Hue = Mathf.Repeat(parent.Hue + hueShift, 360f),
                Age = 0f,
            };
        }

        private void PopulationCleaner()
        {
            int excess = craigles.Count - config.MaxPopulation;
            for (int i = 0; i < excess; i++)
            {
                int victim = Random.Range(0, craigles.Count);
                int last = craigles.Count - 1;
                craigles[victim] = craigles[last]; // swap-and-pop: O(1) removal, order doesn't matter
                craigles.RemoveAt(last);
            }
        }
    }
}
