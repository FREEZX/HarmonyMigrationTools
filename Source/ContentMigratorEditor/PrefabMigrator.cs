
using System;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEditor.Content;
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

      var prefabProxy = Editor.Instance.ContentDatabase.GetProxy<Prefab>();
      // TaskCompletionSource<AssetItem> tcs = new TaskCompletionSource<AssetItem>();
      // Action<ContentItem> onContentAdded = (ContentItem contentItem) =>
      // {
      //   Debug.Log(Path.GetFileName(contentItem.Path));
      //   if (Path.GetFileName(prefabFile) == Path.GetFileName(contentItem.Path))
      //   {
      //     tcs.SetResult(contentItem as AssetItem);
      //   }
      // };
      // Editor.Instance.ContentDatabase.ItemAdded += onContentAdded;
      // prefabProxy.Create(newProjectRelativePath, null);
      // var assetItem = await tcs.Task;
      // Editor.Instance.ContentDatabase.ItemAdded -= onContentAdded;
      // var assetItem = Editor.Instance.ContentDatabase.Find(newProjectRelativePath);

      var prefab = FlaxEngine.Content.Load(newProjectRelativePath) as Prefab;

      // Iterate docs 
      foreach (var doc in prefabDeserializer.Documents)
      {
        var anchor = doc.RootNode.Anchor.Value;
        var mappingRootNode = (doc.RootNode as YamlMappingNode);
        var rootNodeChildren = mappingRootNode.Children;
        Debug.Log("Processing doc!");
        for (var i = 0; i < rootNodeChildren.Count; ++i)
        {
          var keyStr = (rootNodeChildren[i].Key as YamlScalarNode).Value;
          Debug.Log("KeyStr keyStr");

          switch (keyStr)
          {
            case "GameObject":
              Debug.Log("Found GO!");
              break;
            case "Transform":
              Debug.Log("Found transform!");
              break;
          }
        }
        // foreach (var node in doc.RootNode.AllNodes)
        // {
        //   var mapNode = node as YamlMappingNode;
        //   // foreach (var mapNodeKV in mapNode.Children)
        //   // {
        //   //   var key = (mapNodeKV.Key as YamlScalarNode).Value;
        //   // }
        // }
      }
    }
    if (metaErrors)
    {
      Debug.LogError("Meta errors. Migration stopping.");
    }
  }
}
