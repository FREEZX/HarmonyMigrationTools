using System;
using System.IO;
using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEngine;
using YamlDotNet.Serialization;


namespace ContentMigratorEditor
{

    public class ContentMigratorEditorWindow : CustomEditorWindow
    {
        private TextBoxElement textbox;

        public override void Initialize(LayoutElementsContainer layout)
        {
            layout.Label("Unity project path");
            textbox = layout.TextBox();
            var browseBtn = layout.Button("Browse");
            browseBtn.Button.Clicked += BrowseClicked;
            layout.Space(20);
            var button = layout.Button("Migrate content", Color.Blue);
            button.Button.Clicked += MigrateClicked;
        }

        private void BrowseClicked()
        {
            string path;
            FileSystem.ShowBrowseFolderDialog(this.Window.RootWindow.Window, null, "Select unity project directory", out path);
            textbox.Text = path;
        }

        private bool IsValidProject(string projectPath)
        {
            return Directory.Exists(projectPath) && Directory.Exists(Path.Join(projectPath, "Assets"));
        }

        private void MigrateClicked()
        {
            var path = textbox.Text;
            if (!IsValidProject(path))
            {
                MessageBox.Show("Invalid project!");
                return;
            }

            var prefabFiles = Directory.GetFiles(path, "*.prefab");
            foreach (var prefabFile in prefabFiles)
            {
                var prefabContents = File.ReadAllText(prefabFile);
                var deserializer = new DeserializerBuilder().Build();
                var deserialized = deserializer.Deserialize(prefabContents);
                Debug.Log(deserialized);
            }
        }
    }
}
