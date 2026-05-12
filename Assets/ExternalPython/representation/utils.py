import numpy as np
import torch


def greedy_topology_match(start_adj_matrix: np.ndarray, target_adj_matrix: np.ndarray) -> tuple[np.ndarray, list[tuple[int]]]:
    """
    Performs a greedy permutation of `start_adj_matrix` in such a way that
    every step tries to minimize the topology difference (via the edge connections)
    with the target adjacency matrix.

    start_adj_matrix: `np.ndarray`
        The starting adjacency matrix to be permuted greedily.
        Assumes the matrix contains both its upper and lower triangle values, and has
        identical values along the diagonal.

    target_adj_matrix: `np.ndarray`
        The target adjacency matrix to be permuted greedily into.
        Assumes the matrix contains both its upper and lower triangle values, and has
        identical values along the diagonal.

    Returns: `np.ndarray`, `list[tuple[int]]`
        The first returned item is the permuted adjacency matrix that was greedily
        matched to `target_adj_matrix`.
        The second returned item is the sequence of swaps (each relative to the nodes
        at that point in time, not from the beginning) that led to the found matrix.
    """

    # Stores values for the function
    best_matrix = start_adj_matrix.copy()
    best_swaps = []
    num_nodes = best_matrix.shape[0]

    # Continues trying until there are no more greedy choices
    while True:

        # Stores the current edge difference
        current = np.abs(best_matrix-target_adj_matrix).sum()

        # Stores the iteration's greediest step
        best_change = 0
        best_swap = None

        # Runs through each node pair
        for n1 in range(num_nodes):
            for n2 in range(n1+1, num_nodes):
                
                # Simulates this node pair swap
                matrix_copy = best_matrix.copy()
                matrix_copy[[n1,n2],:] = matrix_copy[[n2,n1],:]
                matrix_copy[:,[n1,n2]] = matrix_copy[:,[n2,n1]]

                # Checks if this swap is the best seen so far
                change = current - np.abs(matrix_copy-target_adj_matrix).sum()
                if change > best_change:
                    best_change = change
                    best_swap = (n1,n2)

        # Stop if no greedy swap was found
        if best_swap is None:
            break

        # Performs the best swap
        best_swaps.append(best_swap)
        n1, n2 = best_swap
        best_matrix[[n1,n2],:] = best_matrix[[n2,n1],:]
        best_matrix[:,[n1,n2]] = best_matrix[:,[n2,n1]]

    return best_matrix, best_swaps


NON_COLINEAR_POINT = np.array([np.pi, np.e, np.sqrt(2)])
def find_line_normals(point1: np.ndarray, point2: np.ndarray) -> tuple[np.ndarray]:
    """
    Finds consistent normals for the given line.

    point1: np.ndarray
        The start point on the line. Assumes in the unit cube.

    point2: np.ndarray
        The last point on the line. Assumes in the unit cube.

    Returns: tuple[np.ndarray]
        The two (normalized) normals of the line. Finds
        these deterministically for any given line, assuming
        point1 and point2 are ordered consistently.
    """

    # Finds the plane vectors
    vector1 = point2 - point1
    vector2 = NON_COLINEAR_POINT - point1

    # Finds the plane normal
    normal1 = np.cross(vector1, vector2)
    normal1 /= np.linalg.norm(normal1)

    # Finds the second normal
    normal2 = np.cross(vector1, normal1)
    normal2 /= np.linalg.norm(normal2)

    return normal1, normal2


NON_COLINEAR_POINT_TENSOR = torch.from_numpy(NON_COLINEAR_POINT).to(torch.float32).unsqueeze(0)
def find_line_normals_torch(points1: torch.Tensor, points2: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Finds consistent normals for the given line. Finds these deterministically for any
    given line, assuming point1 and point2 are ordered consistently.

    points1: `torch.Tensor`
        A `(N,3)` float tensor with the start points on the lines.
        Assumes in the unit cube.
        `N` is the number of points.

    point2: `torch.Tensor`
        A `(N,3)` float tensor with the end points on the lines.
        Assumes in the unit cube.
        `N` is the number of points.

    Returns: tuple[torch.Tensor, torch.Tensor]
        A `(N,3)` float tensor with first normal vector.
        
        A `(N,3)` float tensor with second normal vector.

        `N` is the number of points.
    """

    # Finds the plane vectors
    vector1 = points2 - points1
    vector2 = NON_COLINEAR_POINT_TENSOR - points1

    # Finds the plane normal
    normal1 = torch.cross(vector1, vector2, dim=-1)
    normal1 /= normal1.norm(p=2, dim=-1, keepdim=True)

    # Finds the second normal
    normal2 = torch.cross(vector1, normal1, dim=-1)
    normal2 /= normal2.norm(p=2, dim=-1, keepdim=True)

    return normal1, normal2


def rotate_around_axis(vectors: np.ndarray, axis: np.ndarray, angle: float) -> np.ndarray:
    """
    Uses Rodrigues' rotation formula to rotate the given vector
    by the given amount around the given axis.

    vector: np.ndarray
        The vector(s) to be rotated, organized into a 2d
        numpy array in row-major order.

    axis: np.ndarray
        The axis around which the vector(s) will be rotated.
        Assumed to be a 1d array of a unit vector.

    angle: float
        The angle by which the given vector will be rotated.
        Assumed to be in radians.

    Returns: np.ndarray
        The rotated vector(s).
    """

    return (
        vectors * np.cos(angle) +
        np.cross(axis, vectors) * np.sin(angle) +
        np.dot(vectors, axis)[:,np.newaxis] @ axis[np.newaxis,:] * (1 - np.cos(angle))
    )
