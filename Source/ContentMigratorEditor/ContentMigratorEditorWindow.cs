using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.Scripting;
using FlaxEngine;
using FlaxEngine.GUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace ContentMigratorEditor
{
    public class ContentMigratorEditorWindow : CustomEditorWindow
    {
        private TextBoxElement projectPathTextbox;
        private TextBoxElement targetDirTextbox;

        private string metaMapCacheJson = "projectMetaMap.json";
        private string targetMetaDir = "unityMeta";

        private Dictionary<string, string> guidMap;
        private TextureMigrator textureMigrator = new TextureMigrator();
        private AudioMigrator audioMigrator = new AudioMigrator();
        private MaterialMigrator matMigrator = new MaterialMigrator();

        private List<BuiltinMaterial> builtinMapList = new List<BuiltinMaterial>();
        private List<GuidMaterial> materialMapList = new List<GuidMaterial>();
        private ShaderMapperEditor openedShaderMapper;

        public override void Initialize(LayoutElementsContainer layout)
        {
            builtinMapList = MaterialMigrator.DefaultBuiltinMaterials.ToList();
            projectPathTextbox = layout.TextBox();
            var browseBtn = layout.Button("Browse");
            browseBtn.Button.Clicked += BrowseClicked;
            layout.Space(20);

            targetDirTextbox = layout.TextBox();
            targetDirTextbox.Text = Path.Join(Path.GetFullPath(Editor.Instance.GameProject.ProjectFolderPath), "Content", "Migrated");
            var browseTargetBtn = layout.Button("Browse");
            browseTargetBtn.Button.Clicked += BrowseTargetClicked;
            layout.Space(20);

            var openShaderMapperBtn = layout.Button("Shader Mapper");
            openShaderMapperBtn.Button.Clicked += OpenShaderMapperClicked;

            var button = layout.Button("Migrate content", Color.Blue);
            button.Button.Clicked += MigrateClicked;
        }

        protected override void Deinitialize()
        {
            if (openedShaderMapper != null)
            {
                openedShaderMapper.Window.Close();
            }
        }

        private void OpenShaderMapperClicked()
        {
            if (openedShaderMapper != null && !openedShaderMapper.Window.IsHidden)
            {
                openedShaderMapper.Window.FocusOrShow();
            }
            else
            {
                openedShaderMapper = new ShaderMapperEditor();
                openedShaderMapper.Window.Parent = Window;
                openedShaderMapper.BuiltinMapList = builtinMapList;
                openedShaderMapper.MaterialMapList = materialMapList;
                openedShaderMapper.OwningMigrator = this;
                openedShaderMapper.Show();
            }
        }

        private void BrowseClicked()
        {
            string path;
            FileSystem.ShowBrowseFolderDialog(this.Window.RootWindow.Window, null, "Select unity project directory", out path);
            projectPathTextbox.Text = path;
        }

        private void BrowseTargetClicked()
        {
            string path;
            FileSystem.ShowBrowseFolderDialog(this.Window.RootWindow.Window, null, "Select target content directory", out path);
            targetDirTextbox.Text = path;
        }

        private bool IsValidProject(string projectPath)
        {
            return Directory.Exists(projectPath) && Directory.Exists(Path.Join(projectPath, "Assets"));
        }

        void InitializeMigration(string projectPath, string destinationPath)
        {
            var projectFolder = Editor.Instance.ContentDatabase.Find(Editor.Instance.GameProject.ProjectFolderPath);
            Directory.CreateDirectory(destinationPath);
            var metaFiles = Directory.EnumerateFiles(projectPath, "*.meta", SearchOption.AllDirectories);
            var destinationMetaPath = Path.Join(destinationPath, targetMetaDir);

            guidMap = new Dictionary<string, string>();

            var guidFilePath = Path.Join(destinationMetaPath, metaMapCacheJson);
            if (File.Exists(guidFilePath))
            {
                using (StreamReader file = File.OpenText(guidFilePath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    JObject o2 = (JObject)JToken.ReadFrom(reader);
                    if (o2 != null)
                    {
                        foreach (var property in o2.Properties())
                        {
                            guidMap[property.Name] = property.Value<string>();
                        }
                    }
                }
            }

            Directory.CreateDirectory(destinationMetaPath);
            foreach (var metaFile in metaFiles)
            {
                var metaContents = File.OpenText(metaFile);
                var deserializer = new YamlStream();
                deserializer.Load(metaContents);
                string guid = (deserializer.Documents[0].RootNode["guid"] as YamlScalarNode).Value;
                string destFile = Path.Join(destinationMetaPath, guid);
                try
                {
                    File.Delete(destFile);
                }
                catch (Exception e) { }
                File.Copy(metaFile, destFile);
            }
            Editor.Instance.ContentDatabase.RefreshFolder(projectFolder, true);
        }

        private void MigrateClicked()
        {
            var path = Path.GetFullPath(projectPathTextbox.Text);
            var assetsPath = Path.GetFullPath(Path.Join(path, "Assets"));
            if (!IsValidProject(path))
            {
                MessageBox.Show("Invalid project!");
                return;
            }

            var targetPath = Path.GetFullPath(targetDirTextbox.Text);
            var flaxProjectPath = Path.GetFullPath(Editor.Instance.GameProject.ProjectFolderPath);

            if (!targetPath.StartsWith(flaxProjectPath))
            {
                MessageBox.Show("Invalid target path!");
                return;
            }

            InitializeMigration(assetsPath, targetPath);

            textureMigrator.Migrate(assetsPath, targetPath);
            audioMigrator.Migrate(assetsPath, targetPath);
            matMigrator.Migrate(assetsPath, targetPath);

            return;
            // var prefabFiles = Directory.EnumerateFiles(assetsPath, "*.prefab", SearchOption.AllDirectories);
            //
            // bool metaErrors = false;
            // foreach (var prefabFile in prefabFiles)
            // {
            //     var meta = $"{prefabFile}.meta";
            //     bool exists = File.Exists(meta);
            //     if (!exists)
            //     {
            //         metaErrors = true;
            //         Debug.LogError($"Meta file missing for file {prefabFile}");
            //         continue;
            //     }
            //
            //     var metaContents = File.OpenText(meta);
            //     var deserializer = new YamlStream();
            //     deserializer.Load(metaContents);
            //     Debug.Log(deserializer.Documents[0].RootNode);
            // }
            // if (metaErrors)
            // {
            //     Debug.LogError("Stopping migration due to asset file issues");
            //     return;
            // }
            // foreach (var prefabFile in prefabFiles)
            // {
            //     Debug.Log(prefabFile);
            //     var prefabContents = File.OpenText(prefabFile);
            //     var deserializer = new YamlStream();
            //     deserializer.Load(prefabContents);
            //     foreach (var doc in deserializer.Documents)
            //     {
            //         var deserialized = doc.RootNode;
            //         LogYamlNode(deserialized);
            //     }
            //     // Debug.Log((deserialized as YamlMappingNode).Children);
            //     // Debug.Log(deserialized[0]);
            //     // deserialized.
            //     // foreach (var node in deserialized.Start)
            //     // {
            //     //     Debug.Log(node);
            //     // }
            // }
        }
    }
}
