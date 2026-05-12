from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [0., 0., 0.], # 0
    [0., 0., 1.], # 1
    [0., 1., 0.], # 2
    [0.5, 0.5, 0.0], # 3
    [1., 1., 0.], # 4
    [1., 0., 0.], # 5
    [1., 0., 1.], # 6
])
node_pos[:21] = euclidean_to_pseudo_spherical(node_positions)

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Computes the edge/face parameters of the metamaterial

straight_edge_nodes = [
    [0,1], [1, 2],
    [2, 3], [3, 4],
    [0, 5], [4, 5],
    [4, 6], [5, 6]
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
    [0, 1, 2], 
    [0, 2, 3],
    [0, 3, 5],
    [3, 4, 5],
    [4, 5, 6],
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


# Creates the metamaterial
PERSONAL_SHELL_FLAT = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)