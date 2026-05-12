import trimesh
import numpy as np
from representation.rep_class import *
from representation.surface_meshing import *
from example_materials.tetrahedron_wireframe import *

class VoxelGeometry:

    def __init__(self, filepath: str, pitch: float, verbose=False):
        """
        Creates a voxel geometry from the given non-voxel OBJ mesh.

        filepath: `str`
            The path to the non-voxel OBJ file.

        pitch: `float`
            The size of each dimensions of the voxels to use.

        verbose: `bool`, optional
            Whether status updates will be given as voxelization happens.
        """

        # Stores the geometry properties
        self.pitch = pitch

        # Loads the mesh
        if verbose:
            print("Loading the original mesh...")
        mesh: trimesh.Trimesh = trimesh.load(filepath)

        # Voxelizes the mesh
        if verbose:
            print("Voxelizing the mesh...")
        voxelized_mesh = mesh.voxelized(pitch)
        self.voxels: list[tuple[int]] = [tuple(voxel) for voxel in voxelized_mesh.sparse_indices]

        # Stores the voxel vertices
        if verbose:
            print("Storing the voxel mesh vertices...")
        self.vertex_to_index: dict[tuple[int], int] = {}
        self.vertices: list[np.ndarray] = []
        for x,y,z in self.voxels:

            # Computes each vertex in the current voxel
            for dx in range(2):
                for dy in range(2):
                    for dz in range(2):
                        vertex = (x+dx, y+dy, z+dz)

                        # Checks if duplicate
                        if vertex in self.vertex_to_index:
                            continue

                        # Stores the vertex and its mapping
                        self.vertex_to_index[vertex] = len(self.vertices)
                        self.vertices.append(pitch * np.array(vertex))

        # Stores the vertex adjacency
        if verbose:
            print("Finding adjacent vertices...")
        self.vertex_graph: list[list[int]] = [[] for _ in range(len(self.vertices))]
        for x,y,z in self.vertex_to_index:
            this_index = self.vertex_to_index[(x,y,z)]

            # Runs through each possible adjacent vertex
            for dx in [-1,1]:
                for dy in [-1,1]:
                    for dz in [-1,1]:
                        vertex = (x+dx, y+dy, z+dz)

                        # Stores existent adjacent vertices
                        if vertex in self.vertex_to_index:
                            self.vertex_graph[this_index].append(self.vertex_to_index[vertex])

        # Stores the vertices corresponding to each voxel in the order required by the metamaterial class
        if verbose:
            print("Computing the voxel cubes")
        self.voxel_cubes: list[list[int]] = []
        for x,y,z in self.voxels:
            cube = []
            
            # Runs through each vertex in the cube
            for dy in range(2):
                for dz in range(2):
                    for dx in range(2):
                        vertex = (x+dx, y+dy, z+dz)
                        cube.append(self.vertex_to_index[vertex])

            self.voxel_cubes.append(cube)

        # Stores the faces
        if verbose:
            print("Computing the voxel faces...")
        self.faces: list[tuple[int]] = []
        seen_vertices: set[int] = set()
        for x,y,z in self.voxels:
            seen_vertices.add(self.vertex_to_index[(x,y,z)])

            # Stores the x=0 face
            conflict_vertex = (x-1,y,z)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x,y+dy,z+dz)] for dy,dz in zip([0,1,1,0],[1,1,0,0])))

            # Stores the y=0 face
            conflict_vertex = (x,y-1,z)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x+dx,y,z+dz)] for dx,dz in zip([0,1,1,0],[0,0,1,1])))

            # Stores the z=0 face
            conflict_vertex = (x,y,z-1)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x+dx,y+dy,z)] for dx,dy in zip([0,1,1,0],[1,1,0,0])))

            # Stores the x=1 face
            conflict_vertex = (x+1,y,z)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x+1,y+dy,z+dz)] for dy,dz in zip([0,1,1,0],[0,0,1,1])))

            # Stores the y=1 face
            conflict_vertex = (x,y+1,z)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x+dx,y+1,z+dz)] for dx,dz in zip([0,1,1,0],[1,1,0,0])))

            # Stores the z=1 face
            conflict_vertex = (x,y,z+1)
            if self.vertex_to_index.get(conflict_vertex, -1) not in seen_vertices:
                self.faces.append(tuple(self.vertex_to_index[(x+dx,y+dy,z+1)] for dx,dy in zip([0,1,1,0],[0,0,1,1])))


    def smooth_vertices(self, strength: float):
        """
        Smooths the vertices in the voxel geometry by taking
        the weighted average of each vertex's adjacent vertex
        positions.

        strength: `float`
            The amount to weigh the adjacent vertices' positions.
            Must be <= 1.
        """

        # Will store the new vertices
        new_vertices = []

        # Goes through each vertex
        for v in range(len(self.vertices)):
            new_vertex = np.zeros(3)

            # Goes through each adjacent vertex
            num_vertices = len(self.vertex_graph[v])+1
            for adj_v in self.vertex_graph[v]:
                new_vertex = new_vertex + self.vertices[adj_v]
            new_vertex = new_vertex * (strength/(num_vertices-1))

            # Weighs the current position
            new_vertex = new_vertex + self.vertices[v] * (1-strength)

            # Stores the new vertex
            new_vertices.append(new_vertex)

        # Stores all the new vertices
        self.vertices = new_vertices


    def paint_material(self, material: Metamaterial, filepath: str, status_frequency=999_999):
        """
        Paints the given material at each voxel, where each voxel will
        correspond to one 2x2x2 grid of the given microstructure.

        material: `Metamaterial`
            The material to be painted.

        fliepath: `str`
            The filepath at which the painted model will be saved.

        status_frequency: `int`, optional
            The frequency at which a status message will be printed, with respect
            to how many voxels have been processed.
            The default value is an arbitrary, large value that generally
            results in no prints. If more than that number of voxels exist, manually
            increase the number to prevent printing
        """

        # Stores the vertices and faces for the mesh
        vertices, faces = [], []

        # Goes through each voxel cube
        total_voxels = len(self.voxels)
        for i,voxel_cube in enumerate(self.voxel_cubes):

            # Gives a status update
            if (i+1) % status_frequency == 0:
                print(f"Voxel {i+1}/{total_voxels} ({round(100*(i+1)/total_voxels, 2)}%)")

            boundaries = np.stack([self.vertices[vertex_index] for vertex_index in voxel_cube])

            # Applies the voxel boundaries
            voxel_material = material.copy()
            voxel_material.set_boundaries(boundaries)

            # Stores the vertices and faces
            next_vertices, next_faces = generate_metamaterial_grid_surface_mesh(voxel_material, being_painted=True)
            vertices.extend(next_vertices)
            faces.extend(next_faces)

        # Saves the mesh
        save_multi_obj(vertices, faces, filepath, one_component=True)


    def save_voxel_mesh(self, filepath: str):
        """
        Saves the voxel mesh with vertices as currently defined.
        Does NOT paint with a metamaterial.

        filepath: `str`
            The file path at which to save the voxel mesh.
        """

        vertices = [tuple(vertex) for vertex in self.vertices]
        save_multi_obj([vertices], [self.faces], filepath, one_component=True)
        
