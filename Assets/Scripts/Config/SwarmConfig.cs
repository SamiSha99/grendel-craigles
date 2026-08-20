using UnityEngine;

namespace Craigles
{
    [CreateAssetMenu(menuName = "Craigles/Swarm Config", fileName = "SwarmConfig")]
    public class SwarmConfig : ScriptableObject
    {
        [Header("Environment")]
        [Tooltip("Additive on top of (0, 0, 0) to (BoundsSize, BoundsSize, BoundsSize).")]
        public float BoundsSize = 100f;

        [Header("Spawning")]
        [Tooltip("Craigles per second that are born at the (0, 0, 0) corner.")]
        public float SpawnRate = 1f;
        [Tooltip("Random half angle the mesh is spawned and yeeted from.")]
        public float SpawnConeHalfAngle = 45f;
        public float SpawnSpeedMin = 30f;
        public float SpawnSpeedMax = 50f;
        public float InitialSize = 1f;

        [Header("Swarm")]
        [Tooltip("Craigles within this distance of each other are considered part of the same swarm.")]
        public float PerceptionRadius = 10f;
        public float CohesionMaxAcceleration = 40f;
        [Tooltip("Craigles closer than \"SeparationDistanceFactor * current size\" push apart.")]
        public float SeparationDistanceFactor = 2f;
        public float SeparationMaxAcceleration = 100f;
        [Tooltip("In degrees per second.")]
        public float AlignmentMaxTurnSpeed = 200f;

        [Header("Growth")]
        [Tooltip("A % of InitialSize added per second.")]
        public float GrowthRatePerSecond = 0.15f;

        [Header("Mitosis")]
        public float MitosisMinAge = 2f;
        [Range(0f, 1f)] public float MitosisChancePerUpdate = 0.2f;
        [Range(0f, 1f)] public float MitosisSizeMultiplier = 0.7f;
        public float MitosisHueShift = 10f;
        public float MitosisVelocityBend = 30f;

        [Header("Crowding")]
        public int CrowdingNeighborThreshold = 100;
        [Range(0f, 1f)] public float CrowdingDeathChance = 0.2f;
        [Tooltip("The max population, Unity's Graphics.DrawMeshInstanced does not allow more than 1023.")]
        [Range(1, 1023)] public int MaxPopulation = 500;

        [Header("Rendering")]
        [Tooltip("Optional. Leave to use the triangle mesh. Recommended to set VisualScaleMultiplier to lower.")]
        public Mesh CraigleMesh;
        public float VisualScaleMultiplier = 5f; // Since its 100^3 size cube might as well make it slightly larger in render
    }
}
