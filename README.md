# Metamaterials

Large obj interpolation files were stored as LFS, use git lfs checkout to repopulate the working directory with the actual file contents from LFS. Need 
  git lfs install
  git lfs pull

Launch Unity hub
SampleScene: Generate a microstructure from obj in list of manifest.txt. Obj and txt files in StreamingAssets -> Meshes
InterpolationScene: Visualise interpolation between two microstructures, all steps of interpolation are in obj format. Examples in StreamingAssets -> Meshes -> any folder with name microstructure1-microstructure2
MetamaterialDemo: Generate microstructure from raw python representation, mirroring octants, adding nodes/edges/faces, deleting nodes/edges/faces, moving nodes/edges.
  - related scripts are in Scripts -> Microstructure
  - generated jsons are in Assets (e.x. newest_hold_block_shell.json)
MainMenu: originally the menu to move from SampleScene to InterpolationScene
GenerateScene: a copy(?) of SampleScene that came after, not sure if works properly

Created on macOS
