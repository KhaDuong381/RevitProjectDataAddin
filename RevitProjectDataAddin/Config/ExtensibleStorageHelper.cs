using System.Collections.Generic;
namespace RevitProjectDataAddin
{
    public static class ExtensibleStorageHelper
    {
        private static readonly Dictionary<string, ProjectData> _projectStorage = new Dictionary<string, ProjectData>();


        public static void SetProjectData(string projectName, ProjectData data)
        {
            _projectStorage[projectName] = data;
        }
    }
}

