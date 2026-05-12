from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [0.5, 1.,  0. ], # 0
    [0.,  1.,  0.5], # 1
    [0.,  0.5, 1. ], # 2
    [0.5, 0.,  1. ], # 3
    [1.,  0.,  0.5], # 4
    [1.,  0.5, 0. ], # 5
    [0.5, 0.5, 0.5], # 6
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
n3 = 6
for n1 in range(6):
    n2 = (n1+1)%6
    n1,n2 = sorted((n1,n2))

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

    # Computes the best-fit edge/face parameters
    fit_face_params, fit_edge1_params, fit_edge2_params, fit_edge3_params = flat_face_params(node_positions[n1], node_positions[n2], node_positions[n3])

    # Stores the face parameters
    face_params[face_index : face_index + FACE_BEZIER_COORDS] = fit_face_params

    # Computes the edge parameter indices
    edge1_index *= EDGE_BEZIER_COORDS
    edge2_index *= EDGE_BEZIER_COORDS
    edge3_index *= EDGE_BEZIER_COORDS

    # Stores the edge parameters
    edge_params[edge1_index : edge1_index + EDGE_BEZIER_COORDS] = fit_edge1_params
    edge_params[edge2_index : edge2_index + EDGE_BEZIER_COORDS] = fit_edge2_params
    edge_params[edge3_index : edge3_index + EDGE_BEZIER_COORDS] = fit_edge3_params

# Creates the metamaterial
HEXAGON_SHELL = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)