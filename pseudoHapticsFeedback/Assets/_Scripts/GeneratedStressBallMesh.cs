using UnityEngine;
using LazySquirrelLabs.SphereGenerator.Generators;

[RequireComponent(typeof(MeshFilter))]
public class GeneratedStressBallMesh : MonoBehaviour {
    [Header("Sphere Settings")]
    [Tooltip("Radius of the generated sphere in local mesh units")]
    public float radius = 0.5f;

    [Tooltip("Icosphere fragmentation depth")]
    [Range(0, 6)]
    public int depth = 3;

    [Header("Generation")]
    [Tooltip("Generate the mesh automatically when the scene starts")]
    public bool generateOnAwake = true;

    [Header("Debug")]
    public int vertexCount = 0;
    public int triangleCount = 0;

    void Awake() {
        // Generate the mesh before the local mesh deformer duplicates it
        if (generateOnAwake) {
            GenerateIcosphere();
        }
    }

    [ContextMenu("Generate Icosphere")]
    public void GenerateIcosphere() {
        // Generate an icosphere and assign it to the MeshFilter
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null) {
            Debug.LogWarning("Generated stress ball mesh missing MeshFilter");
            return;
        }

        IcosphereGenerator generator = new IcosphereGenerator(radius, (ushort)depth);
        Mesh mesh = generator.Generate();

        mesh.name = "Generated Icosphere Depth " + depth;

        meshFilter.mesh = mesh;

        vertexCount = mesh.vertexCount;
        triangleCount = mesh.triangles.Length / 3;

        Debug.Log("Generated icosphere vertices " + vertexCount + " triangles " + triangleCount);
    }
}