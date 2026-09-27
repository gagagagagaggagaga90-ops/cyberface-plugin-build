using System.Reflection;
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
using System.Collections.Generic;

namespace FrostySdk.Managers.Entries
{
    public class EbxAssetEntry
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string DisplayName { get; set; }
    }
}

namespace FrostySdk.Managers
{
    using FrostySdk.Managers.Entries;

    public class AssetManager
    {
        public IEnumerable<EbxAssetEntry> EnumerateEbx(string path, bool includeModified, bool includeHidden, bool recursive, string filter)
        {
            return null;
        }

        public EbxAssetEntry GetEbxEntry(string name)
        {
            return null;
        }
    }
}
