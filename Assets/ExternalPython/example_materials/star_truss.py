from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [0.75, 1.,   0.25, ],
    [1.,   1.,   0.5,  ],
    [1.,   0.75, 0.25, ],
    [0.5,  0.5,  0.,   ],
    
    [1.,   0.25, 0.75, ],
    [1.,   0.5,  1.,   ],
    [0.75, 0.25, 1.,   ],
    [0.5,  0.,   0.5,  ],
    
    [0.25, 0.75, 1.,   ],
    [0.5,  1.,   1.,   ],
    [0.25, 1.,   0.75, ],
    [0.,   0.5,  0.5,  ],
])
node_pos[:36] = euclidean_to_pseudo_spherical(node_positions)

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)

# Stores the edge-node pairings
edge_nodes = [
    [0,1], [1,2],  [2,3],   [3,0],
    [4,5], [5,6],  [6,7],   [7,4],
    [8,9], [9,10], [10,11], [11,8],
    [2,4], [6,8],  [10, 0]
]

# Computes the edge adjacencies/parameters of the metamaterial
for n1, n2 in edge_nodes:

    # Sets up the edge adjacency
    n1,n2 = sorted((n1,n2))
    edge_index = edge_adj_index(n1, n2)
    edge_adj[edge_index] = 1
    edge_index *= EDGE_BEZIER_COORDS

    # Stores the edge parameters
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = flat_edge_params(node_positions[n1], node_positions[n2])

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Creates the metamaterial
STAR_TRUSS = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)