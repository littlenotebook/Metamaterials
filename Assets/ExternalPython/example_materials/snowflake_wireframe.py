from representation.rep_class import *
from representation.rep_utils import *

# Prepares the node positions of the metamaterial
node_pos = np.zeros(NODE_POS_SIZE)

# Computes the node positions of the metamaterial
node_positions = np.array([
    [1.0,   1.0,   0.5  ],
    [0.375, 1.0,   0.0  ],
    [0.0,   1.0,   0.0  ],
    [0.0,   1.0,   0.375],
    [0.5,   1.0,   1.0  ],
    [0.0,   0.375, 1.0  ],
    [0.0,   0.0,   1.0  ],
    [0.375, 0.0,   1.0  ],
    [1.0,   0.5,   1.0  ],
    [1.0,   0.0,   0.375],
    [1.0,   0.0,   0.0  ],
    [1.0,   0.375, 0.0  ],
])
node_pos[:36] = euclidean_to_pseudo_spherical(node_positions)

# Prepares the edge adjacencies of the metamaterial
edge_adj = np.zeros(EDGE_ADJ_SIZE)

# Prepares the edge parameters of the metamaterial
edge_params = np.zeros(EDGE_PARAMS_SIZE)

# Computes the edge adjacencies/parameters of the metamaterial
for n1 in range(12):
    n2 = (n1+1)%12

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2)
    edge_adj[edge_index] = 1
    edge_index *= EDGE_BEZIER_COORDS

    # Computes straight edge parameters for the snowflake tips
    if (n2 // 2) % 2 == 1:
        fit_edge_params = flat_edge_params(node_positions[min(n1,n2)], node_positions[max(n1,n2)])
    
    # Computed curved edge parameters for non-tips
    elif n1 % 4 == 0:
        pull_point = node_positions[n1].copy() - node_positions[min(n1,n2)]
        pull_point[(n1//4)%3] -= 0.25
        fit_edge_params = np.concatenate([pull_point] * EDGE_BEZIER_POINTS).flatten()
    elif n2 % 4 == 0:
        pull_point = node_positions[n2].copy() - node_positions[min(n1,n2)]
        pull_point[(n2//4+1)%3] -= 0.25
        fit_edge_params = np.concatenate([pull_point] * EDGE_BEZIER_POINTS).flatten()

    # Stores the edge parameters
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)

# Creates the metamaterial
SNOWFLAKE_WIREFRAME = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)