# GENERATE SINGULAR MICROSTRUCTURE
# import sys
# from example_materials.hexagon_shell import HEXAGON_SHELL
# from representation.surface_meshing_torch import (
#     generate_metamaterial_grid_surface_mesh,
#     save_material_obj
# )

# output_path = sys.argv[1] if len(sys.argv) > 1 else "Assets/Meshes/microstructure_2.obj"


# save_material_obj(*generate_metamaterial_grid_surface_mesh(HEXAGON_SHELL, shape=(2,2,2)), output_path)

# GENERATE ALL MICROSTRUCTURES
import sys
import os
from pathlib import Path

# Import all the material classes
from example_materials.hexagon_shell import HEXAGON_SHELL
from example_materials.hexagon_wireframe import HEXAGON_WIREFRAME
from example_materials.hole_block_shell import HOLE_BLOCK_SHELL
from example_materials.hole_block_wireframe import HOLE_BLOCK_WIREFRAME
from example_materials.personal_curve_wireframe import PERSONAL_CURVE_WIREFRAME
from example_materials.personal_shell_flat import PERSONAL_SHELL_FLAT
from example_materials.schwarz_p_shell import SCHWARZ_P_SHELL
from example_materials.schwarz_p_wireframe import SCHWARZ_P_WIREFRAME
from example_materials.snowflake_wireframe import SNOWFLAKE_WIREFRAME
from example_materials.star_beams import STAR_BEAMS
from example_materials.star_truss import STAR_TRUSS
from example_materials.tetrahedron_curved import TETRAHEDRON_CURVED
from example_materials.tetrahedron_mixed import TETRAHEDRON_MIXED
from example_materials.tetrahedron_shell import TETRAHEDRON_SHELL
from example_materials.tetrahedron_wireframe import TETRAHEDRON_WIREFRAME
from example_materials.personal_wireframe import PERSONAL_WIREFRAME

from representation.surface_meshing_torch import (
    generate_metamaterial_grid_surface_mesh,
    save_material_obj
)

# Create output directory
output_dir = "microstructures"
os.makedirs(output_dir, exist_ok=True)

# Map of material names to their classes and configurations
materials = {
    "hexagon_shell": (HEXAGON_SHELL, (2, 2, 2)),
    "hexagon_wireframe": (HEXAGON_WIREFRAME, (2, 2, 2)),
    "hole_block_shell": (HOLE_BLOCK_SHELL, (2, 2, 2)),
    "hole_block_wireframe": (HOLE_BLOCK_WIREFRAME, (2, 2, 2)),
    "personal_wireframe": (PERSONAL_WIREFRAME, (2, 2, 2)),  # Using hexagon_shell as default microstructure
    "personal_curve_wireframe": (PERSONAL_CURVE_WIREFRAME, (2, 2, 2)),
    "personal_shell_flat": (PERSONAL_SHELL_FLAT, (2, 2, 2)),
    "schwarz_p_shell": (SCHWARZ_P_SHELL, (2, 2, 2)),
    "schwarz_p_wireframe": (SCHWARZ_P_WIREFRAME, (2, 2, 2)),
    "snowflake_wireframe": (SNOWFLAKE_WIREFRAME, (2, 2, 2)),
    "star_beams": (STAR_BEAMS, (2, 2, 2)),
    "star_truss": (STAR_TRUSS, (2, 2, 2)),
    "tetrahedron_curved": (TETRAHEDRON_CURVED, (2, 2, 2)),
    "tetrahedron_mixed": (TETRAHEDRON_MIXED, (2, 2, 2)),
    "tetrahedron_shell": (TETRAHEDRON_SHELL, (2, 2, 2)),
    "tetrahedron_wireframe": (TETRAHEDRON_WIREFRAME, (2, 2, 2)),
}

print(f"Generating {len(materials)} microstructures in {output_dir}...")

# Generate all OBJ files
for name, (material_class, shape) in materials.items():
    try:
        output_path = os.path.join(output_dir, f"{name}.obj")
        print(f"Generating {name} -> {output_path}")
        
        # Generate and save the mesh
        save_material_obj(*generate_metamaterial_grid_surface_mesh(material_class, shape=shape), output_path)
        
        print(f"✓ Successfully generated {name}")
        
    except Exception as e:
        print(f"✗ Failed to generate {name}: {e}")

print("Generation complete!")
print(f"Generated {len([f for f in os.listdir(output_dir) if f.endswith('.obj')])} OBJ files in {output_dir}")

# Create manifest file
manifest_path = os.path.join(output_dir, "manifest.txt")
with open(manifest_path, 'w') as f:
    obj_files = [f for f in os.listdir(output_dir) if f.endswith('.obj')]
    obj_files.sort()
    for obj_file in obj_files:
        f.write(obj_file + '\n')

print(f"Created manifest file: {manifest_path}")

# # GENERATE INTERPOLATION

# from representation.generation import smooth_interpolation
# import os
# from example_materials.hexagon_shell import HEXAGON_SHELL
# from example_materials.tetrahedron_wireframe import TETRAHEDRON_WIREFRAME
# from representation.surface_meshing_torch import (
#     generate_metamaterial_grid_surface_mesh,
#     save_material_obj
# )


# # --- Settings ---
# output_dir = "interpolations/hexagon_shell-tetrahedron_wireframe"
# os.makedirs(output_dir, exist_ok=True)

# mat1 = HEXAGON_SHELL.copy()
# mat2 = TETRAHEDRON_WIREFRAME.copy()

# # Generate and save all interpolated meshes
# for i, interp_material in enumerate(smooth_interpolation(mat1, mat2)):
#     filename = f"interp_{i:03d}.obj"
#     output_path = os.path.join(output_dir, filename)
    
#     # Save to OBJ
#     save_material_obj(*generate_metamaterial_grid_surface_mesh(interp_material, shape=(2,2,2)), output_path)
#     print(f"Saved interpolation {i} to {output_path}")
