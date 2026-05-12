using System.IO;

public class ManifestGenerator
{
    public static void CreateManifest(string folderPath)
    {
        // Check folder exists
        if (!Directory.Exists(folderPath))
        {
            UnityEngine.Debug.LogError("Folder does not exist: " + folderPath);
            return;
        }

        // Open or create manifest.txt
        string manifestPath = Path.Combine(folderPath, "manifest.txt");
        using (StreamWriter writer = new StreamWriter(manifestPath))
        {
            // Generate filenames interp_000.obj to interp_229.obj
            for (int i = 0; i <= 229; i++)
            {
                string fileName = $"interp_{i:D3}.obj"; // D3 ensures 3 digits with leading zeros
                writer.WriteLine(fileName);
            }
        }

        UnityEngine.Debug.Log("Manifest created at: " + manifestPath);
    }
}
