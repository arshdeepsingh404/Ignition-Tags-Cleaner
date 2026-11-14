using System.Text.Json.Nodes;

namespace TagCleaner
{
    public readonly record struct FilterResult(int RemovedFolders, int RemovedTags);

    /// <summary>
    /// Prunes a tag tree in place according to a <see cref="CleanerConfig"/>:
    /// - a folder in FoldersToKeepFull is kept with everything inside it;
    /// - a folder in FoldersToKeepFiltered is kept, its contents are filtered, and it is removed if left empty;
    /// - any other folder is kept only if something inside it is kept;
    /// - a tag is kept only inside a filtered folder, when its name is in TagsToKeep.
    /// </summary>
    public static class TagFilter
    {
        #region Rules
        private sealed class Rules
        {
            public HashSet<string> Full { get; }
            public HashSet<string> Filtered { get; }
            public HashSet<string> Tags { get; }

            public Rules(CleanerConfig config)
            {
                Full = new HashSet<string>(config.FoldersToKeepFull, StringComparer.OrdinalIgnoreCase);
                Filtered = new HashSet<string>(config.FoldersToKeepFiltered, StringComparer.OrdinalIgnoreCase);
                Tags = new HashSet<string>(config.TagsToKeep, StringComparer.OrdinalIgnoreCase);
            }
        }
        #endregion

        #region Public API
        /// <summary>Filters a whole export, taking the root folder's own name into account.</summary>
        public static FilterResult Apply(TagExport export, CleanerConfig config)
        {
            if (export.Tags == null) return default;

            var rules = new Rules(config);
            bool insideFilteredFolder = false;

            if (export.RootIsFolder)
            {
                string rootName = TagExport.GetName(export.Root) ?? "";
                if (rules.Full.Contains(rootName)) return default;
                insideFilteredFolder = rules.Filtered.Contains(rootName);
            }

            int removedFolders = 0, removedTags = 0;
            Apply(export.Tags, rules, insideFilteredFolder, ref removedFolders, ref removedTags);
            return new FilterResult(removedFolders, removedTags);
        }

        /// <summary>Filters a tag array that is not itself inside a listed folder.</summary>
        public static FilterResult Apply(JsonArray tags, CleanerConfig config)
        {
            int removedFolders = 0, removedTags = 0;
            Apply(tags, new Rules(config), false, ref removedFolders, ref removedTags);
            return new FilterResult(removedFolders, removedTags);
        }
        #endregion

        #region Recursive pruning
        // Returns true when at least one item in the array was kept.
        private static bool Apply(JsonArray tags, Rules rules, bool insideFilteredFolder, ref int removedFolders, ref int removedTags)
        {
            bool keptAny = false;

            // Walk backwards so items can be removed in place.
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                var item = tags[i];
                string name = TagExport.GetName(item) ?? "";
                bool isFolder = TagExport.IsFolder(item);
                var children = isFolder ? item!["tags"] as JsonArray : null;
                bool keep;

                if (!isFolder)
                {
                    keep = insideFilteredFolder && rules.Tags.Contains(name);
                }
                else if (rules.Full.Contains(name))
                {
                    keep = true;
                }
                else if (rules.Filtered.Contains(name))
                {
                    keep = true;
                    if (children != null)
                    {
                        Apply(children, rules, true, ref removedFolders, ref removedTags);
                        if (children.Count == 0) keep = false;
                    }
                }
                else
                {
                    keep = children != null && Apply(children, rules, insideFilteredFolder, ref removedFolders, ref removedTags);
                }

                if (keep)
                {
                    keptAny = true;
                    continue;
                }

                if (isFolder)
                {
                    // Whatever is still inside the folder goes with it.
                    var nested = children != null ? TagExport.Count(children) : (Folders: 0, Tags: 0);
                    removedFolders += 1 + nested.Folders;
                    removedTags += nested.Tags;
                }
                else
                {
                    removedTags++;
                }
                tags.RemoveAt(i);
            }

            return keptAny;
        }
        #endregion
    }
}
