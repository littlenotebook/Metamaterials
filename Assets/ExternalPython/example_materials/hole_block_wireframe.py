from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [1.0, 0.5, 0.0],
    [1.0, 1.0, 0.5],
    [0.5, 1.0, 0.0],

    [0.0, 1.0, 0.0],
    
    [0.0, 1.0, 0.5],
    [0.5, 1.0, 1.0],
    [0.0, 0.5, 1.0],
    
    [0.0, 0.0, 1.0],

    [0.0, 0.0, 0.5],
    [0.0, 0.5, 0.0],
    [0.5, 0.0, 0.0],
    
    [1.0, 0.0, 0.0],
])
node_pos[:36] = euclidean_to_pseudo_spherical(node_positions)

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

# Stores the edge-node pairings
curved_edge_nodes = [
    [0,1], [1,2], [2,0],
    [4,5], [5,6], [6,4],
    [8,9], [9,10], [10,8],
]

# Computes the curved edge adjacencies/parameters of the metamaterial
for n1, n2 in curved_edge_nodes:
    n1, n2 = sorted((n1, n2))

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2)
    edge_adj[edge_index] = 1
    edge_index *= EDGE_BEZIER_COORDS

    # Stores the edge parameters
    fit_edge_params = circle_quadrant_edge_params(circle_centers[n1//4], node_positions[n1], node_positions[n2])
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Stores the edge-node pairings
straight_edge_nodes = [
    [2,3], [3,4],
    [6,7], [7,8],
    [10,11], [11,0],
    [3,9],
]

# Computes the straight edge adjacencies/parameters of the metamaterial
for n1, n2 in straight_edge_nodes:
    n1, n2 = sorted((n1, n2))

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2)
    edge_adj[edge_index] = 1
    edge_index *= EDGE_BEZIER_COORDS

    # Stores the edge parameters
    fit_edge_params = flat_edge_params(node_positions[n1], node_positions[n2])
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Creates the metamaterial
HOLE_BLOCK_WIREFRAME = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)