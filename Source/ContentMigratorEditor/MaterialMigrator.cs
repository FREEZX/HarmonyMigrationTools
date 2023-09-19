
using System.Collections.Generic;
using System.Globalization;
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
      return FlaxEngine.Content.Load(assetItem.ID) as Material;
    }

    async Task<MaterialInstance> CreateMaterialInstance(MaterialBase materialBase, string matFile, string directory)
    {
      // Delete old mat in case it exists
      var newParent = Editor.Instance.ContentDatabase.Find(directory) as ContentFolder;
      var materialInstanceProxy = Editor.Instance.ContentDatabase.GetProxy<MaterialInstance>();
      TaskCompletionSource<MaterialInstance> tcs = new TaskCompletionSource<MaterialInstance>();
      Editor.Instance.Windows.ContentWin.NewItem(materialInstanceProxy, null, item =>
        {
          var assetItem = (AssetItem)item;
          var matInstance = FlaxEngine.Content.Load<MaterialInstance>(assetItem.ID);
          tcs.SetResult(matInstance);
        }, Path.GetFileNameWithoutExtension(matFile), false
      );
      var matInstance = await tcs.Task;
      var moveList = new List<ContentItem>();
      moveList.Add(Editor.Instance.ContentDatabase.FindAsset(matInstance.ID));
      Editor.Instance.ContentDatabase.Move(moveList, newParent);
      matInstance.BaseMaterial = materialBase;
      matInstance.Save();
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
            parentMat = processedMatInstances[node.Value];
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

        var savedProps = matMaterialData["m_SavedProperties"];
        // Texture props
        var texSeq = savedProps["m_TexEnvs"] as YamlSequenceNode;
        foreach (var texInfo in texSeq)
        {
          foreach (var key in (texInfo as YamlMappingNode).Children.Keys)
          {
            var data = texInfo[key];

            var texFile = data["m_Texture"] as YamlMappingNode;
            var fileId = int.Parse((texFile["fileID"] as YamlScalarNode).Value);
            if (fileId != 0)
            {
              bool hasGuid = (texFile.Children).ContainsKey("guid");
              if (hasGuid)
              {
                string unityGuid = (texFile["guid"] as YamlScalarNode).Value;
                // Load local texture, if possible
                System.Guid flaxGuid = System.Guid.Empty;
                bool success = OwnerMigratorEditor.unityFlaxGuidMap.TryGetValue(unityGuid, out flaxGuid);
                if (success)
                {
                  var texture = FlaxEngine.Content.Load(flaxGuid) as Texture;
                  instance.SetParameterValue((key as YamlScalarNode).Value, texture);
                }
              }
            }
            //Debug.Log(key);
          }
        }
        var floatsSeq = savedProps["m_Floats"] as YamlSequenceNode;
        foreach (var floatInfo in floatsSeq)
        {
          var floatMap = (floatInfo as YamlMappingNode);
          foreach (var key in (floatMap as YamlMappingNode).Children.Keys)
          {
            // Debug.Log(float.Parse((floatMap[key] as YamlScalarNode).Value));
            instance.SetParameterValue((key as YamlScalarNode).Value, float.Parse((floatMap.Children[key] as YamlScalarNode).Value, CultureInfo.InvariantCulture.NumberFormat));
          }
        }
        var intsSeq = savedProps["m_Ints"] as YamlSequenceNode;
        foreach (var intInfo in intsSeq)
        {
          var intsMap = (intInfo as YamlMappingNode);
          foreach (var key in (intsMap as YamlMappingNode).Children.Keys)
          {
            instance.SetParameterValue((key as YamlScalarNode).Value, int.Parse((intsMap.Children[key] as YamlScalarNode).Value));
          }
        }
        var colorsSeq = savedProps["m_Colors"] as YamlSequenceNode;
        foreach (var colorInfo in colorsSeq)
        {
          var intsMap = (colorInfo as YamlMappingNode);
          foreach (var key in (intsMap as YamlMappingNode).Children.Keys)
          {
            var data = intsMap[key];

            var r = (data["r"] as YamlScalarNode).Value;
            var g = (data["g"] as YamlScalarNode).Value;
            var b = (data["b"] as YamlScalarNode).Value;
            var a = (data["a"] as YamlScalarNode).Value;
            // Debug.Log(intsMap);
            instance.SetParameterValue((key as YamlScalarNode).Value, new Float4(
                  float.Parse(r, CultureInfo.InvariantCulture),
                  float.Parse(g, CultureInfo.InvariantCulture),
                  float.Parse(b, CultureInfo.InvariantCulture),
                  float.Parse(a, CultureInfo.InvariantCulture)));
          }
        }
        // Iterate and set Textures
        // for (int i = 0; i <)
        // instance.SetParameterValue()
        processedMatInstances[(matGuid as YamlScalarNode).Value] = instance;
      }
      if (metaErrors)
      {
        Debug.LogError("Meta errors. Migration stopping.");
      }
    }
  }
}
