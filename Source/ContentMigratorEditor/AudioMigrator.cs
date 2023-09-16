using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

class AudioMigrator : AssetMigratorBase
{
  protected override string[] HandledExtensions
  {
    get
    {
      return handledExtensions;
    }
  }
  string[] handledExtensions = new string[] {
    "*.ogg",
    "*.wav",
    "*.mp3",
  };

  public override void Migrate(string assetsPath, string destinationPath)
  {

    var assetsDir = new DirectoryInfo(assetsPath);
    var audioFiles = Directory.
        EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
        Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));

    bool metaErrors = false;
    foreach (var audioFile in audioFiles)
    {
      var meta = $"{audioFile}.meta";
      bool exists = File.Exists(meta);
      if (!exists)
      {
        metaErrors = true;
        Debug.LogError($"Meta file missing for file {audioFile}");
        continue;
      }

      var metaContents = File.OpenText(meta);
      var deserializer = new YamlStream();
      deserializer.Load(metaContents);
      var rootNode = deserializer.Documents[0].RootNode;
      var guid = rootNode["guid"];
      var textureImporterSettings = rootNode["AudioImporter"];
      var is3D = int.Parse((textureImporterSettings["3D"] as YamlScalarNode).Value);

      var assetsRelativePath = Path.GetRelativePath(assetsPath, audioFile);
      var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

      // Import
      Request importRequest = new Request();
      importRequest.InputPath = audioFile;
      importRequest.OutputPath = newProjectRelativePath;
      importRequest.SkipSettingsDialog = true;

      var importSettings = new AudioImportSettings();
      importSettings.Is3D = is3D > 0;
      importSettings.Format = AudioFormat.Vorbis;
      importRequest.Settings = importSettings;
      var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
      Directory.CreateDirectory(targetDirectory);
      var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
      Editor.Instance.ContentImporting.Import(audioFile, contentFolder, false, importSettings);
      var importEntry = TextureImportEntry.CreateEntry(ref importRequest);
      bool success = importEntry.Import();
    }
    if (metaErrors)
    {
      Debug.LogError("Meta errors. Migration stopping.");
    }
  }
}
