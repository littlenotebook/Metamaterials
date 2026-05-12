from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    # [1.,  0.,  0.5],
    # [0.5, 0.,  1. ],
    # [0.,  0.0, 0.5 ],
    # [0.5,  0.0, 0.0 ],
    # [0.,  0.5,  1.],
    # [0., 1.,  0.5 ],
    # [0.,  0.5, 0.0 ],
    # [0.5,  1.0, 1.0 ],
    # [1.,  1.0, 0.5 ],
    # [0.5,  1.0, 0.0 ],
    # [1.,  0.5, 1.0 ],
    # [1.,  0.5, 0.0 ],

    [1.0, 0.0, 1.0], # 0
    [1., 1., 0.], # 1 
    [0., 1., 1.], # 2
    
])
node_pos[:9] = euclidean_to_pseudo_spherical(node_positions)

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)

circle_centers = [
    np.array([1.0, 0.0, 0.0]),
    np.array([0.0, 1.0, 0.0]),
    np.array([0.0, 0.0, 1.0])
]

for n1 in range(3):
    n2 = (n1+1)%3

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2)
    edge_adj[edge_index] = 1
    edge_index *= EDGE_BEZIER_COORDS

    # Computes the circle center around which the edge goes
    circle_center = circle_centers[n1]

    # Computes the edge parameters
    n1, n2 = sorted((n1, n2))
    fit_edge_params = circle_quadrant_edge_params(circle_center, node_positions[n1], node_positions[n2])

    # Stores the edge parameters
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Creates the metamaterial
PERSONAL_CURVE_WIREFRAME = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)