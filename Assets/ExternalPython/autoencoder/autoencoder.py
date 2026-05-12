import torch
import math
from torch import nn
from torch.utils.data import DataLoader
from representation.rep_utils import *


class MetamaterialAE(nn.Module):
    """
    A class for handling computations involving
    the metamaterial autoencoder.
    """

    def __init__(self, device):
        """
        Constructs a metamaterial autoencoder with the given properties.

        device: `str`
            The device on which the model will be placed.
        """
        super().__init__()

        # Node Position Encoder
        self.node_pos_input_size = NODE_POS_SIZE
        self.node_pos_hidden_size = self.node_pos_input_size*4
        self.node_pos_latent_size = self.node_pos_input_size//5
        self.node_pos_encoder = nn.Sequential(
            nn.Linear(in_features=self.node_pos_input_size, out_features=self.node_pos_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.node_pos_hidden_size, out_features=self.node_pos_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.node_pos_hidden_size, out_features=self.node_pos_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.node_pos_hidden_size, out_features=self.node_pos_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.node_pos_hidden_size, out_features=self.node_pos_latent_size*2),
        )

        # Edge Adjacency Encoder
        self.edge_adj_input_size = EDGE_ADJ_SIZE
        self.edge_adj_hidden_size = self.edge_adj_input_size*4
        self.edge_adj_latent_size = self.edge_adj_input_size//5
        self.edge_adj_encoder = nn.Sequential(
            nn.Linear(in_features=self.edge_adj_input_size, out_features=self.edge_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_adj_hidden_size, out_features=self.edge_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_adj_hidden_size, out_features=self.edge_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_adj_hidden_size, out_features=self.edge_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_adj_hidden_size, out_features=self.edge_adj_latent_size*2),
        )

        # Edge Parameters Encoder
        self.edge_params_input_size = EDGE_PARAMS_SIZE
        self.edge_params_hidden_size = self.edge_params_input_size*4
        self.edge_params_latent_size = self.edge_params_input_size//5
        self.edge_params_encoder = nn.Sequential(
            nn.Linear(in_features=self.edge_params_input_size, out_features=self.edge_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_params_hidden_size, out_features=self.edge_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_params_hidden_size, out_features=self.edge_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_params_hidden_size, out_features=self.edge_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.edge_params_hidden_size, out_features=self.edge_params_latent_size*2),
        )

        # Face Adjacency Encoder
        self.face_adj_input_size = FACE_ADJ_SIZE
        self.face_adj_hidden_size = self.face_adj_input_size*4
        self.face_adj_latent_size = self.face_adj_input_size//5
        self.face_adj_encoder = nn.Sequential(
            nn.Linear(in_features=self.face_adj_input_size, out_features=self.face_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_adj_hidden_size, out_features=self.face_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_adj_hidden_size, out_features=self.face_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_adj_hidden_size, out_features=self.face_adj_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_adj_hidden_size, out_features=self.face_adj_latent_size*2),
        )

        # Face Parameters Encoder
        self.face_params_input_size = FACE_PARAMS_SIZE
        self.face_params_hidden_size = self.face_params_input_size*4
        self.face_params_latent_size = self.face_params_input_size//5
        self.face_params_encoder = nn.Sequential(
            nn.Linear(in_features=self.face_params_input_size, out_features=self.face_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_params_hidden_size, out_features=self.face_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_params_hidden_size, out_features=self.face_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_params_hidden_size, out_features=self.face_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.face_params_hidden_size, out_features=self.face_params_latent_size*2),
        )

        # Global Parameters Encoder
        self.global_params_input_size = GLOBAL_PARAMS_SIZE
        self.global_params_hidden_size = self.global_params_input_size*4
        self.global_params_latent_size = math.ceil(self.global_params_input_size/5)
        self.global_params_encoder = nn.Sequential(
            nn.Linear(in_features=self.global_params_input_size, out_features=self.global_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.global_params_hidden_size, out_features=self.global_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.global_params_hidden_size, out_features=self.global_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.global_params_hidden_size, out_features=self.global_params_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.global_params_hidden_size, out_features=self.global_params_latent_size*2),
        )

        # Topology Encoder
        self.topology_input_size = EDGE_ADJ_SIZE+FACE_ADJ_SIZE
        self.topology_hidden_size = self.topology_input_size*4
        self.topology_latent_size = self.topology_input_size//5
        self.topology_encoder = nn.Sequential(
            nn.Linear(in_features=self.topology_input_size, out_features=self.topology_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.topology_hidden_size, out_features=self.topology_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.topology_hidden_size, out_features=self.topology_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.topology_hidden_size, out_features=self.topology_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.topology_hidden_size, out_features=self.topology_latent_size*2),
        )

        # Geometry Encoder
        self.geometry_input_size = NODE_POS_SIZE+EDGE_PARAMS_SIZE+FACE_PARAMS_SIZE+GLOBAL_PARAMS_SIZE
        self.geometry_hidden_size = self.geometry_input_size*4
        self.geometry_latent_size = self.geometry_input_size//5
        self.geometry_encoder = nn.Sequential(
            nn.Linear(in_features=self.geometry_input_size, out_features=self.geometry_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.geometry_hidden_size, out_features=self.geometry_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.geometry_hidden_size, out_features=self.geometry_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.geometry_hidden_size, out_features=self.geometry_hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=self.geometry_hidden_size, out_features=self.geometry_latent_size*2),
        )

        # Stores all the encoders compactly
        self.encoders = [
            self.node_pos_encoder,
            self.edge_adj_encoder,
            self.edge_params_encoder,
            self.face_adj_encoder,
            self.face_params_encoder,
            self.global_params_encoder,
            self.topology_encoder,
            self.geometry_encoder,
        ]

        # Stores latent space properties
        self.latent_size = (
            self.node_pos_latent_size +
            self.edge_adj_latent_size +
            self.edge_params_latent_size + 
            self.face_adj_latent_size + 
            self.face_params_latent_size +
            self.global_params_latent_size +
            self.topology_latent_size +
            self.geometry_latent_size
        )

        # Node Position Decoder
        input_size = self.node_pos_latent_size+self.geometry_latent_size
        hidden_size = input_size*4
        output_size = self.node_pos_input_size
        self.node_pos_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Edge Adjacency Decoder
        input_size = self.edge_adj_latent_size+self.topology_latent_size
        hidden_size = input_size*4
        output_size = self.edge_adj_input_size
        self.edge_adj_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Edge Parameters Decoder
        input_size = self.edge_params_latent_size+self.geometry_latent_size
        hidden_size = input_size*4
        output_size = self.edge_params_input_size
        self.edge_params_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Face Adjacency Decoder
        input_size = self.face_adj_latent_size+self.topology_latent_size
        hidden_size = input_size*4
        output_size = self.face_adj_input_size
        self.face_adj_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Face Parameters Decoder
        input_size = self.face_params_latent_size+self.geometry_latent_size
        hidden_size = input_size*4
        output_size = self.face_params_input_size
        self.face_params_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Global Parameters Decoder
        input_size = self.global_params_latent_size+self.geometry_latent_size
        hidden_size = input_size*4
        output_size = self.global_params_input_size
        self.global_params_decoder = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )
        
        # Stores all the decoders compactly
        self.decoders = [
            self.node_pos_decoder,
            self.edge_adj_decoder,
            self.edge_params_decoder,
            self.face_adj_decoder,
            self.face_params_decoder,
            self.global_params_decoder,
        ]

        # Volume Predictor
        input_size = self.latent_size
        hidden_size = input_size*6
        self.volume_predictor = nn.Sequential(
            nn.Linear(in_features=input_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=hidden_size),
            nn.ReLU(),
            nn.Linear(in_features=hidden_size, out_features=output_size),
        )

        # Stores the device
        self.device = device
        self.to(device)


    def encode(self, x: torch.Tensor) -> torch.Tensor:
        """
        Runs the network's encoder on the input.
        """

        # Stores the indices
        edge_adj_index = NODE_POS_SIZE
        edge_params_index = edge_adj_index+EDGE_ADJ_SIZE
        face_adj_index = edge_params_index+EDGE_PARAMS_SIZE
        face_params_index = face_adj_index+FACE_ADJ_SIZE
        global_params_index = face_params_index+FACE_PARAMS_SIZE

        # Stores the values from the representation
        node_pos =      x[...,                     : edge_adj_index     ]
        edge_adj =      x[..., edge_adj_index      : edge_params_index  ]
        edge_params =   x[..., edge_params_index   : face_adj_index     ]
        face_adj =      x[..., face_adj_index      : face_params_index  ]
        face_params =   x[..., face_params_index   : global_params_index]
        global_params = x[..., global_params_index :                    ]

        # Stores the latent values
        node_pos_latent = self.node_pos_encoder(node_pos)
        edge_adj_latent = self.edge_adj_encoder(edge_adj)
        edge_params_latent = self.edge_params_encoder(edge_params)
        face_adj_latent = self.face_adj_encoder(face_adj)
        face_params_latent = self.face_params_encoder(face_params)
        global_params_latent = self.global_params_encoder(global_params)
        topology_latent = self.topology_encoder(torch.cat([edge_adj, face_adj], dim=-1))
        geometry_latent = self.geometry_encoder(torch.cat([node_pos, edge_params, face_params, global_params], dim=-1))

        return torch.cat([
            node_pos_latent,
            edge_adj_latent,
            edge_params_latent,
            face_adj_latent,
            face_params_latent,
            global_params_latent,
            topology_latent,
            geometry_latent,
        ], dim=-1)
    

    def get_latent_distribution(self, latent_vector: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        """
        Gets the mean and variance of the latent distribution.
        """

        return latent_vector[..., ::2], latent_vector[..., 1::2]
    

    def sample_latent_space(self, mean: torch.Tensor, logvar: torch.Tensor):
        """
        Assuming the autoencoder is a VAE, splits the latent
        vector into a normal distribution's mean/std and samples
        from the distribution.
        """

        # Samples the distribution
        eps = torch.randn(mean.shape).to(self.device)
        return mean + eps * torch.exp(logvar)
    

    def decode(self, z: torch.Tensor) -> torch.Tensor:
        """
        Runs the network's decoder on the input.
        """

        # Stores the indices
        edge_adj_index = self.node_pos_latent_size
        edge_params_index = edge_adj_index + self.edge_adj_latent_size
        face_adj_index = edge_params_index + self.edge_params_latent_size
        face_params_index = face_adj_index + self.face_adj_latent_size
        global_params_index = face_params_index + self.face_params_latent_size
        topology_index = global_params_index + self.global_params_latent_size
        geometry_index = topology_index + self.topology_latent_size

        # Stores the latent values
        node_pos_latent =      z[...,                     : edge_adj_index     ]
        edge_adj_latent =      z[..., edge_adj_index      : edge_params_index  ]
        edge_params_latent =   z[..., edge_params_index   : face_adj_index     ]
        face_adj_latent =      z[..., face_adj_index      : face_params_index  ]
        face_params_latent =   z[..., face_params_index   : global_params_index]
        global_params_latent = z[..., global_params_index : topology_index     ]
        topology_latent =      z[..., topology_index      : geometry_index     ]
        geometry_latent =      z[..., geometry_index      :                    ]

        # Stores the decoded values
        node_pos = self.node_pos_decoder(torch.cat([node_pos_latent, geometry_latent], dim=-1))
        edge_adj = self.edge_adj_decoder(torch.cat([edge_adj_latent, topology_latent], dim=-1))
        edge_params = self.edge_params_decoder(torch.cat([edge_params_latent, geometry_latent], dim=-1))
        face_adj = self.face_adj_decoder(torch.cat([face_adj_latent, topology_latent], dim=-1))
        face_params = self.face_params_decoder(torch.cat([face_params_latent, geometry_latent], dim=-1))
        global_params = self.global_params_decoder(torch.cat([global_params_latent, geometry_latent], dim=-1))

        return torch.cat([
            node_pos,
            edge_adj,
            edge_params,
            face_adj,
            face_params,
            global_params,
        ], dim=-1)
    

    def predict_volume(self, z: torch.Tensor) -> torch.Tensor:
        """
        Predicts the volume of the latent sample.
        """

        return self.volume_predictor(z)


    def forward(self, x: torch.Tensor):
        """
        Defines a forward pass through the network.
        """

        encoding = self.encode(x)
        mean, logvar = self.get_latent_distribution(encoding)
        z = self.sample_latent_space(mean, logvar)
        decoding = self.decode(z)
        volumes = self.predict_volume(z)

        return mean, logvar, decoding, volumes


def run_epoch(epoch: int, model: MetamaterialAE, dataloader: DataLoader, optim=None, verbose=True, report_frequency=200) -> float:

    """
    Runs a epoch on the model with the given data.
    
    epoch: `int`
        The current epoch.

    model: `MetamaterialAE`
        The model to be used.

    dataloader: `DataLoader`
        The data to use.

    optim: optional
        The optimizer to use in training.
        If not `None`, then model will be optimized.

    verbose: `bool`, optional
        Whether batch progress will output to the terminal.

    report_frequency: `int`, optional
        The number of batches between which reports will be printed if
        the verbose option is chosen.

    Returns: `float`
        If `train=False`, returns:
        1) The total loss.
        2) The reconstruction loss.
        3) The KL Divergence loss.
        4) The volume prediction loss.
        5) The node position R^2 coefficient.
        6) The fraction of edges correctly decoded.
        7) The edge parameters R^2 coefficient.
        8) The fraction of faces correctly decoded.
        9) The face parameters R^2 coefficient.
        10) The global parameters R^2 coefficient.
        11) The volume prediction R^2 coefficient.
    """

    # Computes the size of the dataset
    dataset_size = len(dataloader.dataset)

    # Prepares values for the epoch
    samples_used = 0
    total_loss = 0
    total_reconstruction_loss = 0
    total_kld_loss = 0
    total_volume_loss = 0
    node_pos_residual_sum = 0
    correct_edges = 0
    edge_params_residual_sum = 0
    correct_faces = 0
    face_params_residual_sum = 0
    global_params_residual_sum = 0
    volume_residual_sum = 0

    # Sets up the model's mode
    train = optim is not None
    if train:
        model.train()
    else:
        model.eval()

    # Prepares the loss function
    loss_fn = nn.MSELoss()

    # Runs through each batch
    for batch, (X,v), in enumerate(dataloader):

        # Computes the forward pass
        mean, logvar, decoding, volume = model(X)

        # Computes the losses
        reconstruction_loss: torch.Tensor = loss_fn(decoding, X)
        kld_scale = max(0.0, min(0.0025, (epoch-5) * 5e-4)) if train else 1.0
        kld_loss: torch.Tensor = kld_scale * -0.5 * torch.sum(1 + logvar - mean.pow(2) - logvar.exp()) / mean.numel()
        volume_scale = 0.1 + (epoch+1)/10 if train else 1.0
        volume_loss: torch.Tensor = volume_scale * loss_fn(volume, v)
        loss: torch.Tensor = reconstruction_loss + kld_loss + volume_loss

        # Accumulates the total loss
        total_loss += loss.item() * X.shape[0]

        # Accumulates the different individual errors
        if not train:

            # Accumulates the different losses
            total_reconstruction_loss += reconstruction_loss.item() * X.shape[0]
            total_kld_loss += kld_loss.item() * X.shape[0]
            total_volume_loss += volume_loss.item() * X.shape[0]

            # Accumulates the error in node positions
            y_nodes = X[:,:NODE_POS_SIZE]
            decoding_nodes = decoding[:,:NODE_POS_SIZE]
            node_pos_residual_sum += torch.sum((y_nodes-decoding_nodes)**2).item()

            # Accumulates the proportion of edges that were decoded correctly
            y_edges = X[:,NODE_POS_SIZE:][:,:EDGE_ADJ_SIZE]
            decoding_edges = decoding[:,NODE_POS_SIZE:][:,:EDGE_ADJ_SIZE]
            correct_edges += torch.sum(torch.abs(decoding_edges-y_edges) < 0.5).item() / EDGE_ADJ_SIZE

            # Accumulates the error in edge parameters
            y_edge_params = X[:,NODE_POS_SIZE+EDGE_ADJ_SIZE:][:,:EDGE_PARAMS_SIZE]
            decoding_edge_params = decoding[:,NODE_POS_SIZE+EDGE_ADJ_SIZE:][:,:EDGE_PARAMS_SIZE]
            edge_params_residual_sum += torch.sum((y_edge_params-decoding_edge_params)**2).item()

            # Accumulates the proportion of faces that were decoded correctly
            y_faces = X[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE:][:,:FACE_ADJ_SIZE]
            decoding_faces = decoding[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE:][:,:FACE_ADJ_SIZE]
            correct_faces += torch.sum(torch.abs(decoding_faces-y_faces) < 0.5).item() / FACE_ADJ_SIZE

            # Accumulates the error in face parameters
            y_face_params = X[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE:][:,:FACE_PARAMS_SIZE]
            decoding_face_params = decoding[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE:][:,:FACE_PARAMS_SIZE]
            face_params_residual_sum += torch.sum((y_face_params-decoding_face_params)**2).item()

            # Accumulates the error in global parameters
            y_global_params = X[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE+FACE_PARAMS_SIZE:][:,:GLOBAL_PARAMS_SIZE]
            decoding_global_params = decoding[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE+FACE_PARAMS_SIZE:][:,:GLOBAL_PARAMS_SIZE]
            global_params_residual_sum += torch.sum((y_global_params-decoding_global_params)**2).item()

            # Accumulates the error in volumes
            volume_residual_sum += torch.sum((v-volume)**2).item()

        # Runs backpropagation and gradient descent
        if train:
            loss.backward()
            optim.step()
            optim.zero_grad()

        # Prints the loss when the report frequency is met
        samples_used += X.shape[0]
        if verbose and (batch+1) % report_frequency == 0:
            print(f"Loss: {(total_loss / samples_used):>7f} [{samples_used}/{dataset_size}]")

    # Averages the errors
    total_loss /= samples_used
    total_reconstruction_loss /= samples_used
    total_kld_loss /= samples_used
    total_volume_loss /= samples_used
    correct_edges /= samples_used
    correct_faces /= samples_used

    # Computes the actual data means
    material_means = dataloader.dataset.metamaterials.sum(dim=0) / dataset_size
    node_pos_mean = material_means[:NODE_POS_SIZE].sum().item() / NODE_POS_SIZE
    edge_params_mean = material_means[NODE_POS_SIZE+EDGE_ADJ_SIZE:][:EDGE_PARAMS_SIZE].sum().item() / EDGE_PARAMS_SIZE
    face_params_mean = material_means[NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE:][:FACE_PARAMS_SIZE].sum().item() / FACE_PARAMS_SIZE
    global_params_mean = material_means[-GLOBAL_PARAMS_SIZE:].sum().item() / GLOBAL_PARAMS_SIZE 
    volume_mean = dataloader.dataset.volumes.sum().item() / dataset_size

    # Computes the R^2 coefficient
    node_pos_vals = dataloader.dataset.metamaterials[:,:NODE_POS_SIZE]
    node_pos_r2 = 1 - node_pos_residual_sum / ((node_pos_vals-node_pos_mean)**2).sum().item()
    edge_params_vals = dataloader.dataset.metamaterials[:,NODE_POS_SIZE+EDGE_ADJ_SIZE:][:,:EDGE_PARAMS_SIZE]
    edge_params_r2 = 1 - edge_params_residual_sum / ((edge_params_vals-edge_params_mean)**2).sum().item()
    face_params_vals = dataloader.dataset.metamaterials[:,NODE_POS_SIZE+EDGE_ADJ_SIZE+EDGE_PARAMS_SIZE+FACE_ADJ_SIZE:][:,:FACE_PARAMS_SIZE]
    face_params_r2 = 1 - face_params_residual_sum / ((face_params_vals-face_params_mean)**2).sum().item()
    global_params_vals = dataloader.dataset.metamaterials[:,-GLOBAL_PARAMS_SIZE:]
    global_params_r2 = 1 - global_params_residual_sum / ((global_params_vals-global_params_mean)**2).sum().item()
    volume_r2 = 1 - volume_residual_sum / ((dataloader.dataset.volumes-volume_mean)**2).sum().item()
    
    if not train:
        return (
            total_loss,
            total_reconstruction_loss,
            total_kld_loss,
            total_volume_loss,
            node_pos_r2,
            correct_edges,
            edge_params_r2,
            correct_faces,
            face_params_r2,
            global_params_r2,
            volume_r2
        )

