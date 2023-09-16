abstract class AssetMigratorBase
{
  protected abstract string[] HandledExtensions
  {
    get;
  }

  public abstract void Migrate(string assetsPath, string destinationPath);
}
