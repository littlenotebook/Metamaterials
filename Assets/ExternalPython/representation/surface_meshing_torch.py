import torch
import trimesh
from representation.rep_class import *
from representation.generation import *
from representation.rep_utils import *


def generate_base_node_mesh() -> tuple[torch.Tensor, np.ndarray]:
    """
    Generates the vertices and faces for a generic node mesh.

    Returns: `tuple[torch.Tensor, np.ndarray]`
        A `(V,3)` float tensor with the base node vertex coordinates.
        `V` is the number of vertices in the node mesh.
        
        A `(F,3)` int numpy array with the base node vertex indices for each face.
        `F` is the number of faces in the node mesh.
    """

    # Stores convenient values for the function
    node_segments = EDGE_SEGMENTS//2

    # Pre-computes the number of vertices/faces
    num_vertices = node_segments * (node_segments - 1) + 2
    num_faces = node_segments * (node_segments - 1) * 2

    # Will store the vertices/faces
    vertices = torch.zeros((num_vertices,3), device=DEVICE)
    faces = np.zeros((num_faces,3), dtype=np.int32)

    # Stores the current vertex/face index
    vertex_index, face_index = 0, 0

    # Computes angle values
    def thetas():
        for i in range(node_segments+1):
            yield i, i/node_segments * np.pi
    def phis():
        for i in range(node_segments):
            yield i, i/node_segments * 2*np.pi

    # Stores the base coordinates
    i_hat = torch.tensor([1.0, 0.0, 0.0], device=DEVICE)
    j_hat = torch.tensor([0.0, 1.0, 0.0], device=DEVICE)
    k_hat = torch.tensor([0.0, 0.0, 1.0], device=DEVICE)

    # Runs through each point on the surface of the sphere
    for i,theta in thetas():

        # Computes the z Euclidean coordinate
        z = k_hat * np.cos(theta)

        # Handles all other points in between
        sin_theta = np.sin(theta)
        for j,phi in phis():

            # Computes the Euclidean coordinates
            x = i_hat * sin_theta * np.cos(phi)
            y = j_hat * sin_theta * np.sin(phi)

            # Stores the vertices along the ring
            vertices[vertex_index] = x+y+z
            vertex_index += 1

            # Skips redundantly computing the poles
            if i == 0 or i == node_segments:
                break

        # Computes the face vertices
        for j in range(node_segments):

            # Skips when only the north pole point has been added
            if i == 0:
                break

            # Handles the faces touching the north pole
            if i == 1:
                faces[face_index] = np.array([0, 1 + j, 1 + (j+1)%node_segments])
                face_index += 1
                continue

            # Handles the faces touching the south pole
            if i == node_segments:
                offset = vertex_index-node_segments-1
                faces[face_index] = np.array([offset + (j+1)%node_segments, offset + j, offset + node_segments])
                face_index += 1
                continue

            # Handles all other faces
            ring1_index = vertex_index-2*node_segments
            ring2_index = vertex_index-node_segments
            faces[face_index:face_index+2] = np.array([
                [ring2_index + (j+1)%node_segments, ring1_index + (j+1)%node_segments, ring1_index + j],
                [ring1_index + j, ring2_index + j, ring2_index + (j+1)%node_segments]
            ])
            face_index += 2

    return vertices, faces

NODE_BASE_VERTICES, NODE_BASE_FACES = generate_base_node_mesh()


def generate_node_surface_meshes(material: Metamaterial) -> tuple[list[np.ndarray], list[np.ndarray]]:
    """
    Generates the vertices and faces for the active nodes of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose nodes will be meshed.

    Returns: `tuple[list[np.ndarray], list[np.ndarray]]`
        A list of `(V,3)` float numpy arrays with the nodes' vertex coordinates.
        `V` is the number of vertices in each node mesh.

        A list of `(F,3)` int numpy arrays with the nodes' face vertex indices.
        `F` is the number of faces in each node mesh.
    """

    # Computes the node position
    centers = material.get_active_node_positions()

    # Geometric properties of the node meshes
    active_nodes = centers.shape[0]
    sphere_radius = material.get_thickness() * 51/50

    # Computes the vertices
    vertices = (NODE_BASE_VERTICES.unsqueeze(0) * sphere_radius + centers.unsqueeze(1)).numpy()

    return [vertices[node] for node in range(active_nodes)], [NODE_BASE_FACES for _ in range(active_nodes)]


def generate_base_edge_mesh() -> tuple[torch.Tensor, torch.Tensor, np.ndarray]:
    """
    Generates the base edge mesh parts.

    Returns: `tuple[torch.Tensor, torch.Tensor, np.ndarray]`
        A `(V,1)` float tensor with the edge ring weights along the first normal direction.
        `V` is the number of vertices in the edge mesh.

        A `(V,1)` float tensor with the edge ring weights along the second normal direction.
        `V` is the number of vertices in the edge mesh.
        
        A `(F,3)` int numpy array with the base edge vertex indices for each face.
        `F` is the number of faces in the edge mesh.
    """

    # Computes the theta values
    base_theta = 2 * np.pi / EDGE_SEGMENTS
    theta = base_theta * torch.arange(EDGE_SEGMENTS, dtype=torch.float32, device=DEVICE).repeat(EDGE_SEGMENTS+1).reshape((1,-1,1))

    # Computes the edge normal direction points
    normal1_weights = torch.cos(theta)
    normal2_weights = torch.sin(theta)

    # Will store the faces
    num_faces = 2 * (EDGE_SEGMENTS**2 + EDGE_SEGMENTS - 2)
    faces = np.zeros((num_faces, 3), dtype=np.int32)
    
    # Stores values for face computation
    face_index = 0
    vertex_count = 0

    # Computes the face corresponding to each edge segment
    for edge in range(EDGE_SEGMENTS+1):

        # Adds the face at the ends of the cylindrical edge
        if edge == 0:
            faces[face_index:face_index+EDGE_SEGMENTS-2] = np.array([[vertex_count+i+1, vertex_count+i, vertex_count] for i in range(1,EDGE_SEGMENTS-1)])
            face_index += EDGE_SEGMENTS-2
        elif edge == EDGE_SEGMENTS:
            faces[face_index:face_index+EDGE_SEGMENTS-2] = np.array([[vertex_count, vertex_count+i, vertex_count+i+1] for i in range(1,EDGE_SEGMENTS-1)])
            face_index += EDGE_SEGMENTS-2

        # Adds the faces that connect edge segments
        if edge != 0:
            for i in range(EDGE_SEGMENTS):
                faces[face_index:face_index+2] = np.array([
                    [vertex_count-EDGE_SEGMENTS+i, vertex_count-EDGE_SEGMENTS+(i+1)%EDGE_SEGMENTS, vertex_count+(i+1)%EDGE_SEGMENTS],
                    [vertex_count+(i+1)%EDGE_SEGMENTS, vertex_count+i, vertex_count-EDGE_SEGMENTS+i]
                ])
                face_index += 2

        # Updates the vertex count
        vertex_count += EDGE_SEGMENTS

    return normal1_weights, normal2_weights, faces

EDGE_BASE_NORMAL1_WEIGHTS, EDGE_BASE_NORMAL2_WEIGHTS, EDGE_BASE_FACES = generate_base_edge_mesh()


def generate_edge_surface_meshes(material: Metamaterial) -> tuple[list[np.ndarray], list[np.ndarray]]:
    """
    Generates the vertices and faces for all active edges of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose edges will be meshed.

    Returns: `tuple[list[np.ndarray], list[np.ndarray]]`
        A list of `(V,3)` float numpy arrays with the edges' vertex coordinates.
        `V` is the number of vertices in each edge mesh.

        A list of `(F,3)` int numpy arrays with the edges' face vertex indices.
        `F` is the number of faces in each edge mesh.
    """

    # Computes the active edges
    active_edges = torch.nonzero(torch.from_numpy(material.edge_adj)).squeeze(1)
    num_edges = active_edges.shape[0]

    # Checks if there are any edges
    if num_edges == 0:
        return [], []

    # Computes the edge points
    edge_points = material.compute_many_edge_points(active_edges)

    # Computes starting normals
    normal1, normal2 = find_line_normals_torch(edge_points[:,0], edge_points[:,1])

    # Will store all the normal vectors
    normals1, normals2 = [normal1], [normal2]

    # Computes the remaning normals
    for edge in range(EDGE_SEGMENTS-1):

        # Computes the direction of the edge segment to be formed
        edge_dir = edge_points[:,edge+1] - edge_points[:,edge]

        # Computes the new normals
        normal2 = torch.cross(edge_dir, normal1, dim=-1)
        normal2 /= normal2.norm(p=2, dim=-1, keepdim=True)
        normal1 = torch.cross(normal2, edge_dir, dim=-1)
        normal1 /= normal1.norm(p=2, dim=-1, keepdim=True)

        # Stores the normals
        normals1.append(normal1)
        normals2.append(normal2)

    # Computes geometric properties of the material
    thickness = material.get_thickness() * 101/100

    # Formats the normals as needed for the base edge vertices tensor
    all_normals1 = torch.stack(normals1, dim=1).repeat_interleave(EDGE_SEGMENTS+1, 1)
    all_normals2 = torch.stack(normals2, dim=1).repeat_interleave(EDGE_SEGMENTS+1, 1)

    # Computes the vertices
    edge_points = edge_points.repeat_interleave(EDGE_SEGMENTS, 1)
    vertices = (edge_points + EDGE_BASE_NORMAL1_WEIGHTS * thickness * all_normals1 + EDGE_BASE_NORMAL2_WEIGHTS * thickness * all_normals2)

    return [vertices[edge] for edge in range(num_edges)], [EDGE_BASE_FACES for _ in range(num_edges)]


def generate_base_face_mesh() -> tuple[torch.Tensor, np.ndarray]:
    """
    Generates the base face mesh parts.

    Returns: `tuple[torch.Tensor, np.ndarray]`
        A `(V,3)` int tensor with the base face normal indices for each vertex.
        `V` is the number of vertices in the face mesh.

        A `(F,3)` int numpy array with the base face vertex indices for each face.
        `F` is the number of faces in the face mesh.
    """
    
    # Computes the face normal indices
    num_points = (EDGE_SEGMENTS+2) * (EDGE_SEGMENTS+1) // 2
    normal_index = 0
    vertex_normal_indices = torch.zeros((num_points, 3), dtype=torch.int32, device=DEVICE)
    for s,t,u in BEZIER_TRIANGLE_PARAMETERS:

        # Stores the base point for the normal
        base_point = bezier_triangle_index(s,t)

        # Stores the points from which the normal is deduced
        if s+t != EDGE_SEGMENTS:
            point1 = bezier_triangle_index(s+1,t)
            point2 = bezier_triangle_index(s,t+1)

        elif s == EDGE_SEGMENTS:
            point1 = bezier_triangle_index(s-1,t+1)
            point2 = bezier_triangle_index(s-1,t)

        elif t == EDGE_SEGMENTS:
            point1 = bezier_triangle_index(s,t-1)
            point2 = bezier_triangle_index(s+1,t-1)

        else:
            point1 = bezier_triangle_index(s-1,t+1)
            point2 = bezier_triangle_index(s-1,t)

        # Stores the indices
        vertex_normal_indices[normal_index] = torch.tensor([base_point, point1, point2])
        normal_index += 1

    # Will store the faces
    num_faces = 8 * EDGE_SEGMENTS + 4 * EDGE_SEGMENTS * (EDGE_SEGMENTS-1) // 2
    face_index = 0
    faces = np.zeros((num_faces, 3), dtype=np.int32)

    # Runs through each top/bottom face
    for s in range(EDGE_SEGMENTS):
        for t in range(EDGE_SEGMENTS-s):

            # Stores the point indices
            point1_index = bezier_triangle_index(s,t)*2
            point2_index = bezier_triangle_index(s+1,t)*2
            point3_index = bezier_triangle_index(s,t+1)*2

            # Adds the faces
            faces[face_index:face_index+2] = np.array([
                [point3_index, point2_index, point1_index], # Bottom face
                [point1_index+1, point2_index+1, point3_index+1] # Top face
            ])
            face_index += 2

    # Checks for a second face
    for s in range(EDGE_SEGMENTS):
        for t in range(EDGE_SEGMENTS-s):
            if s+t+2 <= EDGE_SEGMENTS:

                # Stores the point indices
                point2_index = bezier_triangle_index(s+1,t)*2
                point3_index = bezier_triangle_index(s,t+1)*2
                point4_index = bezier_triangle_index(s+1,t+1)*2

                # Adds the faces
                faces[face_index:face_index+2] = np.array([
                    [point2_index, point3_index, point4_index], # Bottom face
                    [point4_index+1, point3_index+1, point2_index+1]# Top face
                ])
                face_index += 2

    # Runs through the edge faces
    for t in range(EDGE_SEGMENTS):
        s = EDGE_SEGMENTS-t # Computes the opposite parameter

        # Stores the u=0 edge faces
        faces[face_index] = np.array([
            bezier_triangle_index(s,t)*2,
            bezier_triangle_index(s-1,t+1)*2,
            bezier_triangle_index(s-1,t+1)*2+1,
        ])
        faces[face_index+1] = np.array([
            bezier_triangle_index(s-1,t+1)*2+1,
            bezier_triangle_index(s,t)*2+1,
            bezier_triangle_index(s,t)*2,
        ])
        face_index += 2

        # Stores the t=0 edge faces
        faces[face_index] = np.array([
            bezier_triangle_index(t,0)*2,
            bezier_triangle_index(t+1,0)*2,
            bezier_triangle_index(t+1,0)*2+1,
        ])
        faces[face_index+1] = np.array([
            bezier_triangle_index(t+1,0)*2+1,
            bezier_triangle_index(t,0)*2+1,
            bezier_triangle_index(t,0)*2,
        ])
        face_index += 2

        # Stores the s=0 edge faces
        faces[face_index] = np.array([
            bezier_triangle_index(0,t+1)*2+1,
            bezier_triangle_index(0,t+1)*2,
            bezier_triangle_index(0,t)*2,
        ])
        faces[face_index+1] = np.array([
            bezier_triangle_index(0,t)*2,
            bezier_triangle_index(0,t)*2+1,
            bezier_triangle_index(0,t+1)*2+1,
        ])
        face_index += 2

    return vertex_normal_indices, faces

FACE_BASE_NORMAL_INDICES, FACE_BASE_FACES = generate_base_face_mesh()


def generate_face_surface_meshes(material: Metamaterial) -> tuple[list[np.ndarray], list[np.ndarray]]:
    """
    Generates the vertices and faces for all active faces of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose faces will be meshed.

    Returns: `tuple[list[np.ndarray], list[np.ndarray]]`
        A list of `(V,3)` float numpy arrays with the faces' vertex coordinates.
        `V` is the number of vertices in each face mesh.

        A list of `(F,3)` int numpy arrays with the faces' face vertex indices.
        `F` is the number of faces in each face mesh.
    """

    # Computes the active faces
    active_faces = torch.nonzero(torch.from_numpy(material.face_adj)).squeeze(1)
    num_faces = active_faces.shape[0]

    # Checks if there are any faces
    if num_faces == 0:
        return [], []

    # Computes the face points
    face_points = material.compute_many_face_points(active_faces)

    # Stores the vertex indices
    face_normals = torch.cross(
        face_points[:,FACE_BASE_NORMAL_INDICES[:,1]] - face_points[:,FACE_BASE_NORMAL_INDICES[:,0]],
        face_points[:,FACE_BASE_NORMAL_INDICES[:,2]] - face_points[:,FACE_BASE_NORMAL_INDICES[:,0]],
    dim=-1)
    face_normals /= face_normals.norm(p=2, dim=-1, keepdim=True)

    # Computes the vertices
    thickness = material.get_thickness()
    vertices = torch.stack([
        face_points - face_normals * thickness,
        face_points + face_normals * thickness,
    ], dim=2).reshape((num_faces,-1,3))

    return [vertices[face] for face in range(num_faces)], [FACE_BASE_FACES for _ in range(num_faces)]


def generate_metamaterial_surface_mesh(material: Metamaterial) -> tuple[list[np.ndarray], list[np.ndarray]]:
    """
    Generates the mesh for the metamaterial.

    material: Metamaterial
        The material for which the mesh will be generated.

    Returns: `tuple[list[np.ndarray], list[np.ndarray]]`
        A list of `(V,3)` float numpy arrays with the material's vertex coordinates.
        `V` is the number of vertices in the material mesh.

        A list of `(F,3)` int numpy arrays with the material's face vertex indices.
        `F` is the number of faces in the material mesh.
    """

    # Computes the vertices and faces
    node_vertices, node_faces = generate_node_surface_meshes(material)
    edge_vertices, edge_faces = generate_edge_surface_meshes(material)
    face_vertices, face_faces = generate_face_surface_meshes(material)

    # Applies the transformations
    vertices = [
        vertex_group[i] * (1-material.mirrors) + (1-vertex_group[i]) * material.mirrors + material.translations
            for vertex_group in [node_vertices, edge_vertices, face_vertices]
                for i in range(len(vertex_group))
    ]

    return vertices, node_faces + edge_faces + face_faces


def generate_metamaterial_grid_surface_mesh(metamaterial: Metamaterial, shape=(1,1,1)) -> tuple[list[np.ndarray], list[np.ndarray]]:
    """
    Generates the mesh for the metamaterial.

    metamaterial: Metamaterial
        The material for which the mesh will be generated.

    shape: tuple of ints
        The amount in the x, y, and z directions to grid the metamaterial.
        If no gridding (i.e., just the metamaterial by itself), the input
        should be (1,1,1).

    Returns: `tuple[list[np.ndarray], list[np.ndarray]]`
        A list of `(V,3)` float numpy arrays with the material's vertex coordinates.
        `V` is the number of vertices in each component's mesh.

        A list of `(F,3)` int numpy arrays with the material's face vertex indices.
        `F` is the number of faces in each component's mesh.
    """

    # Computes the materials to mesh
    materials = metamaterial_grid(metamaterial, shape)

    # Stores values for the meshing
    vertices, faces = [], []

    # Runs through each material
    for material in materials:

        # Computes the material's components
        mat_vertices, mat_faces = generate_metamaterial_surface_mesh(material)

        # Stores the material's components
        vertices.extend(mat_vertices)
        faces.extend(mat_faces)

    return vertices, faces


def save_material_obj(vertices: list[np.ndarray], faces: list[np.ndarray], filepath: str, verbose=True):
    """
    Saves an .obj file of the metamaterial's mesh.

    vertices: `list[list[tuple]]`
        A list of `(V,3)` float numpy arrays with the material's vertex coordinates.
        `V` is the number of vertices in each component's mesh.

    faces: `list[list[tuple]]`
        A list of `(F,3)` int numpy arrays with the material's face vertex indices.
        `F` is the number of faces in each component's mesh.

    filepath: `str`
        The path at which the OBJ file will be saved.

    verbose: `bool`, optional
        Whether status messages will be printed to the console.
        Default is `True`.
    """

    # Tells the user the saving is beginning
    if verbose:
        print("Saving:", filepath)

    # Places all components into the trimesh scene
    scene = trimesh.Scene()
    for i in range(len(vertices)):
        scene.add_geometry(trimesh.Trimesh(vertices=vertices[i], faces=faces[i]), node_name=f"Component {i+1}")

    # Exports the OBJ file
    scene.export(filepath)

    # Tells the user the save finished
    if verbose:
        print("Saved!")


def estimate_volumes(materials: torch.Tensor) -> float:

    # Will store the material volumes
    num_materials = materials.shape[0]
    materials = materials.to(DEVICE)
    volumes = torch.zeros((num_materials,), device=DEVICE, dtype=torch.float64)

    # Stores indices for edges/faces
    face_edge_indices = torch.tensor([
        [edge_adj_index(n1,n2), edge_adj_index(n1,n3), edge_adj_index(n2,n3)]
            for n1 in range(NUM_NODES)
                for n2 in range(n1+1, NUM_NODES)
                    for n3 in range(n2+1, NUM_NODES)
    ])

    # Stores the node properties
    node_coords_shape = (num_materials,NUM_NODES,1,3)
    node_coords = pseudo_spherical_to_euclidean_torch(materials[:,:NODE_POS_SIZE].reshape(node_coords_shape)[:,:,0])
    node_coords = node_coords.reshape(node_coords_shape)

    # Stores the edge properties
    edge_adj = materials[:,NODE_POS_SIZE:][:,:EDGE_ADJ_SIZE]
    edge_params_shape = (num_materials,EDGE_ADJ_SIZE,EDGE_BEZIER_POINTS,3)
    edge_params = materials[:,NODE_POS_SIZE+EDGE_ADJ_SIZE:][:,:EDGE_PARAMS_SIZE].reshape(edge_params_shape)

    # Stores the face properties
    face_adj = materials[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE:][:,:FACE_ADJ_SIZE]
    face_params_shape = (num_materials,FACE_ADJ_SIZE,FACE_BEZIER_POINTS,3)
    face_params = materials[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE:][:,:FACE_PARAMS_SIZE].reshape(face_params_shape)

    # Stores the global parameter properties
    thickness = materials[:,-1] * THICKNESS

    # Runs through each edge for active node counting
    active_nodes = torch.zeros((num_materials, NUM_NODES), device=DEVICE)
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):

            # Computes the edge index
            edge_index = edge_adj_index(n1,n2)

            # Accounts for each node in the edge
            active_nodes[:,n1] += edge_adj[:,edge_index]
            active_nodes[:,n2] += edge_adj[:,edge_index]

    # Stores whether nodes are active with bools
    active_nodes = active_nodes.to(torch.bool)

    # Computes the nodes' volumes
    one_node_volume = 4/3 * np.pi * (thickness*51/50)**3
    volumes += one_node_volume * active_nodes.sum(dim=-1)

    # Transforms the edge parameters into the world coordinate system
    edge_params += node_coords[:,EDGE_TO_NODES[:,0]]

    # Stores values for the Bezier parameters for edges
    node1_coords = node_coords[:,EDGE_TO_NODES[:,0]]
    node2_coords = node_coords[:,EDGE_TO_NODES[:,1]]
    
    # Stores the Bezier parameters for edges
    edge_bezier_params = torch.cat([node1_coords, edge_params, node2_coords], dim=2).to(torch.float64)

    # Computes the edges' points
    # Shape: (num_materials, num_edges, edge_segments_vertices, 3)
    edge_points = (BEZIER_CURVE_COEFFICIENTS_TENSOR.unsqueeze(0).unsqueeze(0) @ edge_bezier_params).to(torch.float32)

    # Masks the edge points to only include active edges' points
    edge_points *= edge_adj.unsqueeze(-1).unsqueeze(-1)

    # Computes the edge segment lengths
    edge_segments = edge_points[:,:,1:] - edge_points[:,:,:-1]
    segment_lengths: torch.Tensor = edge_segments.norm(p=2, dim=-1)

    # Computes the edge volumes
    edge_volumes = segment_lengths.sum(dim=-1) * np.pi * (thickness.unsqueeze(-1)*101/100)**2

    # Adjusts the volume for the edges
    # 1. Edge additional volume
    # 2. Node-edge intersection excess volume 
    #  - Each edge intersects with half of two nodes
    volumes += edge_volumes.sum(dim=-1)
    volumes -= one_node_volume * edge_adj.sum(dim=-1)

    # Transforms the face parameters into the world coordinate system
    face_params += node_coords[:,FACE_TO_NODES[:,0]]

    # Stores values for the Bezier parameters for faces
    node1_coords = node_coords[:,FACE_TO_NODES[:,0]]
    node2_coords = node_coords[:,FACE_TO_NODES[:,1]]
    node3_coords = node_coords[:,FACE_TO_NODES[:,2]]
    edge1_params = edge_params[:,face_edge_indices[:,0]]
    edge2_params = edge_params[:,face_edge_indices[:,1]]
    edge3_params = edge_params[:,face_edge_indices[:,2]]

    # Stores the Bezier parameters for faces
    face_bezier_params = torch.cat([
        node1_coords, node2_coords, node3_coords,
        edge1_params, edge2_params, edge3_params,
        face_params
    ], dim=2).to(torch.float64)

    # Computes the faces' points
    # Shape: (num_materials, num_face, face_segments_vertices, 3)
    face_points = (BEZIER_TRIANGLE_COEFFICIENTS_TENSOR.unsqueeze(0).unsqueeze(0) @ face_bezier_params).to(torch.float32)

    # Masks the face points to only include active faces' points
    face_points *= face_adj.unsqueeze(-1).unsqueeze(-1)

    # Computes the face segment areas
    triangle_leg1 = face_points[:,:,FACE_VERTEX_INDICES[:,1]]-face_points[:,:,FACE_VERTEX_INDICES[:,0]]
    triangle_leg2 = face_points[:,:,FACE_VERTEX_INDICES[:,2]]-face_points[:,:,FACE_VERTEX_INDICES[:,0]]
    triangle_areas: torch.Tensor = torch.cross(triangle_leg1, triangle_leg2, dim=-1).norm(p=2, dim=-1) / 2

    # Computes the face volumes
    face_volumes = triangle_areas.sum(dim=-1) * 2 * thickness.unsqueeze(-1)

    # Adjusts the volume for the faces
    # 1. Face additional volume
    # 2. Edge-face intersection excess volume
    #  - Each face intersects with half of its three edges
    volumes += face_volumes.sum(dim=-1)
    excess_edge_volume = (
        edge_volumes[:,face_edge_indices[:,0]] +
        edge_volumes[:,face_edge_indices[:,1]] +
        edge_volumes[:,face_edge_indices[:,2]]
    )
    volumes -= (excess_edge_volume * face_adj).sum(dim=-1) / 2

    return volumes
