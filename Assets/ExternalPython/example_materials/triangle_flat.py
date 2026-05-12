from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [0., 0., 0.],
    [1., 0., 0.],
    [0., 0., 1.],
])
node_pos[:9] = euclidean_to_pseudo_spherical(node_positions)

# Computes the edge indices
edge1_index = edge_adj_index(0,1) * EDGE_BEZIER_COORDS
edge2_index = edge_adj_index(0,2) * EDGE_BEZIER_COORDS
edge3_index = edge_adj_index(1,2) * EDGE_BEZIER_COORDS

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)
edge_adj[edge1_index // EDGE_BEZIER_COORDS] = 1
edge_adj[edge2_index // EDGE_BEZIER_COORDS] = 1
edge_adj[edge3_index // EDGE_BEZIER_COORDS] = 1

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)
face_index = face_adj_index(0,1,2)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

# Computes the edge/face parameters of the metamaterial
auto_face_params, auto_edge1_params, auto_edge2_params, auto_edge3_params = flat_face_params(node_positions[0], node_positions[1], node_positions[2])

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)
edge_params[edge1_index : edge1_index + EDGE_BEZIER_COORDS] = auto_edge1_params
edge_params[edge2_index : edge2_index + EDGE_BEZIER_COORDS] = auto_edge2_params
edge_params[edge3_index : edge3_index + EDGE_BEZIER_COORDS] = auto_edge3_params

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)
face_params[face_index : face_index + FACE_BEZIER_COORDS] = auto_face_params

# Creates the metamaterial
TRIANGLE_FLAT = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params)