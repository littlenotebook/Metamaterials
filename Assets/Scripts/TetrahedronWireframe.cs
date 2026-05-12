using UnityEngine;

public class TetrahedronNodes : MonoBehaviour
{
    [Tooltip("Radius of each node sphere")]
    public float radius = 0.05f;

    void Start()
    {
        // Your tetrahedron node coordinates from Python
        Vector3[] nodes = new Vector3[]
        {
            new Vector3(0f, 1f, 1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 1f, 0f)
        };

        CreateNodes(nodes);
    }

    void CreateNodes(Vector3[] positions)
    {
        float diameter = radius * 2f;

        foreach (Vector3 pos in positions)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            // Remove collider (not needed for visualization)
            Destroy(sphere.GetComponent<Collider>());

            sphere.transform.position = pos - Vector3.one * 0.5f;
            sphere.transform.localScale = Vector3.one * diameter;
            sphere.transform.SetParent(transform, true);

            sphere.name = "Node";
        }
    }
}
