using System.Threading.Tasks;
using ContentMigratorEditor;

abstract class AssetMigratorBase
{
  public ContentMigratorEditorWindow OwnerMigratorEditor;
  protected abstract string[] HandledExtensions
  {
    get;
  }

  public abstract Task Migrate(string assetsPath, string destinationPath);
}
