using System;

// Utility methods mirroring the indexing logic in representation/rep_utils.py
public static class RepUtils
{
    // Computes the flattened edge-adjacency index for two node indices.
    // Mirrors Python's edge_adj_index(node1, node2) behavior.
    public static int EdgeAdjIndex(int node1, int node2, int numNodes)
    {
        if (node1 == node2)
            throw new ArgumentException("node1 and node2 must be different");

        // sort
        if (node1 > node2)
        {
            int t = node1; node1 = node2; node2 = t;
        }

        int offset2d = node1 * (2 * numNodes - node1 - 1) / 2;
        int offset1d = node2 - node1 - 1;

        return offset2d + offset1d;
    }

    // Computes the flattened face-adjacency index for three node indices.
    // Mirrors Python's face_adj_index(node1, node2, node3) behavior.
    public static int FaceAdjIndex(int node1, int node2, int node3, int numNodes)
    {
        // sort
        if (node1 > node2) { int t = node1; node1 = node2; node2 = t; }
        if (node2 > node3) { int t = node2; node2 = node3; node3 = t; }
        if (node1 > node2) { int t = node1; node1 = node2; node2 = t; }

        int a = node1;
        int b = node2;
        int c = node3;

        // offset3d = NUM_NODES * (NUM_NODES-1) * (NUM_NODES-2) // 6 - (NUM_NODES-node1) * (NUM_NODES-node1-1) * (NUM_NODES-node1-2) // 6
        int totalTetrahedral = numNodes * (numNodes - 1) * (numNodes - 2) / 6;
        int remaining = (numNodes - a) * (numNodes - a - 1) * (numNodes - a - 2) / 6;
        int offset3d = totalTetrahedral - remaining;

        // offset2d = (NUM_NODES-node1-1) * (NUM_NODES-node1-2) // 2 - (NUM_NODES-node1-1 - (node2-node1-1)) * (NUM_NODES-node1-1 - (node2-node1-1) - 1) // 2
        int rows = (numNodes - a - 1) * (numNodes - a - 2) / 2;
        int inner = (numNodes - a - 1 - (b - a - 1));
        int innerRows = inner * (inner - 1) / 2;
        int offset2d = rows - innerRows;

        int offset1d = c - b - 1;

        return offset3d + offset2d + offset1d;
    }
}