using System.DirectoryServices.AccountManagement;

namespace vibe.DirectoryServices.Providers.Adsi
{
    public interface IAdsiDirectoryProvider : IDirectoryProvider<string>
    {
        Principal ResolvePrincipal(IDirectoryEntity<string> entity);
    }
}