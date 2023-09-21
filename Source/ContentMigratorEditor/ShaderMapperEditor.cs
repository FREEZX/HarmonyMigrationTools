using System.Collections.Generic;
using System.Diagnostics;
using FlaxEditor;
using FlaxEditor.CustomEditors;
using FlaxEditor.CustomEditors.Elements;
using FlaxEditor.GUI;
using FlaxEditor.Scripting;
using FlaxEngine;
using FlaxEngine.GUI;

namespace ContentMigratorEditor
{
  public class ShaderMapperEditor : CustomEditorWindow
  {
    public ContentMigratorEditorWindow OwningMigrator;
    public List<GuidMaterial> MaterialMapList = new List<GuidMaterial>();
    public List<BuiltinMaterial> BuiltinMapList = new List<BuiltinMaterial>();
    private VerticalPanelElement builtinShadersVPanel;
    private VerticalPanelElement otherShadersVPanel;
    private List<AssetPicker> builtinAssetPickers = new List<AssetPicker>();
    private List<AssetPicker> otherAssetPickers = new List<AssetPicker>();


    public override void Initialize(LayoutElementsContainer layout)
    {
      var vPanel = layout.VerticalPanel();
      var builtinGroup = layout.Group("Builtin Shaders");
      builtinShadersVPanel = builtinGroup.VerticalPanel();
      var otherGroup = layout.Group("Other Shaders");
      otherShadersVPanel = otherGroup.VerticalPanel();
      DrawBuiltinMaterialMapper();
      OtherMaterialMapper();
    }

    private void AddMatClicked()
    {
      MaterialMapList.Add(new GuidMaterial());
      OtherMaterialMapper();
    }

    private void DrawBuiltinMaterialMapper()
    {
      otherAssetPickers.Clear();
      foreach (var child in otherShadersVPanel.Children)
      {
        child.Control.Dispose();
      }
      otherShadersVPanel.ClearLayout();
      otherShadersVPanel.Children.Clear();

      for (int i = 0; i < BuiltinMapList.Count; ++i)
      {
        var idx = i;
        var hPanel = builtinShadersVPanel.HorizontalPanel();
        hPanel.Label(BuiltinMapList[i].ShaderName);
        var picker = new AssetPicker(new ScriptType(typeof(Material)), new Float2(200, 2.5f))
        {
          Parent = hPanel.Panel,
          Height = 45,
        };
        picker.SelectedItem = Editor.Instance.ContentDatabase.FindAsset(BuiltinMapList[idx].Material.ID);
        picker.SelectedItemChanged += () =>
        {
          BuiltinMapList[idx].Material = picker.SelectedAsset as Material;
        };
        builtinAssetPickers.Add(picker);
      }
    }

    private void OtherMaterialMapper()
    {
      otherAssetPickers.Clear();
      foreach (var child in otherShadersVPanel.Children)
      {
        child.Control.Dispose();
      }
      otherShadersVPanel.ClearLayout();
      otherShadersVPanel.Children.Clear();

      for (int i = 0; i < MaterialMapList.Count; ++i)
      {
        var idx = i;
        var hPanel = otherShadersVPanel.HorizontalPanel();
        hPanel.Label("Name");
        var nameTextBox = hPanel.TextBox();
        nameTextBox.Text = MaterialMapList[idx].ShaderName;
        nameTextBox.Control.Size = new Float2(200, 10);
        nameTextBox.TextBox.TextChanged += () =>
        {
          MaterialMapList[idx].ShaderName = nameTextBox.Text;
        };
        hPanel.Space(20);
        hPanel.Label("Unity Guid");
        var guidTextBox = hPanel.TextBox();
        guidTextBox.Text = MaterialMapList[idx].Guid;
        guidTextBox.Control.Size = new Float2(300, 10);
        guidTextBox.TextBox.TextChanged += () =>
        {
          MaterialMapList[idx].Guid = guidTextBox.Text;
        };


        var picker = new AssetPicker(new ScriptType(typeof(Material)), new Float2(200, 2.5f))
        {
          Parent = hPanel.Panel,
          Height = 45,
        };
        if (MaterialMapList[idx].Material?.ID != null)
        {
          picker.SelectedItem = Editor.Instance.ContentDatabase.FindAsset(MaterialMapList[idx].Material.ID);
        }
        picker.SelectedItemChanged += () =>
        {
          MaterialMapList[idx].Material = picker.SelectedAsset as Material;
        };
        otherAssetPickers.Add(picker);

        var deleteMatBtn = hPanel.Button("Delete", Color.Red);
        deleteMatBtn.Button.Clicked += () =>
        {
          MaterialMapList.RemoveAt(idx);
          OtherMaterialMapper();
        };
      }
      var addMatBtn = otherShadersVPanel.Button("Add material (Unity shader)", Color.Blue);

      addMatBtn.Button.Clicked += AddMatClicked;
    }
  }
}
