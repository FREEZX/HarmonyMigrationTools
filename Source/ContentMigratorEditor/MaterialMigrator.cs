
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    async Task<MaterialInstance> CreateMaterialInstance(MaterialBase materialBase, string matFile, string directory)
    {
      var materialInstanceProxy = Editor.Instance.ContentDatabase.GetProxy<MaterialInstance>();
      TaskCompletionSource<MaterialInstance> tcs = new TaskCompletionSource<MaterialInstance>();
      Editor.Instance.Windows.ContentWin.NewItem(materialInstanceProxy, null, item =>
        {
          var assetItem = (AssetItem)item;
          var matInstance = FlaxEngine.Content.LoadAsync<MaterialInstance>(assetItem.ID);
          // matInstance.BaseMaterial = materialBase;
          // matInstance.Save();
          tcs.SetResult(matInstance);
        }, Path.GetFileNameWithoutExtension(matFile), false
      );
      var matInstance = await tcs.Task;
      var moveList = new List<ContentItem>();
      moveList.Add(Editor.Instance.ContentDatabase.FindAsset(matInstance.ID));
      var newParent = Editor.Instance.ContentDatabase.Find(directory) as ContentFolder;
      Editor.Instance.ContentDatabase.Move(moveList, newParent);
      matInstance.Reload();
      FlaxEngine.Content.LoadAsync<MaterialInstance>(matInstance.ID);
      Debug.Log("MatBase: " + materialBase);

      // matInstance.BaseMaterial = materialBase;
      // matInstance.Save();
      return matInstance;
    }

    public override async Task Migrate(string assetsPath, string destinationPath)
    {
      var assetsDir = new DirectoryInfo(assetsPath);
      var materialFiles = Directory.
          EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
          Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName))).ToList();
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
      var processedMatInstances = new Dictionary<string, MaterialInstance>();

      var maxIterations = 50000;
      var currentIterations = 0;

      while (materialFiles.Count > 0)
      {
        ++currentIterations;
        if (currentIterations > maxIterations)
        {
          Debug.Log($"MaterialMigrator max iterations exceeded. Current items in list: {materialFiles.Count}");
        }
        string matFile = materialFiles[0];
        materialFiles.RemoveAt(0);
        var meta = $"{matFile}.meta";
        bool exists = File.Exists(meta);
        if (!exists)
        {
          metaErrors = true;
          Debug.LogError($"Meta file missing for file {matFile}");
          continue;
        }

        var metaContents = File.OpenText(meta);
        var metaDeserializer = new YamlStream();
        metaDeserializer.Load(metaContents);

        var matContents = File.OpenText(matFile);
        var matDeserializer = new YamlStream();
        matDeserializer.Load(matContents);


        var matRootNode = matDeserializer.Documents[0].RootNode;
        var matGuid = metaDeserializer.Documents[0].RootNode["guid"];
        var matMaterialData = matRootNode["Material"];
        var matShaderParent = matMaterialData["m_Parent"];
        var matShaderParentGuid = (matShaderParent as YamlMappingNode).Children.ContainsKey("guid") ? matShaderParent["guid"] : null;

        MaterialBase parentMat = null;

        if (matShaderParentGuid != null)
        {
          var node = matShaderParentGuid as YamlScalarNode;
          if (!processedMatInstances.ContainsKey(node.Value))
          {
            // Skipping this file, parent not processed yet.
            materialFiles.Add(matFile);
            continue;
          }
          else
          {
            // Parent processed. Use it as a parent
            parentMat = FallbackMaterial;
          }
        }
        else
        {
          var matShader = matMaterialData["m_Shader"];

          var matShaderGuid = matShader["guid"];

          string matShaderGuidStr = (matShaderGuid as YamlScalarNode).Value;
          Material shaderForMaterial;
          // Special case: Unity builtin shaders 
          if (matShaderGuidStr == "0000000000000000f000000000000000")
          {
            Debug.Log("GuidStr means builtin!");
            var matShaderFileId = (matShader as YamlMappingNode).Children.ContainsKey("fileID") ? matShader["fileID"] : null;
            Debug.Log(matShader);
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
          parentMat = shaderForMaterial;
        }


        var assetsRelativePath = Path.GetRelativePath(assetsPath, matFile);
        var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

        var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
        Directory.CreateDirectory(targetDirectory);
        Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);
        var instance = await CreateMaterialInstance(parentMat, matFile, targetDirectory);
        processedMatInstances[(matGuid as YamlScalarNode).Value] = instance;
      }
      if (metaErrors)
      {
        Debug.LogError("Meta errors. Migration stopping.");
      }
    }
  }
}
