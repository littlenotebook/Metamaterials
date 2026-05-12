import torch
from torch.utils.data import Dataset

class MetamaterialDataset(Dataset):

    # Initializes the dataset
    def __init__(self, material_data_filepath: str, volume_data_filepath: str, device):
        super().__init__()
        self.metamaterials: torch.Tensor = torch.load(material_data_filepath).to(device)
        self.volumes: torch.Tensor = torch.load(volume_data_filepath).to(device)

    # The length of the dataset
    def __len__(self):
        return self.metamaterials.shape[0]
    
    # Gets an input/label pair from the dataset
    def __getitem__(self, index):
        return self.metamaterials[index], self.volumes[index]
    