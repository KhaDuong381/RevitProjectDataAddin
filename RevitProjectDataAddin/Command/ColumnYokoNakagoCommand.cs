using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnYokoNakagoCommand : IExternalCommand
    {
        private const string NotFound = "NOT FOUND";
        private const string SectionName = "柱頭";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Column Yoko Nakago Debug", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Column Yoko Nakago Debug", "No active Revit document was found.");
                return Result.Cancelled;
            }

            ProjectData projectData = StorageUtils.LoadProject(uiDoc.Document, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Column Yoko Nakago Debug", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            TaskDialog.Show("Column Yoko Nakago Debug", BuildDebugMessage(projectData));
            return Result.Succeeded;
        }

        private static string BuildDebugMessage(ProjectData projectData)
        {
            List<string> xNames = GetNamedList(projectData.Kihon, "NameX");
            List<string> yNames = GetNamedList(projectData.Kihon, "NameY");
            List<string> kaiNames = GetNamedList(projectData.Kihon, "NameKai");

            string kai = FirstOrDefault(kaiNames, "1F");
            string yName = FirstOrDefault(yNames, "Y1");
            string xName = FirstOrDefault(xNames, "X1");
            int xIndex = Math.Max(0, xNames.FindIndex(name => string.Equals(name, xName, StringComparison.OrdinalIgnoreCase)));

            object columnLayout = GetFirstItem(GetPropertyValue(GetPropertyValue(projectData, "Haichi"), "柱配置図"));
            string mapKey = $"{kai}::{yName}";
            object segment = GetSegment(columnLayout, mapKey, xIndex);
            string columnCode = FirstNonEmpty(GetStringProperty(segment, "柱の符号"), "C0");
            object columnData = GetColumnData(projectData, kai, columnCode);
            object columnSection = GetPropertyValue(columnData, "柱の配置");
            object sectionData = GetSectionData(columnSection, SectionName);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Project: {ProjectManager.SelectedProjectName}");
            sb.AppendLine();
            sb.AppendLine("Target:");
            sb.AppendLine($"Kai: {ValueOrNotFound(kai)}");
            sb.AppendLine($"Grid: {ValueOrNotFound(yName)}-{ValueOrNotFound(xName)}");
            sb.AppendLine($"ColumnCode: {ValueOrNotFound(columnCode)}");
            sb.AppendLine($"Section: {SectionName}");
            sb.AppendLine();
            sb.AppendLine("Axis:");
            sb.AppendLine($"NameX: {FormatList(xNames)}");
            sb.AppendLine($"NameY: {FormatList(yNames)}");
            sb.AppendLine($"NameKai: {FormatList(kaiNames)}");
            sb.AppendLine($"ListSpanX: {FormatNameSpanList(GetPropertyValue(projectData.Kihon, "ListSpanX"))}");
            sb.AppendLine($"ListSpanY: {FormatNameSpanList(GetPropertyValue(projectData.Kihon, "ListSpanY"))}");
            sb.AppendLine($"ListSpanKai: {FormatNameSpanList(GetPropertyValue(projectData.Kihon, "ListSpanKai"))}");
            sb.AppendLine();
            sb.AppendLine("Column layout:");
            sb.AppendLine($"MapKey: {mapKey}");
            sb.AppendLine($"Position: {FirstNonEmpty(GetStringProperty(segment, "位置表示"), $"{kai} {yName}-{xName}")}");
            sb.AppendLine($"ColumnCode: {ValueOrNotFound(columnCode)}");
            sb.AppendLine($"LeftOffset: {ValueOrNotFound(GetStringProperty(segment, "左側のズレ"))}");
            sb.AppendLine($"RightOffset: {ValueOrNotFound(GetStringProperty(segment, "右側のズレ"))}");
            sb.AppendLine($"TopOffset: {ValueOrNotFound(GetStringProperty(segment, "上側のズレ"))}");
            sb.AppendLine($"BottomOffset: {ValueOrNotFound(GetStringProperty(segment, "下側のズレ"))}");
            sb.AppendLine();
            sb.AppendLine("Column section 柱頭:");
            sb.AppendLine($"Width: {GetSectionProperty(columnSection, SectionName, "柱幅", "幅")}");
            sb.AppendLine($"Depth: {GetSectionProperty(columnSection, SectionName, "柱成", "成")}");
            sb.AppendLine($"MainDia: {GetSectionProperty(columnSection, SectionName, "主筋径")}");
            sb.AppendLine($"HoopDia: {GetSectionProperty(columnSection, SectionName, "HOOP径")}");
            sb.AppendLine($"HoopPitch: {GetSectionProperty(columnSection, SectionName, "ピッチ")}");
            sb.AppendLine($"CoverTop: {ValueOrNotFound(GetStringProperty(sectionData, "上"))}");
            sb.AppendLine($"CoverBottom: {ValueOrNotFound(GetStringProperty(sectionData, "下"))}");
            sb.AppendLine($"CoverLeft: {ValueOrNotFound(GetStringProperty(sectionData, "左"))}");
            sb.AppendLine($"CoverRight: {ValueOrNotFound(GetStringProperty(sectionData, "右"))}");
            sb.AppendLine($"YokoNakagoDia: {GetSectionProperty(columnSection, SectionName, "横向き中子径")}");
            sb.AppendLine($"YokoNakagoShape: {GetSectionProperty(columnSection, SectionName, "横向き中子形")}");
            sb.AppendLine($"YokoNakagoMaterial: {GetSectionProperty(columnSection, SectionName, "横向き中子材質")}");
            sb.AppendLine($"YokoNakagoPitch: {GetSectionProperty(columnSection, SectionName, "横向き中子ピッチ")}");
            sb.AppendLine($"YokoNakagoCount: {FirstNonEmpty(GetSectionProperty(columnSection, SectionName, "横向き中子本数", "柱頭横向き中子本数"), GetStringProperty(sectionData, "横向き中子本数"), NotFound)}");
            sb.AppendLine($"HookPosition: {ValueOrNotFound(GetStringProperty(sectionData, "フックの位置"))}");
            sb.AppendLine();
            sb.AppendLine("YokoNakago UI data:");
            sb.AppendLine($"CustomPositions: {FormatValue(GetPropertyValue(sectionData, "YokogaoNakagoCustomPositions"))}");
            sb.AppendLine($"Directions: {FormatValue(GetPropertyValue(sectionData, "YokogaoNakagoDirections"))}");
            sb.AppendLine();
            sb.AppendLine("Old-style generated data:");
            sb.AppendLine($"index_yokomuki: {FoundStatus(FindPropertyValue(projectData, "index_yokomuki"))}");
            sb.AppendLine($"offset_data1: {FoundStatus(FindPropertyValue(projectData, "offset_data1", "yokomuki nakago offset_data1"))}");
            sb.AppendLine($"hook_data1: {FoundStatus(FindPropertyValue(projectData, "hook_data1", "yokomuki nakago hook_data1"))}");
            sb.AppendLine();
            sb.AppendLine("Generated old-style preview:");
            AppendGeneratedOldStylePreview(sb, sb.ToString(), sectionData);
            sb.AppendLine();
            sb.AppendLine("Notes:");
            sb.AppendLine("No Rebar was created. ProjectData and Revit elements were not modified.");

            return sb.ToString();
        }

        private static void AppendGeneratedOldStylePreview(StringBuilder sb, string existingDebugText, object sectionData)
        {
            string widthText = GetDebugLineValue(existingDebugText, "Width");
            string depthText = GetDebugLineValue(existingDebugText, "Depth");
            string mainDiaText = GetDebugLineValue(existingDebugText, "MainDia");
            string hoopDiaText = GetDebugLineValue(existingDebugText, "HoopDia");
            string yokoDiaText = GetDebugLineValue(existingDebugText, "YokoNakagoDia");
            string shapeText = GetDebugLineValue(existingDebugText, "YokoNakagoShape");
            string countText = GetDebugLineValue(existingDebugText, "YokoNakagoCount");
            string coverTopText = GetDebugLineValue(existingDebugText, "CoverTop");
            string coverBottomText = GetDebugLineValue(existingDebugText, "CoverBottom");
            string coverLeftText = GetDebugLineValue(existingDebugText, "CoverLeft");
            string coverRightText = GetDebugLineValue(existingDebugText, "CoverRight");

            List<string> missing = new List<string>();
            double width = ParsePreviewDouble(widthText, "Width", missing);
            double depth = ParsePreviewDouble(depthText, "Depth", missing);
            double mainDia = ParsePreviewDouble(mainDiaText, "MainDia", missing);
            double hoopDia = ParsePreviewDouble(hoopDiaText, "HoopDia", missing);
            double yokoDia = ParsePreviewDouble(yokoDiaText, "YokoNakagoDia", missing);
            double coverTop = ParsePreviewDouble(coverTopText, "CoverTop", missing);
            double coverBottom = ParsePreviewDouble(coverBottomText, "CoverBottom", missing);
            double coverLeft = ParsePreviewDouble(coverLeftText, "CoverLeft", missing);
            double coverRight = ParsePreviewDouble(coverRightText, "CoverRight", missing);

            int count;
            if (!TryParsePositiveInt(countText, out count))
            {
                missing.Add($"YokoNakagoCount='{ValueOrNotFound(countText)}'");
                count = 0;
            }

            if (missing.Count > 0)
            {
                sb.AppendLine($"index_yokomuki_generated: NOT COMPUTED ({string.Join(", ", missing)})");
                sb.AppendLine($"offset_data1_generated: NOT COMPUTED ({string.Join(", ", missing)})");
                sb.AppendLine($"hook_data1_generated: NOT COMPUTED ({string.Join(", ", missing)})");
                return;
            }

            object customPositions = GetPropertyValue(sectionData, "YokogaoNakagoCustomPositions");
            object directions = GetPropertyValue(sectionData, "YokogaoNakagoDirections");

            // Preview-only offset calculation. This will be matched exactly to the old Python formula in the next step.
            double leftX = -width / 2.0 + coverLeft + hoopDia - yokoDia;
            double rightX = width / 2.0 - coverRight - hoopDia + yokoDia;
            double centerY = -(depth / 2.0 + GetActualBarDiameter(mainDia));
            double halfRange = Math.Min(
                depth / 2.0 - coverTop - hoopDia - yokoDia / 2.0,
                depth / 2.0 - coverBottom - hoopDia - yokoDia / 2.0) / 2.0;
            if (halfRange < 0.0)
            {
                halfRange = 0.0;
            }

            sb.AppendLine($"index_yokomuki_generated: {count}");
            sb.AppendLine();
            for (int i = 0; i < count; i++)
            {
                int positionIndex = GetIndexedIntValue(customPositions, i, i);
                bool isReversed = GetIndexedBoolValue(directions, i, false);
                string[] hookData = GetYokoNakagoHookData(shapeText, isReversed);
                double y = count == 1
                    ? centerY
                    : centerY - halfRange + 2.0 * halfRange * i / (count - 1);

                sb.AppendLine($"bar{i + 1}:");
                sb.AppendLine($"positionIndex: {positionIndex}");
                sb.AppendLine($"isReversed: {isReversed.ToString().ToLowerInvariant()}");
                sb.AppendLine($"offset_data{i + 1}_generated: [[{FormatDouble(leftX)}, {FormatDouble(y)}], [{FormatDouble(rightX)}, {FormatDouble(y)}]]");
                sb.AppendLine($"hook_data{i + 1}_generated: [{hookData[0]}, {hookData[1]}]");
                sb.AppendLine();
            }
        }

        private static object GetSegment(object columnLayout, string mapKey, int xIndex)
        {
            object mapObject = GetPropertyValue(columnLayout, "BeamSegmentsMap");
            if (!(mapObject is IDictionary map) || !map.Contains(mapKey))
            {
                return null;
            }

            return GetItemAt(map[mapKey], xIndex);
        }

        private static object GetColumnData(ProjectData projectData, string kai, string columnCode)
        {
            object columnLists = GetPropertyValue(GetPropertyValue(projectData, "リスト"), "柱リスト");
            foreach (object floorList in Enumerate(columnLists))
            {
                string floorName = GetStringProperty(floorList, "各階");
                if (!string.Equals(floorName, kai, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (object column in Enumerate(GetPropertyValue(floorList, "柱")))
                {
                    if (string.Equals(GetStringProperty(column, "Name"), columnCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return column;
                    }
                }
            }

            return null;
        }

        private static object GetSectionData(object columnSection, string sectionName)
        {
            object mapObject = GetPropertyValue(columnSection, "gridbotdata");
            if (mapObject is IDictionary map && map.Contains(sectionName))
            {
                return map[sectionName];
            }

            return null;
        }

        private static string GetSectionProperty(object columnSection, string sectionName, params string[] baseNames)
        {
            if (columnSection == null || baseNames == null)
            {
                return NotFound;
            }

            foreach (string baseName in baseNames.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                List<string> candidates = new List<string>();
                if (sectionName == "柱頭")
                {
                    candidates.Add(baseName + "1");
                    candidates.Add("柱頭" + baseName);
                }

                candidates.Add(baseName);

                foreach (string candidate in candidates)
                {
                    string value = GetStringProperty(columnSection, candidate);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return NotFound;
        }

        private static List<string> GetNamedList(object source, string propertyName)
        {
            return Enumerate(GetPropertyValue(source, propertyName))
                .Select(item => GetStringProperty(item, "Name"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property?.GetValue(source);
        }

        private static string GetStringProperty(object source, string propertyName)
        {
            object value = GetPropertyValue(source, propertyName);
            return value?.ToString();
        }

        private static object FindPropertyValue(object root, params string[] propertyNames)
        {
            if (root == null || propertyNames == null)
            {
                return null;
            }

            HashSet<object> visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            Queue<object> queue = new Queue<object>();
            queue.Enqueue(root);

            while (queue.Count > 0 && visited.Count < 1000)
            {
                object current = queue.Dequeue();
                if (current == null || IsSimpleValue(current) || !visited.Add(current))
                {
                    continue;
                }

                foreach (PropertyInfo property in current.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    object value = null;
                    try
                    {
                        value = property.GetValue(current);
                    }
                    catch
                    {
                        continue;
                    }

                    if (propertyNames.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        return value ?? new object();
                    }

                    if (value == null || IsSimpleValue(value))
                    {
                        continue;
                    }

                    if (value is IEnumerable && !(value is string))
                    {
                        foreach (object item in Enumerate(value))
                        {
                            if (item != null && !IsSimpleValue(item))
                            {
                                queue.Enqueue(item);
                            }
                        }
                    }
                    else
                    {
                        queue.Enqueue(value);
                    }
                }
            }

            return null;
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            if (value == null || value is string)
            {
                yield break;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    yield return entry.Value;
                }

                yield break;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    yield return item;
                }
            }
        }

        private static object GetFirstItem(object value)
        {
            return Enumerate(value).FirstOrDefault();
        }

        private static object GetItemAt(object value, int index)
        {
            if (value == null || index < 0)
            {
                return null;
            }

            int currentIndex = 0;
            foreach (object item in Enumerate(value))
            {
                if (currentIndex == index)
                {
                    return item;
                }

                currentIndex++;
            }

            return null;
        }

        private static string FormatNameSpanList(object value)
        {
            List<string> entries = Enumerate(value)
                .Select(item =>
                {
                    string name = GetStringProperty(item, "Name");
                    string span = GetStringProperty(item, "Span");
                    if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(span))
                    {
                        return null;
                    }

                    return string.IsNullOrWhiteSpace(span) ? name : $"{name}={span}";
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .ToList();

            return FormatList(entries);
        }

        private static string FormatList(IEnumerable<string> values)
        {
            List<string> list = values?.Where(value => !string.IsNullOrWhiteSpace(value)).ToList() ?? new List<string>();
            return list.Count == 0 ? NotFound : string.Join(", ", list);
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return NotFound;
            }

            if (value is IDictionary dictionary)
            {
                List<string> entries = new List<string>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries.Add($"{entry.Key}:{entry.Value}");
                }

                return entries.Count == 0 ? NotFound : string.Join(", ", entries);
            }

            if (!(value is string) && value is IEnumerable enumerable)
            {
                List<string> entries = new List<string>();
                foreach (object item in enumerable)
                {
                    entries.Add(item?.ToString() ?? string.Empty);
                }

                return entries.Count == 0 ? NotFound : string.Join(", ", entries);
            }

            return ValueOrNotFound(value.ToString());
        }

        private static string GetDebugLineValue(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(label))
            {
                return NotFound;
            }

            string prefix = label + ":";
            foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return line.Substring(prefix.Length).Trim();
                }
            }

            return NotFound;
        }

        private static double ParsePreviewDouble(string value, string label, List<string> missing)
        {
            double result;
            if (!TryParseDouble(value, out result))
            {
                missing.Add($"{label}='{ValueOrNotFound(value)}'");
                return 0.0;
            }

            return result;
        }

        private static bool TryParseDouble(string value, out double result)
        {
            result = 0.0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = ExtractNumberText(value);
            return double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out result)
                || double.TryParse(
                    normalized,
                    NumberStyles.Float | NumberStyles.AllowLeadingSign,
                    CultureInfo.CurrentCulture,
                    out result);
        }

        private static bool TryParsePositiveInt(string value, out int result)
        {
            result = 0;
            double doubleValue;
            if (!TryParseDouble(value, out doubleValue))
            {
                return false;
            }

            result = (int)Math.Round(doubleValue);
            return result > 0;
        }

        private static string ExtractNumberText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string text = value.Trim().Replace(",", string.Empty);
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsDigit(c) || c == '.' || c == '-' || c == '+')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static int GetIndexedIntValue(object dictionaryObject, int index, int fallback)
        {
            object value;
            if (!TryGetIndexedDictionaryValue(dictionaryObject, index, out value))
            {
                return fallback;
            }

            int result;
            if (value is int)
            {
                return (int)value;
            }

            return int.TryParse(value?.ToString(), out result) ? result : fallback;
        }

        private static bool GetIndexedBoolValue(object dictionaryObject, int index, bool fallback)
        {
            object value;
            if (!TryGetIndexedDictionaryValue(dictionaryObject, index, out value))
            {
                return fallback;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool result;
            return bool.TryParse(value?.ToString(), out result) ? result : fallback;
        }

        private static bool TryGetIndexedDictionaryValue(object dictionaryObject, int index, out object value)
        {
            value = null;
            IDictionary dictionary = dictionaryObject as IDictionary;
            if (dictionary == null)
            {
                return false;
            }

            if (dictionary.Contains(index))
            {
                value = dictionary[index];
                return true;
            }

            string stringIndex = index.ToString(CultureInfo.InvariantCulture);
            if (dictionary.Contains(stringIndex))
            {
                value = dictionary[stringIndex];
                return true;
            }

            return false;
        }

        private static string[] GetYokoNakagoHookData(string shapeText, bool isReversed)
        {
            int shape;
            if (!TryParsePositiveInt(shapeText, out shape))
            {
                return new[] { "NOT COMPUTED", "NOT COMPUTED" };
            }

            switch (shape)
            {
                case 1:
                    return new[] { "180", "180" };
                case 2:
                    return isReversed
                        ? new[] { "180", "90" }
                        : new[] { "90", "180" };
                case 3:
                    return new[] { "90", "90" };
                case 4:
                    return new[] { "135", "135" };
                case 5:
                    return isReversed
                        ? new[] { "135", "90" }
                        : new[] { "90", "135" };
                default:
                    return new[] { "NOT COMPUTED", "NOT COMPUTED" };
            }
        }

        private static double GetActualBarDiameter(double nominalDiameter)
        {
            int diameter = (int)Math.Round(nominalDiameter);
            switch (diameter)
            {
                case 10: return 11.0;
                case 13: return 14.0;
                case 16: return 18.0;
                case 19: return 21.0;
                case 22: return 25.0;
                case 25: return 28.0;
                case 29: return 33.0;
                case 32: return 36.0;
                case 35: return 40.0;
                case 38: return 43.0;
                default: return nominalDiameter;
            }
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        private static string ValueOrNotFound(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? NotFound : value;
        }

        private static string FirstOrDefault(List<string> values, string preferred)
        {
            return values.FirstOrDefault(value => string.Equals(value, preferred, StringComparison.OrdinalIgnoreCase))
                ?? values.FirstOrDefault()
                ?? preferred;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return NotFound;
            }

            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? NotFound;
        }

        private static string FoundStatus(object value)
        {
            return value == null ? "NOT FOUND" : "FOUND";
        }

        private static bool IsSimpleValue(object value)
        {
            Type type = value.GetType();
            return type.IsPrimitive
                || type.IsEnum
                || value is string
                || value is decimal
                || value is DateTime;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
