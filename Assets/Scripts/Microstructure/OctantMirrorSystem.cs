using System.Collections.Generic;
using UnityEngine;

namespace Microstructure
{
    /// <summary>
    /// Defines all 8 octants of the [0,0,0]→[2,2,2] cube and manages
    /// mirroring edits from the active octant to the other 7.
    /// 
    /// Octant index convention (mirrors Unity's quadrant layout):
    ///   0: [1,1,1]→[2,2,2]  (canonical/core, +x+y+z)
    ///   1: [0,1,1]→[1,2,2]  (-x mirror)
    ///   2: [1,0,1]→[2,1,2]  (-y mirror)
    ///   3: [0,0,1]→[1,1,2]  (-x-y mirror)
    ///   4: [1,1,0]→[2,2,1]  (-z mirror)
    ///   5: [0,1,0]→[1,2,1]  (-x-z mirror)
    ///   6: [1,0,0]→[2,1,1]  (-y-z mirror)
    ///   7: [0,0,0]→[1,1,1]  (-x-y-z mirror)
    /// </summary>
    public class OctantMirrorSystem : MonoBehaviour
    {
        public static OctantMirrorSystem Instance { get; private set; }

        [Header("Octant Settings")]
        [Tooltip("The full microstructure occupies [0,0,0] to [2,2,2]. Centre is (1,1,1).")]
        public Vector3 structureMin = Vector3.zero;
        public Vector3 structureMax = new Vector3(2, 2, 2);
        public int activeOctantIndex = 0; // default: canonical +x+y+z octant

        // Each octant is defined by its min/max bounds and its mirror signs
        public struct OctantDefinition
        {
            public int     index;
            public Vector3 min;
            public Vector3 max;
            public Vector3 mirrorSigns; // (1,1,1) = no mirror, (-1,1,1) = flip X, etc.
            public string  label;
        }

        private OctantDefinition[] _octants;
        public OctantDefinition[] Octants => _octants;

        // Centre of the full structure — the mirror pivot
        private Vector3 _centre;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Debug.LogWarning("Multiple OctantMirrorSystem instances!"); return; }

            _centre = (structureMin + structureMax) * 0.5f; // (1,1,1)
            BuildOctantDefinitions();
        }

        private void BuildOctantDefinitions()
        {
            // Mirror signs for each octant relative to centre (1,1,1)
            // The canonical octant (index 0) is [1,1,1]→[2,2,2], signs = (+1,+1,+1)
            var signs = new Vector3[]
            {
                new Vector3( 1,  1,  1), // 0: canonical
                new Vector3(-1,  1,  1), // 1: flip X
                new Vector3( 1, -1,  1), // 2: flip Y
                new Vector3(-1, -1,  1), // 3: flip X+Y
                new Vector3( 1,  1, -1), // 4: flip Z
                new Vector3(-1,  1, -1), // 5: flip X+Z
                new Vector3( 1, -1, -1), // 6: flip Y+Z
                new Vector3(-1, -1, -1), // 7: flip X+Y+Z
            };

            _octants = new OctantDefinition[8];
            for (int i = 0; i < 8; i++)
            {
                // Derive min/max from signs:
                // +x sign → x in [1,2], -x sign → x in [0,1], etc.
                float xMin = signs[i].x > 0 ? _centre.x : structureMin.x;
                float xMax = signs[i].x > 0 ? structureMax.x : _centre.x;
                float yMin = signs[i].y > 0 ? _centre.y : structureMin.y;
                float yMax = signs[i].y > 0 ? structureMax.y : _centre.y;
                float zMin = signs[i].z > 0 ? _centre.z : structureMin.z;
                float zMax = signs[i].z > 0 ? structureMax.z : _centre.z;

                _octants[i] = new OctantDefinition
                {
                    index       = i,
                    min         = new Vector3(xMin, yMin, zMin),
                    max         = new Vector3(xMax, yMax, zMax),
                    mirrorSigns = signs[i],
                    label       = $"Octant{i}({(signs[i].x>0?"+":"-")}x" +
                                  $"{(signs[i].y>0?"+":"-")}y" +
                                  $"{(signs[i].z>0?"+":"-")}z)"
                };
            }

            Debug.Log($"[OctantMirrorSystem] Built {_octants.Length} octants. " +
                      $"Centre: {_centre}, Active: {_octants[activeOctantIndex].label}");
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the active octant's bounds for clamping node placement.
        /// </summary>
        public OctantDefinition ActiveOctant => _octants[activeOctantIndex];

        /// <summary>
        /// Returns true if the position is within the active octant's valid infinite region.
        /// Only checks the inner boundary planes — no upper/outer clamp.
        /// </summary>
        public bool IsInActiveOctant(Vector3 pos)
        {
            var oct = ActiveOctant;
            bool xValid = oct.mirrorSigns.x > 0 ? pos.x >= _centre.x : pos.x <= _centre.x;
            bool yValid = oct.mirrorSigns.y > 0 ? pos.y >= _centre.y : pos.y <= _centre.y;
            bool zValid = oct.mirrorSigns.z > 0 ? pos.z >= _centre.z : pos.z <= _centre.z;
            return xValid && yValid && zValid;
        }

        /// <summary>
        /// Clamps a position to the active octant's inner boundary only.
        /// Positions beyond the outer face are allowed (infinite outward extent).
        /// </summary>
        public Vector3 ClampToActiveOctant(Vector3 pos)
        {
            var oct = ActiveOctant;
            float x = oct.mirrorSigns.x > 0
                ? Mathf.Max(pos.x, _centre.x)
                : Mathf.Min(pos.x, _centre.x);
            float y = oct.mirrorSigns.y > 0
                ? Mathf.Max(pos.y, _centre.y)
                : Mathf.Min(pos.y, _centre.y);
            float z = oct.mirrorSigns.z > 0
                ? Mathf.Max(pos.z, _centre.z)
                : Mathf.Min(pos.z, _centre.z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Given a world position in the active octant, returns the mirrored
        /// position for the octant at mirrorIndex.
        /// </summary>
        public Vector3 MirrorPosition(Vector3 activePos, int mirrorIndex)
        {
            var active = ActiveOctant;
            var target = _octants[mirrorIndex];

            Vector3 relativeToCenter = activePos - _centre;

            Vector3 mirrored = new Vector3(
                relativeToCenter.x * (target.mirrorSigns.x / active.mirrorSigns.x),
                relativeToCenter.y * (target.mirrorSigns.y / active.mirrorSigns.y),
                relativeToCenter.z * (target.mirrorSigns.z / active.mirrorSigns.z));

            return _centre + mirrored;
        }

        /// <summary>
        /// Returns the movement delta that should be applied to the mirror node
        /// in <paramref name="targetOctantIndex"/> when the source node (in the
        /// active octant) moves by <paramref name="delta"/>.
        ///
        /// Because MirrorPosition maps positions by multiplying their
        /// centre-relative components by (targetSign / activeSign), the same
        /// ratio applies directly to a displacement vector — no translation
        /// by _centre is needed.
        ///
        /// Examples with activeOctant = 0 (all signs +1):
        ///   octant 1 (−x): delta ( 1, 0, 0) → (−1, 0, 0)   X is flipped
        ///   octant 3 (−x−y): delta ( 1, 1, 0) → (−1,−1, 0)  X and Y flipped
        ///   octant 7 (−x−y−z): delta ( 1, 1, 1) → (−1,−1,−1) all flipped
        /// </summary>
        public Vector3 MirrorDelta(Vector3 delta, int targetOctantIndex)
        {
            var active = ActiveOctant;
            var target = _octants[targetOctantIndex];

            // Each component is scaled by the same ratio used in MirrorPosition.
            // Since mirrorSigns are always ±1, this is simply a per-axis sign flip.
            return new Vector3(
                delta.x * (target.mirrorSigns.x / active.mirrorSigns.x),
                delta.y * (target.mirrorSigns.y / active.mirrorSigns.y),
                delta.z * (target.mirrorSigns.z / active.mirrorSigns.z));
        }

        /// <summary>
        /// Returns all 8 mirrored positions for a given active-octant position,
        /// indexed 0-7 (index 0 = the position itself unchanged).
        /// </summary>
        public Vector3[] AllMirroredPositions(Vector3 activePos)
        {
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
                result[i] = MirrorPosition(activePos, i);
            return result;
        }

        /// <summary>
        /// Change which octant is being edited. GraphManager calls this.
        /// </summary>
        public void SetActiveOctant(int index)
        {
            if (index < 0 || index >= 8)
            {
                Debug.LogWarning($"[OctantMirrorSystem] Invalid octant index {index}");
                return;
            }
            activeOctantIndex = index;
            Debug.Log($"[OctantMirrorSystem] Active octant set to {_octants[index].label}");
        }

        /// <summary>
        /// Returns which octant index a world position falls into.
        /// Returns 0 (canonical) if no match found.
        /// </summary>
        public int GetOctantForPosition(Vector3 pos)
        {
            for (int i = 0; i < _octants.Length; i++)
            {
                var oct = _octants[i];
                bool xValid = oct.mirrorSigns.x > 0 ? pos.x >= _centre.x : pos.x <= _centre.x;
                bool yValid = oct.mirrorSigns.y > 0 ? pos.y >= _centre.y : pos.y <= _centre.y;
                bool zValid = oct.mirrorSigns.z > 0 ? pos.z >= _centre.z : pos.z <= _centre.z;
                if (xValid && yValid && zValid)
                    return i;
            }
            Debug.LogWarning($"[OctantMirrorSystem] Position {pos} doesn't fall in any octant — defaulting to 0");
            return 0;
        }
    }
}

// using System.Collections.Generic;
// using UnityEngine;

// namespace Microstructure
// {
//     /// <summary>
//     /// Defines all 8 octants of the [0,0,0]→[2,2,2] cube and manages
//     /// mirroring edits from the active octant to the other 7.
//     /// 
//     /// Octant index convention (mirrors Unity's quadrant layout):
//     ///   0: [1,1,1]→[2,2,2]  (canonical/core, +x+y+z)
//     ///   1: [0,1,1]→[1,2,2]  (-x mirror)
//     ///   2: [1,0,1]→[2,1,2]  (-y mirror)
//     ///   3: [0,0,1]→[1,1,2]  (-x-y mirror)
//     ///   4: [1,1,0]→[2,2,1]  (-z mirror)
//     ///   5: [0,1,0]→[1,2,1]  (-x-z mirror)
//     ///   6: [1,0,0]→[2,1,1]  (-y-z mirror)
//     ///   7: [0,0,0]→[1,1,1]  (-x-y-z mirror)
//     /// </summary>
//     public class OctantMirrorSystem : MonoBehaviour
//     {
//         public static OctantMirrorSystem Instance { get; private set; }

//         [Header("Octant Settings")]
//         [Tooltip("The full microstructure occupies [0,0,0] to [2,2,2]. Centre is (1,1,1).")]
//         public Vector3 structureMin = Vector3.zero;
//         public Vector3 structureMax = new Vector3(2, 2, 2);
//         public int activeOctantIndex = 0; // default: canonical +x+y+z octant

//         // Each octant is defined by its min/max bounds and its mirror signs
//         public struct OctantDefinition
//         {
//             public int     index;
//             public Vector3 min;
//             public Vector3 max;
//             public Vector3 mirrorSigns; // (1,1,1) = no mirror, (-1,1,1) = flip X, etc.
//             public string  label;
//         }

//         private OctantDefinition[] _octants;
//         public OctantDefinition[] Octants => _octants;

//         // Centre of the full structure — the mirror pivot
//         private Vector3 _centre;

//         private void Awake()
//         {
//             if (Instance == null) Instance = this;
//             else { Debug.LogWarning("Multiple OctantMirrorSystem instances!"); return; }

//             _centre = (structureMin + structureMax) * 0.5f; // (1,1,1)
//             BuildOctantDefinitions();
//         }

//         private void BuildOctantDefinitions()
//         {
//             // Mirror signs for each octant relative to centre (1,1,1)
//             // The canonical octant (index 0) is [1,1,1]→[2,2,2], signs = (+1,+1,+1)
//             var signs = new Vector3[]
//             {
//                 new Vector3( 1,  1,  1), // 0: canonical
//                 new Vector3(-1,  1,  1), // 1: flip X
//                 new Vector3( 1, -1,  1), // 2: flip Y
//                 new Vector3(-1, -1,  1), // 3: flip X+Y
//                 new Vector3( 1,  1, -1), // 4: flip Z
//                 new Vector3(-1,  1, -1), // 5: flip X+Z
//                 new Vector3( 1, -1, -1), // 6: flip Y+Z
//                 new Vector3(-1, -1, -1), // 7: flip X+Y+Z
//             };

//             _octants = new OctantDefinition[8];
//             for (int i = 0; i < 8; i++)
//             {
//                 // Derive min/max from signs:
//                 // +x sign → x in [1,2], -x sign → x in [0,1], etc.
//                 float xMin = signs[i].x > 0 ? _centre.x : structureMin.x;
//                 float xMax = signs[i].x > 0 ? structureMax.x : _centre.x;
//                 float yMin = signs[i].y > 0 ? _centre.y : structureMin.y;
//                 float yMax = signs[i].y > 0 ? structureMax.y : _centre.y;
//                 float zMin = signs[i].z > 0 ? _centre.z : structureMin.z;
//                 float zMax = signs[i].z > 0 ? structureMax.z : _centre.z;

//                 _octants[i] = new OctantDefinition
//                 {
//                     index       = i,
//                     min         = new Vector3(xMin, yMin, zMin),
//                     max         = new Vector3(xMax, yMax, zMax),
//                     mirrorSigns = signs[i],
//                     label       = $"Octant{i}({(signs[i].x>0?"+":"-")}x" +
//                                   $"{(signs[i].y>0?"+":"-")}y" +
//                                   $"{(signs[i].z>0?"+":"-")}z)"
//                 };
//             }

//             Debug.Log($"[OctantMirrorSystem] Built {_octants.Length} octants. " +
//                       $"Centre: {_centre}, Active: {_octants[activeOctantIndex].label}");
//         }

//         // ── Public API ───────────────────────────────────────────────────────

//         /// <summary>
//         /// Returns the active octant's bounds for clamping node placement.
//         /// </summary>
//         public OctantDefinition ActiveOctant => _octants[activeOctantIndex];

//         /// <summary>
//         /// Returns true if the position is within the active octant's valid infinite region.
//         /// Only checks the inner boundary planes — no upper/outer clamp.
//         /// </summary>
//         public bool IsInActiveOctant(Vector3 pos)
//         {
//             var oct = ActiveOctant;
//             // Only enforce the inner boundary (the face touching centre at (1,1,1))
//             // Signs tell us which direction is "inward" for this octant
//             // +x sign → x must be >= centre.x, -x sign → x must be <= centre.x
//             bool xValid = oct.mirrorSigns.x > 0 ? pos.x >= _centre.x : pos.x <= _centre.x;
//             bool yValid = oct.mirrorSigns.y > 0 ? pos.y >= _centre.y : pos.y <= _centre.y;
//             bool zValid = oct.mirrorSigns.z > 0 ? pos.z >= _centre.z : pos.z <= _centre.z;
//             return xValid && yValid && zValid;
//         }

//         /// <summary>
//         /// Clamps a position to the active octant's inner boundary only.
//         /// Positions beyond the outer face are allowed (infinite outward extent).
//         /// </summary>
//         public Vector3 ClampToActiveOctant(Vector3 pos)
//         {
//             var oct = ActiveOctant;
//             float x = oct.mirrorSigns.x > 0
//                 ? Mathf.Max(pos.x, _centre.x)   // +x octant: must be >= 1
//                 : Mathf.Min(pos.x, _centre.x);  // -x octant: must be <= 1
//             float y = oct.mirrorSigns.y > 0
//                 ? Mathf.Max(pos.y, _centre.y)
//                 : Mathf.Min(pos.y, _centre.y);
//             float z = oct.mirrorSigns.z > 0
//                 ? Mathf.Max(pos.z, _centre.z)
//                 : Mathf.Min(pos.z, _centre.z);
//             return new Vector3(x, y, z);
//         }

//         /// <summary>
//         /// Given a world position in the active octant, returns the mirrored
//         /// position for the octant at mirrorIndex.
//         /// </summary>
//         public Vector3 MirrorPosition(Vector3 activePos, int mirrorIndex)
//         {
//             var   active = ActiveOctant;
//             var   target = _octants[mirrorIndex];

//             // Compute position relative to active octant's inner corner (the centre)
//             Vector3 relativeToCenter = activePos - _centre;

//             // Apply the relative mirror: divide out active signs, apply target signs
//             Vector3 mirrored = new Vector3(
//                 relativeToCenter.x * (target.mirrorSigns.x / active.mirrorSigns.x),
//                 relativeToCenter.y * (target.mirrorSigns.y / active.mirrorSigns.y),
//                 relativeToCenter.z * (target.mirrorSigns.z / active.mirrorSigns.z));

//             return _centre + mirrored;
//         }

//         /// <summary>
//         /// Returns all 8 mirrored positions for a given active-octant position,
//         /// indexed 0-7 (index 0 = the position itself unchanged).
//         /// </summary>
//         public Vector3[] AllMirroredPositions(Vector3 activePos)
//         {
//             var result = new Vector3[8];
//             for (int i = 0; i < 8; i++)
//                 result[i] = MirrorPosition(activePos, i);
//             return result;
//         }

//         /// <summary>
//         /// Change which octant is being edited. GraphManager calls this.
//         /// </summary>
//         public void SetActiveOctant(int index)
//         {
//             if (index < 0 || index >= 8)
//             {
//                 Debug.LogWarning($"[OctantMirrorSystem] Invalid octant index {index}");
//                 return;
//             }
//             activeOctantIndex = index;
//             Debug.Log($"[OctantMirrorSystem] Active octant set to {_octants[index].label}");
//         }

//         /// <summary>
//         /// Returns which octant index a world position falls into.
//         /// Returns 0 (canonical) if no match found.
//         /// </summary>
//         public int GetOctantForPosition(Vector3 pos)
//         {
//             for (int i = 0; i < _octants.Length; i++)
//             {
//                 var oct = _octants[i];
//                 // Check only inner boundary — no outer cap
//                 bool xValid = oct.mirrorSigns.x > 0 ? pos.x >= _centre.x : pos.x <= _centre.x;
//                 bool yValid = oct.mirrorSigns.y > 0 ? pos.y >= _centre.y : pos.y <= _centre.y;
//                 bool zValid = oct.mirrorSigns.z > 0 ? pos.z >= _centre.z : pos.z <= _centre.z;
//                 if (xValid && yValid && zValid)
//                     return i;
//             }
//             Debug.LogWarning($"[OctantMirrorSystem] Position {pos} doesn't fall in any octant — defaulting to 0");
//             return 0;
//         }
//     }
// }