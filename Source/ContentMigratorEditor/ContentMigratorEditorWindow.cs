using System;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEngine;
using YamlDotNet.RepresentationModel;

namespace ContentMigratorEditor
{

    public class ContentMigratorEditorWindow : CustomEditorWindow
    {
        private TextBoxElement projectPathTextbox;
        private TextBoxElement targetDirTextbox;

        private string metaMapperContainer = "projectMetaMap.txt";
        private string targetMetaDir = "unityMeta";

        public override void Initialize(LayoutElementsContainer layout)
        {
            layout.Label("Unity project path");
            projectPathTextbox = layout.TextBox();
            var browseBtn = layout.Button("Browse");
            browseBtn.Button.Clicked += BrowseClicked;
            layout.Space(20);
            targetDirTextbox = layout.TextBox();
            targetDirTextbox.Text = Path.Join(Path.GetFullPath(Editor.Instance.GameProject.ProjectFolderPath), "Content", "Migrated");
            var browseTargetBtn = layout.Button("Browse");
            browseTargetBtn.Button.Clicked += BrowseTargetClicked;
            layout.Space(20);
            var button = layout.Button("Migrate content", Color.Blue);
            button.Button.Clicked += MigrateClicked;
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
            Directory.CreateDirectory(destinationPath);
            var metaFiles = Directory.EnumerateFiles(projectPath, "*.meta", SearchOption.AllDirectories);
            var destinationMetaPath = Path.Join(destinationPath, targetMetaDir);
            Directory.CreateDirectory(destinationMetaPath);
            foreach (var metaFile in metaFiles)
            {
                var metaContents = File.OpenText(metaFile);
                var deserializer = new YamlStream();
                deserializer.Load(metaContents);
                string guid = (deserializer.Documents[0].RootNode["guid"] as YamlScalarNode).Value;
                File.Copy(metaFile, Path.Join(destinationMetaPath, guid));
            }
        }

        private void MigrateTextures(string assetsPath)
        {
            var assetsDir = new DirectoryInfo(assetsPath);
            var masks = new string[] {
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
            var texFiles = Directory.
                EnumerateFiles(assetsPath, "*", SearchOption.AllDirectories).
                Where(fileName => masks.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, fileName)));

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
                Debug.Log(deserializer.Documents[0].RootNode["guid"]);
                // var mis = new ModelImportSettings();
                // mis.Settings.OptimizeKeyframe
            }
            if (metaErrors)
            {
                Debug.LogError("Meta errors. Migration stopping.");
            }
        }
        //
        // private void MigrateModels(string assetsPath)
        // {
        //     var modelFiles = Directory.EnumerateFiles(assetsPath, "*.fbx", SearchOption.AllDirectories);
        //
        //     bool metaErrors = false;
        //     foreach (var prefabFile in modelFiles)
        //     {
        //         var meta = $"{prefabFile}.meta";
        //         bool exists = File.Exists(meta);
        //         if (!exists)
        //         {
        //             metaErrors = true;
        //             Debug.LogError($"Meta file missing for file {prefabFile}");
        //             continue;
        //         }
        //
        //         var metaContents = File.OpenText(meta);
        //         var deserializer = new YamlStream();
        //         deserializer.Load(metaContents);
        //         Debug.Log(deserializer.Documents[0].RootNode["guid"]);
        //         // var mis = new ModelImportSettings();
        //         // mis.Settings.OptimizeKeyframe
        //     }
        //     if (metaErrors)
        //     {
        //         Debug.LogError("Meta errors. Migration stopping.");
        //     }
        // }

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

            MigrateTextures(assetsPath);

            return;
            var prefabFiles = Directory.EnumerateFiles(assetsPath, "*.prefab", SearchOption.AllDirectories);

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
                Debug.Log(deserializer.Documents[0].RootNode);
            }
            if (metaErrors)
            {
                Debug.LogError("Stopping migration due to asset file issues");
                return;
            }
            foreach (var prefabFile in prefabFiles)
            {
                Debug.Log(prefabFile);
                var prefabContents = File.OpenText(prefabFile);
                var deserializer = new YamlStream();
                deserializer.Load(prefabContents);
                foreach (var doc in deserializer.Documents)
                {
                    var deserialized = doc.RootNode;
                    LogYamlNode(deserialized);
                }
                // Debug.Log((deserialized as YamlMappingNode).Children);
                // Debug.Log(deserialized[0]);
                // deserialized.
                // foreach (var node in deserialized.Start)
                // {
                //     Debug.Log(node);
                // }
            }
        }
        protected void LogYamlNode(YamlNode node)
        {
            switch (node.NodeType)
            {
                case YamlNodeType.Mapping:
                    var children = (node as YamlMappingNode).Children;
                    Debug.Log("{");
                    Debug.Log(node.Tag);
                    Debug.Log(node.Anchor);
                    foreach (var e in children)
                    {
                        Debug.Log(e.Key + ":");
                        LogYamlNode(e.Value);
                    }
                    Debug.Log("}");
                    break;
                case YamlNodeType.Sequence:
                    var seqChildren = (node as YamlSequenceNode).Children;
                    Debug.Log("[");
                    foreach (var e in seqChildren)
                    {
                        LogYamlNode(e);
                    }
                    Debug.Log("]");
                    break;
                default:
                    Debug.Log(node);
                    break;
            }
        }
    }

}
