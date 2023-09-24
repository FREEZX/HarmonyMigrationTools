
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using FlaxEditor;
using FlaxEngine;
using FlaxEngine.Assertions;
using YamlDotNet.RepresentationModel;

class PrefabMigrator : AssetMigratorBase
{
  // Used for root objects.
  class UnityBaseDescriptor
  {
    public string fileId;
    public static UnityBaseDescriptor FromYamlNode(YamlNode node)
    {
      var keys = (node as YamlMappingNode).Children.Keys.ToArray();
      Assert.IsTrue(keys.Length == 1);
      var node0 = node[keys[0]] as YamlMappingNode;


      switch ((keys[0] as YamlScalarNode).Value)
      {
        case "GameObject":
          return UnityGameObjectDescriptor.FromYamlNode(node0);
        case "Transform":
          return UnityTransformDescriptor.FromYamlNode(node0);
        case "MonoBehaviour":
          return MonoBehaviourDescriptor.FromYamlNode(node0);
      }
      return new UnityBaseDescriptor();
    }
  }

  // Used for inner objects
  class FileIdDescriptor
  {
    public string fileId;
  }

  class ReferenceDescriptor : FileIdDescriptor
  {
    public string guid;
    public int type;
  }

  class BitsDescriptor
  {
    int bits;
  }
  class Vector2Descriptor
  {
    public float x;
    public float y;
  }
  class Vector3Descriptor : Vector2Descriptor
  {
    public float z;
  }
  class Vector4Descriptor : Vector3Descriptor
  {
    public float w;
  }
  class UnityGameObjectDescriptor : UnityBaseDescriptor
  {
    public int staticEditorFlags;
    public int layer;
    public List<FileIdDescriptor> components;
    public static UnityGameObjectDescriptor FromYamlNode(YamlMappingNode node)
    {
      return new UnityGameObjectDescriptor();
    }
  }
  class UnityComponentDescriptor : UnityBaseDescriptor
  {
    public FileIdDescriptor gameObject;
    public bool enabled;
    public void ApplyFromYamlNode(YamlMappingNode node)
    {

    }
  }
  class UnityTransformDescriptor : UnityComponentDescriptor
  {
    public FileIdDescriptor father;
    public List<FileIdDescriptor> children;
    public Vector3Descriptor localPos;
    public Vector4Descriptor localRot;
    public Vector3Descriptor localScale;
    public static UnityTransformDescriptor FromYamlNode(YamlMappingNode node)
    {
      var descriptor = new UnityTransformDescriptor();
      descriptor.ApplyFromYamlNode(node);
      return descriptor;
    }
  }
  class ColliderDescriptor : UnityComponentDescriptor
  {
    public FileIdDescriptor material;
    public BitsDescriptor includeLayers;
    public BitsDescriptor excludeLayers;
  }
  class BoxColliderDescriptor : ColliderDescriptor
  {
    public Vector3Descriptor size;
    public Vector3Descriptor center;
  }

  public enum ScriptParamType
  {
    Array,
    String,
    Int,
    Double,
    ExternalReference,
    InternalReference,
    Object
  }

  class ScriptParamDescriptor
  {
    public ScriptParamType ParamType;
  }

  class ArrayParamDescriptor : ScriptParamDescriptor
  {
    public List<ScriptParamDescriptor> InternalParams;
  }

  class StringParamDescriptor : ScriptParamDescriptor
  {
    public string String;
  }

  class IntParamDescriptor : ScriptParamDescriptor
  {
    public int Int;
  }

  class DoubleParamDescriptor : ScriptParamDescriptor
  {
    public double Double;
  }

  class ReferenceParamDescriptor : ScriptParamDescriptor
  {
    public ReferenceDescriptor Reference;
  }

  class ObjectParamDescriptor : ScriptParamDescriptor
  {
    public Dictionary<string, ScriptParamDescriptor> Object;
  }

  class MonoBehaviourDescriptor : UnityComponentDescriptor
  {
    public ReferenceDescriptor script;
    public Dictionary<string, ScriptParamDescriptor> scriptParams;
  }

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


      var root = new EmptyActor();
      root.Name = "Root";

      var gameObjects = new Dictionary<string, Actor>();
      var transforms = new Dictionary<string, Actor>();

      // PrefabManager.CreatePrefab();
      // var prefabProxy = Editor.Instance.ContentDatabase.GetProxy<Prefab>();
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

      // var prefab = FlaxEngine.Content.Load(newProjectRelativePath) as Prefab;

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
              // Create this game object as empty actor
              var go = new EmptyActor();
              go.Name = (rootNodeChildren[i].Value["m_Name"] as YamlScalarNode).Value;
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
