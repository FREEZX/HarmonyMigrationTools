
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

class PrefabMigrator : AssetMigratorBase
{
  protected override string[] HandledExtensions
  {
    get
    {
      return handledExtensions;
    }
  }
  string[] handledExtensions = new string[] {
    "*.prefab",
  };

  public override async Task Migrate(string assetsPath, string destinationPath)
  {

    var assetsDir = new DirectoryInfo(assetsPath);
    var prefabFiles = Directory.
        EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
        Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));
    var destinationFolder = Editor.Instance.ContentDatabase.Find(destinationPath);

    bool metaErrors = false;
    foreach (var prefabFile in prefabFiles)
    {
      var meta = $"{prefabFile}.meta";
      bool exists = File.Exists(meta);
      if (!exists)
      {
        metaErrors = true;
        Debug.LogError($"Meta file missing for file {prefabFile}");
        continue;
      }

      var metaContents = File.OpenText(meta);
      var deserializer = new YamlStream();
      deserializer.Load(metaContents);
      var rootNode = deserializer.Documents[0].RootNode;
      var guid = rootNode["guid"];

      var prefabContents = File.OpenText(prefabFile);
      var prefabDeserializer = new YamlStream();
      prefabDeserializer.Load(prefabContents);


      var assetsRelativePath = Path.GetRelativePath(assetsPath, prefabFile);
      var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

      // Import
      Request importRequest = new Request();
      importRequest.InputPath = prefabFile;
      // importRequest.OutputPath = ;
      importRequest.SkipSettingsDialog = true;

      var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
      Directory.CreateDirectory(targetDirectory);
      Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);


      var prefabProxy = Editor.Instance.ContentDatabase.GetProxy<Prefab>();
      prefabProxy.Create(newProjectRelativePath, null);

      var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
      // Editor.Instance.ContentImporting.Import(prefabFile, contentFolder, false, importSettings);
      // var importEntry = TextureImportEntry.CreateEntry(ref importRequest);
      // bool success = importEntry.Import();
    }
    if (metaErrors)
    {
      Debug.LogError("Meta errors. Migration stopping.");
    }
  }
}
