using System.Threading.Tasks;

abstract class AssetMigratorBase
{
  protected abstract string[] HandledExtensions
  {
    get;
  }

  public abstract Task Migrate(string assetsPath, string destinationPath);
}
