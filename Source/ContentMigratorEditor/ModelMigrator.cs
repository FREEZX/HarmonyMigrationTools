using System.Globalization;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

class ModelMigrator : AssetMigratorBase
{
  protected override string[] HandledExtensions
  {
    get
    {
      return handledExtensions;
    }
  }

  string[] handledExtensions = new string[] {
    "*.obj",
    "*.fbx",
    "*.x",
    "*.dae",
    "*.gltf",
    "*.glb",
    "*.blend",
    "*.bvh",
    "*.ase",
    "*.ply",
    "*.dxf",
    "*.ifc",
    "*.nff",
    "*.smd",
    "*.vta",
    "*.mdl",
    "*.md2",
    "*.md3",
    "*.md5mesh",
    "*.q3o",
    "*.q3s",
    "*.ac",
    "*.stl",
    "*.lwo",
    "*.lws",
    "*.lxo",
  };

  public override async Task Migrate(string assetsPath, string destinationPath)
  {

    var assetsDir = new DirectoryInfo(assetsPath);
    var modelFiles = Directory.
        EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
        Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));
    var destinationFolder = Editor.Instance.ContentDatabase.Find(destinationPath);

    bool metaErrors = false;
    foreach (var modelFile in modelFiles)
    {
      var meta = $"{modelFile}.meta";
      bool exists = File.Exists(meta);
      if (!exists)
      {
        metaErrors = true;
        Debug.LogError($"Meta file missing for file {modelFile}");
        continue;
      }

      var metaContents = File.OpenText(meta);
      var deserializer = new YamlStream();
      deserializer.Load(metaContents);
      var rootNode = deserializer.Documents[0].RootNode;
      var guid = rootNode["guid"];
      var modelImporterSettings = rootNode["ModelImporter"];
      var materialsData = modelImporterSettings["materials"] as YamlMappingNode;
      var materialsDataDict = materialsData.Children;
      var importMats = materialsDataDict.ContainsKey("importMaterials") ? int.Parse((materialsDataDict["importMaterials"] as YamlScalarNode).Value) : 0;
      // if (importMaterials > 0)
      // {
      //   var matImportMode = int.Parse((materialsData["materialImportMode"] as YamlScalarNode).Value);
      // }

      var meshesData = modelImporterSettings["meshes"] as YamlMappingNode;
      var scale = float.Parse((meshesData["globalScale"] as YamlScalarNode).Value, CultureInfo.InvariantCulture);
      var importBlendShapes = int.Parse((meshesData["importBlendShapes"] as YamlScalarNode).Value);

      var tangentSpaceData = modelImporterSettings["tangentSpace"] as YamlMappingNode;
      var normalImportMode = int.Parse((tangentSpaceData["normalImportMode"] as YamlScalarNode).Value);
      var normalSmoothingAngle = int.Parse((tangentSpaceData["normalSmoothAngle"] as YamlScalarNode).Value);
      var tangentImportMode = int.Parse((tangentSpaceData["tangentImportMode"] as YamlScalarNode).Value);


      var animations = modelImporterSettings["animations"] as YamlMappingNode;
      var animClips = (animations["clipAnimations"] as YamlSequenceNode);
      if (animClips.Children.Count > 0)
      {

      }

      var animType = int.Parse((modelImporterSettings["animationType"] as YamlScalarNode).Value); // 0 - No anim, 1 = Legacy, 2 = Generic, 3 = Humanoid

      var assetsRelativePath = Path.GetRelativePath(assetsPath, modelFile);
      var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

      // Import
      Request importRequest = new Request();
      importRequest.InputPath = modelFile;
      importRequest.OutputPath = newProjectRelativePath;
      importRequest.SkipSettingsDialog = true;

      var importSettings = new ModelImportSettings();
      // Import as model first.
      importSettings.Settings.Type = animType == 0 ? FlaxEngine.Tools.ModelTool.ModelType.Model : FlaxEngine.Tools.ModelTool.ModelType.SkinnedModel;
      importSettings.Settings.Scale = scale;
      importSettings.Settings.ImportMaterials = importMats > 0;
      importSettings.Settings.CalculateNormals = normalImportMode > 0;
      importSettings.Settings.SmoothingNormalsAngle = normalSmoothingAngle;
      importSettings.Settings.CalculateTangents = tangentImportMode != 2 && tangentImportMode != 0; // 0 = Import; 2 = None
      importSettings.Settings.ImportBlendShapes = importBlendShapes > 0;
      importRequest.Settings = importSettings;
      var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
      Directory.CreateDirectory(targetDirectory);
      Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);
      var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
      Editor.Instance.ContentImporting.Import(modelFile, contentFolder, false, importSettings);
      var importEntry = ModelImportEntry.CreateEntry(ref importRequest);
      bool success = importEntry.Import();

      if (animClips.Children.Count > 0)
      {

      }
    }
    if (metaErrors)
    {
      Debug.LogError("Meta errors. Migration stopping.");
    }
  }
}
