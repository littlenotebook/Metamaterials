import numpy as np
import torch
from math import factorial

# User-controlled properties
NUM_NODES = 13 # Non-center nodes plus the single center node
EDGE_BEZIER_POINTS = 2 # The number of points to describe curved edges
EDGE_SEGMENTS = 16 # The number of segments to use to mesh edges/faces
CUBE_CENTER = np.ones(3)/2 # The center of the metamaterial cube
THICKNESS = 0.1 # The thickness scale of metamaterials

# Automatically-chosen properties
NODE_POS_SIZE = NUM_NODES * 3 # The number of parameters in the node position array
EDGE_ADJ_SIZE = NUM_NODES * (NUM_NODES-1) // 2 # The number of parameters in the edge adjacency array
EDGE_BEZIER_COORDS = EDGE_BEZIER_POINTS * 3 # The number of edge curvature parameters per edge
EDGE_PARAMS_SIZE = EDGE_ADJ_SIZE * EDGE_BEZIER_COORDS # The total number of edge curvature parameters
FACE_ADJ_SIZE = NUM_NODES * (NUM_NODES-1) * (NUM_NODES-2) // 6 # The number of parameters in the face adjacency array
FACE_BEZIER_POINTS = EDGE_BEZIER_POINTS * (EDGE_BEZIER_POINTS-1) // 2 # The number of points to described curved faces
FACE_BEZIER_COORDS = FACE_BEZIER_POINTS * 3 # The number of face curvature parameters per face
FACE_PARAMS_SIZE = FACE_ADJ_SIZE * FACE_BEZIER_COORDS # The total number of face curvature parameters
GLOBAL_PARAMS_SIZE = 1
REP_SIZE = NODE_POS_SIZE + EDGE_ADJ_SIZE + EDGE_PARAMS_SIZE + FACE_ADJ_SIZE + FACE_PARAMS_SIZE + GLOBAL_PARAMS_SIZE # The total number of parameters in the representation

# The device on which tensor computations will be done
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

# Computes the node IDs corresponding to each edge, in order of edge IDs
EDGE_TO_NODES = torch.tensor([
    [n1, n2]
        for n1 in range(NUM_NODES)
            for n2 in range(n1+1, NUM_NODES)
], device=DEVICE)

# Computes the node IDs corresponding to each face, in order of face IDs
FACE_TO_NODES = torch.tensor([
    [n1, n2, n3]
        for n1 in range(NUM_NODES)
            for n2 in range(n1+1, NUM_NODES)
                for n3 in range(n2+1, NUM_NODES)
], device=DEVICE)


def euclidean_to_pseudo_spherical(points: np.ndarray) -> np.ndarray:
    """
    Computes the psuedo-spherical coordinates, centered at `CUBE_CENTER`,
    of the given Euclidean points.

    points: np.ndarray
        A 2D numpy array containing the points whose pseudo-spherical
        coordinates will be computed. The first axis should separate
        different points. The second axis should separate the `(x,y,z)`
        coordinates of a particular point. Assumed points are centered
        at `CUBE_CENTER`. Assumes points are in [0,1].

    Returns: np.ndarray
        A flattened 2D numpy array containing the pseudo-spherical coordinates.
        The first axis separates different pseudo-spherical points.
        The second axis separates the `(radius, theta, phi)` coordinates
        of a particular point. The `radius` value is a value in [0,1].
        At a radius of 0, the point is at `CUBE_CENTER`. At a radius
        of 1, the point is on the surface of the cube. Everything in
        between varies linearly. The `theta` and `phi` values are as
        conventionally defined for spherical coordinates, normalized
        to [0,1].
    """

    # Stores the indices for the different coordinates
    x_index, y_index, z_index = 0, 1, 2

    # Transforms the points to be centered at the origin
    points = points*2 - 1

    # Computes the actual radii
    radius = np.sqrt((points**2).sum(axis=1))

    # Computes the pseudo radii of the points
    pseudo_radius = np.abs(points).max(axis=1)

    # Ignores divide by 0 warnings
    with np.errstate(invalid='ignore'):

        # Computes theta, forcing the center to have theta=0
        theta = np.arccos(points[:,z_index]/radius)
        theta = np.nan_to_num(theta)
        theta /= np.pi

        # Computes phi, forcing the center to have phi=0
        phi = np.arctan2(
            points[:,y_index]/np.sin(theta)/radius,
            points[:,x_index]/np.sin(theta)/radius,
        )
        phi = (phi + 2*np.pi) % (2*np.pi)
        phi = np.nan_to_num(phi)
        phi /= (2*np.pi)

    # Returns the pseudo-spherical coordinates in a stack
    return np.stack([
        pseudo_radius, theta, phi
    ], axis=1).flatten()


def euclidean_to_pseudo_spherical_torch(points: torch.Tensor) -> torch.Tensor:
    """
    Computes the psuedo-spherical coordinates of the given Euclidean points.

    points: `torch.Tensor`
        A `(K,N,3)` tensor with 3D `(x,y,z)` Euclidean coordinates.
        `K` is the number of metamaterial samples.
        `N` is the number of nodes in the metamaterial.
        Assumes points are in [0,1].

    Returns: `torch.Tensor`
        A `(K,N,3)` tensor with 3D `(radius, theta, phi)` pseudo-spherical
        coordinates.
        `K` is the number of metamaterial samples.
        `N` is the number of nodes in the metamaterial.
        All values are linearly normalized to be in [0,1].
        At a radius of 0, the point is at `CUBE_CENTER`. At a radius
        of 1, the point is on the surface of the cube. Everything in
        between varies linearly. The `theta` and `phi` values are as
        conventionally defined for spherical coordinates, normalized
        to [0,1].
    """

    # Stores the indices for the different coordinates
    x_index, y_index, z_index = 0, 1, 2

    # Transforms the points to be centered at the origin
    points = points*2 - 1

    # Computes the actual radii
    radius = torch.sqrt((points**2).sum(dim=2)) # (K,N)

    # Computes the pseudo radii of the points
    pseudo_radius = torch.abs(points).max(dim=2)[0] # (K,N)

    # Computes theta, forcing the center to have theta=0
    theta = torch.arccos(points[:,:,z_index]/radius)
    theta = torch.nan_to_num(theta)
    theta /= np.pi

    # Computes phi, forcing the center to have phi=0
    phi = torch.arctan2(
        points[:,:,y_index]/torch.sin(theta)/radius,
        points[:,:,x_index]/torch.sin(theta)/radius,
    )
    phi = (phi + 2*np.pi) % (2*np.pi)
    phi = torch.nan_to_num(phi)
    phi /= 2*np.pi

    # Returns the pseudo-spherical coordinates in a stack
    return torch.stack([
        pseudo_radius, theta, phi
    ], dim=2)


def pseudo_spherical_to_euclidean(points: np.ndarray) -> np.ndarray:
    """
    Computes the Euclidean coordinates, of the given pseudo-spherical
    points centered at `CUBE_CENTER`.

    points: np.ndarray
        A 2D numpy array containing the pseudo-spherical coordinates.
        The first axis separates different pseudo-spherical points.
        The second axis separates the `(radius, theta, phi)` coordinates
        of a particular point. Assumes the `radius` value is a value in
        [0,1]. At a radius of 0, the point is at `CUBE_CENTER`. At a radius
        of 1, the point is on the surface of the cube. Everything in
        between varies linearly, depending on the other two angles.
        The `theta` and `phi` values are assumed to be as conventionally
        defined for spherical coordinates, but linearly normalized to [0,1].

    Returns: np.ndarray
        A 2D numpy array containing the points whose pseudo-spherical
        coordinates will be computed. The first axis separates
        different points. The second axis separates the `(x,y,z)`
        coordinates of a particular point. Points are centered
        at `CUBE_CENTER`. Points are in [0,1].
    """

    # Stores the pseudo-spherical coordinate values
    radius = points[:,0:1]
    theta = points[:,1] * np.pi
    phi = points[:,2] * 2*np.pi

    # Computes the Euclidian coordinates on the unit sphere
    euclidean_points: np.ndarray = np.stack([
        np.sin(theta) * np.cos(phi),
        np.sin(theta) * np.sin(phi),
        np.cos(theta),
    ], axis=1)

    # Projects the unit sphere onto the unit cube
    euclidean_points /= np.abs(euclidean_points).max(axis=1, keepdims=True)

    # Normalizes the points within the cube based on the pseudo-radius
    euclidean_points *= radius

    # Transforms the points to be within the unit cube
    euclidean_points = (euclidean_points+1)/2

    return euclidean_points


def pseudo_spherical_to_euclidean_torch(points: torch.Tensor) -> torch.Tensor:
    """
    Computes the Euclidean coordinates, of the given pseudo-spherical
    points centered at `CUBE_CENTER`.

    points: np.ndarray
        A 2D numpy array containing the pseudo-spherical coordinates.
        The first axis separates different pseudo-spherical points.
        The second axis separates the `(radius, theta, phi)` coordinates
        of a particular point. Assumes the `radius` value is a value in
        [0,1]. At a radius of 0, the point is at `CUBE_CENTER`. At a radius
        of 1, the point is on the surface of the cube. Everything in
        between varies linearly, depending on the other two angles.
        The `theta` and `phi` values are assumed to be as conventionally
        defined for spherical coordinates, but linearly normalized to [0,1].

    Returns: np.ndarray
        A 2D numpy array containing the points whose pseudo-spherical
        coordinates will be computed. The first axis separates
        different points. The second axis separates the `(x,y,z)`
        coordinates of a particular point. Points are centered
        at `CUBE_CENTER`. Points are in [0,1].
    """

    # Stores the pseudo-spherical coordinate values
    radius = points[..., [0]]
    theta =  points[..., 1] * np.pi
    phi =    points[..., 2] * 2*np.pi

    # Computes the Euclidian coordinates on the unit sphere
    euclidean_points: torch.Tensor = torch.stack([
        torch.sin(theta) * torch.cos(phi),
        torch.sin(theta) * torch.sin(phi),
        torch.cos(theta),
    ], dim=-1)

    # Projects the unit sphere onto the unit cube
    euclidean_points /= torch.abs(euclidean_points).max(dim=-1, keepdim=True)[0]

    # Normalizes the points within the cube based on the pseudo-radius
    euclidean_points *= radius

    # Transforms the points to be within the unit cube
    return (euclidean_points+1)/2


def edge_adj_index(node1: int, node2: int) -> int:
    """
    Computes the index at which the two nodes' edge adjacency is contained
    in the edge adjacency representation array.

    node1: int
        The ID of the first node. Must not be equal to node2. Must be < NUM_NODES.

    node2: int
        The ID of the second node. Must not be equal to node1. Must be < NUM_NODES.

    Returns: int
        The index at which the given nodes' edge adjacency is located in the
        edge adjacency array.
    """

    # Sorts the nodes by ascending index size
    node1, node2 = sorted((node1, node2))

    # Computes the index at which the edge adjacency is in the flattened vector
    offset2d = node1 * (2*NUM_NODES - node1 - 1) // 2
    offset1d = node2 - node1 - 1

    # The idea for offset2d comes from the fact that at the upper triangle, the
    # number of entries per row is a triangular number, starting from the bottom
    # row. Treating node1 as the row index, the parial sum of higher triangular
    # numbers can be computed as the offset2d. Intuitively, offset2d determines
    # which row of the upper triangle is being inspected. The formula used was
    # derived from simple algebraic manipulation of the original expression.

    # The idea for offset1d comes from the fact that as you move down the rows of
    # the upper triangle, the rows become shorter. Intuitively, offset1d
    # determines which column of the upper triangle is being inspected.

    return offset2d + offset1d


# Computes the edge IDs corresponding to each node pair
NODES_TO_EDGE = torch.tensor([[
    edge_adj_index(n1,n2)
        for n1 in range(NUM_NODES)]
            for n2 in range(NUM_NODES)
], device=DEVICE)


def to_edge_adj_rep(edge_adj_matrix: np.ndarray) -> np.ndarray:
    """
    Converts the given edge adjacency matrix into the edge adjacency
    representation array.

    edge_adj_matrix: ndarray
        A matrix where the entry at index (i,j) is 1 if the nodes with
        IDs i and j have an edge between them, or 0 otherwise.

    Returns: ndarray
        The edge adjacency array as described in the Metamaterial initializer.
    """

    # Prepares the rep array
    edge_adj = np.zeros(EDGE_ADJ_SIZE).astype(float)

    # Stores each non-redundant edge adjacency from the matrix
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):
            edge_adj[edge_adj_index(n1, n2)] = edge_adj_matrix[n1, n2]

    return edge_adj


def to_edge_adj_matrix(edge_adj: np.ndarray) -> np.ndarray:
    """
    Converts the given edge adjacency representation into an edge adjacency
    matrix.

    edge_adj: ndarray
        The edge adjacency array as described in the Metamaterial initializer.

    Returns: ndarray
        A matrix where the entry at index (i,j) is 1 if the nodes with
        IDs i and j have an edge between them, or 0 otherwise.
    """

    # Prepares the adjacency matrix
    edge_adj_matrix = np.zeros((NUM_NODES,) * 2)

    # Stores each edge adjacency from the rep
    for n1 in range(NUM_NODES):
        for n2 in range(NUM_NODES):

            # Gets the edge adjacency from the rep
            if n1 != n2:
                edge_adj_matrix[n1, n2] = edge_adj[edge_adj_index(n1, n2)]

    return edge_adj_matrix


def face_adj_index(node1: int, node2: int, node3: int) -> int:
    """
    Computes the index at which the three nodes' face adjacency is contained
    in the face adjacency representation array.

    node1: int
        The ID of the first node. Must not be equal to node2 or node3.
        Must be < NUM_NODES.

    node2: int
        The ID of the second node. Must not be equal to node2 or node3.
        Must be < NUM_NODES.

    node3: int
        The ID of the third node. Must not be equal to node2 or node3.
        Must be < NUM_NODES.

    Returns: int
        The index at which the given nodes' face adjacency is located.
    """

    # Sorts the nodes by ascending index size
    node1, node2, node3 = sorted((node1, node2, node3))

    # Computes the index at which the relevant window of face adjacencies is located
    offset3d = NUM_NODES * (NUM_NODES-1) * (NUM_NODES-2) // 6 - (NUM_NODES-node1) * (NUM_NODES-node1-1) * (NUM_NODES-node1-2) // 6
    offset2d = (NUM_NODES-node1-1) * (NUM_NODES-node1-2) // 2 - (NUM_NODES-node1-1 - (node2-node1-1)) * (NUM_NODES-node1-1 - (node2-node1-1) - 1) // 2
    offset1d = node3 - node2 - 1

    # The idea for offset3d comes from very similar logic as that of offset2d.
    # Namely, the number of entries at each depth along the tensor is a
    # tetrahedral number, starting from the last depth. Intuitively, offset3d
    # determines which depth of the upper tetrahedron is being inspected. The
    # formula used was derived from simple algebraic manipulation of the original
    # expression.

    # The idea for offset2d comes from the fact that at an upper tetrahedron, the
    # number of entries per row is a triangular number, starting from the bottom
    # row. Treating node1 as the row index, the parial sum of higher triangular
    # numbers can be computed as the offset2d. Intuitively, offset2d determines
    # which row of the upper tetrahedron is being inspected. The formula used was
    # derived from simple algebraic manipulation of the original expression.

    # The idea for offset1d comes from the fact that as you move down the rows of
    # the upper tetrahedron, the rows become shorter. Intuitively, offset1d
    # determines which column of the upper tetrahedron is being inspected.

    return offset3d + offset2d + offset1d


def to_face_adj_rep(face_adj_tensor: np.ndarray) -> np.ndarray:
    """
    Converts the given face adjacency tensor into the face adjacency
    representation array.

    face_adj_tensor: ndarray
        A 3D tensor where the entry at index (i,j,k) is 1 if the nodes with
        IDs i, j, and k have a triangular face between them, or 0 otherwise.

    Returns: ndarray
        The face adjacency array as described in the Metamaterial initializer.
    """

    # Prepares the rep array
    face_adj = np.zeros(FACE_ADJ_SIZE).astype(float)

    # Stores each non-redundant face adjacency from the tensor
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):
            for n3 in range(n2+1, NUM_NODES):
                face_adj[face_adj_index(n1, n2, n3)] = face_adj_tensor[n1, n2, n3]

    return face_adj


def to_face_adj_tensor(face_adj: np.ndarray) -> np.ndarray:
    """
    Converts the given face adjacency representation into a face adjacency
    tensor.

    face_adj: ndarray
        The face adjacency array as described in the Metamaterial initializer.

    Returns: ndarray
        A 3D tensor where the entry at index (i,j,k) is 1 if the nodes with
        IDs i, j, and k have a triangular face between them, or 0 otherwise.
    """

    # Prepares the adjacency tensor
    face_adj_matrix = np.zeros((NUM_NODES,) * 3)

    # Stores each face adjacency from the rep
    for n1 in range(NUM_NODES):
        for n2 in range(NUM_NODES):
            for n3 in range(NUM_NODES):

                # Gets the face adjacency from the rep
                if not (n1 == n2 or n1 == n3 or n2 == n3):
                    face_adj_matrix[n1, n2, n3] = face_adj[face_adj_index(n1, n2, n3)]

    return face_adj_matrix


# Computes every monomial used in the Bezier curves/triangles
BEZIER_MONOMIALS = np.array([[
    (t/EDGE_SEGMENTS) ** i
        for i in range(EDGE_BEZIER_POINTS+2)]
            for t in range(EDGE_SEGMENTS+1)
])

# Computes the binomial coefficients for a Bezier curve
BINOMIAL_COEFFICIENTS = np.ones(EDGE_BEZIER_POINTS+2)
for i in range(1,EDGE_BEZIER_POINTS+1):
    BINOMIAL_COEFFICIENTS[i] = BINOMIAL_COEFFICIENTS[i-1] * (EDGE_BEZIER_POINTS+2-i) // i

# Computes the general coefficients for a Bezier curve
BEZIER_CURVE_COEFFICIENTS = BINOMIAL_COEFFICIENTS[np.newaxis,:] * BEZIER_MONOMIALS * BEZIER_MONOMIALS[::-1,::-1]


def find_edge_params(edge_function) -> np.ndarray:
    """
    Runs regression to find the edge parameters that most closely
    produce the given target edge points.

    edge_function: (int) -> np.ndarray
        A function that takes in the Bezier parameter in [0,EDGE_SEGMENTS],
        where t=0 corresponds to the initial point and t=1 to the end point.
        It outputs the corresponding target point on the edge as a 1x3 numpy
        array, where the coordinates are along the second axis.

    Returns: np.ndarray
        The edge parameters that most closely match the given edge
        points, formatted in the way that the Metamaterial class
        edge_params value should be for a particular edge.
    """

    # Separates the node positions (which are fixed)
    node1_pos = edge_function(0)
    node2_pos = edge_function(EDGE_SEGMENTS)

    # Computes the effect of the node positions
    node1_effect = BEZIER_CURVE_COEFFICIENTS[1:-1,:1] @ node1_pos
    node2_effect = BEZIER_CURVE_COEFFICIENTS[1:-1,-1:] @ node2_pos

    # Computes the linear system's target output (the coordinates are on second axis)
    b = np.concatenate([edge_function(t) for t in range(1, EDGE_SEGMENTS)], axis=0)
    b -= node1_effect + node2_effect

    # Computes the linear system's matrix
    A = BEZIER_CURVE_COEFFICIENTS[1:-1,1:-1]

    # Solves the system for each coordinate
    edge_params = np.concatenate([np.linalg.solve(A.T @ A, A.T @ b[:,i]).reshape(EDGE_BEZIER_POINTS, 1) for i in range(3)], axis=1)
    return (edge_params - node1_pos).flatten()


def flat_edge_params(node1_pos: np.ndarray, node2_pos: np.ndarray) -> np.ndarray:
    """
    Finds the edge parameters for costructing a straight edge between
    the two given points.

    node1_pos: np.ndarray
        The 1d array position of the starting node of the edge.

    node2_pos: np.ndarray
        The 1d array position of the ending node of the edge.

    Returns: np.ndarray
        The edge parameters that produce a straight edge between
        the two given points, structured as they are in
        `find_edge_params`.
    """

    # Prepares the function for computing points along the edge
    def edge_function(t):

        # Prepares the Bezier parameter
        t /= EDGE_SEGMENTS

        # Interpolates between the two edge vertices
        return ((1-t)*node1_pos + t*node2_pos)[np.newaxis,:]

    # Computes the best-fit edge parameters
    return find_edge_params(edge_function)


# Computes the multinomial coefficients that are relevant for Bezier triangle interpolation
MULTINOMIAL_COEFFICIENTS = np.zeros((EDGE_BEZIER_POINTS-1, EDGE_BEZIER_POINTS-1))
for i in range(EDGE_BEZIER_POINTS-1):
    for j in range(EDGE_BEZIER_POINTS-1-i):
        MULTINOMIAL_COEFFICIENTS[i,j] = factorial(EDGE_BEZIER_POINTS+1) // factorial(i+1) // factorial(j+1) // factorial(EDGE_BEZIER_POINTS-1-i-j)


def bezier_triangle_coefficients(s: int, t: int) -> np.ndarray:
    """
    Computes the coefficients of a Bezier triangle for
    a given set of parameters. The third parameter is
    inferred from the two given ones.

    s: int
        The first parameter of the triangle, corresponding to
        the first point defining the face.
        Must be in [0,EDGE_SEGMENTS].

    t: int
        The second parameter of the triangle, corresponding to
        the second point defining the face.
        Must be in [0,EDGE_SEGMENTS].

    Returns: np.ndarray
        A 1D numpy array containing the Bezier triangle coefficients.
        The ordering of the triangle coefficients is as follows:

        The first 1 coefficient corresponds to the face's first vertex
        control point.

        The next 1 coefficient corresponds to the face's second vertex
        control point.

        The next 1 coefficient corresponds to the face's third vertex
        control point.

        The next `EDGE_BEZIER_POINTS` coefficients correspond to the
        control points along the edge from the first vertex control point
        to the second vertex control point.

        The next `EDGE_BEZIER_POINTS` coefficients correspond to the
        control points along the edge from the first vertex control point
        to the third vertex control point.

        The next `EDGE_BEZIER_POINTS` coefficients correspond to the
        control points along the edge from the second vertex control point
        to the third vertex control point.

        The last `FACE_BEZIER_POINTS` coefficients correspond to the
        control points in the middle of the triangle.
    """

    # Infers the third parameter
    u = EDGE_SEGMENTS - s - t

    # Computes each coefficient for each control point
    return np.array([
        [
            BEZIER_MONOMIALS[s, EDGE_BEZIER_POINTS+1], # The weight for the face's first vertex control point
            BEZIER_MONOMIALS[t, EDGE_BEZIER_POINTS+1], # The weight for the face's second vertex control point
            BEZIER_MONOMIALS[u, EDGE_BEZIER_POINTS+1], # The weight for the face's third vertex control point
        ] + [
            # The weights for the edge between the face's first and second vertex control points
            BINOMIAL_COEFFICIENTS[i] * BEZIER_MONOMIALS[s,EDGE_BEZIER_POINTS+1-i] * BEZIER_MONOMIALS[t,i]
                for i in range(1,EDGE_BEZIER_POINTS+1)
        ] + [
            # The weights for the edge between the first and third vertex control points
            BINOMIAL_COEFFICIENTS[i] * BEZIER_MONOMIALS[s,EDGE_BEZIER_POINTS+1-i] * BEZIER_MONOMIALS[u,i]
                for i in range(1,EDGE_BEZIER_POINTS+1)
        ] + [
            # The weights for the edge between the second and third vertex control points
            BINOMIAL_COEFFICIENTS[i] * BEZIER_MONOMIALS[t,EDGE_BEZIER_POINTS+1-i] * BEZIER_MONOMIALS[u,i]
                for i in range(1,EDGE_BEZIER_POINTS+1)
        ] + [
            # The weights for the control points in the middle of the triangle
            MULTINOMIAL_COEFFICIENTS[i,j] * BEZIER_MONOMIALS[s, i+1] * BEZIER_MONOMIALS[t, j+1] * BEZIER_MONOMIALS[u, EDGE_BEZIER_POINTS-1-i-j]
                for i in range(EDGE_BEZIER_POINTS-1) for j in range(EDGE_BEZIER_POINTS-1-i) # The number of iterations = FACE_BEZIER_POINTS
        ]
    ])


def bezier_triangle_index(s: int, t: int) -> int:
    """
    Gets the index of the entry in `BEZIER_TRIANGLE_COEFFICIENTS`
    that corresponds to the given triangle parameters. The
    formula is similar in principle to that used in
    `edge_adj_index()`.

    s: int
        The first parameter of the triangle, corresponding to
        the first point defining the face.
        Must be in `[0,EDGE_SEGMENTS]`.

    t: int
        The second parameter of the triangle, corresponding to
        the second point defining the face.
        Must be in `[0,EDGE_SEGMENTS]`.
    """
    return t + s*(3-s)//2 + s*EDGE_SEGMENTS


# Computes the parameters of a Bezier triangle in the order they are used in BEZIER_TRIANGLE_COEFFICIENTS
BEZIER_TRIANGLE_PARAMETERS = np.array([
    [s, t, EDGE_SEGMENTS-s-t] for s in range(EDGE_SEGMENTS+1) for t in range(EDGE_SEGMENTS+1-s)
])
BEZIER_CURVE_COEFFICIENTS_TENSOR = torch.from_numpy(BEZIER_CURVE_COEFFICIENTS).to(DEVICE)

# Computes the general coefficients for a Bezier triangle
BEZIER_TRIANGLE_COEFFICIENTS = np.concatenate([
    bezier_triangle_coefficients(s, t) for s,t,u in BEZIER_TRIANGLE_PARAMETERS
], axis=0)
BEZIER_TRIANGLE_COEFFICIENTS_TENSOR = torch.from_numpy(BEZIER_TRIANGLE_COEFFICIENTS).to(DEVICE)

FACE_VERTEX_INDICES_LIST = []
# Runs through each bottom face
for s in range(EDGE_SEGMENTS):
    for t in range(EDGE_SEGMENTS-s):

        # Stores the point indices
        point1_index = bezier_triangle_index(s,t)
        point2_index = bezier_triangle_index(s+1,t)
        point3_index = bezier_triangle_index(s,t+1)

        # Adds the faces
        FACE_VERTEX_INDICES_LIST.append((point3_index, point2_index, point1_index)) # Bottom face

        # Checks for a second face
        if s+t+2 <= EDGE_SEGMENTS:

            # Stores the new point index
            point4_index = bezier_triangle_index(s+1,t+1)

            # Adds the faces
            FACE_VERTEX_INDICES_LIST.append((point2_index, point3_index, point4_index)) # Bottom face

FACE_VERTEX_INDICES = torch.tensor(FACE_VERTEX_INDICES_LIST)


def find_face_params(edge_params: np.ndarray | None, face_function):
    """
    Runs regression to find the face parameters that most closely
    produce the face points produced by the given function.

    edge_params: nd.array or None
        A 2D numpy array containing the edge parameters to be assumed in the
        regression. The ordering should be that of the edge between the nodes with:
        1) The lowest and second lowest index in the metamaterial.
        2) The lowest and highest index in the metamaterial.
        3) The second lowest and highest index in the metamaterial.
        
        All these nodes index orderings are relative to the nodes
        themselves, i.e., not relative to all nodes in the metamaterial.
        The first axis should separate the different edge parameters,
        ordered as they are ordered in Metamaterial's `edge_params`.
        The second axis should separate the coordinates of a given
        edge parameter.

        If this value is `None`, then the edge parameters are also computed
        and returned by this function.

    face_function: (int, int) -> np.ndarray
        A function that takes in the Bezier parameters in [0,EDGE_SEGMENTS]
        corresponding to the node of lowest and second lowest index,
        respectively, and outputs the corresponding target point on the
        face as a 1x3 numpy array, where the coordinates are along the
        second axis.

    Returns: np.ndarray
        If `edge_params` is not `None`, this returns the face
        parameters that most closely match the target face
        points, formatted in the way that the Metamaterial class
        `face_params` value should be for a particular face.
        
        If `edge_params` is `None`, then this also returns the edge
        parameters, where the first row in the returned array contains
        the face parameters, and the remaining three rows are the
        edge parameters of the three edges defining this face, where the
        edges are ordered as described in `edge_params` description.
    """

    # Computes the node positions
    node1_pos = face_function(EDGE_SEGMENTS, 0)
    node2_pos = face_function(0, EDGE_SEGMENTS)
    node3_pos = face_function(0, 0)

    # Combines the fixed parameters
    fixed_params = np.concatenate([
        node1_pos, node2_pos, node3_pos
    ], axis=0)
    if edge_params is not None:
        fixed_params = np.concatenate([fixed_params, edge_params], axis=0)

    # Defines how to ignore indices of Bezier triangle coefficients/parameters
    if edge_params is not None:
        # Ignores node-only and edge-only points along the face
        is_not_ignored_index = lambda a: a[0] > 0 and a[1] > 0 and a[2] > 0
    else:
        # Ignores node-only points along the face
        is_not_ignored_index = lambda a: a[0] != EDGE_SEGMENTS and a[1] != EDGE_SEGMENTS and a[2] != EDGE_SEGMENTS

    # Keeps only the Bezier triangle coefficients for which the face parameter is non-zero
    relevant_indices = np.array([i for i,a in enumerate(BEZIER_TRIANGLE_PARAMETERS) if is_not_ignored_index(a)])
    relevant_bezier_coefficients = BEZIER_TRIANGLE_COEFFICIENTS[relevant_indices, :]
    relevant_bezier_parameters = BEZIER_TRIANGLE_PARAMETERS[relevant_indices, :]

    # Computes the effect of the node and edge parameters
    cutoff_index = -FACE_BEZIER_POINTS - (EDGE_BEZIER_POINTS*3 if edge_params is None else 0)
    fixed_effect = relevant_bezier_coefficients[:,:cutoff_index] @ fixed_params

    # Computes the linear system's target output (the coordinates are on second axis)
    b = np.concatenate([
        face_function(s,t) for s,t,u in relevant_bezier_parameters
    ], axis=0)
    b -= fixed_effect

    # Computes the linear system's matrix
    A = relevant_bezier_coefficients[:,cutoff_index:]

    # Solves the system for each coordinate
    inferred_params = np.concatenate([np.linalg.solve(A.T @ A, A.T @ b[:,i]).reshape(-cutoff_index, 1) for i in range(3)], axis=1)
    inferred_params = inferred_params.flatten()

    # Returns the correctly formatted parameters
    if edge_params is not None:
        return inferred_params - np.tile(node1_pos[0], FACE_BEZIER_POINTS)
    else:
        return (
            inferred_params[-FACE_BEZIER_COORDS :] - np.tile(node1_pos[0], FACE_BEZIER_POINTS),
            inferred_params[: EDGE_BEZIER_COORDS] - np.tile(node1_pos[0], EDGE_BEZIER_POINTS),
            inferred_params[EDGE_BEZIER_COORDS : EDGE_BEZIER_COORDS*2] - np.tile(node1_pos[0], EDGE_BEZIER_POINTS),
            inferred_params[EDGE_BEZIER_COORDS*2 : -FACE_BEZIER_COORDS] - np.tile(node2_pos[0], EDGE_BEZIER_POINTS),
        )


def flat_face_params(node1_pos: np.ndarray, node2_pos: np.ndarray, node3_pos: np.ndarray) -> np.ndarray:
    """
    Finds the face parameters for costructing a flat face between
    the three given points.

    node1_pos: np.ndarray
        The position of the node of lowest index of the face.

    node2_pos: np.ndarray
        The position of the node of second lowest index of the face.

    node3_pos: np.ndarray
        The position of the node of highest index of the face.

    Returns: np.ndarray
        The face and edge parameters that produce a flat face between
        the three given points, structure as they are structured in
        `find_face_params`.
    """

    # Prepares the function for computing points along the face
    def face_function(s, t):

        # Prepares the Bezier parameters
        s /= EDGE_SEGMENTS
        t /= EDGE_SEGMENTS
        u = 1-s-t

        # Interpolates between the three face vertices
        return (s*node1_pos + t*node2_pos + u*node3_pos)[np.newaxis, :]

    # Computes the best-fit edge/face parameters
    return find_face_params(None, face_function)


def circle_quadrant_edge_params(circle_center: np.ndarray, node1_pos: np.ndarray, node2_pos: np.ndarray) -> np.ndarray:
    """
    Computes the edge parameters that describe the circle quadrant
    between the two given nodes with the given center.

    circle_center: np.ndarray
        The center of the circle to be described.

    node1_pos: np.ndarray
        The position of the starting node. Must have a node index
        in its metamaterial lower than the other given node.

    node2_pos: np.ndarray
        The position of the ending node. Must have a node index
        in its metamaterial higher than the other given node.

    Returns: np.ndarray
        The edge parameters that describe the specified circle quadrant.
    """

    # Computes the cos() and sin() coefficients
    cos_coeff = node1_pos - circle_center
    sin_coeff = node2_pos - circle_center

    # Defines the function producing edge points
    def edge_function(t):

        # Normalizes the Bezier parameter
        t /= EDGE_SEGMENTS
        t *= np.pi/2

        # Returns the interpolated edge point
        return (circle_center + cos_coeff*np.cos(t) + sin_coeff*np.sin(t))[np.newaxis,:]
    
    return find_edge_params(edge_function)


def triangle_center(vertex1: np.ndarray, vertex2: np.ndarray, vertex3: np.ndarray) -> np.ndarray:
    """
    Gives an approximation for the triangle center based on
    its vertices. Assumes the vertices describe a valid
    triangle.

    vertex1: np.ndarray
        One of the vertices of the triangle.

    vertex2: np.ndarray
        Another of the vertices of the triangle.

    vertex3: np.ndarray
        Another of the vertices of the triangle.
    """

    # Returns the vertex average
    return (vertex1 + vertex2 + vertex3) / 3


def sphere_octant_face_params(sphere_center: np.ndarray, node1_pos: np.ndarray, node2_pos: np.ndarray, node3_pos: np.ndarray) -> np.ndarray:
    """
    Computes the face parameters that describe the sphere octant
    between the three given nodes with the given center.

    sphere_center: np.ndarray
        The center of the sphere to be described.

    node1_pos: np.ndarray
        The position of the node corresponding to the first Bezier
        parameter. Must have a node index in its metamaterial lower
        than the other given nodes.

    node2_pos: np.ndarray
        The position of the node corresponding to the second Bezier
        parameter. Must have a node index in its metamaterial 
        between the other given nodes.

    node3_pos: np.ndarray
        The position of the node corresponding to the third Bezier
        parameter. Must have a node index in its metamaterial higher
        than the other given nodes.

    Returns: tuple of np.ndarray
        The face/edge parameters that describe the specified sphere
        octant. The face parameters are returned as the first item,
        followed by the edge parameters ordered as described in
        `find_face_params()`.
    """

    coeff1 = node1_pos - sphere_center
    coeff2 = node2_pos - sphere_center
    coeff3 = node3_pos - sphere_center

    # Defines the face function
    def face_function(s, t):

        # Normalizes the Bezier parameters
        s /= EDGE_SEGMENTS
        t /= EDGE_SEGMENTS
        u = 1-s-t

        # Transforms into sphere coordinates
        s = np.sqrt(s)
        t = np.sqrt(t)
        u = np.sqrt(u)

        return (sphere_center + s*coeff1 + t*coeff2 + u*coeff3)[np.newaxis,:]

    return find_face_params(None, face_function)

