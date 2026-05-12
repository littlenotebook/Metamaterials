using UnityEngine;

public static class HandleFactory
{
    public static GameObject CreateGhostHandle(Material ghostMat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.localScale = Vector3.one * 0.2f;
        Object.Destroy(go.GetComponent<Collider>());

        Renderer r = go.GetComponent<Renderer>();
        r.material = ghostMat;

        go.name = "GhostHandle";
        return go;
    }

    public static GameObject CreateAxisHandle(Material axisMat, Vector3 direction)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());

        go.transform.localScale = new Vector3(0.1f, 0.1f, 0.7f);
        go.transform.forward = direction;

        go.GetComponent<Renderer>().material = axisMat;

        go.name = "AxisHandle";
        return go;
    }
}


// using UnityEngine;

// public static class HandleFactory
// {
//     public static GameObject CreateGhostHandle(Material mat)
//     {
//         GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//         g.transform.localScale = Vector3.one * 0.15f;
//         g.GetComponent<Collider>().enabled = false;

//         Renderer r = g.GetComponent<Renderer>();
//         r.material = mat;

//         return g;
//     }

//     public static GameObject CreateAxisHandle(Material mat, Vector3 direction)
//     {
//         GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
//         arrow.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
//         arrow.transform.localRotation = Quaternion.LookRotation(direction);
//         arrow.GetComponent<Collider>().enabled = false;

//         Renderer r = arrow.GetComponent<Renderer>();
//         r.material = mat;

//         return arrow;
//     }
// }
