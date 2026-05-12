import sys
import pathlib
sys.path.append(str(pathlib.Path(__file__).parents[1]))

import os
import torch
import time
import numpy as np
import matplotlib.pyplot as plt
from torch.utils.data import DataLoader
from autoencoder.autoencoder import *
from autoencoder.dataset import *

# Initializes the model logistics
model_name = sys.argv[1]
model_dir = f"local_test/models/{model_name}"
if not os.path.exists(model_dir):
    os.mkdir(model_dir)
if not os.path.exists(f"{model_dir}/models"):
    os.mkdir(f"{model_dir}/models")

# Creates the model components
device = "cuda"
model = MetamaterialAE(device)
loss_fn = nn.MSELoss()
for param in model.volume_predictor.parameters(): # Ignores training the volume predictor
    param.requires_grad = False
optimized_params = [param for param in model.parameters() if param.requires_grad]
optim = torch.optim.Adam(optimized_params, lr=5e-4)

# Stores the model progress components
best_loss = float('inf')
epoch = 0

# Loads the checkpoint
if sys.argv[2] == "true":
    print("Loading the checkpoint...")
    checkpoint = torch.load(f"{model_dir}/checkpoint.pth")
    model.load_state_dict(checkpoint['model_state_dict'])
    optim.load_state_dict(checkpoint['optimizer_state_dict'])
    best_loss = checkpoint['best_loss']
    epoch = checkpoint['epoch']+1

# Stores the model size
components = [
    "Node Position",
    "Edge Adjacency",
    "Edge Parameters",
    "Face Adjacency",
    "Face Parameters",
    "Global Parameters",
    "Topology",
    "Geometry",
]
with open(f"{model_dir}/sizes.txt", "w") as f:
    for i,encoder in enumerate(model.encoders):
        f.write(f"{components[i]} Encoder:\n")
        f.write(str(encoder))
        f.write("\n\n")
    for i,decoder in enumerate(model.decoders):
        f.write(f"{components[i]} Decoder:\n")
        f.write(str(decoder))
        f.write("\n\n")
    f.write(f"Volume Predictor:\n")
    f.write(str(model.volume_predictor))
    f.write("\n\n")


print("Loading training data...")
train_materials_filepath = "local_test/datasets/train_materials_dataset.pt"
train_volumes_filepath = "local_test/datasets/train_volumes_dataset.pt"
train_dataloader = DataLoader(MetamaterialDataset(train_materials_filepath, train_volumes_filepath, device), batch_size=512, shuffle=True)
print("Loading testing data...")
test_materials_filepath = "local_test/datasets/test_materials_dataset.pt"
test_volumes_filepath = "local_test/datasets/test_volumes_dataset.pt"
test_dataloader = DataLoader(MetamaterialDataset(test_materials_filepath, test_volumes_filepath, device), batch_size=512, shuffle=True)

fig, axes = plt.subplots(4, 3, figsize=(10, 8))
x, y = [], []
value_titles = [
    "Target Loss",
    "Total Loss",
    "Reconstruction Loss",
    "KL Divergence Loss",
    "Volume Loss",
    "Node Position R^2",
    "Correct Edges",
    "Edge Parameters R^2",
    "Correct Faces",
    "Face Parameters R^2",
    "Global Parameters R^2",
    "Volume R^2"
]

while True:
    print(f"Epoch: {epoch+1}")
    start_time = time.time()

    run_epoch(epoch, model, train_dataloader, optim=optim, report_frequency=64)
    print()
    test_values = run_epoch(epoch, model, test_dataloader, verbose=False)
    (test_total_loss, test_reconstruction_loss, test_kld_loss, test_volume_loss,
    node_pos_r2, correct_edges, edge_params_r2, correct_faces, face_params_r2,
    global_params_r2, volume_r2) = test_values

    extra = ""

    # Stores the best model so far
    # total_loss = test_reconstruction_loss + test_kld_loss + test_volume_loss
    target_loss = test_reconstruction_loss + test_kld_loss
    test_values = (target_loss,) + test_values
    if target_loss < best_loss:
        best_loss = target_loss
        print("NEW BEST LOSS!")
        extra = " (NEW BEST!)"
        torch.save(model.state_dict(), f"{model_dir}/best_model.pth")

    # Stores the checkpoint
    checkpoint = {
        'model_state_dict': model.state_dict(),
        'optimizer_state_dict': optim.state_dict(),
        'epoch': epoch,
        'best_loss' : best_loss,
    }
    torch.save(checkpoint, f"{model_dir}/checkpoint.pth")

    # Stores the model
    torch.save(model.state_dict(), f"{model_dir}/models/epoch{str(epoch).zfill(3)}.pth")

    # Logs the test epoch
    with open(f"{model_dir}/log.txt", "a") as f:
        f.write(f"Epoch: {epoch+1}{extra}\n")
        for title,val in zip(value_titles, test_values):
            f.write(f"{title}: {val}\n")
        f.write(f"Time Elapsed (sec): {time.time()-start_time}\n")
        f.write("\n")

    # Prints the test epoch
    for title,val in zip(value_titles, test_values):
        print(f"{title}: {val}")
    print("\nTime Elapsed:", time.time()-start_time)
    print("\n-----------------------------------------------\n")

    # Stores values for plotting
    x.append(epoch+1)
    y.append(test_values)

    # Plots the test values
    plt.clf()
    for i,title in enumerate(value_titles):
        r,c = i//3,i%3
        axes[r,c].plot(x,y[i])
        axes[r,c].set_title(f"{title} Per Epoch")
        axes[r,c].set_xlabel("Epoch")
        axes[r,c].set_ylabel(title)

    plt.savefig(f"{model_dir}/test_plots.png")

    epoch += 1
