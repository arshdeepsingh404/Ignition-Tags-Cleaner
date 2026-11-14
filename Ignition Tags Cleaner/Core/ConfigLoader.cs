using System.IO;
using ClosedXML.Excel;

namespace TagCleaner
{
    /// <summary>
    /// Reads the keep-rules from an Excel workbook. Throws if the workbook cannot be read.
    /// </summary>
    public static class ConfigLoader
    {
        #region Loading
        public static CleanerConfig Load(string excelPath)
        {
            var config = new CleanerConfig();
            if (!File.Exists(excelPath)) return config;

            // Read through a shared stream into memory so the workbook can stay open in Excel.
            using var fs = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            ms.Position = 0;

            using var workbook = new XLWorkbook(ms);
            foreach (var ws in workbook.Worksheets)
            {
                string sName = ws.Name.Trim();
                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                if (lastRow < 2) continue;

                bool isFullSheet = sName.Contains("Folders To Keep", StringComparison.OrdinalIgnoreCase) || sName.Contains("Keep Full", StringComparison.OrdinalIgnoreCase);
                bool isFilteredSheet = sName.Contains("Folders To Filter", StringComparison.OrdinalIgnoreCase) || sName.Contains("Keep Filtered", StringComparison.OrdinalIgnoreCase);
                bool isTagsSheet = sName.Contains("Tags To Keep", StringComparison.OrdinalIgnoreCase) || sName.Contains("Keep Tag", StringComparison.OrdinalIgnoreCase);
                bool isMasterRulesSheet = sName.Equals("Rules", StringComparison.OrdinalIgnoreCase);

                if (!isFullSheet && !isFilteredSheet && !isTagsSheet && !isMasterRulesSheet) continue;

                for (int r = 2; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    string c1 = GetCellText(row.Cell(1));
                    string c2 = GetCellText(row.Cell(2));
                    string c3 = GetCellText(row.Cell(3));
                    string c4 = GetCellText(row.Cell(4));

                    if (isMasterRulesSheet)
                    {
                        // Rules sheet: Rule Type | (group) | Name | Enabled
                        string ruleType = c1;
                        string itemName = c3;
                        if (!IsEnabled(c4) || string.IsNullOrWhiteSpace(itemName)) continue;

                        if (ruleType.Contains("Full", StringComparison.OrdinalIgnoreCase))
                            AddUnique(config.FoldersToKeepFull, itemName);
                        else if (ruleType.Contains("Filter", StringComparison.OrdinalIgnoreCase))
                            AddUnique(config.FoldersToKeepFiltered, itemName);
                        else if (ruleType.Contains("Tag", StringComparison.OrdinalIgnoreCase))
                            AddUnique(config.TagsToKeep, itemName);
                    }
                    else
                    {
                        // Per-rule sheets: (group) | Name | Enabled, or a single Name column
                        string itemName = !string.IsNullOrWhiteSpace(c2) ? c2 : c1;
                        string enabledStr = !string.IsNullOrWhiteSpace(c3) ? c3 : c4;

                        if (string.IsNullOrWhiteSpace(itemName) ||
                            itemName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                            itemName.Equals("Location", StringComparison.OrdinalIgnoreCase) ||
                            itemName.Equals("Category", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!IsEnabled(enabledStr)) continue;

                        if (isFullSheet) AddUnique(config.FoldersToKeepFull, itemName);
                        else if (isFilteredSheet) AddUnique(config.FoldersToKeepFiltered, itemName);
                        else AddUnique(config.TagsToKeep, itemName);
                    }
                }
            }

            return config;
        }
        #endregion

        #region Helpers
        private static bool IsEnabled(string value) =>
            !value.Equals("FALSE", StringComparison.OrdinalIgnoreCase) && value != "0";

        private static void AddUnique(List<string> list, string item)
        {
            if (!list.Contains(item, StringComparer.OrdinalIgnoreCase)) list.Add(item);
        }

        private static string GetCellText(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty()) return "";
            try
            {
                return cell.GetFormattedString()?.Trim() ?? "";
            }
            catch
            {
                try
                {
                    return cell.Value.ToString()?.Trim() ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }
        #endregion
    }
}
