import math
import trimesh
from representation.rep_class import *
from representation.generation import *
from representation.rep_utils import *


def generate_node_surface_mesh(material: Metamaterial, node: int, being_painted=False) -> tuple[list[tuple], list[tuple]]:
    """
    Generates the vertices and faces for a node of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose edge will be meshed.

    node1: int
        The node ID for the target node.

    being_painted: `bool`, optional
        Whether this metamaterial is being painted on a geometry.
        If `True`, assumes this material is part of a 2x2x2 grid.
    """

    # Computes the node position
    center = material.get_node_position(node)
    if being_painted:
        center = material.transform_along_boundary(center)

    # Stores the mesh values
    vertices, faces = [], []

    # Geometric properties of the node mesh
    node_segments = EDGE_SEGMENTS//2
    sphere_radius = material.get_thickness() * 51/50

    # Computes angle values
    def thetas():
        for i in range(node_segments+1):
            yield i, i/node_segments * np.pi
    def phis():
        for i in range(node_segments):
            yield i, i/node_segments * 2*np.pi

    # Runs through each point on the surface of the sphere
    for i,theta in thetas():

        # Computes the z Euclidean coordinate
        z = np.array([0.,0.,1.]) * sphere_radius * np.cos(theta)

        # Handles all other points in between
        for j,phi in phis():

            # Computes the Euclidean coordinates
            x = np.array([1.,0.,0.]) * sphere_radius * np.sin(theta) * np.cos(phi)
            y = np.array([0.,1.,0.]) * sphere_radius * np.sin(theta) * np.sin(phi)

            # Stores the vertices along the ring
            vertices.append(tuple(center+x+y+z))

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
                faces.append((0, 1 + j, 1 + (j+1)%node_segments))
                continue

            # Handles the faces touching the south pole
            if i == node_segments:
                offset = len(vertices)-node_segments-1
                faces.append((offset + (j+1)%node_segments, offset + j, offset + node_segments))
                continue

            # Handles all other faces
            ring1_index = len(vertices)-2*node_segments
            ring2_index = len(vertices)-node_segments
            face1 = (ring2_index + (j+1)%node_segments, ring1_index + (j+1)%node_segments, ring1_index + j)
            face2 = (ring1_index + j, ring2_index + j, ring2_index + (j+1)%node_segments)
            faces.append(face1)
            faces.append(face2)

    return vertices, faces


def generate_edge_surface_mesh(material: Metamaterial, node1: int, node2: int, being_painted=False) -> tuple[list[tuple], list[tuple]]:
    """
    Generates the vertices and faces for an edge of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose edge will be meshed.

    node1: int
        The node ID for one of the nodes defining the edge.

    node2: int
        The node ID for the other node defining the edge.

    being_painted: `bool`, optional
        Whether this metamaterial is being painted on a geometry.
        If `True`, assumes this material is part of a 2x2x2 grid.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the vertex (x,y,z) coordinates.
        The second is a list containing each face's corresponding vertices,
        where the vertex numbers correspond to the index of the vertex in
        the first entry.
    """

    # Computes the points along the edge
    edge_points_function = material.compute_edge_points(node1, node2, being_painted=being_painted)

    # Stores the edge points for faster computation
    edge_points = [edge_points_function(edge) for edge in range(EDGE_SEGMENTS+1)]

    # Stores vertex/face values for the function
    vertices, faces = [], []

    # Computes the normal vectors
    normal1, normal2 = find_line_normals(edge_points[0], edge_points[1])

    # Stores the number of vertices seen so far
    vertex_count = 0

    # Runs through each edge point
    thickness = material.get_thickness() * 101/100
    for edge in range(EDGE_SEGMENTS+1):

        # Adds all of the vertices on the circular face of this edge point
        vertices.extend(
            tuple(edge_points[edge]
             + np.cos(2*np.pi*theta/EDGE_SEGMENTS) * normal1*thickness
             + np.sin(2*np.pi*theta/EDGE_SEGMENTS) * normal2*thickness)
                for theta in range(EDGE_SEGMENTS)
        )

        # Adds the face at the ends of the cylindrical edge
        if edge == 0:
            faces.extend((vertex_count+i+1, vertex_count+i, vertex_count) for i in range(1,EDGE_SEGMENTS-1))
        elif edge == EDGE_SEGMENTS:
            faces.extend((vertex_count, vertex_count+i, vertex_count+i+1) for i in range(1,EDGE_SEGMENTS-1))

        # Adds the faces that connect edge segments
        if edge != 0:
            for i in range(EDGE_SEGMENTS):
                face1 = (vertex_count-EDGE_SEGMENTS+i, vertex_count-EDGE_SEGMENTS+(i+1)%EDGE_SEGMENTS, vertex_count+(i+1)%EDGE_SEGMENTS)
                face2 = (vertex_count+(i+1)%EDGE_SEGMENTS, vertex_count+i, vertex_count-EDGE_SEGMENTS+i)
                faces.append(face1)
                faces.append(face2)

        # Updates the normals
        if edge != EDGE_SEGMENTS:

            # Computes the direction of the edge segment to be formed
            edge_dir = edge_points[edge+1] - edge_points[edge]

            # Computes the new normals
            normal2 = np.cross(edge_dir, normal1)
            normal2 /= np.linalg.norm(normal2)
            normal1 = np.cross(normal2, edge_dir)
            normal1 /= np.linalg.norm(normal1)

        # Updates the vertex count
        vertex_count += EDGE_SEGMENTS

    return vertices, faces


def generate_face_surface_mesh(material: Metamaterial, node1: int, node2: int, node3: int, being_painted=False) -> tuple[list[tuple], list[tuple]]:
    """
    Generates the vertices and faces for a face of the metamaterial.
    
    material: Metamaterial
        The metamaterial whose face will be meshed.

    node1: int
        The node ID for one of the nodes defining the face.

    node2: int
        The node ID for one of the other nodes defining the face.

    node3: int
        The node ID of the last node defining the face.

    being_painted: `bool`, optional
        Whether this metamaterial is being painted on a geometry.
        If `True`, assumes this material is part of a 2x2x2 grid.

    Returns: (list of tuples of floats, list of tuples of ints)
        The first entry is a list of the vertex (x,y,z) coordinates.
        The second is a list containing each face's corresponding vertices,
        where the vertex numbers correspond to the index of the vertex in
        the first entry.
    """

    # Creates function for computing face points
    face_points_function = material.compute_face_points(node1, node2, node3, being_painted=being_painted)

    # Pre-computes the face points for faster computation
    face_points = [
        face_points_function(s,t)
            for s,t,u in BEZIER_TRIANGLE_PARAMETERS
    ]

    # Pre-computes the face normals for faster computation
    face_normals = []
    thickness = material.get_thickness()
    for s,t,u in BEZIER_TRIANGLE_PARAMETERS:

        # Stores the base point for the normal
        base_point = face_points[bezier_triangle_index(s,t)]

        # Stores the points from which the normal is deduced
        if s+t != EDGE_SEGMENTS:
            point1 = face_points[bezier_triangle_index(s+1,t)]
            point2 = face_points[bezier_triangle_index(s,t+1)]

        elif s == EDGE_SEGMENTS:
            point1 = face_points[bezier_triangle_index(s-1,t+1)]
            point2 = face_points[bezier_triangle_index(s-1,t)]

        elif t == EDGE_SEGMENTS:
            point1 = face_points[bezier_triangle_index(s,t-1)]
            point2 = face_points[bezier_triangle_index(s+1,t-1)]

        else:
            point1 = face_points[bezier_triangle_index(s-1,t+1)]
            point2 = face_points[bezier_triangle_index(s-1,t)]

        # Computes the face's normal
        face_normal = np.cross(point1-base_point, point2-base_point)
        face_normal /= np.linalg.norm(face_normal)

        # Stores the face's normal
        face_normals.append(face_normal*thickness)

    # Stores the vertices/faces
    vertices, faces = [], []

    # Stores each vertex
    for point, normal in zip(face_points, face_normals):
        vertices.append(tuple(point - normal)) # Bottom vertices
        vertices.append(tuple(point + normal)) # Top vertices

    # Runs through each top/bottom face
    for s in range(EDGE_SEGMENTS):
        for t in range(EDGE_SEGMENTS-s):

            # Stores the point indices
            point1_index = bezier_triangle_index(s,t)*2
            point2_index = bezier_triangle_index(s+1,t)*2
            point3_index = bezier_triangle_index(s,t+1)*2

            # Adds the faces
            faces.append((point3_index, point2_index, point1_index)) # Bottom face
            faces.append((point1_index+1, point2_index+1, point3_index+1)) # Top face

            # Checks for a second face
            if s+t+2 <= EDGE_SEGMENTS:

                # Stores the new point index
                point4_index = bezier_triangle_index(s+1,t+1)*2

                # Adds the faces
                faces.append((point2_index, point3_index, point4_index)) # Bottom face
                faces.append((point4_index+1, point3_index+1, point2_index+1)) # Top face

    # Runs through the edge faces
    for t in range(EDGE_SEGMENTS):
        s = EDGE_SEGMENTS-t # Computes the opposite parameter

        # Stores the u=0 edge faces
        faces.append((
            bezier_triangle_index(s,t)*2,
            bezier_triangle_index(s-1,t+1)*2,
            bezier_triangle_index(s-1,t+1)*2+1,
        ))
        faces.append((
            bezier_triangle_index(s-1,t+1)*2+1,
            bezier_triangle_index(s,t)*2+1,
            bezier_triangle_index(s,t)*2,
        ))

        # Stores the t=0 edge faces
        faces.append((
            bezier_triangle_index(t,0)*2,
            bezier_triangle_index(t+1,0)*2,
            bezier_triangle_index(t+1,0)*2+1,
        ))
        faces.append((
            bezier_triangle_index(t+1,0)*2+1,
            bezier_triangle_index(t,0)*2+1,
            bezier_triangle_index(t,0)*2,
        ))

        # Stores the s=0 edge faces
        faces.append((
            bezier_triangle_index(0,t+1)*2+1,
            bezier_triangle_index(0,t+1)*2,
            bezier_triangle_index(0,t)*2,
        ))
        faces.append((
            bezier_triangle_index(0,t)*2,
            bezier_triangle_index(0,t)*2+1,
            bezier_triangle_index(0,t+1)*2+1,
        ))

    return vertices, faces


def generate_metamaterial_surface_mesh(material: Metamaterial, being_painted=False) -> tuple[list[list[tuple]], list[list[tuple]]]:
    """
    Generates the mesh for the metamaterial.

    material: Metamaterial
        The material for which the mesh will be generated.

    being_painted: `bool`, optional
        Whether this metamaterial is being painted on a geometry.
        If `True`, assumes this material is part of a 2x2x2 grid.

    Returns: `tuple[list[list[tuple]], list[list[tuple]]]`
        The first item contains the vertices of each component
        in the metamaterial mesh, where the first axis
        separates distinct components, and the second axis
        separates the coordinates of a component's vertices.

        The second item contains the faces of each component
        in the metamaterial mesh, where the first axis
        separates distinct components, and the second axis
        separates the indices of a component's faces.
    """

    # Stores the vertices and faces
    vertices, faces = [], []

    # Runs through each face
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):
            for n3 in range(n2+1, NUM_NODES):

                # Skips nodes without a face between them
                if not material.has_face(n1, n2, n3):
                    continue

                # Skips faces on non-displayed planes
                if not material.face_is_displayed(n1, n2, n3):
                    continue

                # Computes the face's components
                face_vertices, face_faces = generate_face_surface_mesh(material, n1, n2, n3, being_painted=being_painted)

                # Stores the face's components
                vertices.append(face_vertices)
                faces.append(face_faces)

    # Runs through each edge
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):

            # Skips nodes without an edge between them, except when this needs to be overriden
            if not material.has_edge(n1, n2):
                continue

            # Skips edges on non-displayed planes
            if not material.edge_is_displayed(n1, n2):
                continue

            # Computes the edge's components
            edge_vertices, edge_faces = generate_edge_surface_mesh(material, n1, n2, being_painted=being_painted)

            # Stores the edge's components
            vertices.append(edge_vertices)
            faces.append(edge_faces)

    # Runs through each node
    for node in material.active_nodes():

        # Skips nodes on non-displayed planes
        if not material.node_is_displayed(node):
            continue

        # Computes the node's components
        node_vertices, node_faces = generate_node_surface_mesh(material, node, being_painted=being_painted)

        # Stores the node's components
        vertices.append(node_vertices)
        faces.append(node_faces)

    return vertices, faces


def generate_metamaterial_grid_surface_mesh(metamaterial: Metamaterial, shape=(1,1,1), being_painted=False) -> tuple[list[list[tuple]], list[list[tuple]]]:
    """
    Generates the mesh for the metamaterial.

    metamaterial: Metamaterial
        The material for which the mesh will be generated.

    shape: tuple of ints
        The amount in the x, y, and z directions to grid the metamaterial.
        If no gridding (i.e., just the metamaterial by itself), the input
        should be (1,1,1).

    being_painted: `bool`, optional
        Whether this metamaterial is being painted on a geometry.
        If `True`, assumes this material is part of a 2x2x2 grid.

    Returns: `tuple[list[list[tuple]], list[list[tuple]]]`
        The first item contains the vertices of each component
        in the metamaterial mesh, where the first axis
        separates distinct components, and the second axis
        separates the coordinates of a component's vertices.

        The second item contains the faces of each component
        in the metamaterial mesh, where the first axis
        separates distinct components, and the second axis
        separates the indices of a component's faces.
    """

    # Defaults the shape when being painted
    if being_painted:
        shape = (2,2,2)

    # Computes the materials to mesh
    materials = metamaterial_grid(metamaterial, shape)

    # Stores values for the meshing
    vertices, faces = [], []

    # Runs through each material
    for material in materials:

        # Computes the material's components
        mat_vertices, mat_faces = generate_metamaterial_surface_mesh(material, being_painted=being_painted)

        # Stores the material's components
        vertices.extend(mat_vertices)
        faces.extend(mat_faces)

    return vertices, faces


def generate_metamaterials_zigzag_surface_meshes(metamaterials: list[Metamaterial], shape=(1,1,1)) -> tuple[list[tuple], list[tuple]]:
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
    vertices, faces = [], []

    # Places each metamaterial along a zigzag
    for i, material in enumerate(metamaterials):

        # Stores the displacements
        dz = i//square_side
        dx = i%square_side if dz % 2 == 0 else (square_side-i-1)%square_side

        # Meshes the material
        material = material.translate(dx=(dx-square_side//2)*1.5*max(shape), dz=(dz-square_side//2)*1.5*max(shape))
        next_vertices, next_faces = generate_metamaterial_grid_surface_mesh(material, shape)
        vertices.extend(next_vertices)
        faces.extend(next_faces)

    return vertices, faces


def save_multi_obj(vertices: list[list[tuple]], faces: list[list[tuple]], filepath: str, precision=6, verbose=True, one_component=False):
    """
    Saves an .obj file with multiple objects.

    vertices: `list[list[tuple]]`
        The vertices of the different objects. The first
        axis separates the different objects. The second
        axis separates different vertices in the same
        object.

    faces: `list[list[tuple]]`
        The faces of the different objects. The first
        axis separates the different objects. The second
        axis separates different faces in the same
        object. Must have `len(faces) == len(vertices)`.
        The indices should be local to their own object.
        The indices should be 0-indexed.

    filepath: `str`
        The path at which the file will be saved.

    precision: `int`, optional
        The number of decimal places with which to save the
        vertex coordinates. Default is `6`.

    verbose: `bool`, optional
        Whether status messages will be printed to the console.
        Default is `True`.

    one_component: `bool`, optional
        Whether only one component should be meshed, despite any
        vertex/face separation in the input.
    """

    # Tells the user the saving is beginning
    if verbose:
        print("Saving:", filepath[filepath.rindex("/")+1:])

    # Stores data for the file writing
    num_vertices = 0

    # Writes to the file
    with open(filepath, "w") as f:

        # Runs through each object
        for i, obj_parts in enumerate(zip(vertices, faces)):

            # Stores the parts of the object
            obj_vertices, obj_faces = obj_parts

            # Writes the object
            if i == 0 or not one_component:
                f.write(f"o Component{i}\n")

            # Writes each vertex
            for vertex in obj_vertices:
                f.write("v")
                for coord in vertex:
                    f.write(f" {np.round(coord, precision)}")
                f.write("\n")

            # Writes each face
            for face in obj_faces:
                f.write("f")
                for vertex_index in face:
                    f.write(f" {vertex_index + num_vertices + 1}")
                f.write("\n")

            # Updates the number of vertices
            num_vertices += len(obj_vertices)

    # Tells the user the save finihsed
    if verbose:
        print("Saved!")


def find_contained_voxels(start_voxel: tuple[int], contains_function, resolution=50):

    # Stores the node's voxels
    seen_voxels = set()
    queue = [start_voxel]

    # Runs through each adjacent voxel
    while queue:

        # Stores the current voxel
        current_voxel = queue.pop()
        seen_voxels.add(current_voxel)

        # Checks if the current voxel is inside
        x,y,z = current_voxel
        voxel_point = ((x + 0.5)/resolution, (y + 0.5)/resolution, (z + 0.5)/resolution)
        if not contains_function(voxel_point):
            continue
        yield current_voxel

        # Queues the adjacent voxels
        for dx in [-1,1]:
            if (x+dx,y,z) not in seen_voxels:
                queue.append((x+dx,y,z))
            if (x,y+dx,z) not in seen_voxels:
                queue.append((x,y+dx,z))
            if (x,y,z+dx) not in seen_voxels:
                queue.append((x,y,z+dx))


import time
def voxelize_material(material: Metamaterial, filepath: str, resolution=50, verbose=True):

    # Stores the voxel coordinates
    all_voxels = set()

    # Runs through each node
    for node in material.active_nodes():
        if verbose:
            print(f"Node {node}")

        # Computes the node's internal voxels
        vertices, faces = generate_node_surface_mesh(material, node)
        node_mesh = trimesh.Trimesh(vertices=vertices, faces=faces)
        start_point = material.get_node_position(node)
        start_voxel = tuple(map(int, np.round(start_point * resolution)))
        contains_function = lambda x:node_mesh.contains([x])[0]
        all_voxels.update(find_contained_voxels(start_voxel, contains_function, resolution=resolution))

    # Runs through each edge
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1,NUM_NODES):
            
            # Skips non-edge
            if not material.has_edge(n1,n2):
                continue
            
            if verbose:
                print(f"Edge ({n1},{n2})")
                
            # Computes the edge's internal voxels
            vertices, faces = generate_edge_surface_mesh(material, n1, n2)
            edge_mesh = trimesh.Trimesh(vertices=vertices, faces=faces)
            start_point = material.compute_edge_points(n1,n2)(EDGE_SEGMENTS//2)
            start_voxel = tuple(map(int, np.round(start_point * resolution)))
            contains_function = lambda x:edge_mesh.contains([x])[0]
            all_voxels.update(find_contained_voxels(start_voxel, contains_function, resolution=resolution))

    # Runs through each face
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1,NUM_NODES):
            for n3 in range(n2+1,NUM_NODES):
            
                # Skips non-face
                if not material.has_face(n1,n2,n3):
                    continue
                
                if verbose:
                    print(f"Face ({n1},{n2},{n3})")
                    
                # Computes the face's internal voxels
                start_time = time.time()
                vertices, faces = generate_face_surface_mesh(material, n1, n2, n3)
                face_mesh = trimesh.Trimesh(vertices=vertices, faces=faces)
                start_point = material.compute_face_points(n1,n2,n3)(EDGE_SEGMENTS//3,EDGE_SEGMENTS//3)
                start_voxel = tuple(map(int, np.round(start_point * resolution)))
                contains_function = lambda x:face_mesh.contains([x])[0]
                all_voxels.update(find_contained_voxels(start_voxel, contains_function, resolution=resolution))
                print(time.time()-start_time)

    # Fixes non-manifold edges
    target_voxels = all_voxels
    while True:
        new_voxels = set()
        for voxel in target_voxels:

            for i in range(3):
                for dx in [-1,1]:
                    for dy in [-1,1]:

                        # Computes the diagonal voxel
                        diagonal = list(voxel)
                        diagonal[(i+1)%3] += dx
                        diagonal[(i+2)%3] += dy
                        diagonal = tuple(diagonal)

                        # Computes the first adjacent voxel
                        adj1 = list(voxel)
                        adj1[(i+1)%3] += dx
                        adj1 = tuple(adj1)

                        # Computes the second adjacent voxel
                        adj2 = list(voxel)
                        adj2[(i+2)%3] += dy
                        adj2 = tuple(adj2)

                        # Fixes if non-manifold
                        if diagonal in all_voxels and adj1 not in all_voxels and adj2 not in all_voxels:
                            new_voxels.add(adj1)
                            new_voxels.add(adj2)

        # Stores the new voxels
        all_voxels.update(voxel for voxel in new_voxels)
        target_voxels = new_voxels

        # Stops if no voxels are left
        if len(target_voxels) == 0:
            break

    # Stores the voxels
    with open(filepath, "w") as f:
        for x,y,z in all_voxels:
            f.write(f"{x} {y} {z}\n")


def voxels_to_surface(voxel_filepath: str, obj_filepath: str, resolution=50):

    # Stores the voxels
    voxels = set()
    with open(voxel_filepath, "r") as f:
        line = f.readline()
        while len(line) > 0:
            x,y,z = map(int, line[:-1].split(" "))
            voxels.add((x,y,z))
            line = f.readline()

    # Stores data about the mesh
    num_vertices = 0
    vertex_index = {}
    vertices, faces = [], []

    # Stores the vertices in a non-duplicating manner
    for voxel in voxels:
        x,y,z = voxel

        # Checks each voxel vertex coordinate
        for dx in range(2):
            for dy in range(2):
                for dz in range(2):

                    # Checks this point
                    vertex = (x+dx,y+dy,z+dz)
                    if vertex not in vertex_index:
                        vertex_index[vertex] = num_vertices
                        vertices.append(tuple(map(lambda x:x/resolution, vertex)))
                        num_vertices += 1

    # Adds the cube faces
    for voxel in voxels:
            x,y,z = voxel

            # Handles the x=0 face
            if (x-1,y,z) not in voxels:
                faces.append((
                    vertex_index[(x,y,z+1)],
                    vertex_index[(x,y+1,z+1)],
                    vertex_index[(x,y+1,z)],
                    vertex_index[(x,y,z)],
                ))
            
            # Handles the y=0 face
            if (x,y-1,z) not in voxels:
                faces.append((
                    vertex_index[(x,y,z)],
                    vertex_index[(x+1,y,z)],
                    vertex_index[(x+1,y,z+1)],
                    vertex_index[(x,y,z+1)],
                ))
            
            # Handles the z=0 face
            if (x,y,z-1) not in voxels:
                faces.append((
                    vertex_index[(x,y,z)],
                    vertex_index[(x,y+1,z)],
                    vertex_index[(x+1,y+1,z)],
                    vertex_index[(x+1,y,z)],
                ))
            
            # Handles the x=1 face
            if (x+1,y,z) not in voxels:
                faces.append((
                    vertex_index[(x+1,y,z)],
                    vertex_index[(x+1,y+1,z)],
                    vertex_index[(x+1,y+1,z+1)],
                    vertex_index[(x+1,y,z+1)],
                ))
            
            # Handles the y=1 face
            if (x,y+1,z) not in voxels:
                faces.append((
                    vertex_index[(x,y+1,z+1)],
                    vertex_index[(x+1,y+1,z+1)],
                    vertex_index[(x+1,y+1,z)],
                    vertex_index[(x,y+1,z)],
                ))
            
            # Handles the z=1 face
            if (x,y,z+1) not in voxels:
                faces.append((
                    vertex_index[(x,y,z+1)],
                    vertex_index[(x,y+1,z+1)],
                    vertex_index[(x+1,y+1,z+1)],
                    vertex_index[(x+1,y,z+1)],
                ))

    save_multi_obj([vertices], [faces], obj_filepath)




def voxel_to_obj(voxel_filepath: str, obj_filepath: str, resolution=50):

    # Stores the vertices and faces of the obj mesh
    vertices, faces = [], []

    # Stores each vertex of the voxel cubes
    num_vertices = 0
    vertex_index = {}
    
    # Goes through each voxel
    with open(voxel_filepath, "r") as f:
        line = f.readline()
        while len(line) > 0:
            x,y,z = map(int, line[:-1].split(" "))
            line = f.readline()

            # Checks each vertex coordinate
            for dx in range(2):
                for dy in range(2):
                    for dz in range(2):

                        # Checks this point
                        vertex = (x+dx,y+dy,z+dz)
                        if vertex not in vertex_index:
                            vertex_index[vertex] = num_vertices
                            vertices.append(tuple(map(lambda x:x/resolution, vertex)))
                            num_vertices += 1

            # Adds the cube faces
            faces.append(( # x=0
                vertex_index[(x,y,z)],
                vertex_index[(x,y+1,z)],
                vertex_index[(x,y+1,z+1)],
                vertex_index[(x,y,z+1)],
            ))
            faces.append(( # y=0
                vertex_index[(x,y,z)],
                vertex_index[(x+1,y,z)],
                vertex_index[(x+1,y,z+1)],
                vertex_index[(x,y,z+1)],
            ))
            faces.append(( # z=0
                vertex_index[(x,y,z)],
                vertex_index[(x,y+1,z)],
                vertex_index[(x+1,y+1,z)],
                vertex_index[(x+1,y,z)],
            ))
            faces.append(( # x=1
                vertex_index[(x+1,y,z)],
                vertex_index[(x+1,y+1,z)],
                vertex_index[(x+1,y+1,z+1)],
                vertex_index[(x+1,y,z+1)],
            ))
            faces.append(( # y=1
                vertex_index[(x,y+1,z)],
                vertex_index[(x+1,y+1,z)],
                vertex_index[(x+1,y+1,z+1)],
                vertex_index[(x,y+1,z+1)],
            ))
            faces.append(( # z=1
                vertex_index[(x,y,z+1)],
                vertex_index[(x,y+1,z+1)],
                vertex_index[(x+1,y+1,z+1)],
                vertex_index[(x+1,y,z+1)],
            ))

    save_multi_obj([[tuple(map(lambda x:x/resolution, vertex)) for vertex in vertices]], [faces], obj_filepath)


def union_obj_components(vertices: list[list[tuple]], faces: list[list[tuple]], filepath: str, check_manifold=True, verbose=True):
    """
    Takes the union of all the components in the given source .obj
    file, and stores the union in the given output file.

    vertices: `list[list[tuple]]`
        The vertices of the different objects. The first
        axis separates the different objects. The second
        axis separates different vertices in the same
        object.

    faces: `list[list[tuple]]`
        The faces of the different objects. The first
        axis separates the different objects. The second
        axis separates different faces in the same
        object. Must have `len(faces) == len(vertices)`.
        The indices should be local to their own object.
        The indices should be 0-indexed.

    filepath: `str`
        The filepath into which the unioned components will be
        stored.

    check_manifold: `bool`, optional
        Whether the resulting mesh will be checked to be a manifold.
        Is an expensive operation. Default is `True`.

    verbose: `bool`, optional
        Whether status messages will be printed to the console.
        Default is `True`.
    """

    # Prints a beginning status
    if verbose:
        print(f"Taking the union of {filepath}...")

    # Stores each component's mesh
    component_meshes = [trimesh.Trimesh(vertices=vertices[i], faces=faces[i]) for i in range(len(vertices))]

    # Computes the union of the meshes
    union_mesh: trimesh.Trimesh = trimesh.boolean.union(component_meshes)

    # Checks for manifold properties
    if check_manifold and not union_mesh.is_watertight:

        # Tries to fix the mesh
        union_mesh.remove_degenerate_faces()
        union_mesh.remove_duplicate_faces()
        union_mesh.merge_vertices()
        union_mesh.fill_holes()
        union_mesh.fix_normals()

        # Warns the user the mesh is still not manifold
        if not union_mesh.is_watertight:
            print(f"WARNING: {filepath} is not a manifold mesh.")

    # Rescales the vertices
    rescaled_vertices = np.array(union_mesh.vertices)
    union_mesh = trimesh.Trimesh(vertices=rescaled_vertices, faces=union_mesh.faces)

    # Exports the mesh
    union_mesh.export(filepath)

    # Prints a ending status
    if verbose:
        print("Union finished!")

    return union_mesh


def estimate_volumes(materials: torch.Tensor, device='cuda') -> float:

    # Will store the material volumes
    num_materials = materials.shape[0]
    materials = materials.to(device)
    volumes = torch.zeros(num_materials, dtype=torch.float64).to(device)

    # Stores indices for edges/faces
    edge_node_indices = torch.tensor([[n1,n2] for n1 in range(NUM_NODES) for n2 in range(n1+1, NUM_NODES)])
    face_node_indices = torch.tensor([[n1,n2,n3] for n1 in range(NUM_NODES) for n2 in range(n1+1, NUM_NODES) for n3 in range(n2+1, NUM_NODES)])
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
    active_nodes = torch.zeros((num_materials, NUM_NODES)).to(device)
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
    edge_params += node_coords[:,edge_node_indices[:,0]]

    # Stores values for the Bezier parameters for edges
    node1_coords = node_coords[:,edge_node_indices[:,0]]
    node2_coords = node_coords[:,edge_node_indices[:,1]]
    
    # Stores the Bezier parameters for edges
    edge_bezier_params = torch.cat([node1_coords, edge_params, node2_coords], dim=2)

    # Computes the edges' points
    # Shape: (num_materials, num_edges, edge_segments_vertices, 3)
    edge_points = BEZIER_CURVE_COEFFICIENTS_TENSOR.to(device) @ edge_bezier_params

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
    face_params += node_coords[:,face_node_indices[:,0]]

    # Stores values for the Bezier parameters for faces
    node1_coords = node_coords[:,face_node_indices[:,0]]
    node2_coords = node_coords[:,face_node_indices[:,1]]
    node3_coords = node_coords[:,face_node_indices[:,2]]
    edge1_params = edge_params[:,face_edge_indices[:,0]]
    edge2_params = edge_params[:,face_edge_indices[:,1]]
    edge3_params = edge_params[:,face_edge_indices[:,2]]

    # Stores the Bezier parameters for faces
    face_bezier_params = torch.cat([
        node1_coords, node2_coords, node3_coords,
        edge1_params, edge2_params, edge3_params,
        face_params
    ], dim=2)

    # Computes the faces' points
    # Shape: (num_materials, num_face, face_segments_vertices, 3)
    face_points = BEZIER_TRIANGLE_COEFFICIENTS_TENSOR.to(device) @ face_bezier_params

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


def estimate_volume(material: Metamaterial) -> float:
    """
    Estimates the volume of the given metamaterial with the process
    currently used for meshing its surface.

    material: `Metamaterial`
        The material whose volume will be estimated.

    Returns: `float`
        The estimated volume of the given metamaterial.
    """

    # Computes the volume due to the nodes using sphere volume
    total_volume = 4 / 3 * np.pi * material.get_thickness() ** 3 * len(material.active_nodes())

    # Computes the volume due to edges and faces
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):

            # Skips non-edge
            if not material.has_edge(n1,n2):
                continue

            # Computes edge volume
            total_volume += np.linalg.norm(material.get_node_position(n1) - material.get_node_position(n2)) * np.pi * material.get_thickness() ** 2

            for n3 in range(n2+1, NUM_NODES):

                # Skips non-faces
                if not material.has_face(n1,n2,n3):
                    continue
                
                # Computes face volume
                cross_prod = np.cross(material.get_node_position(n2)-material.get_node_position(n1), material.get_node_position(n3)-material.get_node_position(n1))
                total_volume += np.linalg.norm(cross_prod) * material.get_thickness()

    return total_volume


def estimate_surface_area(material: Metamaterial) -> float:
    """
    Estimates the surface area of the given metamaterial with the process
    currently used for meshing its surface.

    material: `Metamaterial`
        The material whose surface area will be estimated.

    Returns: `float`
        The estimated surface area of the given metamaterial.
    """

    # Computes the surface area due to the nodes using sphere surface area
    total_surface_area = 4 * np.pi * material.get_thickness() ** 2 * len(material.active_nodes())

    # Computes the surface area due to edges and faces
    for n1 in range(NUM_NODES):
        for n2 in range(n1+1, NUM_NODES):

            # Skips non-edge
            if not material.has_edge(n1,n2):
                continue

            # Computes edge volume
            total_surface_area += np.linalg.norm(material.get_node_position(n1) - material.get_node_position(n2)) * 2 * np.pi * material.get_thickness()

            for n3 in range(n2+1, NUM_NODES):

                # Skips non-faces
                if not material.has_face(n1,n2,n3):
                    continue
                
                # Computes face volume
                cross_prod = np.cross(material.get_node_position(n2)-material.get_node_position(n1), material.get_node_position(n3)-material.get_node_position(n1))
                total_surface_area += np.linalg.norm(cross_prod)

    return total_surface_area