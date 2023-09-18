using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEditor.Content;
using FlaxEditor.Content.Import;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

class TextureMigrator : AssetMigratorBase
{
  protected override string[] HandledExtensions
  {
    get
    {
      return handledExtensions;
    }
  }
  string[] handledExtensions = new string[] {
    "*.tga",
    "*.png",
    "*.bmp",
    "*.gif",
    "*.tiff",
    "*.tif",
    "*.jpeg",
    "*.jpg",
    "*.dds",
    "*.hdr",
    "*.raw"
  };

  enum TextureType
  {
    Default,
    NormalMap
  }

  public override async Task Migrate(string assetsPath, string destinationPath)
  {
    var assetsDir = new DirectoryInfo(assetsPath);
    var texFiles = Directory.
        EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
        Where(fileName => handledExtensions.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));
    // Find destination path directory
    var destinationFolder = Editor.Instance.ContentDatabase.Find(destinationPath);

    bool metaErrors = false;
    foreach (var texFile in texFiles)
    {
      var meta = $"{texFile}.meta";
      bool exists = File.Exists(meta);
      if (!exists)
      {
        metaErrors = true;
        Debug.LogError($"Meta file missing for file {texFile}");
        continue;
      }

      var metaContents = File.OpenText(meta);
      var deserializer = new YamlStream();
      deserializer.Load(metaContents);
      var rootNode = deserializer.Documents[0].RootNode;
      var guid = rootNode["guid"];
      var textureImporterSettings = rootNode["TextureImporter"];
      var textureType = int.Parse((textureImporterSettings["textureType"] as YamlScalarNode).Value);
      var alphaUsage = int.Parse((textureImporterSettings["alphaUsage"] as YamlScalarNode).Value);
      var sRGBTexture = int.Parse((textureImporterSettings["mipmaps"]["sRGBTexture"] as YamlScalarNode).Value);
      var enableMipMap = int.Parse((textureImporterSettings["mipmaps"]["enableMipMap"] as YamlScalarNode).Value);
      // The standard texture types in Unity are 0 - Normal and 1 - NormalMap
      if (textureType > 1)
      {
        textureType = 0;
      }

      var assetsRelativePath = Path.GetRelativePath(assetsPath, texFile);
      var newProjectRelativePath = Path.Join(destinationPath, assetsRelativePath);

      // Import
      Request importRequest = new Request();
      importRequest.InputPath = texFile;
      importRequest.OutputPath = newProjectRelativePath;
      importRequest.SkipSettingsDialog = true;

      var importSettings = new TextureImportSettings();
      if (textureType == 1)
      {
        importSettings.Settings.Type = TextureFormatType.NormalMap;
      }
      else
      {
        // HDR
        if (sRGBTexture == 0)
        {
          if (alphaUsage == 1)
          {
            importSettings.Settings.Type = TextureFormatType.HdrRGBA;
          }
          else
          {
            importSettings.Settings.Type = TextureFormatType.HdrRGB;
          }
        }
        else
        {
          if (alphaUsage == 1)
          {
            importSettings.Settings.Type = TextureFormatType.ColorRGBA;
          }
          else
          {
            importSettings.Settings.Type = TextureFormatType.ColorRGB;
          }
        }
      }
      importSettings.Settings.GenerateMipMaps = enableMipMap > 0;
      importRequest.Settings = importSettings;
      var targetDirectory = Path.GetDirectoryName(newProjectRelativePath);
      Directory.CreateDirectory(targetDirectory);
      Editor.Instance.ContentDatabase.RefreshFolder(destinationFolder, true);
      var contentFolder = (ContentFolder)Editor.Instance.ContentDatabase.Find(targetDirectory);
      Editor.Instance.ContentImporting.Import(texFile, contentFolder, false, importSettings);
      var importEntry = TextureImportEntry.CreateEntry(ref importRequest);
      bool success = importEntry.Import();
    }
    if (metaErrors)
    {
      Debug.LogError("Meta errors. Migration stopping.");
    }
  }
}
