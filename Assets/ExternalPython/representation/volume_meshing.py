import math
from representation.rep_class import *
from representation.generation import *

THICKNESS = 0.02
NODES_PER_EDGE = 8
NODES_PER_FACE = 6

def generate_edge_segment_volume_mesh(point1, point2, next_point=None, prev_normal=None):
    """
    Generates the nodes and tetrahedra for an edge segment.
    
    point1: ndarray
        The position of the start of the edge segment.
    
    point2: ndarray
        The position of the end of the edge segment.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the node (x,y,z) coordinates.
        The second is a list containing each tetrahedron's corresponding
        nodes, where the node numbers correspond to the index of the
        node in the first entry.
    """

    # Computes the edge direction for face 1
    edge_dir = point1 - point2
    edge_len = np.linalg.norm(edge_dir)
    edge_dir /= edge_len

    # Computes a random vector to be used for orthogonal vector generation for face 1
    if prev_normal is None:
        prev_normal = np.array([1,0,0])
        if np.linalg.norm(np.cross(prev_normal, edge_dir)) < 0.1:
            prev_normal = np.array([0,1,0])
        
    # Computes two co-orthogonal vectors orthogonal to the edge direction for face1
    basis1 = np.cross(edge_dir, prev_normal)
    basis1 /= np.linalg.norm(basis1)
    basis2 = np.cross(edge_dir, basis1)
    basis2 /= np.linalg.norm(basis2)

    # Computes the face 1 (around node1) nodes
    face1_nodes = [
        point1 + basis1*THICKNESS + basis2*THICKNESS,
        point1 + basis1*THICKNESS - basis2*THICKNESS,
        point1 - basis1*THICKNESS - basis2*THICKNESS,
        point1 - basis1*THICKNESS + basis2*THICKNESS,
    ]

    # Adapts the normal to how it will appear on the next segment, if applicable
    if next_point is not None:

        # Computes the edge direction for face 2
        edge_dir = point2 - next_point
        edge_len = np.linalg.norm(edge_dir)
        edge_dir /= edge_len

        # Computes two co-orthogonal vectors orthogonal to the edge direction for face 2
        basis1 = np.cross(edge_dir, prev_normal)
        basis1 /= np.linalg.norm(basis1)
        basis2 = np.cross(edge_dir, basis1)
        basis2 /= np.linalg.norm(basis2)

    # Computes the face 2 (around node2) nodes
    face2_nodes = [
        point2 + basis1*THICKNESS + basis2*THICKNESS,
        point2 + basis1*THICKNESS - basis2*THICKNESS,
        point2 - basis1*THICKNESS - basis2*THICKNESS,
        point2 - basis1*THICKNESS + basis2*THICKNESS,
    ]

    return (
        basis2,
        [tuple(node) for node in face1_nodes] + [tuple(node) for node in face2_nodes], # Vertex coordinates
        [(0,1,4,3), (1,3,2,6), (1,4,5,6), (4,3,7,6)] # Tetrahedron node indices
    )


def generate_edge_volume_mesh(material: Metamaterial, node1, node2):
    """
    Generates the nodes and faces for an edge of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose edge will be meshed.

    node1: int
        The node ID for one of the nodes defining the edge.

    node2: int
        The node ID for the other node defining the edge.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the node (x,y,z) coordinates.
        The second is a list containing each face's corresponding nodes,
        where the node numbers correspond to the index of the node in
        the first entry.
    """

    # Computes the points along the edge
    edge_points = material.compute_edge_points(node1, node2)

    # Stores values for the function
    nodes = []
    tetrahedra = []

    # Runs through each edge segment
    prev_normal = None
    for edge in range(EDGE_SEGMENTS):
        prev_normal, node_list, tetrahedra_list = generate_edge_segment_volume_mesh(edge_points(edge), edge_points(edge+1), next_point=(None if edge+2 > EDGE_SEGMENTS else edge_points(edge+2)), prev_normal=prev_normal)

        # Adds the new nodes/faces
        nodes.extend(node_list)

        # Adds the tetrahedra from the edge
        for tetrahedron in tetrahedra_list:
            tetrahedra.append(tuple(map(lambda x: x+edge*NODES_PER_EDGE, tetrahedron)))

    return nodes, tetrahedra


def generate_face_segment_volume_mesh(point1: np.ndarray, point2: np.ndarray, point3: np.ndarray, face_normal: np.ndarray):
    """
    Generates the nodes and tetrahedra for an face segment.
    
    point1: ndarray
        One of the three nodes of the face.
    
    point2: ndarray
        Another of the three nodes of the face.
    
    point3: ndarray
        The last of the three nodes of the face.

    face_normal: np.ndarray
        The normal to the face. One normal is used for the whole face.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the node (x,y,z) coordinates.
        The second is a list containing each tetrahedron's corresponding
        nodes, where the node numbers correspond to the index of the node
        in the first entry.
    """

    # Computes all vertices of the face segment
    segment_nodes = [
        point1 - face_normal*THICKNESS,
        point2 - face_normal*THICKNESS,
        point3 - face_normal*THICKNESS,
        point1 + face_normal*THICKNESS,
        point2 + face_normal*THICKNESS,
        point3 + face_normal*THICKNESS,
    ]

    # Returns the nodes and tetrahedra
    return (
        [tuple(node) for node in segment_nodes],
        [(0,1,2,3), (1,2,3,4), (2,3,4,5)]
    )


def generate_face_volume_mesh(material: Metamaterial, node1, node2, node3):
    """
    Generates the nodes and tetrahedra for a face of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose face will be meshed.

    node1: int
        The node ID for one of the nodes defining the face.

    node2: int
        The node ID for one of the other nodes defining the face.

    node3: int
        The node ID of the last node defining the face.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the node (x,y,z) coordinates.
        The second is a list containing each tetrahedron's corresponding
        nodes, where the node numbers correspond to the index of the node
        in the first entry.
    """
    
    # Creates function for computing face points
    face_points = material.compute_face_points(node1, node2, node3)

    # Stores the nodes and tetrahedra
    nodes = []
    tetrahedra = []

    # Stores the number of nodes so far
    node_count = 0

    # Computes the face normal
    face_normal = np.cross(face_points(0,EDGE_SEGMENTS) - face_points(EDGE_SEGMENTS, 0), face_points(0,0) - face_points(EDGE_SEGMENTS, 0))
    face_normal /= np.linalg.norm(face_normal)

    # Runs through each face
    for t in range(EDGE_SEGMENTS):
        for s in range(EDGE_SEGMENTS-t):

            # Gets the first face's nodes and tetrahedra
            node_list, tetrahedra_list = generate_face_segment_volume_mesh(face_points(s,t), face_points(s+1,t), face_points(s,t+1), face_normal)
            
            # Stores the nodes
            nodes.extend(node_list)

            # Stores the tetrahedra
            for tetrahedron in tetrahedra_list:
                tetrahedra.append(tuple(map(lambda x:x+node_count, tetrahedron)))
            node_count += NODES_PER_FACE

            # Checks for a second face
            if s+t+2 <= EDGE_SEGMENTS:

                # Gets the first face's nodes and tetrahedra
                node_list, tetrahedra_list = generate_face_segment_volume_mesh(face_points(s+1,t+1), face_points(s,t+1), face_points(s+1,t), face_normal)
                
                # Stores the nodes
                nodes.extend(node_list)

                # Stores the tetrahedra
                for tetrahedron in tetrahedra_list:
                    tetrahedra.append(tuple(map(lambda x:x+node_count, tetrahedron)))
                node_count += NODES_PER_FACE
    
    return nodes, tetrahedra


def generate_metamaterial_volume_mesh(material: Metamaterial):
    """
    Generates the mesh for the metamaterial.

    material: Metamaterial
        The material for which the mesh will be generated.

    Returns: (list of tuples, list of tuples)
        The node and face list, respectively, as described in 
        the save_obj() function's specification.
    """

    # Stores the nodes and faces
    nodes = []
    tetrahedra = []
    node_count = 0

    # Generates the edge mesh for each edge
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):

            # Skips non-edges
            if not material.has_edge(n1, n2):
                continue

            # Adds the nodes from the edge
            node_list, tetrahedra_list = generate_edge_volume_mesh(material, n1, n2)
            nodes.extend(node_list)

            # Adds the faces from the edge
            for tetrahedron in tetrahedra_list:
                tetrahedra.append(tuple(map(lambda x: x + node_count, tetrahedron)))

            node_count += len(node_list)

    # Generates the face mesh for each face
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):
            for n3 in range(n2+1, NUM_NODES):

                # Skips non-faces
                if not material.has_face(n1, n2, n3):
                    continue

                # Adds the nodes from the face
                node_list, tetrahedra_list = generate_face_volume_mesh(material, n1, n2, n3)
                nodes.extend(node_list)

                # Adds the faces from the face
                for tetrahedron in tetrahedra_list:
                    tetrahedra.append(tuple(map(lambda x: x + node_count, tetrahedron)))

                node_count += len(node_list)

    return nodes, tetrahedra


def generate_metamaterial_grid_volume_mesh(metamaterial: Metamaterial, shape=(1,1,1)):
    """
    Generates the mesh for the metamaterial.

    metamaterial: Metamaterial
        The material for which the mesh will be generated.

    shape: tuple of ints
        The amount in the x, y, and z directions to grid the metamaterial.
        If no gridding (i.e., just the metamaterial by itself), the input
        should be (1,1,1).

    Returns: (list of tuples, list of tuples)
        The node and face list, respectively, as described in 
        the save_obj() function's specification.
    """

    # Computes the materials to mesh
    materials = metamaterial_grid(metamaterial, shape)

    # Stores values for the meshing
    node_count = 0
    nodes, tetrahedra = [], []

    # Meshes each material
    for material in materials:
        next_nodes, next_tetrahedra = generate_metamaterial_volume_mesh(material)
        nodes.extend(next_nodes)
        tetrahedra.extend([tuple(map(lambda x: x+node_count, tetrahedron)) for tetrahedron in next_tetrahedra])
        node_count += len(next_nodes)

    return nodes, tetrahedra


def generate_metamaterials_zigzag_volume_meshes(metamaterials: list[Metamaterial], shape=(1,1,1)):
    """
    Generates meshes for the given metamaterials with the given shape in a
    zigzag pattern.

    metamaterials: list[Metamaterial]
        The metamaterials of which meshes will be generated.

    shape: tuple of ints
        The gridding to be done on the metamaterials. If (1,1,1), then only
        one of each metamaterial will be meshed.
    """
    
    # Finds the best square
    square_side = math.ceil(len(metamaterials)**0.5)

    # Stores values for the function
    nodes, tetrahedra = [], []
    node_count = 0

    # Places each metamaterial along a zigzag
    for i, material in enumerate(metamaterials):

        # Stores the displacements
        dz = i//square_side
        dx = i%square_side if dz % 2 == 0 else (square_side-i-1)%square_side

        # Meshes the material
        material = material.translate(dx=(dx-square_side//2)*1.5*max(shape), dz=(dz-square_side//2)*1.5*max(shape))
        next_nodes, next_tetrahedra = generate_metamaterial_grid_volume_mesh(material, shape)
        nodes.extend(next_nodes)
        tetrahedra.extend([tuple(map(lambda x: x+node_count, tetrahedron)) for tetrahedron in next_tetrahedra])
        node_count += len(next_nodes)

    return nodes, tetrahedra


def optimize_nodes(nodes, tetrahedra):

    # Stores data structures for removing duplicate nodes
    unique_nodes = 0
    new_nodes = []
    node_to_index = {}
    index_to_index = {}

    # Removes duplicate nodes
    for i,node in enumerate(nodes):

        # First time seeing node
        if node not in node_to_index:
            node_to_index[node] = unique_nodes
            unique_nodes += 1
            new_nodes.append(node)
            
        index_to_index[i] = node_to_index[node]

    # Stores the tetrahedra with new indices
    new_tetrahedra = [map(lambda x:index_to_index[x], tetrahedron) for tetrahedron in tetrahedra]

    return new_nodes, new_tetrahedra


def save_msh(nodes, tetrahedra, filepath):
    """
    Saves the .msh file with the given nodes and tetrahedron node
    indices.

    nodes: list of tuples
        The node (x,y,z) coordinates.

    tetrahedra: list of tuples
        The node indices corresponding to each tetrahedron.

    filepath: str
        The path at which the file will be saved.
    """

    # Optimizes the node count
    nodes, tetrahedra = optimize_nodes(nodes, tetrahedra)

    # Writes the new file
    with open(filepath, 'w') as f:

        # Writes the mesh format lines
        f.write("$MeshFormat\n")
        f.write("2.0 0 8\n")
        f.write("$EndMeshFormat\n")

        # Writes each node
        f.write("$Nodes\n")
        f.write(f"{len(nodes)}\n")
        for i,node in enumerate(nodes):
            f.write(f"{i+1} {' '.join(map(str, map(lambda v: np.round(v,4), node)))}\n") # Rounds to 4 decimal places
        f.write("$EndNodes\n")

        # Writes each tetrahedron
        f.write("$Elements\n")
        f.write(f"{len(tetrahedra)}\n")
        for i,tetrahedron in enumerate(tetrahedra):
            f.write(f"{i+1} 4 0 {' '.join(map(str, map(lambda x:x+1, tetrahedron)))}\n")
        f.write("$EndElements\n")