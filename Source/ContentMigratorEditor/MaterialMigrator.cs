
using System.Collections.Generic;
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
    public string ShaderName; // User-friendly name
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

    // Some known FileIds for builtin mats:
    // 45 - Standard Specular
    // 46 - Standard
    // 10701 - VertexLit
    // 10750 - Unlit/Transparent
    // 10751 - Unlit/Transparent Cutout
    // 10752 - Unlit/Texture
    // 10755 - Unlit/Color
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

    public static GuidMaterial[] DefaultGuidMaterials
    {
      get
      {
        return new GuidMaterial[]{
          new GuidMaterial() {
            Guid = "GUIDHERE",
            ShaderName = "Standard",
            Material = LoadBuiltinShader("Standard.flax")
        }
        };
      }
    }

    public static Material FallbackMaterial
    {
      get
      {
        return LoadBuiltinShader("Standard.flax");
      }
    }

    public BuiltinMaterial[] BuiltinMaterials;
    public GuidMaterial[] GuidMaterials;

    protected static Material LoadBuiltinShader(string name)
    {
      var materialPath = Path.Join(Path.GetFullPath(Editor.Instance.GameProject.ProjectFolderPath), "Content", "MigratorAssets", name);
      var assetItem = Editor.Instance.ContentDatabase.Find(materialPath) as AssetItem;
      return FlaxEngine.Content.LoadAsync(assetItem.ID) as Material;
    }


    public override void Migrate(string assetsPath, string destinationPath)
    {
      var assetsDir = new DirectoryInfo(assetsPath);
      var materialFiles = Directory.
          EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
          Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));
      var destinationFolder = Editor.Instance.ContentDatabase.Find(destinationPath);
      var builtinDictionary = new Dictionary<int, Material>();
      foreach (var builtinMat in BuiltinMaterials)
      {
        builtinDictionary[builtinMat.FileId] = builtinMat.Material;
      }
      var guidMatsDictionary = new Dictionary<string, Material>();
      foreach (var guidMat in GuidMaterials)
      {
        guidMatsDictionary[guidMat.Guid] = guidMat.Material;
      }

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

        string matShaderGuidStr = (matShaderGuid as YamlScalarNode).Value;
        Material shaderForMaterial;
        // Special case: Unity builtin shaders 
        if (matShaderGuidStr == "0000000000000000f000000000000000")
        {
          var shaderFileId = int.Parse((matShaderFileId as YamlScalarNode).Value);

          if (!builtinDictionary.ContainsKey(shaderFileId))
          {
            shaderFileId = 46; // Revert to standard
          }
          shaderForMaterial = builtinDictionary[shaderFileId];
        }
        else
        {
          if (!guidMatsDictionary.ContainsKey(matShaderGuidStr))
          {
            // No special mat defined. Fallback
            Debug.LogWarning($"Material not found for shader {matShaderGuidStr}. Using standard.");
            shaderForMaterial = FallbackMaterial;
          }
          else
          {
            shaderForMaterial = guidMatsDictionary[matShaderGuidStr];
          }
        }

        var assetsRelativePath = Path.GetRelativePath(assetsPath, matFile);
        var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

        // Import
        // var matProxy = Editor.Instance.ContentDatabase.GetProxy<Material>();

        // var materialInstanceProxy = Editor.Instance.ContentDatabase.GetProxy<MaterialInstance>();
        // Editor.Instance.Windows.ContentWin.NewItem(materialInstanceProxy, null, item => OnMaterialInstanceCreated(item, materialItem), Path.GetFileNameWithoutExtension(matFile));
        // MaterialProxy.CreateMaterialInstance(
        // (shaderForMaterial);

        var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
        Directory.CreateDirectory(targetDirectory);
        Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);
        var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
      }
      if (metaErrors)
      {
        Debug.LogError("Meta errors. Migration stopping.");
      }
    }
  }
}
