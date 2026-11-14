using System.IO;
using System.Text.Json.Nodes;

namespace TagCleaner
{
    /// <summary>
    /// A parsed Ignition tag export. The root is either a folder object with a "tags" array,
    /// a wrapper object with a "tags" array, or a bare array of tags.
    /// </summary>
    public sealed class TagExport
    {
        #region Constants & properties
        public const string UnknownPlc = "Unknown PLC";

        // Ignition system folders that never name the PLC.
        private static readonly string[] SystemFolderNames = { "_Diagnostics_", "_System_", "Configuration" };

        public JsonNode Root { get; }

        /// <summary>The top-level tag array, or null when the export has none.</summary>
        public JsonArray? Tags { get; }

        public bool RootIsFolder { get; }

        /// <summary>
        /// The root folder's name, otherwise the first top-level folder that is not a system folder,
        /// otherwise the first item's name, otherwise <see cref="UnknownPlc"/>.
        /// </summary>
        public string PlcName { get; }
        #endregion

        #region Loading
        private TagExport(JsonNode root)
        {
            Root = root;
            string name = UnknownPlc;

            if (root is JsonObject obj)
            {
                RootIsFolder = IsFolder(obj);
                if (RootIsFolder) name = GetName(obj) ?? name;
                Tags = obj["tags"] as JsonArray;
            }
            else if (root is JsonArray arr)
            {
                Tags = arr;
            }

            if (name == UnknownPlc && Tags != null)
            {
                var firstValidFolder = Tags
                    .Where(IsFolder)
                    .FirstOrDefault(t => !SystemFolderNames.Contains(GetName(t) ?? "", StringComparer.OrdinalIgnoreCase));

                if (firstValidFolder != null) name = GetName(firstValidFolder) ?? name;
                else if (Tags.Count > 0) name = GetName(Tags[0]) ?? name;
            }

            PlcName = name;
        }

        /// <summary>Reads and parses an export file. Returns null when the file holds JSON null.</summary>
        public static TagExport? Load(string path) => Parse(File.ReadAllText(path));

        public static TagExport? Parse(string json)
        {
            JsonNode? root = JsonNode.Parse(json);
            return root == null ? null : new TagExport(root);
        }
        #endregion

        #region Counting
        /// <summary>Counts every folder and tag below the root.</summary>
        public (int Folders, int Tags) Count() => Tags == null ? (0, 0) : Count(Tags);

        public static (int Folders, int Tags) Count(JsonArray tags)
        {
            int folders = 0, tagCount = 0;
            foreach (var item in tags)
            {
                if (item == null) continue;
                if (IsFolder(item))
                {
                    folders++;
                    if (item["tags"] is JsonArray children)
                    {
                        var nested = Count(children);
                        folders += nested.Folders;
                        tagCount += nested.Tags;
                    }
                }
                else
                {
                    tagCount++;
                }
            }
            return (folders, tagCount);
        }
        #endregion

        #region Node helpers
        public static bool IsFolder(JsonNode? node) =>
            node is JsonObject obj && string.Equals(obj["tagType"]?.ToString(), "Folder", StringComparison.OrdinalIgnoreCase);

        public static string? GetName(JsonNode? node) => (node as JsonObject)?["name"]?.ToString();
        #endregion
    }
}
