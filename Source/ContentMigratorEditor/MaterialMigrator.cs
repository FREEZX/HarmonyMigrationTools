
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

namespace ContentMigratorEditor
{
  public class BuiltinMaterial
  {
    public int FileId;
    public string ShaderName; // User-friendly name
    public Material Material;
  }

  public class GuidMaterial
  {
    public string Guid;
    public Material Material;
  }

  class MaterialMigrator : AssetMigratorBase
  {
    protected override string[] HandledExtensions
    {
      get
      {
        return handledExtensions;
      }
    }
    string[] handledExtensions = new string[] {
      "*.mat",
    };

    public static BuiltinMaterial[] DefaultBuiltinMaterials
    {
      get
      {
        return new BuiltinMaterial[]{
          new BuiltinMaterial() {
            FileId = 46,
            ShaderName = "Standard",
            Material = LoadBuiltinShader("Standard.flax")
        }
        };
      }
    }

    protected static Material LoadBuiltinShader(string name)
    {
      var materialPath = Path.Join(Path.GetFullPath(Editor.Instance.GameProject.ProjectFolderPath), "Content", "MigratorAssets", name);
      var assetItem = Editor.Instance.ContentDatabase.Find(materialPath) as AssetItem;
      Debug.Log(assetItem);
      Debug.Log(materialPath);
      return FlaxEngine.Content.LoadAsync(assetItem.ID) as Material;
    }


    public override void Migrate(string assetsPath, string destinationPath)
    {

      var assetsDir = new DirectoryInfo(assetsPath);
      var materialFiles = Directory.
          EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
          Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));
      var destinationFolder = Editor.Instance.ContentDatabase.Find(destinationPath);

      bool metaErrors = false;
      foreach (var matFile in materialFiles)
      {
        var meta = $"{matFile}.meta";
        bool exists = File.Exists(meta);
        if (!exists)
        {
          metaErrors = true;
          Debug.LogError($"Meta file missing for file {matFile}");
          continue;
        }

        var metaContents = File.OpenText(meta);
        var deserializer = new YamlStream();
        deserializer.Load(metaContents);

        var matContents = File.OpenText(matFile);
        var matDeserializer = new YamlStream();
        matDeserializer.Load(matContents);

        var matRootNode = matDeserializer.Documents[0].RootNode;
        var matShader = matRootNode["Material"]["m_Shader"];

        var matShaderGuid = matShader["guid"];
        var matShaderFileId = matShader["fileId"];
        // Special case: Unity builtin shaders 
        if ((matShaderGuid as YamlScalarNode).Value == "0000000000000000f000000000000000")
        {
          var shaderFileId = int.Parse((matShaderFileId as YamlScalarNode).Value);
          switch (shaderFileId)
          {
            case 45:
              // Standard specular
              break;
            case 46:
              // Standard
              break;
            case 10701:
              // VertexLit
              break;
            case 10752:
              // Unlit/Texture
              break;
            case 10750:
              // Unlit/Transparent
              break;
            case 10751:
              // Unlit/Transparent Cutout
              break;
            case 10755:
              // Unlit/Color
              break;
          }
        }

        var assetsRelativePath = Path.GetRelativePath(assetsPath, matFile);
        var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

        // Import
        Request importRequest = new Request();
        importRequest.InputPath = matFile;
        importRequest.OutputPath = newProjectRelativePath;
        importRequest.SkipSettingsDialog = true;

        var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
        Directory.CreateDirectory(targetDirectory);
        Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);
        var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
        var importEntry = TextureImportEntry.CreateEntry(ref importRequest);
        bool success = importEntry.Import();
      }
      if (metaErrors)
      {
        Debug.LogError("Meta errors. Migration stopping.");
      }
    }
  }
}
