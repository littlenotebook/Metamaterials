using System.Collections.Generic;
using UnityEngine;

namespace Microstructure
{
    /// Least-squares fitting.
    public static class BezierFitter
    {
        /// <summary>
        /// Fit M interior Bézier control points to a target polyline.
        ///
        /// Parameters:
        ///   targetPoints  — sampled points on the target curve (T samples)
        ///   endpoint0     — fixed start (node i world position)
        ///   endpoint1     — fixed end   (node j world position)
        ///   M             — number of free interior control points (degree = M+1)
        ///
        /// Returns M control points as displacements from endpoint0.
        /// Returns empty list if M == 0 (straight edge).
        /// </summary>
        public static List<Vector3> FitDisplacements(
            List<Vector3> targetPoints,
            Vector3 endpoint0,
            Vector3 endpoint1,
            int M)
        {
            if (M <= 0)
                return new List<Vector3>(); // straight edge

            int T = targetPoints.Count;
            if (T < 2)
            {
                Debug.LogWarning("[BezierFitter] Need at least 2 target points.");
                return new List<Vector3>();
            }

            // Parameterize target by chord length → t_k in [0,1]
            float[] tParams = ChordLengthParams(targetPoints);

            // Total control points = M+2 (including fixed endpoints p0, pM+1)
            // Free unknowns = interior points p1..pM
            // Bernstein basis degree n = M+1

            int n = M + 1; // degree

            // For each sample t_k, the Bezier value is:
            // C(t_k) = sum_{i=0}^{n} B(i,n,t_k) * P_i
            //
            // Rearranged for fixed endpoints:
            // C(t_k) - B(0,n,t_k)*P0 - B(n,n,t_k)*Pn = sum_{i=1}^{M} B(i,n,t_k)*P_i
            //
            // This is a linear system: A * X = R
            // where A is T×M, X is M×3, R is T×3

            // Build matrix A (T rows, M columns)
            float[,] A = new float[T, M];
            for (int k = 0; k < T; k++)
            {
                float t = tParams[k];
                for (int i = 1; i <= M; i++)
                    A[k, i - 1] = Bernstein(i, n, t);
            }

            // Build RHS: R_k = target_k - B(0,n,t_k)*P0 - B(n,n,t_k)*Pn
            Vector3[] R = new Vector3[T];
            for (int k = 0; k < T; k++)
            {
                float t  = tParams[k];
                float b0 = Bernstein(0, n, t);
                float bn = Bernstein(n, n, t);
                R[k] = targetPoints[k] - b0 * endpoint0 - bn * endpoint1;
            }

            // Solve normal equations: (A^T A) X = A^T R
            // AtA is M×M, AtR is M×3
            float[,] AtA = new float[M, M];
            Vector3[] AtR = new Vector3[M];

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < M; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < T; k++)
                        sum += A[k, i] * A[k, j];
                    AtA[i, j] = sum;
                }

                Vector3 rSum = Vector3.zero;
                for (int k = 0; k < T; k++)
                    rSum += A[k, i] * R[k];
                AtR[i] = rSum;
            }

            // Solve M×M system via Gaussian elimination
            Vector3[] solution = GaussianElimination(AtA, AtR, M);
            if (solution == null)
            {
                Debug.LogWarning("[BezierFitter] Gaussian elimination failed — returning straight edge.");
                return new List<Vector3>();
            }

            // Convert raw control points to displacements from endpoint0
            var displacements = new List<Vector3>(M);
            for (int i = 0; i < M; i++)
                displacements.Add(solution[i] - endpoint0);

            return displacements;
        }

        /// <summary>
        /// Reconstructs absolute world-space control points from stored
        /// displacements and the current endpoint0 (node i position).
        /// Returns the full list including both endpoints:
        ///   [endpoint0, endpoint0+disp[0], ..., endpoint0+disp[M-1], endpoint1]
        /// </summary>
        public static List<Vector3> DisplacementsToWorldPoints(
            List<Vector3> displacements,
            Vector3 endpoint0,
            Vector3 endpoint1)
        {
            var pts = new List<Vector3> { endpoint0 };
            foreach (var d in displacements)
                pts.Add(endpoint0 + d);
            pts.Add(endpoint1);
            return pts;
        }

        /// <summary>
        /// Samples a straight line between p0 and p1 at T uniform parameters.
        /// Used as the default target when no analytic shape is given.
        /// </summary>
        public static List<Vector3> SampleStraightLine(Vector3 p0, Vector3 p1, int T)
        {
            var pts = new List<Vector3>(T);
            for (int k = 0; k < T; k++)
                pts.Add(Vector3.Lerp(p0, p1, k / (float)(T - 1)));
            return pts;
        }

        /// <summary>
        /// Samples a sine-wave arc between p0 and p1 with given amplitude.
        /// Useful for testing curved fitting.
        /// </summary>
        public static List<Vector3> SampleSineArc(
            Vector3 p0, Vector3 p1, float amplitude, int T)
        {
            var pts = new List<Vector3>(T);
            Vector3 axis   = (p1 - p0).normalized;
            Vector3 perp   = Vector3.Cross(axis,
                Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.99f
                    ? Vector3.up : Vector3.right).normalized;

            for (int k = 0; k < T; k++)
            {
                float u = k / (float)(T - 1);
                Vector3 straight = Vector3.Lerp(p0, p1, u);
                float offset = amplitude * Mathf.Sin(u * Mathf.PI);
                pts.Add(straight + perp * offset);
            }
            return pts;
        }

        // ── Math helpers ─────────────────────────────────────────────────────

        /// <summary>Bernstein basis polynomial B(i, n, t).</summary>
        public static float Bernstein(int i, int n, float t)
        {
            return BinomialCoeff(n, i)
                   * Mathf.Pow(t, i)
                   * Mathf.Pow(1f - t, n - i);
        }

        /// <summary>
        /// Evaluate a Bezier curve at parameter t given full control point list
        /// (including endpoints).
        /// </summary>
        public static Vector3 EvalBezier(List<Vector3> controlPts, float t)
        {
            var pts = new List<Vector3>(controlPts);
            int n = pts.Count;
            for (int r = 1; r < n; r++)
                for (int i = 0; i < n - r; i++)
                    pts[i] = Vector3.Lerp(pts[i], pts[i + 1], t);
            return pts[0];
        }

        /// <summary>Chord-length parameterization → t values in [0,1].</summary>
        private static float[] ChordLengthParams(List<Vector3> pts)
        {
            int T = pts.Count;
            float[] chord = new float[T];
            chord[0] = 0f;
            for (int k = 1; k < T; k++)
                chord[k] = chord[k - 1] + Vector3.Distance(pts[k], pts[k - 1]);

            float total = chord[T - 1];
            float[] t = new float[T];
            for (int k = 0; k < T; k++)
                t[k] = total > 0 ? chord[k] / total : k / (float)(T - 1);
            return t;
        }

        /// <summary>Gaussian elimination with partial pivoting. Returns null on failure.</summary>
        private static Vector3[] GaussianElimination(float[,] A, Vector3[] b, int n)
        {
            // Augment [A | b] — store b as 3 separate float arrays
            float[,] M  = (float[,])A.Clone();
            float[] bx  = new float[n];
            float[] by  = new float[n];
            float[] bz  = new float[n];
            for (int i = 0; i < n; i++)
            {
                bx[i] = b[i].x;
                by[i] = b[i].y;
                bz[i] = b[i].z;
            }

            // Forward elimination with partial pivoting
            for (int col = 0; col < n; col++)
            {
                // Find pivot
                int pivot = col;
                float maxVal = Mathf.Abs(M[col, col]);
                for (int row = col + 1; row < n; row++)
                {
                    float v = Mathf.Abs(M[row, col]);
                    if (v > maxVal) { maxVal = v; pivot = row; }
                }

                if (maxVal < 1e-10f)
                    return null; // singular

                // Swap rows
                if (pivot != col)
                {
                    for (int j = 0; j < n; j++)
                    {
                        (M[col, j], M[pivot, j]) = (M[pivot, j], M[col, j]);
                    }
                    (bx[col], bx[pivot]) = (bx[pivot], bx[col]);
                    (by[col], by[pivot]) = (by[pivot], by[col]);
                    (bz[col], bz[pivot]) = (bz[pivot], bz[col]);
                }

                // Eliminate below
                float diag = M[col, col];
                for (int row = col + 1; row < n; row++)
                {
                    float factor = M[row, col] / diag;
                    for (int j = col; j < n; j++)
                        M[row, j] -= factor * M[col, j];
                    bx[row] -= factor * bx[col];
                    by[row] -= factor * by[col];
                    bz[row] -= factor * bz[col];
                }
            }

            // Back substitution
            var x = new Vector3[n];
            for (int i = n - 1; i >= 0; i--)
            {
                float rx = bx[i], ry = by[i], rz = bz[i];
                for (int j = i + 1; j < n; j++)
                {
                    rx -= M[i, j] * x[j].x;
                    ry -= M[i, j] * x[j].y;
                    rz -= M[i, j] * x[j].z;
                }
                x[i] = new Vector3(rx / M[i, i], ry / M[i, i], rz / M[i, i]);
            }

            return x;
        }

        private static int BinomialCoeff(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Mathf.Min(k, n - k);
            int result = 1;
            for (int i = 0; i < k; i++)
            {
                result *= (n - i);
                result /= (i + 1);
            }
            return result;
        }
    }
}