import torch
from representation.rep_utils import *

# Computes the face IDs corresponding to each node triplet
NODES_TO_FACE = torch.tensor([[[
    face_adj_index(n1,n2,n3)
        for n1 in range(NUM_NODES)]
            for n2 in range(NUM_NODES)]
                for n3 in range(NUM_NODES)
], device=DEVICE)

# Stores the face indices that corresponds to each edge, ordered by edge ID
FACE_EDGE_INDICES = torch.tensor([
    [face_adj_index(n1,n2,n3) for n3 in range(NUM_NODES) if n3 != n1 and n3 != n2]
        for n1 in range(NUM_NODES)
            for n2 in range(n1+1, NUM_NODES)
])


def generate_random_permutations(max_values: torch.Tensor) -> torch.Tensor:
    """
    Generates random permutations of the numbers between 0 (inclusive)
    and `max_values` (exclusive). Any remaining numbers are a permutation
    of the remaining numbers.
    """

    # Computes all the permutations
    flat_max_values = max_values.flatten()
    num_samples = flat_max_values.shape[0]
    all_perms = torch.rand((num_samples, max_values.max().item()), device=DEVICE).argsort(dim=-1)+1

    # Masks the values above the threshold
    mask = (all_perms <= flat_max_values.unsqueeze(-1)).to(torch.int32)

    # Computes the permutations
    draws = flat_max_values.max().item()
    rows = torch.arange(num_samples, device=DEVICE).reshape(-1,1).expand((num_samples, draws))
    flat_permutations: torch.Tensor = all_perms[rows, mask.argsort(dim=-1, descending=True)]-1

    return flat_permutations.reshape((*max_values.shape, draws))


def generate_random_ints(max_values: torch.Tensor, min_values: torch.Tensor=0) -> torch.Tensor:
    """
    Generates random integers between `min_values` (inclusive) and `max_values` (exclusive).
    """
    return torch.floor(torch.rand(max_values.shape[0], device=DEVICE) * (max_values - min_values) + min_values).to(torch.int32)


def choose_node_x_placements(nodes_left: torch.Tensor, empty_slots: torch.Tensor, grid: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
    """
    Chooses the placement of the next node for each sample along the x dimension.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left for each sample.
        Does NOT mutate.
        `N` is the number of samples.

    empty_slots: `torch.Tensor`
        A `(N,3G+6)` int tensor with a 1 when a particular node placement has not
        been made before, and 0 otherwise.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.

    grid: `torch.Tensor`
        A `(N,G,G,G)` bool tensor with which grid spaces are not taken by a
        node or are too close to a node.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.

    Returns: `tuple[torch.Tensor, torch.Tensor, torch.Tensor]`
        A `(N,)` int tensor with the x slot placements for each node.
        `N` is the number of samples.
        
        A `(N,)` int tensor with the x index placements for each node.
        `N` is the number of samples.
        
        A `(N,)` float tensor with the x coordinate placements for each node.
        `N` is the number of samples.
    """

    # Stores convenient values for the function
    samples_left = grid.shape[0]
    grid_size = grid.shape[1]
    prob_indices = torch.tensor([0] + [i for i in range(grid_size)] + [grid_size-1], dtype=torch.int32, device=DEVICE)

    # Stores which faces are empty
    x0_empty, x1_empty = empty_slots[:,0], empty_slots[:,-3]

    # Checks which faces are still open
    x_probs = grid[:,prob_indices].any(dim=(-2,-1)).to(torch.float32)

    # Disables x=(0,1) when x=(1,0) must happen, and scales the probability
    x_probs[:, 0] *= grid_size*torch.logical_not(torch.logical_and(x1_empty, nodes_left == 1))
    x_probs[:,-1] *= grid_size*torch.logical_not(torch.logical_and(x0_empty, nodes_left == 1))

    # Disables the free choices affecting x=(0,1) when x=(0,1) is still empty
    x_probs[:,[ 1, 2]] *= torch.logical_not(x0_empty).unsqueeze(-1)
    x_probs[:,[-3,-2]] *= torch.logical_not(x1_empty).unsqueeze(-1)

    # Disables free choices when a face must be chosen
    x_probs[:,1:-1] *= (nodes_left > (x0_empty+x1_empty)).unsqueeze(-1)

    # Chooses the placements
    x_placements = torch.multinomial(x_probs, 1).squeeze(1)
    x0_placement = x_placements == 0
    x1_placement = x_placements == grid_size+1
    x_free_placement = torch.logical_not(torch.logical_or(x0_placement, x1_placement))

    # Computes the indices/coordinates due to the placements (x=0 is automatically 0)
    x_indices = (
        x_free_placement * (x_placements-1) +
        x1_placement * (grid_size-1)
    )
    x_coords = (
        x_free_placement * ((x_placements-1 + torch.rand((samples_left,), device=DEVICE)) / grid_size) +
        x1_placement.to(torch.float32)
    )

    return x_placements, x_indices, x_coords


def choose_node_y_placements(nodes_left: torch.Tensor, empty_slots: torch.Tensor, grid: torch.Tensor, x_indices: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
    """
    Chooses the placement of the next node for each sample along the y dimension.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left for each sample.
        Does NOT mutate.
        `N` is the number of samples.

    empty_slots: `torch.Tensor`
        A `(N,3G+6)` int tensor with a 1 when a particular node placement has not
        been made before, and 0 otherwise.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.

    grid: `torch.Tensor`
        A `(N,G,G,G)` bool tensor with which grid spaces are not taken by a
        node or are too close to a node.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.
        
    x_indices: `torch.Tensor`
        A `(N,)` int tensor with the x index placements for each node.
        Does NOT mutate.
        `N` is the number of samples.

    Returns: `tuple[torch.Tensor, torch.Tensor, torch.Tensor]`
        A `(N,)` int tensor with the y slot placements for each node.
        `N` is the number of samples.
        
        A `(N,)` int tensor with the y index placements for each node.
        `N` is the number of samples.
        
        A `(N,)` float tensor with the y coordinate placements for each node.
        `N` is the number of samples.
    """

    # Stores convenient values for the function
    samples_left = grid.shape[0]
    sample_indices = torch.arange(samples_left, device=DEVICE)
    grid_size = grid.shape[1]

    # Stores which faces are empty
    y0_empty, y1_empty = empty_slots[:,1], empty_slots[:,-2]

    # Checks which faces are still open
    y_probs = torch.cat([grid[sample_indices,x_indices,:1], grid[sample_indices,x_indices], grid[sample_indices,x_indices,-1:]], dim=1).any(dim=-1).to(torch.float32)

    # Disables y=(0,1) when y=(1,0) must happen, and scales the probability
    y_probs[:, 0] *= grid_size*torch.logical_not(torch.logical_and(y1_empty, nodes_left == 1))
    y_probs[:,-1] *= grid_size*torch.logical_not(torch.logical_and(y0_empty, nodes_left == 1))

    # Disables the free choices affecting y=(0,1) when y=(0,1) is still empty
    y_probs[:,[ 1, 2]] *= 1-y0_empty.unsqueeze(-1)
    y_probs[:,[-3,-2]] *= 1-y1_empty.unsqueeze(-1)

    # Disables free choices when a face must be chosen
    y_probs[:,1:-1] *= (nodes_left > (y0_empty+y1_empty)).unsqueeze(-1)

    # Chooses the placements
    y_placements = torch.multinomial(y_probs, 1).squeeze(1)
    y0_placement = y_placements == 0
    y1_placement = y_placements == grid_size+1
    y_free_placement = torch.logical_not(torch.logical_or(y0_placement, y1_placement))

    # Computes the indices/coordinates due to the placements (y=0 is automatically 0)
    y_indices = (
        y_free_placement * (y_placements-1) +
        y1_placement * (grid_size-1)
    )
    y_coords = (
        y_free_placement * ((y_placements-1 + torch.rand((samples_left,), device=DEVICE)) / grid_size) +
        y1_placement.to(torch.float32)
    )

    return y_placements, y_indices, y_coords


def choose_node_z_placements(nodes_left: torch.Tensor, empty_slots: torch.Tensor, grid: torch.Tensor, x_indices: torch.Tensor, y_indices: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
    """
    Chooses the placement of the next node for each sample along the z dimension.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left for each sample.
        Does NOT mutate.
        `N` is the number of samples.

    empty_slots: `torch.Tensor`
        A `(N,3G+6)` int tensor with a 1 when a particular node placement has not
        been made before, and 0 otherwise.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.

    grid: `torch.Tensor`
        A `(N,G,G,G)` bool tensor with which grid spaces are not taken by a
        node or are too close to a node.
        Does NOT mutate.
        `N` is the number of samples.
        `G` is the grid size.
        
    x_indices: `torch.Tensor`
        A `(N,)` int tensor with the x index placements for each node.
        Does NOT mutate.
        `N` is the number of samples.
        
    y_indices: `torch.Tensor`
        A `(N,)` int tensor with the y index placements for each node.
        Does NOT mutate.
        `N` is the number of samples.

    Returns: `tuple[torch.Tensor, torch.Tensor, torch.Tensor]`
        A `(N,)` int tensor with the z slot placements for each node.
        `N` is the number of samples.
        
        A `(N,)` int tensor with the z index placements for each node.
        `N` is the number of samples.
        
        A `(N,)` float tensor with the z coordinate placements for each node.
        `N` is the number of samples.
    """

    # Stores convenient values for the function
    samples_left = grid.shape[0]
    sample_indices = torch.arange(samples_left, device=DEVICE)
    grid_size = grid.shape[1]

    # Stores which faces are empty
    z0_empty, z1_empty = empty_slots[:,2], empty_slots[:,-1]

    # Checks which faces are still open
    z_probs = torch.cat([grid[sample_indices,x_indices,y_indices,:1], grid[sample_indices,x_indices,y_indices], grid[sample_indices,x_indices,y_indices,-1:]], dim=1).to(torch.float32)

    # Disables z=(0,1) when z=(1,0) must happen, and scales the probability
    z_probs[:, 0] *= grid_size*torch.logical_not(torch.logical_and(z1_empty, nodes_left == 1))
    z_probs[:,-1] *= grid_size*torch.logical_not(torch.logical_and(z0_empty, nodes_left == 1))

    # Disables the free choices affecting z=(0,1) when z=(0,1) is still empty
    z_probs[:,[ 1, 2]] *= 1-z0_empty.unsqueeze(-1)
    z_probs[:,[-3,-2]] *= 1-z1_empty.unsqueeze(-1)

    # Disables free choices when a face must be chosen
    z_probs[:,1:-1] *= (nodes_left > (z0_empty+z1_empty)).unsqueeze(-1)
    
    # Chooses the placements
    z_placements = torch.multinomial(z_probs, 1).squeeze(1)
    z0_placement = z_placements == 0
    z1_placement = z_placements == grid_size+1
    z_free_placement = torch.logical_not(torch.logical_or(z0_placement, z1_placement))

    # Computes the indices/coordinates due to the placements (z=0 is automatically 0)
    z_indices = (
        z_free_placement * (z_placements-1) +
        z1_placement * (grid_size-1)
    )
    z_coords = (
        z_free_placement * ((z_placements-1 + torch.rand((samples_left,), device=DEVICE)) / grid_size) +
        z1_placement.to(torch.float32)
    )

    return z_placements, z_indices, z_coords


def generate_node_positions(num_nodes: torch.Tensor) -> torch.Tensor:
    """
    Generates random node position samples, where the
    number of samples is the same as the number of
    values in the `num_nodes` input.
    Excluded nodes' positions will be set to the cube center.

    The node positions are generated such that:
        1. Every face in the unit cube has at least one node.
        2. All nodes are on at most three faces of the unit cube.
        3. All nodes are at least about 0.14 units apart.
        4. All extra nodes are placed at the unit cube center.

    num_nodes: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes for each sample.
        Each value must be at least 2, and at most `NUM_NODES`.
        `N` is the number of samples.

    Returns: `tuple[torch.Tensor, torch.Tensor]`
        A tuple of tensors containing the following tensors:
        
        A `(N,R)` float tensor with the random pseudo-spherical
        node positions.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.
        
        A `(N,R//3,3)` float tensor with the random node positions
        transformed into Euclidean coordinates.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.
    """

    # Performs safety checks
    assert num_nodes.numel() > 0, "Must have at least one node count."
    assert len(num_nodes.shape) == 1, "num_nodes must be a 1D tensor."
    assert num_nodes.dtype == torch.int32 or num_nodes.dtype == torch.int64, "num_nodes must be an int tensor."
    assert num_nodes.max().item() <= NUM_NODES, "num_nodes cannot contain values above NUM_NODES."
    assert num_nodes.min().item() >= 2, "num_nodes cannot contain values below 2."

    # Stores relevant & convenient values for the function
    num_samples = num_nodes.shape[0]
    samples_per_group = num_samples
    grid_size = 7
    assert NUM_NODES <= 10, "Current value of grid_size may not work"

    # Stores the grid offset indices
    grid_offsets = torch.stack(torch.meshgrid([torch.tensor([-1, 0, 1], device=DEVICE)]*3, indexing="ij")).reshape((3,-1)).unsqueeze(0)

    # Will store the random node Euclidean coordinates
    node_coords = torch.full((num_samples,NUM_NODES,3), 0.5, device=DEVICE)

    # Runs through each sample group
    for sample in range(0, num_samples, samples_per_group):

        # Stores values for the sample group
        end_sample = min(num_samples, sample+samples_per_group)
        group_size = end_sample-sample
        old_indices = torch.arange(group_size, device=DEVICE)

        # Constructs the possible places to put points
        grid = torch.ones((group_size, *([grid_size]*3)), dtype=torch.bool, device=DEVICE)

        # Stores the number of nodes left
        nodes_left = num_nodes[sample:end_sample].clone()
        max_node_count = nodes_left.max().item()

        # Stores which grid slots are empty
        empty_slots = torch.ones((group_size,3*(grid_size+2)), dtype=torch.int32, device=DEVICE)

        # Runs through each node
        for node in range(max_node_count):

            # Stores the samples still being generated
            sample_indices = torch.nonzero(nodes_left > 0).squeeze(1)
            samples_left = sample_indices.shape[0]

            # Updates the data structures according to which samples are left
            nodes_left = nodes_left[sample_indices]
            empty_slots = empty_slots[sample_indices]
            grid = grid[sample_indices]
            old_indices = old_indices[sample_indices]
            sample_indices = torch.arange(samples_left, device=DEVICE)

            # Chooses the node placements
            x_placements, x_indices, x_coords = choose_node_x_placements(nodes_left, empty_slots, grid)
            y_placements, y_indices, y_coords = choose_node_y_placements(nodes_left, empty_slots, grid, x_indices)
            z_placements, z_indices, z_coords = choose_node_z_placements(nodes_left, empty_slots, grid, x_indices, y_indices)

            # Updates the empty faces
            empty_slots[sample_indices,   x_placements*3] = empty_slots[sample_indices,   x_placements*3]-1
            empty_slots[sample_indices, 1+y_placements*3] = empty_slots[sample_indices, 1+y_placements*3]-1
            empty_slots[sample_indices, 2+z_placements*3] = empty_slots[sample_indices, 2+z_placements*3]-1
            empty_slots.clamp_(min=0)

            # Updates the grid
            grid_indices = torch.stack([x_indices, y_indices, z_indices], dim=-1).unsqueeze(-1) + grid_offsets
            grid_indices.clamp_(min=0, max=grid_size-1)
            grid[sample_indices.unsqueeze(-1), grid_indices[:,0], grid_indices[:,1], grid_indices[:,2]] = False

            # Updates the node coordinates
            node_coords[old_indices+sample,node] = torch.stack([x_coords, y_coords, z_coords], dim=-1)

            # Updates the number of nodes left
            nodes_left -= 1
        
    # Permutes the node coordinate dimensions to ensure uniformity in the random choices
    dim_permutation = torch.rand((num_samples,3), device=DEVICE).argsort(dim=-1)
    rows = torch.arange(num_samples, device=DEVICE).unsqueeze(-1).expand((num_samples, 3))
    node_coords[rows, :, dim_permutation] = node_coords.transpose(1,2).clone()

    # Computes the pseudo-spherical positions
    node_pos = euclidean_to_pseudo_spherical_torch(node_coords).reshape((num_samples,-1))

    # Returns the computed values
    return node_pos, node_coords


def add_edges(edge_adj: torch.Tensor, actions: torch.Tensor, old_indices: torch.Tensor, current_node: torch.Tensor, node_orderings: torch.Tensor, nodes_left: torch.Tensor, edges_left: torch.Tensor):
    """
    Adds a new edge in-place between two nodes to connect the material's topology.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's edge adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the Metamaterial representation.

    actions: `torch.Tensor`
        A `(N,)` int tensor with the actions taken for the current nodes.
        This function's action is taken at values of `0`.
        Does NOT mutate.
        `N` is the number of samples.

    old_indices: `torch.Tensor`
        A `(N,)` int tensor with the original sample index for each sample in the
        current sample group.
        Does NOT mutate.
        `N` is the number of samples.

    current_node: `torch.Tensor`
        A `(N,)` int tensor with the current node for connecting in each sample.
        Does NOT mutate.
        `N` is the number of samples.

    node_orderings: `torch.Tensor`
        A `(N,M)` int tensor with the way in which each sample's nodes are permuted.
        Does NOT mutate.
        `N` is the number of samples.
        `M` is the number of maximum nodes samples.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left to connect for each sample.
        DOES mutate.
        `N` is the number of samples.

    edges_left: `torch.Tensor`
        A `(N,)` int tensor with the number of edges left to add for each sample.
        DOES mutate.
        `N` is the number of samples.
    """

    # Computes which samples require this action
    material_indices = torch.nonzero(actions == 0).squeeze(1)
    if material_indices.numel() > 0:

        # Adjusts the number of nodes/edges left
        nodes_left[material_indices] -= 1
        edges_left[material_indices] -= 1

        # Chooses the nodes on which the edge addition will take place
        node1_index = generate_random_ints(current_node[material_indices])
        node1 = node_orderings[material_indices, node1_index]
        node2 = node_orderings[material_indices, current_node[material_indices]]

        # Adds the edge
        edge_adj[old_indices[material_indices], NODES_TO_EDGE[node1, node2]] = 1


def add_two_node_faces(face_adj: torch.Tensor, actions: torch.Tensor, old_indices: torch.Tensor, current_node: torch.Tensor, node_orderings: torch.Tensor, nodes_left: torch.Tensor, faces_left: torch.Tensor):
    """
    Adds a new face in-place at two new nodes to connect the material's topology.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's face adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    actions: `torch.Tensor`
        A `(N,)` int tensor with the actions taken for the current nodes.
        This function's action is taken at values of `2`.
        Does NOT mutate.
        `N` is the number of samples.

    old_indices: `torch.Tensor`
        A `(N,)` int tensor with the original sample index for each sample in the
        current sample group.
        Does NOT mutate.
        `N` is the number of samples.

    current_node: `torch.Tensor`
        A `(N,)` int tensor with the current node for connecting in each sample.
        Does NOT mutate.
        `N` is the number of samples.

    node_orderings: `torch.Tensor`
        A `(N,M)` int tensor with the way in which each sample's nodes are permuted.
        Does NOT mutate.
        `N` is the number of samples.
        `M` is the number of maximum nodes samples.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left to connect for each sample.
        DOES mutate.
        `N` is the number of samples.

    faces_left: `torch.Tensor`
        A `(N,)` int tensor with the number of faces left to add for each sample.
        DOES mutate.
        `N` is the number of samples.
    """
    
    # Computes which samples require this action
    material_indices = torch.nonzero(actions == 2).squeeze(1)
    if material_indices.numel() > 0:

        # Adjusts the number of nodes/faces left
        nodes_left[material_indices] -= 2
        faces_left[material_indices] -= 1

        # Chooses the nodes on which the face addition will take place
        node1_index = generate_random_ints(current_node[material_indices])
        node1 = node_orderings[material_indices, node1_index]
        node2 = node_orderings[material_indices, current_node[material_indices]]
        node3 = node_orderings[material_indices, current_node[material_indices]+1]

        # Adds the face
        face_adj[old_indices[material_indices], NODES_TO_FACE[node1, node2, node3]] = 1


def add_one_node_faces(face_adj: torch.Tensor, actions: torch.Tensor, old_indices: torch.Tensor, current_node: torch.Tensor, node_orderings: torch.Tensor, nodes_left: torch.Tensor, faces_left: torch.Tensor):
    """
    Adds a new face in-place at one new node to connect the material's topology.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's face adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    actions: `torch.Tensor`
        A `(N,)` int tensor with the actions taken for the current nodes.
        This function's action is taken at values of `1`.
        Does NOT mutate.
        `N` is the number of samples.

    old_indices: `torch.Tensor`
        A `(N,)` int tensor with the original sample index for each sample in the
        current sample group.
        Does NOT mutate.
        `N` is the number of samples.

    current_node: `torch.Tensor`
        A `(N,)` int tensor with the current node for connecting in each sample.
        Does NOT mutate.
        `N` is the number of samples.

    node_orderings: `torch.Tensor`
        A `(N,M)` int tensor with the way in which each sample's nodes are permuted.
        Does NOT mutate.
        `N` is the number of samples.
        `M` is the number of maximum nodes samples.

    nodes_left: `torch.Tensor`
        A `(N,)` int tensor with the number of nodes left to connect for each sample.
        DOES mutate.
        `N` is the number of samples.

    faces_left: `torch.Tensor`
        A `(N,)` int tensor with the number of faces left to add for each sample.
        DOES mutate.
        `N` is the number of samples.
    """

    # Computes which samples require this action
    material_indices = torch.nonzero(actions == 1).squeeze(1)
    if material_indices.numel() > 0:

        # Adjusts the number of nodes/faces left
        nodes_left[material_indices] -= 1
        faces_left[material_indices] -= 1

        # Chooses the nodes on which the face addition will take place
        node_choices = generate_random_permutations(current_node[material_indices])[:,:2]
        node1 = node_orderings[material_indices, node_choices[:,0]]
        node2 = node_orderings[material_indices, node_choices[:,1]]
        node3 = node_orderings[material_indices, current_node[material_indices]]

        # Adds the face
        face_adj[old_indices[material_indices], NODES_TO_FACE[node1, node2, node3]] = 1


def fill_in_faces(face_adj: torch.Tensor, num_faces: torch.Tensor, max_faces: torch.Tensor, max_nodes: int) -> torch.Tensor:
    """
    Fills in the remaining faces for each sample in-place.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's face adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate, but does not normalize.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    num_faces: `torch.Tensor`
        A `(N,)` int tensor with the total number of faces to use in each sample.
        Does NOT mutate.
        `N` is the number of samples.

    max_faces: `torch.Tensor`
        A `(N,)` int tensor with the maximum number of faces each sample can have.
        Does NOT mutate.
        `N` is the number of samples.

    max_nodes: `int`
        The maximum number of nodes any of the samples has.

    Returns: `torch.Tensor`
        The normalized face adjacency.
    """

    # Stores convenient values for the function
    num_samples = face_adj.shape[0]
    sample_indices = torch.arange(num_samples, device=DEVICE)
    faces_left = num_faces-face_adj.sum(dim=-1)

    # Stores the face adjacency indices in order increasing largest node, but shuffled
    face_adj_indices = torch.tensor([
        face_adj_index(n1,n2,n3)
            for n3 in range(max_nodes)
                for n2 in range(n3-1, -1, -1)
                    for n1 in range(n2-1, -1, -1)
    ], device=DEVICE)[generate_random_permutations(max_faces)]

    # Fills in the rest of the faces
    for face in range(num_faces.max().item()):
        face_index = face_adj_indices[:,face]
        no_face = torch.logical_not(face_adj[sample_indices, face_index])
        face_adj[sample_indices, face_index] += faces_left
        faces_left.sub_(no_face.to(torch.float32)).clamp_(min=0)

    return (face_adj > 0).to(torch.float32)


def find_face_edges(face_adj: torch.Tensor) -> torch.Tensor:
    """
    Finds the edges that are being used to construct a face in each sample.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's face adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    Returns: `torch.Tensor`
        A `(N,R)` float tensor with each sample's face-edge adjacency, constructed like
        a typical edge adjacency matrix from the Metamaterial representation.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the Metamaterial representation.
    """
    return face_adj[:,FACE_EDGE_INDICES].any(dim=-1).to(torch.float32)


def fill_in_edges(edge_adj: torch.Tensor, face_edges: torch.Tensor, num_edges: torch.Tensor, max_edges: torch.Tensor, max_nodes: int) -> torch.Tensor:
    """
    Fills in the remaining edges for each sample in-place.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with each sample's edge adjacency, as constructed for
        the Metamaterial representation.
        DOES mutate, but does not normalize.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the Metamaterial representation.

    face_edges: `torch.Tensor`
        A `(N,R)` int tensor with each sample's face-edge adjacency, constructed like
        a typical edge adjacency matrix from the Metamaterial representation.
        Does NOT mutate.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the Metamaterial representation.

    num_edges: `torch.Tensor`
        A `(N,)` int tensor with the total number of edges to use in each sample.
        Does NOT mutate.
        `N` is the number of samples.

    max_edges: `torch.Tensor`
        A `(N,)` int tensor with the maximum number of edges each sample can have.
        Does NOT mutate.
        `N` is the number of samples.

    max_nodes: `int`
        The maximum number of nodes any of the samples has.

    Returns: `torch.Tensor`
        The normalized edge adjacency.
    """

    # Stores convenient values for the function
    num_samples = edge_adj.shape[0]
    sample_indices = torch.arange(num_samples, device=DEVICE)

    # Accounts for the face-edges
    edge_offset = face_edges.sum(dim=-1).to(torch.int32)
    edge_adj = ((edge_adj + face_edges) > 0).to(torch.int32)
    edges_left = num_edges + edge_offset - edge_adj.sum(dim=-1)

    # Stores the edge adjacency indices in order increasing largest node, but shuffled
    edge_adj_indices = torch.tensor([
        edge_adj_index(n1,n2)
            for n2 in range(max_nodes)
                for n1 in range(n2-1, -1, -1)
    ], device=DEVICE)[generate_random_permutations(max_edges)]

    # Fills in the rest of the edges
    edge_cutoff = min(max_edges.max().item(), (num_edges+edge_offset).max().item())
    for edge in range(edge_cutoff):
        edge_index = edge_adj_indices[:,edge]
        no_edge = 1-edge_adj[sample_indices, edge_index]
        edge_adj[sample_indices, edge_index] += edges_left
        edges_left.sub_(no_edge).clamp_(min=0)

    return (edge_adj > 0).to(torch.float32)


def generate_adjacencies(num_nodes: torch.Tensor, num_edges: torch.Tensor, num_faces: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Generates random edge and face adjacencies such that the resulting topology
    of the metamaterial is connected.

    num_nodes: `torch.Tensor`
        A `(N,)` int tensor with the number of active nodes to use in each sample.
        `N` is the number of samples.

    num_edges: `torch.Tensor`
        A `(N,)` int tensor with the maximum number of non-face edges to use in
        each sample. The number is the maximum as the choice of faces is prioritized.
        If the choice of faces allows for this many non-face edges, then there will
        be this many edges. Otherwise, the maximum number of edges with the generated
        face arrangement will be chosen. There will always be at least this many edges.
        `N` is the number of samples.

    num_faces: `torch.Tensor`
        A `(N,)` int tensor with the total number of faces to use in each sample.
        `N` is the number of samples.

    Returns: `tuple[torch.Tensor, torch.Tensor]`
        A `(N,R)` float tensor with the samples' edge adjacencies.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.
        
        A `(N,R)` float tensor with the samples' face adjacencies.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.
    """

    # Performs safety checks
    assert num_nodes.numel() > 0, "Must have at least one node count."
    assert num_nodes.numel() == num_edges.numel(), "Must have the same number of node counts as edge counts."
    assert num_nodes.numel() == num_faces.numel(), "Must have the same number of node counts as face counts."
    assert len(num_nodes.shape) == 1, "num_nodes must be a 1D tensor."
    assert len(num_edges.shape) == 1, "num_edges must be a 1D tensor."
    assert len(num_faces.shape) == 1, "num_faces must be a 1D tensor."
    assert num_nodes.dtype == torch.int32 or num_nodes.dtype == torch.int64, "num_nodes must be an int tensor."
    assert num_edges.dtype == torch.int32 or num_edges.dtype == torch.int64, "num_edges must be an int tensor."
    assert num_faces.dtype == torch.int32 or num_faces.dtype == torch.int64, "num_faces must be an int tensor."
    assert num_nodes.max().item() <= NUM_NODES, "num_nodes cannot contain values above NUM_NODES."
    assert num_nodes.min().item() >= 2, "num_nodes cannot contain values below 2."
    assert torch.all(num_nodes <= 1 + num_edges + 2*num_faces).item(), "There are not enough edges and faces to make all nodes connected."
    max_edges = num_nodes * (num_nodes-1) // 2
    assert torch.all(num_edges <= max_edges).item(), "There are not enough nodes to make all the edges."
    max_faces = num_nodes * (num_nodes-1) * (num_nodes-2) // 6
    assert torch.all(num_faces <= max_faces).item(), "There are not enough nodes to make all the faces."

    # Stores relevant & convenient values for the function
    num_samples = num_nodes.shape[0]
    max_nodes = num_nodes.max().item()

    # Will store the edge and face adjancencies
    edge_adj = torch.zeros((num_samples, EDGE_ADJ_SIZE), device=DEVICE)
    face_adj = torch.zeros((num_samples, FACE_ADJ_SIZE), device=DEVICE)

    # Stores the random node permutation for edge/face connection
    node_orderings = generate_random_permutations(num_nodes)

    # Stores values for making connections
    old_indices = torch.arange(num_samples, dtype=torch.int32, device=DEVICE)
    nodes_left = num_nodes.clone()-1
    edges_left = num_edges.clone()
    faces_left = num_faces.clone()

    # Runs through each node to make the base topology connection
    while True:

        # Stores the samples still being generated
        sample_indices = torch.nonzero(nodes_left > 0).squeeze(1)
        samples_left = sample_indices.shape[0]

        # Breaks out if no samples left
        if samples_left == 0:
            break

        # Updates the data structures storing info about the samples
        old_indices = old_indices[sample_indices]
        node_orderings = node_orderings[sample_indices]
        nodes_left = nodes_left[sample_indices]
        edges_left = edges_left[sample_indices]
        faces_left = faces_left[sample_indices]

        # Stores the current node to which a topology connection is being added
        current_node = num_nodes[old_indices]-nodes_left

        # Stores the probability of each action
        # - 0: new edge
        # - 1: new face with one new node
        # - 2: new face with two new nodes
        actions_pdf = torch.stack([
            edges_left,
            faces_left * (nodes_left < faces_left*2 + edges_left) * (current_node > 1),
            faces_left * (nodes_left > 1),
        ], dim=-1).to(torch.float32)

        # Chooses the actions
        actions = torch.multinomial(actions_pdf, 1).squeeze(1)

        # Handles connecting the topology
        add_edges(edge_adj, actions, old_indices, current_node, node_orderings, nodes_left, edges_left)
        add_one_node_faces(face_adj, actions, old_indices, current_node, node_orderings, nodes_left, faces_left)
        add_two_node_faces(face_adj, actions, old_indices, current_node, node_orderings, nodes_left, faces_left)

    # Fills in the remaining edges and faces
    face_adj = fill_in_faces(face_adj, num_faces, max_faces, max_nodes)
    face_edges = find_face_edges(face_adj)
    edge_adj = fill_in_edges(edge_adj, face_edges, num_edges, max_edges, max_nodes)

    return edge_adj, face_adj


def flat_face_parameters(node_coords: torch.Tensor, face_adj: torch.Tensor) -> torch.Tensor:
    """
    Computes the face parameters for flat faces for
    the given samples' node coordinates.

    node_coords: `torch.Tensor`
        A `(N,R//3,3)` float tensor with the samples' node positions
        transformed into Euclidean coordinates.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' face adjacencies.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.

    Returns: `torch.Tensor`
        A `(N,R,B)` float tensor with the flat face parameters.
        Contains only face parameters for the given active faces.
        All other face parameters are 0.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        `B` is the number of Bezier coordinates that define faces.
        Metamaterial representation.
    """

    # Stores the face parameters that make a flat face
    face_params = node_coords[:, FACE_TO_NODES].sum(dim=2) / 3 - node_coords[:, FACE_TO_NODES[:,0]]
    face_params *= face_adj.unsqueeze(-1)

    return face_params


def base_face_parameters(node_coords: torch.Tensor, face_adj: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Computes the base face parameters for the given samples.

    node_coords: `torch.Tensor`
        A `(N,R//3,3)` float tensor with the samples' node positions
        transformed into Euclidean coordinates.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.

    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' face adjacencies.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.

    Returns: `tuple[torch.Tensor]`
        A `(N,R,B)` float tensor with the samples' corresponding
        flat face parameters.

        A `(N,R,B)` float tensor with the samples' corresponding
        curved face parameters.

        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.
        `B` is the number of Bezier coordinates that define faces.
        The face parameters of non-existent faces are 0.
    """

    # Stores convenient values for the function
    num_samples = node_coords.shape[0]

    # Stores the general shape of the face parameter arrays
    face_params_shape = (num_samples, FACE_ADJ_SIZE, FACE_BEZIER_COORDS)

    # Computes the flat/curved face parameters
    flat_face_params = flat_face_parameters(node_coords, face_adj)
    curved_face_params = torch.rand(face_params_shape, device=DEVICE)

    # Makes each curved face parameter not go beyond the unit cube
    curved_face_params -= node_coords[:, FACE_TO_NODES[:,0]].repeat(1,1,FACE_BEZIER_POINTS)

    # Keeps only active faces' parameters
    curved_face_params *= face_adj.unsqueeze(-1)

    return flat_face_params, curved_face_params


def choose_flat_and_curved_faces(num_curved_faces: torch.Tensor, face_adj: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Chooses which faces will correspond to flat/curved faces
    in the given samples.

    num_curved_faces: `torch.Tensor`
        A `(N,)` tensor with the number of curved faces to use in each sample.
        `N` is the number of samples.

    face_adj: `torch.Tensor`
        A `(N,R)` tensor with the samples' face adjacencies.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    Returns: `tuple[torch.Tensor]`
        A `(N,R)` tensor with the samples' face adjacencies
        just for the chosen flat faces.
        
        A `(N,R)` tensor with the samples' face adjacencies
        just for the chosen curved faces.

        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.
    """

    # Stores convenient values for the function
    num_samples = face_adj.shape[0]
    sample_indices = torch.arange(num_samples, device=DEVICE)
    face_adj = face_adj.to(torch.int32)

    # Stores a shuffling for the faces
    face_shuffle = torch.rand(face_adj.shape, device=DEVICE).argsort(dim=-1)

    # Will store the curved faces
    curved_faces = torch.zeros(face_adj.shape, device=DEVICE)
    faces_left = num_curved_faces.clone()

    # Chooses the curved faces
    for face in range(FACE_ADJ_SIZE):
        face_index = face_shuffle[:,face]
        faces = face_adj[sample_indices, face_index]
        curved_faces[sample_indices, face_index] += faces * faces_left
        faces_left -= faces
    curved_faces = (curved_faces > 0).to(torch.float32)

    # Chooses the flat faces
    flat_faces = face_adj - curved_faces

    return flat_faces, curved_faces


def flat_edge_parameters(node_coords: torch.Tensor, edge_adj: torch.Tensor) -> torch.Tensor:
    """
    Computes the edge parameters for flat edges for
    the given samples' node coordinates.

    node_coords: `torch.Tensor`
        A `(N,R//3,3)` float tensor with the samples' node positions
        transformed into Euclidean coordinates.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' edge adjacencies.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.

    Returns: `torch.Tensor`
        A `(N,R,B)` float tensor with the flat edge parameters.
        Contains only edge parameters for the given active edge.
        All other edge parameters are 0.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        `B` is the number of Bezier coordinates that define edges.
        Metamaterial representation.
    """

    # Computes the vector describing each edge
    edge_vectors = node_coords[:,EDGE_TO_NODES[:,1]] - node_coords[:,EDGE_TO_NODES[:,0]]

    # Computes the amount which the edge vector is scaled for each Bezier point
    vector_scales = torch.arange(EDGE_BEZIER_POINTS, dtype=torch.float32, device=DEVICE).reshape((1,1,-1)).repeat_interleave(3,dim=2) + 1
    vector_scales /= EDGE_BEZIER_POINTS + 1

    # Computes the edge parameters that make a flat edge
    edge_params = edge_vectors.repeat(1,1,EDGE_BEZIER_POINTS) * vector_scales
    edge_params *= edge_adj.unsqueeze(-1)

    return edge_params


def base_edge_parameters(node_coords: torch.Tensor, edge_adj: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Computes the base edge parameters for the given samples.

    node_coords: `torch.Tensor`
        A `(N,R//3,3)` float tensor with the samples' node positions
        transformed into Euclidean coordinates.
        `N` is the number of samples.
        `R` is the size of the node position array in the
        Metamaterial representation.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' edge adjacencies.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.

    Returns: `tuple[torch.Tensor]`
        A `(N,R,B)` float tensor with the samples' corresponding
        flat edge parameters.

        A `(N,R,B)` float tensor with the samples' corresponding
        curved edge parameters.

        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.
        `B` is the number of Bezier coordinates that define edges.
        The edge parameters of non-existent edges are 0.
    """

    # Stores convenient values for the function
    num_samples = node_coords.shape[0]

    # Stores the general shape of the edge parameter arrays
    edge_params_shape = (num_samples, EDGE_ADJ_SIZE, EDGE_BEZIER_COORDS)

    # Computes the flat/curved edge parameters
    flat_edge_params = flat_edge_parameters(node_coords, edge_adj)
    curved_edge_params = torch.rand(edge_params_shape, device=DEVICE)

    # Makes each curved edge parameter not go beyond the unit cube
    curved_edge_params -= node_coords[:, EDGE_TO_NODES[:,0]].repeat(1,1,EDGE_BEZIER_POINTS)

    # Keeps only active edges' parameters
    curved_edge_params *= edge_adj.unsqueeze(-1)

    return flat_edge_params, curved_edge_params


def find_flat_and_curved_face_edges(flat_faces: torch.Tensor, curved_faces: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Finds the flat/curved edge adjacencies that correspond to edges
    tied to an active face. Deals with edges that are tied to both
    an active flat face and curved face randomly.

    flat_faces: torch.Tensor
        A `(N,R)` float tensor with the samples' face adjacencies
        just for the chosen flat faces.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.
        
    curved_faces: torch.Tensor
        A `(N,R)` float tensor with the samples' face adjacencies
        just for the chosen curved faces.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.

    Returns: `torch.Tensor`
        A `(N,R)` float tensor with the samples' edge adjacencies
        that correspond to edges tied to active flat faces.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.

        A `(N,R)` float tensor with the samples' edge adjacencies
        that correspond to edges tied to active curved faces.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.
    """

    # Computes the flat/curved edges from existing faces
    flat_edges = find_face_edges(flat_faces)
    curved_edges = find_face_edges(curved_faces)

    # Randomly deals with intersections, where an edge is connected to a flat and curved face
    intersections = flat_edges * curved_edges
    keep_flat_edges = (torch.rand(intersections.shape, device=DEVICE) < 0.5).to(torch.float32)

    # Removes intersections
    flat_edges -= intersections * (1-keep_flat_edges)
    curved_edges -= intersections * keep_flat_edges

    return flat_edges, curved_edges


def choose_flat_and_curved_edges(num_nodes: torch.Tensor, num_curved_edges: torch.Tensor, edge_adj: torch.Tensor, flat_faces: torch.Tensor, curved_faces: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Chooses which edges will correspond to flat/curved edges
    in the given samples.

    num_nodes: `torch.Tensor`
        A `(N,)` int tensor with the number of active nodes to use in each sample.
        `N` is the number of samples.

    num_curved_edges: `torch.Tensor`
        A `(N,)` tensor with the number of curved edges to use in each sample.
        `N` is the number of samples.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' edge adjacencies.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.

    flat_faces: torch.Tensor
        A `(N,R)` float tensor with the samples' face adjacencies
        just for the chosen flat faces.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.
        
    curved_faces: torch.Tensor
        A `(N,R)` float tensor with the samples' face adjacencies
        just for the chosen curved faces.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the
        Metamaterial representation.

    Returns: `tuple[torch.Tensor]`
        A `(N,R)` float tensor with the samples' edge adjacencies
        just for the chosen flat edges.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.
        
        A `(N,R)` float tensor with the samples' edge adjacencies
        just for the chosen curved edges.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the
        Metamaterial representation.
    """

    # Stores relevant values for the function
    max_nodes = num_nodes.max().item()
    max_edges = num_nodes * (num_nodes-1) // 2

    # Computes the curved and flat face-edges
    flat_edges, curved_edges = find_flat_and_curved_face_edges(flat_faces, curved_faces)

    # Fill in the missing curved and flat edges
    curved_edges = fill_in_edges(curved_edges, curved_edges, num_curved_edges, max_edges, max_nodes)
    flat_edges = edge_adj - curved_edges

    return flat_edges, curved_edges


def generate_edge_and_face_parameters(num_nodes: torch.Tensor, num_curved_edges: torch.Tensor, num_curved_faces: torch.Tensor, node_coords: torch.Tensor, edge_adj: torch.Tensor, face_adj: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
    """
    Generates random edge and face parameters according
    to the given sample specifications.

    num_nodes: `torch.Tensor`
        A `(N,)` int tensor with the number of active nodes to use in each sample.
        `N` is the number of samples.

    num_curved_edges: `torch.Tensor`
        A `(N,)` int tensor with the number of non-face curved edges to use in each sample.
        `N` is the number of samples.

    num_curved_faces: `torch.Tensor`
        A `(N,)` int tensor with the number of curved faces to use in each sample.
        `N` is the number of samples.

    node_coords: `torch.Tensor`
        A `(N,R//3,3)` float tensor with the Euclidean coordinates of each node, ordered
        in the second dimension by node ID.
        `N` is the number of samples.
        `R` is the size of the node position array in the Metamaterial representation.

    edge_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' edge adjacencies.
        `N` is the number of samples.
        `R` is the size of the edge adjacency array in the Metamaterial representation.
        
    face_adj: `torch.Tensor`
        A `(N,R)` float tensor with the samples' face adjacencies.
        `N` is the number of samples.
        `R` is the size of the face adjacency array in the Metamaterial representation.

    Returns: `tuple[torch.Tensor, torch.Tensor]`
        A `(N,R)` float tensor with the generated edge parameters.
        Contains only edge parameters for the given active edges.
        All other edge parameters are 0.
        `N` is the number of samples.
        `R` is the size of the edge parameters array in the Metamaterial representation.
        
        A `(N,R)` float tensor with the generated face parameters.
        Contains only face parameters for the given active faces.
        All other face parameters are 0.
        `N` is the number of samples.
        `R` is the size of the face parameters array in the Metamaterial representation.
    """

    # Computes the base face parameters
    flat_face_params, curved_face_params = base_face_parameters(node_coords, face_adj)

    # Stores which faces are to be flat/curved
    flat_faces, curved_faces = choose_flat_and_curved_faces(num_curved_faces, face_adj)

    # Computes the face parameters by combining only the target flat/curved face parameters
    face_params = flat_face_params * flat_faces.unsqueeze(-1) + curved_face_params * curved_faces.unsqueeze(-1)

    # Computes the base edge parameters
    flat_edge_params, curved_edge_params = base_edge_parameters(node_coords, edge_adj)

    # Stores which edges are to be flat/curved
    flat_edges, curved_edges = choose_flat_and_curved_edges(num_nodes, num_curved_edges, edge_adj, flat_faces, curved_faces)

    # Computes the edge parameters by combining only the target flat/curved edge parameters
    edge_params = flat_edge_params * flat_edges.unsqueeze(-1) + curved_edge_params * curved_edges.unsqueeze(-1)

    # Resizes the parameters to match the requirement for the metamaterial representation
    num_samples = edge_params.shape[0]
    edge_params = edge_params.reshape((num_samples, -1))
    face_params = face_params.reshape((num_samples, -1))

    return edge_params, face_params


def random_metamaterials(num_nodes: torch.Tensor, num_edges: torch.Tensor, num_curved_edges: torch.Tensor, num_faces: torch.Tensor, num_curved_faces: torch.Tensor) -> torch.Tensor:
    """
    Generates random metamaterials based on the given attributes.
    The metamaterials satisfy the following properties:

    1) There are no disconnected components.
    2) Every face in the unit cube has at least one node.
    3) All nodes are on at most three faces of the unit cube.

    num_nodes: `torch.Tensor`
        A `(N,)` int tensor with the number of active nodes to use in each sample.
        `N` is the number of samples.

    num_edges: `torch.Tensor`
        A `(N,)` int tensor with the maximum number of non-face edges to use in
        each sample. The number is the maximum as the choice of faces is prioritized.
        If the choice of faces allows for this many non-face edges, then there will
        be this many edges. Otherwise, the maximum number of edges with the generated
        face arrangement will be chosen. There will always be at least this many edges.
        `N` is the number of samples.

    num_curved_edges: `torch.Tensor`
        A `(N,)` int tensor with the number of non-face curved edges to use in each sample.
        `N` is the number of samples.

    num_faces: `torch.Tensor`
        A `(N,)` int tensor with the total number of faces to use in each sample.
        `N` is the number of samples.

    num_curved_faces: `torch.Tensor`
        A `(N,)` int tensor with the number of curved faces to use in each sample.
        `N` is the number of samples.

    Returns: `torch.Tensor`
        A `(N,R)` tensor with the random samples of metamaterials.
        `N` is the number of samples.
        `R` is the Metamaterial representation size.
    """

    # Performs safety checks
    assert torch.all(num_edges >= num_curved_edges).item(), "Cannot have more non-face curved edges than non-face edges."
    assert torch.all(num_faces >= num_curved_faces).item(), "Cannot have more curved faces than faces."

    # Stores convenient values for the function
    num_samples = num_nodes.shape[0]

    # Stores the node positions
    node_pos, node_coords = generate_node_positions(num_nodes)

    # Computes the edge/face adjacencies
    edge_adj, face_adj = generate_adjacencies(num_nodes, num_edges, num_faces)

    # Computes the edge/face parameters
    edge_params, face_params = generate_edge_and_face_parameters(num_nodes, num_curved_edges, num_curved_faces, node_coords, edge_adj, face_adj)

    # Stores the other parameters
    global_params = torch.clamp(torch.randn((num_samples,1), device=DEVICE)/10 + 0.5, min=0.4, max=0.6) # Thickness

    return torch.cat([node_pos, edge_adj, edge_params, face_adj, face_params, global_params], dim=1)

