import sys
import pathlib
sys.path.append(str(pathlib.Path(__file__).parents[1]))

import torch
import numpy as np

from representation.surface_meshing import *
from representation.generation import *
from representation.rep_class import *
from example_materials.tetrahedron_wireframe import TETRAHEDRON_WIREFRAME
from example_materials.hexagon_wireframe import HEXAGON_WIREFRAME
from example_materials.schwarz_p_shell import SCHWARZ_P_SHELL


class Geometry:

    def __init__(self, filepath: str, only_load_basics=False):
        """
        Initializes a geometry from the given quad mesh geometry file.

        filepath: `str`
            The path to the OBJ file where the quad mesh is located.

        only_load_basics: `bool`, optional
            Whether only vertices and faces will be loaded.
        """

        # Stores the geometry
        self.vertices = torch.zeros((0,3), dtype=torch.float64)
        self.faces: list[tuple[int]] = []

        # Opens the file
        with open(filepath) as f:

            # Reads all lines
            while True:
                line = f.readline()

                # EOF
                if len(line) == 0:
                    break

                # Handles a vertex coordinate
                if line[0:2] == "v ":
                    coords = line[2:].split(" ")
                    coords = list(map(float, coords))
                    self.vertices = torch.cat([self.vertices, torch.tensor([coords])])

                # Handles a face
                if line[0] == "f":
                    vertices = line[2:].split(" ")[:4]
                    for i,vertex in enumerate(vertices):
                        if "/" in vertex:
                            vertices[i] = vertex[:vertex.index("/")]
                    self.faces.append(tuple(map(lambda x: int(x)-1, vertices)))

        if not only_load_basics:

            # Stores the vertex edge graph relationships
            # Maps vertices to their connected vertices
            self.edge_graph: list[list[int]] = [[] for _ in range(self.vertices.shape[0])]
            for face in self.faces:
                for v1 in face:
                    for v2 in face:
                        if v1 == v2 or v2 in self.edge_graph[v1]:
                            continue
                        self.edge_graph[v1].append(v2)

            # Stores the vertex face graph relationships
            # Maps vertices to their corresponding faces
            self.face_graph: list[list[int]] = [[] for _ in range(self.vertices.shape[0])]
            for i,face in enumerate(self.faces):
                for v in face:
                    self.face_graph[v].append(i)

            # Stores each face's normal
            self.face_normals = torch.zeros((0,3), dtype=torch.float64)
            for v1,v2,v3,v4 in self.faces:
                edge1 = self.vertices[v3]-self.vertices[v2]
                edge2 = self.vertices[v1]-self.vertices[v2]
                if torch.abs(torch.dot(edge1,edge2) / torch.norm(edge1) / torch.norm(edge2) - 1) < 1e-4:
                    edge1 = self.vertices[v4]-self.vertices[v3]
                    edge2 = self.vertices[v2]-self.vertices[v3]
                normal = torch.cross(edge1, edge2)
                self.face_normals = torch.cat([self.face_normals, (normal / torch.norm(normal)).unsqueeze(0)])

            # Stores an initial guess for vertex normals
            self.vertex_normals = torch.stack([
                torch.stack([self.face_normals[face] for face in self.face_graph[vertex]])
                    .sum(dim=0) / len(self.face_graph[vertex])
                        for vertex in range(self.vertices.shape[0])
            ])
            self.vertex_normals = self.vertex_normals / torch.norm(self.vertex_normals, dim=1, keepdim=True)

            # Stores faces sharing an edge
            self.adjacent_faces: list[list[int]] = [[] for _ in range(len(self.faces))]
            for face,vertices in enumerate(self.faces):
                for i in range(4):
                    v1,v2 = vertices[i],vertices[(i+1)%4]

                    # Checks for the face sharing this edge
                    for other_face in self.face_graph[v1]:
                        if other_face != face and other_face in self.face_graph[v2]:
                            self.adjacent_faces[face].append(other_face)
                            break
                        

    def load_vertex_normals(self, filepath: str):
        """
        Loads the given tensor file into this geometry's vertex normals.

        filepath: `str`
            The filepath at which the tensor is located.
        """
        self.vertex_normals = torch.load(filepath)


    def normal_vector_direction_loss(self, normals: torch.Tensor) -> torch.Tensor:
        """
        Computes the loss of the face normal vector directions.

        normals: `torch.Tensor`
            The a 2D tensor with the normals ordered as the vertices
            of this geometry are ordered. The first dimension separates
            individual normal vectors, and the second dimension separates
            the coordinates of a particular normal vector.

        Returns: `torch.Tensor`
            The loss value.
        """

        # Stores the loss
        loss = torch.tensor([0.], requires_grad=True, dtype=torch.float64)

        # Runs through each vertex
        for n1 in range(self.vertices.shape[0]):

            # Runs through each adjacent vertex
            for i,n2 in enumerate(self.edge_graph[n1]):

                # Computes the loss of how coplanar the normals are
                edge = (self.vertices[n2] - self.vertices[n1]).to(torch.float64)
                if torch.norm(edge) < 1e-4:
                    edge = (self.vertices[self.edge_graph[n1][1-i]] - self.vertices[n1]).to(torch.float64)
                edge = edge / torch.norm(edge)
                loss = loss + torch.dot(torch.cross(edge, normals[n1]), normals[n2]) ** 2

            # Computes the loss of how similar the resulting normal is to the face's individual normals
            for face in self.face_graph[n1]:
                loss = loss - torch.dot(self.face_normals[face], normals[n1])

        # Computes the loss of the normal being a unit vector
        for n in range(len(self.vertices)):
            loss = loss + (torch.norm(normals[n]) - 1) ** 2

        return loss / self.vertices.shape[0]
    

    def optimize_normal_vector_direction(self, iterations: int):
        """
        Optimizes the normal directions of the geometry to form a
        torsion-free beam layout.

        iterations: `int`
            The number of iterations of gradient descent
            optimization to use.

        loss_fn
            The loss function, either for normal vector direction or
            normal vector length.
        """

        # Stores the normals as parameters
        normal_parameters = torch.nn.Parameter(self.vertex_normals)

        # Stores the optimizer
        optim = torch.optim.Adam([normal_parameters], lr=1e-1)

        # Stores the best-seen parameters
        best_loss = float("inf")
        best_parameters = normal_parameters.clone().detach()

        # Runs through each iteration
        for iter in range(iterations):
            loss = self.normal_vector_direction_loss(normal_parameters)
            optim.zero_grad()
            loss.backward()
            optim.step()

            # Prints the loss
            if iter % 1 == 0:
                print(f"Loss #{iter}: {loss.item()}", "(NEW BEST!)" if loss.item() < best_loss else "")
                
            # Stores the best parameters
            if loss.item() < best_loss:
                best_loss = loss.item()
                best_parameters = normal_parameters.clone().detach()

        # Updates the vertex normals
        self.vertex_normals = best_parameters / torch.norm(best_parameters, dim=1, keepdim=True)

        # Saves the vertex normals on disk
        torch.save(self.vertex_normals, "local_test/vertex_normals.pt")


    def normal_vector_length_loss(self, normal_lengths: torch.Tensor) -> torch.Tensor:
        """
        Computes the loss of the face normal vector lengths.

        normals: `torch.Tensor`
            The a 2D tensor with the normals ordered as the vertices
            of this geometry are ordered. The first dimension separates
            individual normal vectors, and the second dimension separates
            the coordinates of a particular normal vector.

        Returns: `torch.Tensor`
            The loss value.
        """

        # Stores the loss
        loss = torch.tensor([0.], requires_grad=True, dtype=torch.float64)

        # Runs through each face
        for v1,v2,v3,v4 in self.faces:

            # Stores the extruded points
            point1 = self.vertices[v1] + self.vertex_normals[v1] * normal_lengths[v1]
            point2 = self.vertices[v2] + self.vertex_normals[v2] * normal_lengths[v2]
            point3 = self.vertices[v3] + self.vertex_normals[v3] * normal_lengths[v3]
            point4 = self.vertices[v4] + self.vertex_normals[v4] * normal_lengths[v4]

            # Computes the edges
            edge1 = point3 - point2
            edge2 = point1 - point2
            normal = torch.cross(edge1, edge2)

            # Computes the loss of how coplanar the extruded quad's points are
            loss = loss + torch.dot(normal, point4-point1)**2
        loss = loss / len(self.faces)

        # Computes the loss of how close to unit vectors the normals are
        loss = loss + ((torch.norm(self.vertex_normals * normal_lengths.unsqueeze(1), dim=1) - 1) ** 2).sum() / self.vertices.shape[0]

        return loss
    

    def optimize_normal_vector_length(self, iterations: int):
        """
        Optimizes the normal directions of the geometry to form a
        torsion-free beam layout.

        iterations: `int`
            The number of iterations of gradient descent
            optimization to use.

        loss_fn
            The loss function, either for normal vector direction or
            normal vector length.
        """

        # Stores the normals as parameters
        length_parameters = torch.nn.Parameter(torch.ones(self.vertices.shape[0]))

        # Stores the optimizer
        optim = torch.optim.Adam([length_parameters], lr=1e-1)

        # Stores the best-seen parameters
        best_loss = float("inf")
        best_parameters = length_parameters.clone().detach()

        # Runs through each iteration
        for iter in range(iterations):
            loss = self.normal_vector_length_loss(length_parameters)
            optim.zero_grad()
            loss.backward()
            optim.step()

            # Prints the loss
            if iter % 1 == 0:
                print(f"Loss #{iter}: {loss.item()}", "(NEW BEST!)" if loss.item() < best_loss else "")
                
            # Stores the best parameters
            if loss.item() < best_loss:
                best_loss = loss.item()
                best_parameters = length_parameters.clone().detach()

        # Updates the vertex normals
        self.vertex_normals = self.vertex_normals * best_parameters.unsqueeze(1)

        # Saves the vertex normals on disk
        torch.save(self.vertex_normals, "local_test/vertex_normals.pt")


    def form_beams(self):

        vertices = [
            tuple(map(lambda t: t.item(), vertex)) for vertex in self.vertices
        ] + [
            tuple(map(lambda t: t.item(), vertex + normal)) for vertex,normal in zip(self.vertices, self.vertex_normals)
        ]

        faces = []
        seen_edges = set()
        for face in self.faces:
            
            for i in range(4):
                edge = tuple(sorted((face[i], face[(i+1)%4])))
                if edge in seen_edges:
                    continue

                faces.append((edge[0], edge[1], edge[1]+self.vertices.shape[0], edge[0]+self.vertices.shape[0]))

        save_multi_obj([vertices], [faces], "local_test/beams.obj")


    def form_shell(self):

        vertices = [
            tuple(map(lambda t: t.item(), vertex + normal)) for vertex,normal in zip(self.vertices, self.vertex_normals)
        ]

        save_multi_obj([vertices], [self.faces], "local_test/0shell.obj")


    def paint_metamaterial_face(self, material: Metamaterial, face: int) -> tuple[list[tuple[int]], list[tuple[int]]]:
        """
        Paints the given metamaterial on the given face of the geometry.

        material: `Metamaterial`
            The metamaterial to be painted on the geometry.

        face: `int`
            The face on which the metamaterial will be painted

        Returns: `tuple[list[tuple[int]], list[tuple[int]]]`
            The mesh vertices and faces as defined in `generate_metamaterial_surface_mesh()`.
        """

        # Stores the face vertices
        v1,v2,v3,v4 = self.faces[face]

        # Computes the boundaries
        boundaries = np.array([
            self.vertices[v1].detach().numpy(),
            self.vertices[v2].detach().numpy(),
            self.vertices[v4].detach().numpy(),
            self.vertices[v3].detach().numpy(),
            (self.vertices[v1] + self.vertex_normals[v1]).detach().numpy(),
            (self.vertices[v2] + self.vertex_normals[v2]).detach().numpy(),
            (self.vertices[v4] + self.vertex_normals[v4]).detach().numpy(),
            (self.vertices[v3] + self.vertex_normals[v3]).detach().numpy(),
        ])

        # Sets up the boundaries
        mat = material.copy()
        mat.set_boundaries(boundaries)

        # Computes the vertices and faces
        return generate_metamaterial_grid_surface_mesh(mat, being_painted=True)


    def paint_metamaterial(self, material: Metamaterial, filepath: str):
        """
        Paints the given metamaterial uniformly (i.e., without interpolation)
        along the surface of this geometry.

        material: `Metamaterial`
            The metamaterial to be painted along the geometry.

        filepath: `str`
            The filepath at which to save the resulting mesh.
        """

        # Stores the vertices and faces for the mesh
        vertices, faces = [], []

        # Runs through each face
        for face in range(len(self.faces)):

            # Gives a status update
            if face % 10 == 0:
                print(f"Face {face}/{len(self.faces)}")

            # Stores the vertices and faces
            next_vertices, next_faces = self.paint_metamaterial_face(material, face)
            vertices.extend(next_vertices)
            faces.extend(next_faces)

        # Saves the object
        save_multi_obj(vertices, faces, filepath, one_component=True)


    def paint_interpolated_metamaterials(self, material1: Metamaterial, face1: int, material2: Metamaterial, face2: int, filepath: str):
        """
        Paints the interplation of the two metamaterials on this geometry.

        material1: `Metamaterial`
            The first material root.

        face1: `int`
            The first face root.

        material2: `Metamaterial`
            The second material root.

        face2: `int`
            The second face root.

        filepath: `str`
            The filepath at which to save the resulting mesh.
        """

        # Stores the face distances
        print("Computing face distances...")
        face_distances = [
            [
                torch.norm(self.vertices[face[0]] - self.vertices[self.faces[face1][0]]).item(),
                torch.norm(self.vertices[face[0]] - self.vertices[self.faces[face2][0]]).item(),
            ] for face in self.faces
        ]

        # Stores the interpolated materials
        print("Interpolating materials...")
        interpolated_materials = []
        for i,material in enumerate(smooth_interpolation(material1, material2)):
            if i % 10 == 0:
                print("Interpolated Material:", i)
            interpolated_materials.append(material)
        num_interpolations = len(interpolated_materials)

        # Stores the interpolation step for each face
        print("Computing the number of interpolation steps...")
        interp_steps = [
            int((num_interpolations-1) * face_distances[face][0] / (face_distances[face][0] + face_distances[face][1]))
                for face in range(len(self.faces))
        ]

        # Stores the vertices and faces for the mesh
        vertices, faces = [], []

        # Runs through each face
        print("Computing each face's mesh...")
        for face,interp_step in enumerate(interp_steps):

            # Gives a status update
            if face % 10 == 0:
                print(f"Face {face}/{len(self.faces)}")

            # Stores the vertices and faces
            material = interpolated_materials[interp_step]
            next_vertices, next_faces = self.paint_metamaterial_face(material, face)
            vertices.extend(next_vertices)
            faces.extend(next_faces)

        # Saves the object
        save_multi_obj(vertices, faces, filepath, one_component=True)


def tri_mesh_to_quad_mesh(input_filepath: str, output_filepath: str):
    """
    Converts the given triangle mesh into a quad mesh.

    input_filepath: `str`
        The filepath to the triangle mesh file. Must be an OBJ.

    output_filepath: `str`
        The filepath to the quad mesh file to be made. Must be an OBJ.
    """

    # Stores the geometry
    geometry = Geometry(input_filepath, only_load_basics=True)

    # Stores each edge
    edge_set = set()
    edges = []
    edge_index = {}
    for face in geometry.faces:
        for i in range(3):
            edge = tuple(sorted((face[i], face[(i+1)%3])))
            if edge not in edge_set:
                edge_index[edge] = len(edges)
                edge_set.add(edge)
                edges.append(edge)

    # Computes the edge midpoints
    edge_midpoints = [
        (geometry.vertices[v1] + geometry.vertices[v2]) / 2
            for v1,v2 in edges
    ]

    # Computes the face midpoints
    face_midpoints = [
        (geometry.vertices[v1] + geometry.vertices[v2] + geometry.vertices[v3]) / 3
            for v1,v2,v3 in geometry.faces
    ]

    # Stores the new faces
    new_faces = []
    for i,face in enumerate(geometry.faces):

        # Runs through each new quad
        for j in range(3):

            # Stores the first corresponding edge
            first_edge = tuple(sorted((face[j], face[(j+1)%3])))

            # Stores the vertex index for the first midpoint along the edge
            first_edge_midpoint_index = geometry.vertices.shape[0] + edge_index[first_edge]

            # Stores the vertex index for the face midpoint
            face_midpoint_index = geometry.vertices.shape[0] + len(edge_midpoints) + i

            # Stores the second corresponding edge
            second_edge = tuple(sorted((face[j], face[(j+2)%3])))

            # Stores the vertex index for the second midpoint along the edge
            second_edge_midpoint_index = geometry.vertices.shape[0] + edge_index[second_edge]

            # Stores the new face
            new_faces.append((face[j], first_edge_midpoint_index, face_midpoint_index, second_edge_midpoint_index))

    # Stores the new vertices
    old_vertices = [tuple(vertex) for vertex in geometry.vertices]
    new_vertices = old_vertices + edge_midpoints + face_midpoints

    # Exports the quad mesh
    save_multi_obj([new_vertices], [new_faces], output_filepath)


# remove_quad_singularities("local_test/quad_stanford_bunny.obj", "local_test/quad_stanford_bunny2.obj")
# tri_mesh_to_quad_mesh("local_test/quad_stanford_bunny2.obj", "local_test/quad_stanford_bunny3.obj")

        

# tri_mesh_to_quad_mesh("local_test/cube.obj", "local_test/cube_quads.obj")

# # geometry = Geometry("local_test/cube_quads.obj")
# geometry = Geometry("local_test/quad_stanford_bunny3.obj")
# # geometry.optimize_normal_vector_direction(125)
# # geometry.optimize_normal_vector_length(300)
# geometry.load_vertex_normals("local_test/bunny_vertex_normals3.pt")
# # geometry.form_beams()
# # geometry.paint_interpolated_metamaterials(TETRAHEDRON_WIREFRAME, 943, HEXAGON_WIREFRAME, 1780, "local_test/octet_to_hexagon_bunny.obj")
# geometry.paint_metamaterial(TETRAHEDRON_WIREFRAME, "local_test/octet_bunny_1000.obj")


from CGAL.CGAL_Kernel import Point_3, Segment_3, Triangle_3
from CGAL.CGAL_Kernel import intersection

# Define two triangles
triangle1 = Triangle_3(Point_3(0, 0, 0), Point_3(1, 0, 0), Point_3(0, 1, 0))
triangle2 = Triangle_3(Point_3(0.2, 0.2, -1), Point_3(0.2, 0.2, 1), Point_3(0.2, 2, -1))

# Check for intersection and get the intersection object
result = intersection(triangle1, triangle2)

print(result)

# Check the type of intersection and retrieve points
if isinstance(result, Point_3):
    print("Intersection at point:", result)
elif isinstance(result, Segment_3):
    print("Intersection along segment from", result.source(), "to", result.target())
else:
    print("No intersection or complex intersection")



