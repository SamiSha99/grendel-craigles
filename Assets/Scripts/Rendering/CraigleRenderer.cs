using UnityEngine;

namespace Craigles
{
    [RequireComponent(typeof(CraigleSimulation))]
    public class CraigleRenderer : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CraigleSimulation simulation;

        private Mesh mesh;
        private Material material;
        private Matrix4x4[] matrices;
        private Vector4[] colors;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            if (simulation == null) simulation = GetComponent<CraigleSimulation>();
            SwarmConfig config = simulation.Config;

            mesh = config.CraigleMesh != null ? config.CraigleMesh : BuildTriangleMesh();
            material = config.CraigleMaterial != null ? config.CraigleMaterial : BuildInstancedMaterial();
            propertyBlock = new MaterialPropertyBlock();

            int capacity = Mathf.Max(1, config.MaxPopulation);
            matrices = new Matrix4x4[capacity];
            colors = new Vector4[capacity];
        }

        private void LateUpdate()
        {
            var craigles = simulation.Craigles;
            int count = craigles.Count;
            if (count == 0) return;

            float visualScale = simulation.Config.VisualScaleMultiplier;
            for (int i = 0; i < count; i++)
            {
                CraigleData c = craigles[i];
                Quaternion rotation = c.Velocity.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(c.Velocity.normalized)
                    : Quaternion.identity;
                matrices[i] = Matrix4x4.TRS(c.Position, rotation, Vector3.one * (c.Size * visualScale));
                colors[i] = Color.HSVToRGB(c.Hue / 360f, 0.85f, 1f);
            }

            propertyBlock.SetVectorArray(BaseColorId, colors);
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, count, propertyBlock);
        }

        private static Material BuildInstancedMaterial()
        {
            Shader shader = Shader.Find("Craigles/InstancedColor");
            return new Material(shader) { enableInstancing = true };
        }

        private static Mesh BuildTriangleMesh()
        {
            const float radius = 0.5f;
            Vector3 nose = new(0f, 0f, radius);
            Vector3 tailLeft = new(-radius * 0.5f, 0f, -radius * 0.5f);
            Vector3 tailRight = new(radius * 0.5f, 0f, -radius * 0.5f);

            Mesh mesh = new() { name = "CraigleTriangle" };
            mesh.SetVertices(new[]
            {
                nose, tailRight, tailLeft, // front
                nose, tailLeft, tailRight, // back 
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
