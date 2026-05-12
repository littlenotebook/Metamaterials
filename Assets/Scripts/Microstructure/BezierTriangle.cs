using System.Collections.Generic;
using UnityEngine;

namespace Microstructure
{
    /// <summary>
    /// Bézier triangle utilities.
    ///
    /// A degree-n Bézier triangle has control points Q[i,j,k] where i+j+k=n.
    /// Evaluated at barycentric coords (s,t,u) with s+t+u=1:
    ///   P(s,t,u) = sum_{i+j+k=n} C(n;i,j,k) * s^i * t^j * u^k * Q[i,j,k]
    ///
    /// Control point storage convention:
    ///   Stored as displacements from corner[0] (first node position),
    ///   matching the edge parameter convention.
    ///
    /// Boundary consistency (C0):
    ///   Edge s=0 (t+u=1): Q[0,j,k] = edge(t,u) control points from incident edge
    ///   Edge t=0 (s+u=1): Q[i,0,k] = edge(s,u) control points from incident edge
    ///   Edge u=0 (s+t=1): Q[i,j,0] = edge(s,t) control points from incident edge
    ///   Corners: Q[n,0,0]=p0, Q[0,n,0]=p1, Q[0,0,n]=p2
    /// </summary>
    public static class BezierTriangle
    {
        // ── Control point indexing ────────────────────────────────────────────

        /// <summary>
        /// Returns all (i,j,k) triples with i+j+k=n in lexicographic order.
        /// This is the canonical ordering for control point arrays.
        /// </summary>
        public static List<(int i, int j, int k)> GetIndices(int n)
        {
            var result = new List<(int, int, int)>();
            for (int i = n; i >= 0; i--)
                for (int j = n - i; j >= 0; j--)
                {
                    int k = n - i - j;
                    result.Add((i, j, k));
                }
            return result;
        }

        /// <summary>
        /// Number of control points for degree n: (n+1)(n+2)/2
        /// </summary>
        public static int ControlPointCount(int n) => (n + 1) * (n + 2) / 2;

        /// <summary>
        /// Maps (i,j,k) index to flat array position.
        /// Uses the same ordering as GetIndices.
        /// </summary>
        public static int IndexToFlat(int i, int j, int k, int n)
        {
            var indices = GetIndices(n);
            for (int idx = 0; idx < indices.Count; idx++)
                if (indices[idx].i == i && indices[idx].j == j && indices[idx].k == k)
                    return idx;
            return -1;
        }

        // ── Evaluation ────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate Bézier triangle at barycentric (s,t,u).
        /// controlPts must be in the order given by GetIndices(n).
        /// </summary>
        public static Vector3 Evaluate(Vector3[] controlPts, int n, float s, float t, float u)
        {
            var indices = GetIndices(n);
            Vector3 result = Vector3.zero;
            for (int idx = 0; idx < indices.Count; idx++)
            {
                var (ci, cj, ck) = indices[idx];
                float b = TrinomialBernstein(ci, cj, ck, n, s, t, u);
                result += b * controlPts[idx];
            }
            return result;
        }

        /// <summary>
        /// Trinomial Bernstein basis: C(n;i,j,k) * s^i * t^j * u^k
        /// </summary>
        public static float TrinomialBernstein(int i, int j, int k, int n,
                                               float s, float t, float u)
        {
            return Multinomial(n, i, j, k)
                   * Mathf.Pow(s, i)
                   * Mathf.Pow(t, j)
                   * Mathf.Pow(u, k);
        }

        // ── Control point construction ────────────────────────────────────────

        /// <summary>
        /// Builds the full control point array for degree n, enforcing C0
        /// boundary consistency with the three incident edge curves.
        ///
        /// Corners:    Q[n,0,0]=p0, Q[0,n,0]=p1, Q[0,0,n]=p2
        /// Edge p0-p1 (u=0): sample edge01 Bézier at j/n for j=0..n
        /// Edge p1-p2 (s=0): sample edge12 Bézier at k/n for k=0..n
        /// Edge p2-p0 (t=0): sample edge20 Bézier at i/n for i=0..n
        /// Interior: fitted by regression or set to flat interpolation.
        ///
        /// edgeXX_pts: full world-space control points of each incident edge
        ///             [start, ...interior..., end] — pass null for straight edge
        /// interiorDisplacements: fitted interior control points as displacements
        ///                        from p0 — pass null for flat face
        /// </summary>
        public static Vector3[] BuildControlPoints(
            int n,
            Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3[] edge01_pts,
            Vector3[] edge12_pts,
            Vector3[] edge20_pts,
            List<Vector3> interiorDisplacements)
        {
            var indices = GetIndices(n);
            int count   = indices.Count;
            var Q       = new Vector3[count];
            
            Debug.Log($"[BezierTriangle] BuildControlPoints START: n={n}, " +
                    $"total points={count}, interior displacements={(interiorDisplacements != null ? interiorDisplacements.Count.ToString() : "null")}");
            
            // Debug: Check if edge curves are valid
            Debug.Log($"[BezierTriangle] Edge curves valid? edge01: {edge01_pts != null && edge01_pts.Length > 0}, " +
                    $"edge12: {edge12_pts != null && edge12_pts.Length > 0}, " +
                    $"edge20: {edge20_pts != null && edge20_pts.Length > 0}");
            
            // Debug: Print corners
            Debug.Log($"[BezierTriangle] Corners: p0={p0}, p1={p1}, p2={p2}");

            // ── Corners ───────────────────────────────────────────────────────
            int idx_p0 = IndexToFlat(n, 0, 0, n);
            int idx_p1 = IndexToFlat(0, n, 0, n);
            int idx_p2 = IndexToFlat(0, 0, n, n);
            
            Q[idx_p0] = p0;
            Q[idx_p1] = p1;
            Q[idx_p2] = p2;
            
            Debug.Log($"[BezierTriangle] Set corners at indices {idx_p0}, {idx_p1}, {idx_p2}");

            // ── Boundary edges ────────────────────────────────────────────────
            // u=0 edge: i+j=n, k=0
            for (int j = 1; j < n; j++)
            {
                int i = n - j;
                float t = j / (float)n;
                Vector3 pt = edge01_pts != null && edge01_pts.Length > 0
                    ? EvalBezierArray(edge01_pts, t)
                    : Vector3.Lerp(p0, p1, t);
                int idx = IndexToFlat(i, j, 0, n);
                Q[idx] = pt;
                Debug.Log($"[BezierTriangle] Edge u=0: i={i},j={j}, t={t:F3}, pt={pt}");
            }

            // s=0 edge: j+k=n, i=0
            for (int k = 1; k < n; k++)
            {
                int j = n - k;
                float u = k / (float)n;
                Vector3 pt = edge12_pts != null && edge12_pts.Length > 0
                    ? EvalBezierArray(edge12_pts, u)
                    : Vector3.Lerp(p1, p2, u);
                int idx = IndexToFlat(0, j, k, n);
                Q[idx] = pt;
                Debug.Log($"[BezierTriangle] Edge s=0: j={j},k={k}, u={u:F3}, pt={pt}");
            }

            // t=0 edge: i+k=n, j=0
            for (int i = 1; i < n; i++)
            {
                int k = n - i;
                float s = i / (float)n;
                Vector3 pt = edge20_pts != null && edge20_pts.Length > 0
                    ? EvalBezierArray(edge20_pts, s)
                    : Vector3.Lerp(p2, p0, s);
                int idx = IndexToFlat(i, 0, k, n);
                Q[idx] = pt;
                Debug.Log($"[BezierTriangle] Edge t=0: i={i},k={k}, s={s:F3}, pt={pt}");
            }

            // ── Interior points ───────────────────────────────────────────────
            int dispIdx = 0;
            int interiorCount = 0;

            // Special debug for face 4-6-8 (you can detect by checking p0 coordinates)
            bool isFace468 = (Mathf.Abs(p0.x - 2.0f) < 0.1f && Mathf.Abs(p0.y - 1.0f) < 0.1f) ||
                            (Mathf.Abs(p0.x - 0.0f) < 0.1f && Mathf.Abs(p0.y - 1.0f) < 0.1f);

            for (int idx = 0; idx < count; idx++)
            {
                var (ci, cj, ck) = indices[idx];
                if (ci > 0 && cj > 0 && ck > 0) // Interior point
                {
                    interiorCount++;
                    
                    if (isFace468)
                    {
                        Debug.Log($"[BezierTriangle] Processing interior point for face 4-6-8 at index {idx}");
                        Debug.Log($"[BezierTriangle] interiorDisplacements count: {(interiorDisplacements != null ? interiorDisplacements.Count : 0)}");
                        Debug.Log($"[BezierTriangle] dispIdx: {dispIdx}");
                    }
                    
                    if (interiorDisplacements != null && dispIdx < interiorDisplacements.Count)
                    {
                        Q[idx] = p0 + interiorDisplacements[dispIdx];
                        if (isFace468)
                        {
                            Debug.Log($"[BezierTriangle] Interior point {ci},{cj},{ck} (USING DISPLACEMENT):");
                            Debug.Log($"  p0={p0}");
                            Debug.Log($"  displacement={interiorDisplacements[dispIdx]}");
                            Debug.Log($"  result={Q[idx]}");
                        }
                        dispIdx++;
                    }
                    else
                    {
                        if (isFace468)
                        {
                            Debug.Log($"[BezierTriangle] WARNING: No displacement available for face 4-6-8!");
                            Debug.Log($"  interiorDisplacements is null? {interiorDisplacements == null}");
                            Debug.Log($"  dispIdx={dispIdx}, Count={(interiorDisplacements != null ? interiorDisplacements.Count : 0)}");
                        }
                        // Fallback computation...
                        float s = ci / (float)n;
                        float t = cj / (float)n;
                        float u = ck / (float)n;
                        
                        float param_ab = (t + u) > 1e-6f ? t / (t + u) : 0.5f;
                        float param_bc = (u + s) > 1e-6f ? u / (u + s) : 0.5f;
                        float param_ca = (s + t) > 1e-6f ? s / (s + t) : 0.5f;
                        
                        Vector3 edge_ab = (edge01_pts != null && edge01_pts.Length > 0) 
                            ? EvalBezierArray(edge01_pts, param_ab) 
                            : Vector3.Lerp(p0, p1, t);
                            
                        Vector3 edge_bc = (edge12_pts != null && edge12_pts.Length > 0) 
                            ? EvalBezierArray(edge12_pts, param_bc) 
                            : Vector3.Lerp(p1, p2, u);
                            
                        Vector3 edge_ca = (edge20_pts != null && edge20_pts.Length > 0) 
                            ? EvalBezierArray(edge20_pts, param_ca) 
                            : Vector3.Lerp(p2, p0, s);
                        
                        Q[idx] = (edge_ab + edge_bc + edge_ca) / 3f;
                        
                        if (isFace468)
                        {
                            Debug.Log($"[BezierTriangle] Interior point {ci},{cj},{ck} (COMPUTED FROM EDGES):");
                            Debug.Log($"  edge_ab={edge_ab}");
                            Debug.Log($"  edge_bc={edge_bc}");
                            Debug.Log($"  edge_ca={edge_ca}");
                            Debug.Log($"  result={Q[idx]}");
                        }
                    }
                }
            }

            // // ── Interior points ───────────────────────────────────────────────
            // int dispIdx = 0;
            // int interiorCount = 0;

            // for (int idx = 0; idx < count; idx++)
            // {
            //     var (ci, cj, ck) = indices[idx];
            //     if (ci > 0 && cj > 0 && ck > 0) // Interior point
            //     {
            //         interiorCount++;
            //         if (interiorDisplacements != null && dispIdx < interiorDisplacements.Count)
            //         {
            //             Q[idx] = p0 + interiorDisplacements[dispIdx];
            //             Debug.Log($"[BezierTriangle] Interior point {ci},{cj},{ck} (USING DISPLACEMENT): {Q[idx]}");
            //             dispIdx++;
            //         }
            //         else
            //         {
            //             // ALWAYS compute from edges for consistency
            //             float s = ci / (float)n;
            //             float t = cj / (float)n;
            //             float u = ck / (float)n;
                        
            //             // Sample each edge parameter
            //             float param_ab = (t + u) > 1e-6f ? t / (t + u) : 0.5f;
            //             float param_bc = (u + s) > 1e-6f ? u / (u + s) : 0.5f;
            //             float param_ca = (s + t) > 1e-6f ? s / (s + t) : 0.5f;
                        
            //             Vector3 edge_ab = (edge01_pts != null && edge01_pts.Length > 0) 
            //                 ? EvalBezierArray(edge01_pts, param_ab) 
            //                 : Vector3.Lerp(p0, p1, t);
                            
            //             Vector3 edge_bc = (edge12_pts != null && edge12_pts.Length > 0) 
            //                 ? EvalBezierArray(edge12_pts, param_bc) 
            //                 : Vector3.Lerp(p1, p2, u);
                            
            //             Vector3 edge_ca = (edge20_pts != null && edge20_pts.Length > 0) 
            //                 ? EvalBezierArray(edge20_pts, param_ca) 
            //                 : Vector3.Lerp(p2, p0, s);
                        
            //             // Simple average of the three edge samples works well for C0 continuity
            //             Q[idx] = (edge_ab + edge_bc + edge_ca) / 3f;
                        
            //             Debug.Log($"[BezierTriangle] Interior point {ci},{cj},{ck} (COMPUTED FROM EDGES): " +
            //                     $"params=({param_ab:F2},{param_bc:F2},{param_ca:F2}), result={Q[idx]}");
            //         }
            //     }
            // }
            
            Debug.Log($"[BezierTriangle] BuildControlPoints END: processed {interiorCount} interior points, " +
                    $"used {dispIdx} displacements");
            
            // Verify all control points are set
            for (int idx = 0; idx < count; idx++)
            {
                if (Q[idx] == Vector3.zero)
                {
                    Debug.LogWarning($"[BezierTriangle] Control point at index {idx} is zero! Indices: {indices[idx]}");
                }
            }

            return Q;
        }

        /// <summary>
        /// Number of strictly interior control points for degree n.
        /// Interior means i>0, j>0, k>0 with i+j+k=n.
        /// Formula: (n-1)(n-2)/2 for n>=2, else 0.
        /// </summary>
        public static int InteriorControlPointCount(int n)
        {
            if (n <= 2) return 0;
            return (n - 1) * (n - 2) / 2;
        }

        // ── Least-squares interior fitting ────────────────────────────────────

        /// <summary>
        /// Fits interior control point displacements to a target surface patch.
        ///
        /// targetSamples: list of (barycentric, worldPos) pairs sampled from target
        /// Returns displacements from p0 for all strictly interior control points
        /// (those with i>0, j>0, k>0).
        /// </summary>
        public static List<Vector3> FitInteriorDisplacements(
            int n,
            Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3[] edge01_pts,
            Vector3[] edge12_pts,
            Vector3[] edge20_pts,
            List<(Vector3 bary, Vector3 worldPos)> targetSamples)
        {
            // Get interior indices
            var allIndices = GetIndices(n);
            var interiorIndices = new List<(int idx, int i, int j, int k)>();
            for (int idx = 0; idx < allIndices.Count; idx++)
            {
                var (ci, cj, ck) = allIndices[idx];
                if (ci > 0 && cj > 0 && ck > 0)
                    interiorIndices.Add((idx, ci, cj, ck));
            }

            int M  = interiorIndices.Count; // free unknowns
            int T  = targetSamples.Count;

            if (M == 0 || T == 0)
                return new List<Vector3>();

            // Build boundary contribution Q_boundary for each sample
            // R_k = target_k - sum_{boundary pts} B(...) * Q_boundary
            var boundaryQ = BuildControlPoints(n, p0, p1, p2,
                edge01_pts, edge12_pts, edge20_pts, null);

            // A[k,m] = Bernstein basis for interior point m at sample k
            float[,] A  = new float[T, M];
            var      R  = new Vector3[T];

            for (int k = 0; k < T; k++)
            {
                float s = targetSamples[k].bary.x;
                float t = targetSamples[k].bary.y;
                float u = targetSamples[k].bary.z;

                // Boundary contribution
                Vector3 boundarySum = Vector3.zero;
                for (int idx = 0; idx < allIndices.Count; idx++)
                {
                    var (ci, cj, ck2) = allIndices[idx];
                    if (ci == 0 || cj == 0 || ck2 == 0) // boundary
                        boundarySum += TrinomialBernstein(ci, cj, ck2, n, s, t, u)
                                       * boundaryQ[idx];
                }
                R[k] = targetSamples[k].worldPos - boundarySum;

                // Interior basis values
                for (int m = 0; m < M; m++)
                {
                    var (_, mi, mj, mk) = interiorIndices[m];
                    A[k, m] = TrinomialBernstein(mi, mj, mk, n, s, t, u);
                }
            }

            // Solve normal equations AtA * X = AtR
            float[,] AtA = new float[M, M];
            var      AtR = new Vector3[M];

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < M; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < T; k++) sum += A[k, i] * A[k, j];
                    AtA[i, j] = sum;
                }
                Vector3 rSum = Vector3.zero;
                for (int k = 0; k < T; k++) rSum += A[k, i] * R[k];
                AtR[i] = rSum;
            }

            var solution = GaussianElimination(AtA, AtR, M);
            if (solution == null)
            {
                Debug.LogWarning("[BezierTriangle] Fit failed — using flat face.");
                return new List<Vector3>();
            }

            // Convert to displacements from p0
            var displacements = new List<Vector3>(M);
            for (int m = 0; m < M; m++)
                displacements.Add(solution[m] - p0);

            return displacements;
        }

        /// <summary>
        /// Generates uniform barycentric sample points at resolution L.
        /// Returns (L+1)(L+2)/2 points covering the triangle.
        /// </summary>
        public static List<Vector3> SampleBarycentricGrid(int L)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i <= L; i++)
                for (int j = 0; j <= L - i; j++)
                {
                    int k = L - i - j;
                    pts.Add(new Vector3(i / (float)L, j / (float)L, k / (float)L));
                }
            return pts;
        }

        // ── Mesh sampling ─────────────────────────────────────────────────────

        /// <summary>
        /// Samples the Bézier triangle on a uniform barycentric grid of
        /// resolution L, returning world positions and estimated normals.
        /// </summary>
        public static void SampleGrid(
            Vector3[] controlPts, int n, int L,
            out Vector3[] positions, out Vector3[] normals)
        {
            var bary  = SampleBarycentricGrid(L);
            int count = bary.Count;
            positions = new Vector3[count];
            normals   = new Vector3[count];

            // Precompute a flat face normal as fallback
            Vector3 flatNormal = Vector3.up;
            if (controlPts.Length >= 3)
            {
                int ip0 = IndexToFlat(n, 0, 0, n);
                int ip1 = IndexToFlat(0, n, 0, n);
                int ip2 = IndexToFlat(0, 0, n, n);
                if (ip0 >= 0 && ip1 >= 0 && ip2 >= 0 &&
                    ip0 < controlPts.Length && ip1 < controlPts.Length && ip2 < controlPts.Length)
                {
                    Vector3 ab = controlPts[ip1] - controlPts[ip0];
                    Vector3 ac = controlPts[ip2] - controlPts[ip0];
                    if (ab.sqrMagnitude > 1e-10f && ac.sqrMagnitude > 1e-10f)
                    {
                        Vector3 fn = Vector3.Cross(ab, ac);
                        if (fn.sqrMagnitude > 1e-10f) flatNormal = fn.normalized;
                    }
                }
            }

            for (int idx = 0; idx < count; idx++)
            {
                float s = bary[idx].x;
                float t = bary[idx].y;
                float u = bary[idx].z;

                positions[idx] = Evaluate(controlPts, n, s, t, u);

                // Use a step size proportional to the grid spacing
                float eps = 1e-4f; // Much smaller epsilon for more accurate normals

                // Compute analytic derivatives if possible, otherwise use finite differences
                Vector3 dPds, dPdt;
                
                // Finite difference for dP/ds
                float s1 = Mathf.Clamp01(s + eps);
                float s2 = Mathf.Clamp01(s - eps);
                float scale1 = (1f - s1) / (1f - s + 1e-10f);
                float scale2 = (1f - s2) / (1f - s + 1e-10f);
                
                Vector3 pPlus = Evaluate(controlPts, n, s1, t * scale1, u * scale1);
                Vector3 pMinus = Evaluate(controlPts, n, s2, t * scale2, u * scale2);
                dPds = (pPlus - pMinus) / (2f * eps);

                // Finite difference for dP/dt
                float t1 = Mathf.Clamp01(t + eps);
                float t2 = Mathf.Clamp01(t - eps);
                scale1 = (1f - t1) / (1f - t + 1e-10f);
                scale2 = (1f - t2) / (1f - t + 1e-10f);
                
                pPlus = Evaluate(controlPts, n, s * scale1, t1, u * scale1);
                pMinus = Evaluate(controlPts, n, s * scale2, t2, u * scale2);
                dPdt = (pPlus - pMinus) / (2f * eps);

                // Normal from cross product
                Vector3 normal = Vector3.Cross(dPds, dPdt);

                if (normal.sqrMagnitude < 1e-8f || float.IsNaN(normal.x))
                {
                    normal = flatNormal;
                }
                else
                {
                    normal.Normalize();
                }

                normals[idx] = normal;
            }
        }

        // ── Math helpers ──────────────────────────────────────────────────────

        public static Vector3 EvalBezierArray(Vector3[] pts, float t)
        {
            var work = (Vector3[])pts.Clone();
            int n    = work.Length;
            for (int r = 1; r < n; r++)
                for (int i = 0; i < n - r; i++)
                    work[i] = Vector3.Lerp(work[i], work[i + 1], t);
            return work[0];
        }

        public static int Multinomial(int n, int i, int j, int k)
        {
            return Factorial(n) / (Factorial(i) * Factorial(j) * Factorial(k));
        }

        private static int Factorial(int n)
        {
            int r = 1;
            for (int i = 2; i <= n; i++) r *= i;
            return r;
        }

        private static Vector3[] GaussianElimination(float[,] A, Vector3[] b, int n)
        {
            float[,] M  = (float[,])A.Clone();
            float[]  bx = new float[n],
                     by = new float[n],
                     bz = new float[n];
            for (int i = 0; i < n; i++)
            { bx[i] = b[i].x; by[i] = b[i].y; bz[i] = b[i].z; }

            for (int col = 0; col < n; col++)
            {
                int   pivot  = col;
                float maxVal = Mathf.Abs(M[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    float v = Mathf.Abs(M[row, col]);
                    if (v > maxVal) { maxVal = v; pivot = row; }
                }
                if (maxVal < 1e-10f) return null;
                if (pivot != col)
                {
                    for (int j = 0; j < n; j++)
                        (M[col, j], M[pivot, j]) = (M[pivot, j], M[col, j]);
                    (bx[col], bx[pivot]) = (bx[pivot], bx[col]);
                    (by[col], by[pivot]) = (by[pivot], by[col]);
                    (bz[col], bz[pivot]) = (bz[pivot], bz[col]);
                }
                float diag = M[col, col];
                for (int row = col + 1; row < n; row++)
                {
                    float f = M[row, col] / diag;
                    for (int j = col; j < n; j++) M[row, j] -= f * M[col, j];
                    bx[row] -= f * bx[col];
                    by[row] -= f * by[col];
                    bz[row] -= f * bz[col];
                }
            }

            var x = new Vector3[n];
            for (int i = n - 1; i >= 0; i--)
            {
                float rx = bx[i], ry = by[i], rz = bz[i];
                for (int j = i + 1; j < n; j++)
                { rx -= M[i,j]*x[j].x; ry -= M[i,j]*x[j].y; rz -= M[i,j]*x[j].z; }
                x[i] = new Vector3(rx/M[i,i], ry/M[i,i], rz/M[i,i]);
            }
            return x;
        }
    }
}