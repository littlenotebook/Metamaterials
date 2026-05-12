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

# Computes the edge adjacencies/parameters of the metamaterial along its contour
for n1 in range(6):
    n2 = (n1+1)%6

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2) * EDGE_BEZIER_COORDS

    # Computes the circle center around which the edge goes
    if n1 % 2 == 0:
        circle_center = np.zeros(3)
        circle_center[(n1//2+1)%3] = 1
    else:
        circle_center = np.ones(3)
        circle_center[(n1//2)%3] = 0

    # Computes the edge parameters
    n1, n2 = sorted((n1, n2))
    fit_edge_params = circle_quadrant_edge_params(circle_center, node_positions[n1], node_positions[n2])

    # Stores the edge parameters
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

# Stores the edge-node pairs for straight edges
straight_edge_nodes = [
    [2,6], [5,6]
]

# Computes the edge adjacencies/parameters
for n1,n2 in straight_edge_nodes:
    n1,n2 = sorted((n1,n2))

    # Sets up the edge adjacency
    edge_index = edge_adj_index(n1, n2) * EDGE_BEZIER_COORDS

    # Computes the edge parameters
    fit_edge_params = flat_edge_params(node_positions[n1], node_positions[n2])

    # Stores the edge parameters
    edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params

    

def curved_ellipse_point(t, theta):

    ellipse_center = np.array([[
        np.sin(theta) / (np.cos(theta) + np.sin(theta)),
        1.,
        np.cos(theta) / (np.cos(theta) + np.sin(theta))
    ]])

    start_point = np.array([[
        0.5 * np.sin(theta),
        1.,
        0.5 * np.cos(theta)
    ]])

    end_point = ellipse_center.copy()
    end_point[0,1] = 0.5

    return ellipse_center + (start_point-ellipse_center)*np.cos(t) + (end_point-ellipse_center)*np.sin(t)

def edge_function(t, node):
    t /= EDGE_SEGMENTS

    interp = node_positions[node] * (1-t) + node_positions[6] * t

    theta = np.arctan2(interp[0], interp[2])

    return curved_ellipse_point(t*np.pi/2, theta)

last = edge_function(0,1)
distances = np.zeros(0)
for v in range(1,EDGE_SEGMENTS*16+1):
    this = edge_function(v/16,1)
    distances = np.concatenate((distances, np.array([np.sqrt(((last-this)**2).sum())])))
    last = this
distances /= distances.sum()

index_mapping = {0:0, EDGE_SEGMENTS:EDGE_SEGMENTS}
length = 0
t = 1
for v in range(1,EDGE_SEGMENTS*16):
    length += distances[v]
    if length > t/EDGE_SEGMENTS - 1e-4:
        index_mapping[t] = v/16
        t += 1
        if t == EDGE_SEGMENTS:
            break

edge_index = edge_adj_index(1,6) * EDGE_BEZIER_COORDS
fit_edge_params = find_edge_params(lambda t: edge_function(index_mapping[t],1))
edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params



edge_index = edge_adj_index(0,6) * EDGE_BEZIER_COORDS
fit_edge_params = fit_edge_params.reshape((-1,3))
fit_edge_params[:,[0,2]] = fit_edge_params[:,[2,0]]
edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = fit_edge_params.flatten()



edge_index = edge_adj_index(3,6) * EDGE_BEZIER_COORDS
other_edge_params = edge_params[edge_adj_index(0,6)*EDGE_BEZIER_COORDS : edge_adj_index(0,6)*EDGE_BEZIER_COORDS+EDGE_BEZIER_COORDS]
other_edge_params = 1 - (other_edge_params + np.tile(node_positions[0], EDGE_BEZIER_POINTS)) - np.tile(node_positions[3], EDGE_BEZIER_POINTS)
edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = other_edge_params



edge_index = edge_adj_index(4,6) * EDGE_BEZIER_COORDS
other_edge_params = edge_params[edge_adj_index(1,6)*EDGE_BEZIER_COORDS : edge_adj_index(1,6)*EDGE_BEZIER_COORDS+EDGE_BEZIER_COORDS]
other_edge_params = 1 - (other_edge_params + np.tile(node_positions[1], EDGE_BEZIER_POINTS)) - np.tile(node_positions[4], EDGE_BEZIER_POINTS)
edge_params[edge_index : edge_index+EDGE_BEZIER_COORDS] = other_edge_params



# Prepares the face adjacencies of the metamaterial
face_adj = np.zeros(FACE_ADJ_SIZE)

# Prepares the face parameters of the metamaterial
face_params = np.zeros(FACE_PARAMS_SIZE)




face_index = face_adj_index(1,2,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS
face_normal = np.cross(node_positions[6]-node_positions[1], node_positions[2]-node_positions[1])
face_normal /= np.linalg.norm(face_normal)
fit_face_params: np.ndarray = curved_ellipse_point(np.pi/3, np.pi/8) - node_positions[1] + face_normal * 0.05

face_params[face_index : face_index + FACE_BEZIER_COORDS] = fit_face_params



face_index = face_adj_index(0,5,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

face_params[face_index : face_index + FACE_BEZIER_COORDS] = fit_face_params.reshape((-1,3))[:,[2,1,0]].flatten()



face_index = face_adj_index(0,1,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

face_params[face_index : face_index + FACE_BEZIER_COORDS] = curved_ellipse_point(np.pi/6, np.pi/4) - node_positions[0]



face_index = face_adj_index(2,3,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

face_params[face_index : face_index + FACE_BEZIER_COORDS] = 1-(fit_face_params.reshape((-1,3))[:,[2,1,0]].flatten() + node_positions[0]) - node_positions[2]



face_index = face_adj_index(4,5,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

face_params[face_index : face_index + FACE_BEZIER_COORDS] = 1-(fit_face_params + node_positions[1]) - node_positions[4]



face_index = face_adj_index(3,4,6)
face_adj[face_index] = 1
face_index *= FACE_BEZIER_COORDS

face_params[face_index : face_index + FACE_BEZIER_COORDS] = 1-curved_ellipse_point(np.pi/6, np.pi/4) - node_positions[3]




# Creates the metamaterial
SCHWARZ_P_SHELL = Metamaterial(node_pos, edge_adj, edge_params, face_adj, face_params, thickness=0.4)



for n1 in range(NUM_NODES):
    for n2 in range(n1+1, NUM_NODES):
        if SCHWARZ_P_SHELL.has_some_face(n1,n2):
            SCHWARZ_P_SHELL.edge_adj[edge_adj_index(n1,n2)] = 1