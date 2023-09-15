using FlaxEditor;
using FlaxEditor.GUI;
using FlaxEngine;

namespace ContentMigratorEditor
{
    public class OpenMigratorButton : EditorPlugin
    {
        private ToolStripButton _button;

        /// <inheritdoc />
        public override void InitializeEditor()
        {
            base.InitializeEditor();

            _button = Editor.UI.ToolStrip.AddButton("Open Migrator");
            _button.Clicked += () => new ContentMigratorEditorWindow().Show();
        }

        /// <inheritdoc />
        public override void DeinitializeEditor()
        {
            if (_button != null)
            {
                _button.Dispose();
                _button = null;
            }

            base.DeinitializeEditor();
        }
    }
}
