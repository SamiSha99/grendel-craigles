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
        private readonly HashSet<int> toRemoveCraigles = new();
        private readonly List<CraigleData> nextGenerationBuffer = new();
        private float spawnTimer;

        // Neighbor lookups use a uniform grid: the world cube is cut into cellsPerAxis^3 cells,
        // each PerceptionRadius wide, stored flat in gridCells. Rebuilt once per frame in BuildGrid.
        private List<int>[] gridCells;
        private int cellsPerAxis;
        private float cachedCellSize;
        private float cachedBoundsSize = -1f;

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
            BuildGrid();

            for (int i = 0; i < count; i++)
            {
                CraigleData c = craigles[i];
                Vector3Int oldCell = CellCoords(c.Position);

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

                Vector3Int newCell = CellCoords(c.Position);
                if (newCell != oldCell)
                {
                    gridCells[CellIndex(oldCell)].Remove(i);
                    gridCells[CellIndex(newCell)].Add(i);
                }

                craigles[i] = c;
            }
        }

        // Checks for how many cells we actually need, then builds the array (at most once) 
        // using PerceptionRadius and BoundsSize. Cell size is actaully PerceptionRadius
        // as reference, which should make it managable for 3x3x3 neighbor checks.
        // Also made it so it rebuilds if the ScriptableObject changes for PerceptionRadius and BoundsSize
        private void EnsureGrid()
        {
            // No perception?! Not allowed, always a value.
            float cellSize = Mathf.Max(config.PerceptionRadius, 0.0001f);
            if (gridCells != null && cachedCellSize == cellSize && cachedBoundsSize == config.BoundsSize) return;

            cachedCellSize = cellSize;
            cachedBoundsSize = config.BoundsSize;
            cellsPerAxis = Mathf.Max(1, Mathf.CeilToInt(config.BoundsSize / cellSize));

            // Total cell count is 1000 (from 10 * 10 * 10) cells using PerceptionRadius default settings.
            gridCells = new List<int>[cellsPerAxis * cellsPerAxis * cellsPerAxis];
            for (int i = 0; i < gridCells.Length; i++) gridCells[i] = new List<int>();
        }

        // First thing first, build the grid and add every craigle into a cell.
        private void BuildGrid()
        {
            EnsureGrid();
            foreach (List<int> cell in gridCells) cell.Clear();
            for (int i = 0; i < craigles.Count; i++) gridCells[CellIndex(CellCoords(craigles[i].Position))].Add(i);
        }

        // Turns a world position into a (x, y, z) cell coordinate. 
        // Value is clamped so a craigle on the edge (or outside) of the cube stays in a cell instead of being invalid.
        private Vector3Int CellCoords(Vector3 position)
        {
            int ix = Mathf.Clamp(Mathf.FloorToInt(position.x / cachedCellSize), 0, cellsPerAxis - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(position.y / cachedCellSize), 0, cellsPerAxis - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(position.z / cachedCellSize), 0, cellsPerAxis - 1);
            return new Vector3Int(ix, iy, iz);
        }
        private int CellIndex(Vector3Int cell) => (cell.z * cellsPerAxis + cell.y) * cellsPerAxis + cell.x;

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

            // Neighbors can only ever be in our own cell or the 26 right next to it, since each cell
            // is PerceptionRadius wide so anything farther than that isn't counted.
            Vector3Int center = CellCoords(self.Position);
            for (int dz = -1; dz <= 1; dz++)
            {
                int iz = center.z + dz;
                if (iz < 0 || iz >= cellsPerAxis) continue; // off the grid, skip the whole slice

                for (int dy = -1; dy <= 1; dy++)
                {
                    int iy = center.y + dy;
                    if (iy < 0 || iy >= cellsPerAxis) continue; // ditto

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int ix = center.x + dx;
                        if (ix < 0 || ix >= cellsPerAxis) continue; // ditto

                        // Whoever's actually parked in this cell right now, usually just a handful.
                        List<int> cell = gridCells[CellIndex(new Vector3Int(ix, iy, iz))];
                        for (int n = 0; n < cell.Count; n++)
                        {
                            int j = cell[n];
                            if (j == index) continue;

                            CraigleData other = craigles[j];
                            Vector3 offset = other.Position - self.Position;
                            float sqrDist = offset.sqrMagnitude;
                            if (sqrDist > perceptionSqr) continue; // shares the cell but still too far despite it

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

            toRemoveCraigles.Clear();
            foreach (int idx in pendingMitosisParents) toRemoveCraigles.Add(idx);
            foreach (int idx in pendingDeaths) toRemoveCraigles.Add(idx);

            nextGenerationBuffer.Clear();
            for (int i = 0; i < count; i++)
            {
                if (toRemoveCraigles.Contains(i)) continue;
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
                craigles[victim] = craigles[last];
                craigles.RemoveAt(last);
            }
        }
    }
}
