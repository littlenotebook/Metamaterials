from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [1.0, 0.5, 0.0], # 0
    [1.0, 1.0, 0.5], # 1
    [0.5, 1.0, 0.0], # 2

    [0.0, 1.0, 0.0], # 3
    
    [0.0, 1.0, 0.5], # 4
    [0.5, 1.0, 1.0], # 5
    [0.0, 0.5, 1.0], # 6
    
    [0.0, 0.0, 1.0], # 7

    [0.0, 0.0, 0.5], # 8
    [0.0, 0.5, 0.0], # 9
    [0.5, 0.0, 0.0], # 10
    
    [1.0, 0.0, 0.0], # 11

    [1.0, 0.0, 1.0], # 12
])
node_pos[:39] = euclidean_to_pseudo_spherical(node_positions)

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)

# Stores the circle centers
circle_centers = [
    np.array([1.,1.,0.]),
    np.array([0.,1.,1.]),
    np.zeros(3)
]

# Stores the quarter circle edge-node pairings
curved_edge_nodes = [
    [0,1], [1,2], [2,0],
    [4,5], [5,6], [6,4],
    [8,9], [9,10], [10,8],
]

# Computes the quarter circle edge adjacencies/parameters of the metamaterial
for n1, n2 in curved_edge_nodes:
    n1, n2 = sorted((n1, n2))

    # Computes the edge index
    edge_index = edge_adj_index(n1, n2) * EDGE_BEZIER_COORDS

    # Stores the edge parameters
    fit_edge_params = circle_quadrant_edge_params(circle_centers[n1//4], node_positions[n1], node_positions[n2])
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Stores the straight edge-node pairings
straight_edge_nodes = [
    [2,3], [3,4],
    [6,7], [7,8],
    [10,11], [11,0],
    [3,9],
    [10,12], [11,12],
    [7,12], [8,12],
    [6,8], [0,10],
    [4,8], [10,2],
    [4,9], [2,9],
]

# Computes the straight edge adjacencies/parameters of the metamaterial
for n1, n2 in straight_edge_nodes:
    n1, n2 = sorted((n1, n2))

    # Computes the edge index
    edge_index = edge_adj_index(n1, n2) * EDGE_BEZIER_COORDS

    # Stores the edge parameters
    fit_edge_params = flat_edge_params(node_positions[n1], node_positions[n2])
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Stores the flat face-node pairings
flat_face_nodes = [
    [10,11,12],
    [7,8,12],
    [8,10,12],
    [6,7,8],
    [0,10,11],
    [4,8,6],
    [10,2,0],
    [4,8,9],
    [2,9,10],
    [4,3,9],
    [2,9,3],
]

# Computes the flat face adjacencies/parameters of the metamaterial
for n1, n2, n3 in flat_face_nodes:
    n1, n2, n3 = sorted((n1, n2, n3))

    # Computes the edge adjacency indices
    edge1_index = edge_adj_index(n1, n2)
    edge2_index = edge_adj_index(n1, n3)
    edge3_index = edge_adj_index(n2, n3)

    # Sets up the edge adjacencies
    edge_adj[edge1_index] = 1
    edge_adj[edge2_index] = 1
    edge_adj[edge3_index] = 1

    # Computes the edge parameter indices
    edge1_index *= EDGE_BEZIER_COORDS
    edge2_index *= EDGE_BEZIER_COORDS
    edge3_index *= EDGE_BEZIER_COORDS

    # Retrieves the edge parameters
    edge1_params = edge_params[edge1_index : edge1_index+EDGE_BEZIER_COORDS].reshape((EDGE_BEZIER_POINTS, 3))
    edge2_params = edge_params[edge2_index : edge2_index+EDGE_BEZIER_COORDS].reshape((EDGE_BEZIER_POINTS, 3))
    edge3_params = edge_params[edge3_index : edge3_index+EDGE_BEZIER_COORDS].reshape((EDGE_BEZIER_POINTS, 3))

    # Sets up the face adjacency
    face_index = face_adj_index(n1, n2, n3)
    face_adj[face_index] = 1
    face_index *= FACE_BEZIER_COORDS

    # Stores the face parameters (only works when 1 face point)
    face_params[face_index : face_index + FACE_BEZIER_COORDS] = triangle_center(node_positions[n1], node_positions[n2], node_positions[n3]) - node_positions[n1]


# Stores the sphere face-node pairings
sphere_face_nodes = [
    [8,10,9],
    [4,5,6],
    [0,1,2],
]

# Computes the sphere face adjacencies/parameters of the metamaterial
for n1, n2, n3 in sphere_face_nodes:

    # Computes the edge adjacency indices
    edge1_index = edge_adj_index(n1, n2)
    edge2_index = edge_adj_index(n1, n3)
    edge3_index = edge_adj_index(n2, n3)

    # Sets up the edge adjacencies
    edge_adj[edge1_index] = 1
    edge_adj[edge2_index] = 1
    edge_adj[edge3_index] = 1

    # Sets up the face adjacency
    face_index = face_adj_index(n1, n2, n3)
    face_adj[face_index] = 1
    face_index *= FACE_BEZIER_COORDS

    # Computes the face normal/center
    face_normal = np.cross(node_positions[n2]-node_positions[n1], node_positions[n3]-node_positions[n1])
    face_normal /= np.linalg.norm(face_normal)
    face_center = triangle_center(node_positions[n1], node_positions[n2], node_positions[n3])

    # Stores the face parameters (only works when 1 face point)
    face_params[face_index : face_index + FACE_BEZIER_COORDS] = face_center + face_normal*0.5 - node_positions[min(n1,n2,n3)]

# Creates the metamaterial
HOLE_BLOCK_SHELL = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)