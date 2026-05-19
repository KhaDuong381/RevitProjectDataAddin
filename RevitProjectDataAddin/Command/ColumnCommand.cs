using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnCommand : IExternalCommand
    {
        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double LocationToleranceFeet = 1e-4;
        private const double SizeToleranceMillimeters = 0.1;
        private static readonly Guid ColumnWidthGuid = new Guid("97588ae9-d518-4479-b90f-454db8294b24");
        private static readonly Guid ColumnDepthGuid = new Guid("9e46f218-ddd2-4f10-900b-75ff734a9ac6");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Column", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Column", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Column", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            if (projectData.Kihon == null)
            {
                TaskDialog.Show("Column", "ProjectData.Kihon is null.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(projectData.Kihon.NameX?.Select(axis => axis?.Name));
            if (xNames == null)
            {
                TaskDialog.Show("Column", "NameX data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> yNames = GetAxisNames(projectData.Kihon.NameY?.Select(axis => axis?.Name));
            if (yNames == null)
            {
                TaskDialog.Show("Column", "NameY data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> kaiNames = GetAxisNames(projectData.Kihon.NameKai?.Select(kai => kai?.Name));
            if (kaiNames == null || kaiNames.Count < 2)
            {
                TaskDialog.Show("Column", "At least two valid NameKai entries are required.");
                return Result.Cancelled;
            }

            柱配置図 columnLayout;
            string columnLayoutError;
            if (!TryGetColumnLayout(projectData, out columnLayout, out columnLayoutError))
            {
                TaskDialog.Show("Column", columnLayoutError);
                return Result.Cancelled;
            }

            Dictionary<string, Grid> gridsByName;
            string gridError;
            if (!TryCollectGridsByName(doc, out gridsByName, out gridError))
            {
                TaskDialog.Show("Column", gridError);
                return Result.Cancelled;
            }

            Dictionary<string, Level> levelsByName;
            string levelError;
            if (!TryCollectLevelsByName(doc, out levelsByName, out levelError))
            {
                TaskDialog.Show("Column", levelError);
                return Result.Cancelled;
            }

            ColumnTypeResolver typeResolver;
            string resolverError;
            if (!TryCreateColumnTypeResolver(doc, out typeResolver, out resolverError))
            {
                TaskDialog.Show("Column", resolverError);
                return Result.Cancelled;
            }

            List<string> warnings = new List<string>();
            bool normalizedColumnLayoutOffsets = NormalizeColumnLayoutOffsetsFromColumnList(projectData, warnings);
            if (normalizedColumnLayoutOffsets)
            {
                StorageUtils.SaveProject(doc, projectData);
                warnings.Add("Column layout offsets were normalized from 柱リスト before drawing.");
            }

            List<ColumnPlacementTarget> targets = BuildColumnSegmentTargets(
                projectData,
                kaiNames,
                yNames,
                xNames,
                columnLayout.BeamSegmentsMap,
                gridsByName,
                levelsByName,
                warnings);

            List<FamilyInstance> duplicateManagedColumnsToDelete;
            Dictionary<string, FamilyInstance> existingColumnsByKey = CollectExistingColumnsByKey(doc, warnings, out duplicateManagedColumnsToDelete);
            HashSet<string> matchedExistingKeys = new HashSet<string>(StringComparer.Ordinal);
            List<string> failedSyncs = new List<string>();
            List<string> failedDeletes = new List<string>();
            int createdCount = 0;
            int unchangedCount = 0;
            int updatedCount = 0;
            int deletedCount = 0;

            using (Transaction transaction = new Transaction(doc, "Create Columns"))
            {
                transaction.Start();

                foreach (ColumnPlacementTarget target in targets)
                {
                    string columnKey = target.StableKey;
                    FamilySymbol columnSymbol;
                    string symbolFailureReason;
                    if (!typeResolver.TryGetOrCreateSymbol(doc, target.WidthMm, target.DepthMm, target.ColumnCode, out columnSymbol, out symbolFailureReason))
                    {
                        failedSyncs.Add($"{target.Label} ({symbolFailureReason})");
                        continue;
                    }

                    if (!columnSymbol.IsActive)
                    {
                        columnSymbol.Activate();
                        doc.Regenerate();
                    }

                    FamilyInstance existingColumn;
                    if (!existingColumnsByKey.TryGetValue(columnKey, out existingColumn))
                    {
                        string createFailureReason;
                        if (TryCreateColumn(doc, columnSymbol, target, out createFailureReason))
                        {
                            createdCount++;
                        }
                        else
                        {
                            failedSyncs.Add($"{target.Label} ({createFailureReason})");
                        }

                        continue;
                    }

                    matchedExistingKeys.Add(columnKey);

                    if (IsColumnInSync(existingColumn, columnSymbol, target))
                    {
                        unchangedCount++;
                        continue;
                    }

                    string updateFailureReason;
                    if (TryReplaceColumn(doc, existingColumn, columnSymbol, target, out updateFailureReason))
                    {
                        updatedCount++;
                    }
                    else
                    {
                        failedSyncs.Add($"{target.Label} ({updateFailureReason})");
                    }
                }

                foreach (FamilyInstance duplicateColumn in duplicateManagedColumnsToDelete)
                {
                    if (duplicateColumn == null || duplicateColumn.Id == ElementId.InvalidElementId || doc.GetElement(duplicateColumn.Id) == null)
                    {
                        continue;
                    }

                    string deleteFailureReason;
                    if (TryDeleteColumn(doc, duplicateColumn, out deleteFailureReason))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        failedDeletes.Add($"{BuildExistingColumnLabel(duplicateColumn)} ({deleteFailureReason})");
                    }
                }

                foreach (KeyValuePair<string, FamilyInstance> orphanEntry in existingColumnsByKey)
                {
                    if (matchedExistingKeys.Contains(orphanEntry.Key))
                    {
                        continue;
                    }

                    string deleteFailureReason;
                    if (TryDeleteColumn(doc, orphanEntry.Value, out deleteFailureReason))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        failedDeletes.Add($"{BuildExistingColumnLabel(orphanEntry.Value)} ({deleteFailureReason})");
                    }
                }

                transaction.Commit();
            }

            TaskDialog.Show("Column", BuildResultMessage(
                createdCount,
                updatedCount,
                unchangedCount,
                deletedCount,
                targets.Count,
                typeResolver.TemplateSymbol,
                typeResolver.CreatedTypeNames,
                failedSyncs,
                failedDeletes,
                warnings));

            return Result.Succeeded;
        }

        private static List<string> GetAxisNames(IEnumerable<string> names)
        {
            if (names == null)
            {
                return null;
            }

            List<string> normalizedNames = names
                .Select(name => name?.Trim())
                .ToList();

            if (normalizedNames.Count == 0 || normalizedNames.Any(string.IsNullOrWhiteSpace))
            {
                return null;
            }

            return normalizedNames;
        }

        private static bool TryGetColumnLayout(ProjectData projectData, out 柱配置図 columnLayout, out string errorMessage)
        {
            columnLayout = null;
            errorMessage = null;

            if (projectData.Haichi == null)
            {
                errorMessage = "ProjectData.Haichi is null.";
                return false;
            }

            if (projectData.Haichi.柱配置図 == null || projectData.Haichi.柱配置図.Count == 0)
            {
                errorMessage = "ProjectData.Haichi.柱配置図 does not contain any layouts.";
                return false;
            }

            columnLayout = projectData.Haichi.柱配置図[0];
            if (columnLayout == null)
            {
                errorMessage = "ProjectData.Haichi.柱配置図[0] is null.";
                return false;
            }

            if (columnLayout.BeamSegmentsMap == null || columnLayout.BeamSegmentsMap.Count == 0)
            {
                errorMessage = "ProjectData.Haichi.柱配置図.BeamSegmentsMap is empty.";
                return false;
            }

            return true;
        }

        private static bool NormalizeColumnLayoutOffsetsFromColumnList(ProjectData projectData, List<string> warnings)
        {
            柱配置図 columnLayout = projectData?.Haichi?.柱配置図?.FirstOrDefault();
            Dictionary<string, ObservableCollection<柱セグメント>> beamSegmentsMap = columnLayout?.BeamSegmentsMap;
            if (beamSegmentsMap == null || beamSegmentsMap.Count == 0)
            {
                return false;
            }

            bool changed = false;
            foreach (KeyValuePair<string, ObservableCollection<柱セグメント>> entry in beamSegmentsMap)
            {
                string mapKey = entry.Key ?? string.Empty;
                ObservableCollection<柱セグメント> segments = entry.Value;
                if (segments == null)
                {
                    continue;
                }

                string[] keyParts = mapKey.Split(new[] { "::" }, StringSplitOptions.None);
                string kaiName = keyParts.Length > 0 ? keyParts[0]?.Trim() : null;
                if (string.IsNullOrWhiteSpace(kaiName))
                {
                    continue;
                }

                foreach (柱セグメント segment in segments)
                {
                    if (segment == null || string.IsNullOrWhiteSpace(segment.柱の符号))
                    {
                        continue;
                    }

                    string widthText;
                    string depthText;
                    double widthMm;
                    double depthMm;
                    if (!TryGetColumnSizeSource(projectData, kaiName, segment.柱の符号?.Trim(), out widthText, out depthText, out widthMm, out depthMm))
                    {
                        continue;
                    }

                    string columnCode = segment.柱の符号?.Trim();
                    if (string.Equals(segment.AutoOffsetSourceColumnCode, columnCode, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(segment.AutoOffsetSourceWidth, widthText, StringComparison.Ordinal)
                        && string.Equals(segment.AutoOffsetSourceDepth, depthText, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    segment.左側のズレ = FormatSize(widthMm / 2.0);
                    segment.右側のズレ = FormatSize(widthMm / 2.0);
                    segment.上側のズレ = FormatSize(depthMm / 2.0);
                    segment.下側のズレ = FormatSize(depthMm / 2.0);
                    segment.AutoOffsetSourceColumnCode = columnCode;
                    segment.AutoOffsetSourceWidth = widthText;
                    segment.AutoOffsetSourceDepth = depthText;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool TryCollectGridsByName(
            Document doc,
            out Dictionary<string, Grid> gridsByName,
            out string errorMessage)
        {
            gridsByName = new Dictionary<string, Grid>(StringComparer.OrdinalIgnoreCase);
            errorMessage = null;
            List<string> duplicateNames = new List<string>();

            foreach (Grid grid in new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>())
            {
                if (grid == null || string.IsNullOrWhiteSpace(grid.Name))
                {
                    continue;
                }

                if (gridsByName.ContainsKey(grid.Name))
                {
                    duplicateNames.Add(grid.Name);
                    continue;
                }

                gridsByName.Add(grid.Name, grid);
            }

            if (duplicateNames.Count > 0)
            {
                errorMessage = "Model contains duplicate grid names. Please resolve them before running Column: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
        }

        private static bool TryCollectLevelsByName(
            Document doc,
            out Dictionary<string, Level> levelsByName,
            out string errorMessage)
        {
            levelsByName = new Dictionary<string, Level>(StringComparer.OrdinalIgnoreCase);
            errorMessage = null;
            List<string> duplicateNames = new List<string>();

            foreach (Level level in new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>())
            {
                if (level == null || string.IsNullOrWhiteSpace(level.Name))
                {
                    continue;
                }

                if (levelsByName.ContainsKey(level.Name))
                {
                    duplicateNames.Add(level.Name);
                    continue;
                }

                levelsByName.Add(level.Name, level);
            }

            if (duplicateNames.Count > 0)
            {
                errorMessage = "Model contains duplicate level names. Please resolve them before running Column: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
        }

        private static bool TryCreateColumnTypeResolver(Document doc, out ColumnTypeResolver resolver, out string errorMessage)
        {
            resolver = null;
            errorMessage = null;

            List<FamilySymbol> symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .Cast<FamilySymbol>()
                .OrderBy(symbol => symbol.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (symbols.Count == 0)
            {
                errorMessage = "No structural column family symbol was found in the model.";
                return false;
            }

            FamilySymbol templateSymbol = symbols.FirstOrDefault(symbol =>
                HasWritableSizeParameters(symbol, out _));

            if (templateSymbol == null)
            {
                errorMessage = "No structural column family symbol exposes writable width/depth type parameters.";
                return false;
            }

            resolver = new ColumnTypeResolver(templateSymbol, symbols);
            return true;
        }

        private static List<ColumnPlacementTarget> BuildColumnSegmentTargets(
            ProjectData projectData,
            List<string> kaiNames,
            List<string> yNames,
            List<string> xNames,
            Dictionary<string, ObservableCollection<柱セグメント>> columnSegmentsMap,
            Dictionary<string, Grid> gridsByName,
            Dictionary<string, Level> levelsByName,
            List<string> warnings)
        {
            List<ColumnPlacementTarget> targets = new List<ColumnPlacementTarget>();

            for (int kaiIndex = 0; kaiIndex < kaiNames.Count - 1; kaiIndex++)
            {
                string kaiName = kaiNames[kaiIndex];
                string topKaiName = kaiNames[kaiIndex + 1];

                Level baseLevel;
                if (!levelsByName.TryGetValue(kaiName, out baseLevel))
                {
                    warnings.Add($"Level '{kaiName}' was not found in the model.");
                    continue;
                }

                Level topLevel;
                if (!levelsByName.TryGetValue(topKaiName, out topLevel))
                {
                    warnings.Add($"Top level '{topKaiName}' was not found in the model.");
                    continue;
                }

                foreach (string tsu in yNames)
                {
                    string mapKey = $"{kaiName}::{tsu}";
                    ObservableCollection<柱セグメント> segments;
                    if (!columnSegmentsMap.TryGetValue(mapKey, out segments) || segments == null)
                    {
                        warnings.Add($"Column layout key '{mapKey}' was not found.");
                        continue;
                    }

                    Grid yGrid;
                    if (!gridsByName.TryGetValue(tsu, out yGrid))
                    {
                        warnings.Add($"Grid '{tsu}' was not found in the model.");
                        continue;
                    }

                    int maxIndex = Math.Min(xNames.Count, segments.Count);
                    if (segments.Count < xNames.Count)
                    {
                        warnings.Add($"Column layout key '{mapKey}' has {segments.Count} segments but NameX has {xNames.Count}; extra X grids were skipped.");
                    }

                    for (int xIndex = 0; xIndex < maxIndex; xIndex++)
                    {
                        柱セグメント segment = segments[xIndex];
                        if (segment == null)
                        {
                            continue;
                        }

                        string columnCode = segment.柱の符号?.Trim();
                        if (string.IsNullOrWhiteSpace(columnCode))
                        {
                            continue;
                        }

                        double widthMm;
                        double depthMm;
                        string sizeError;
                        if (!TryGetColumnSizeFromColumnList(projectData, kaiName, columnCode, out widthMm, out depthMm, out sizeError))
                        {
                            warnings.Add($"{kaiName} {tsu}-{xNames[xIndex]} {columnCode}: {sizeError}");
                            continue;
                        }

                        string xName = xNames[xIndex];
                        Grid xGrid;
                        if (!gridsByName.TryGetValue(xName, out xGrid))
                        {
                            warnings.Add($"Grid '{xName}' was not found in the model.");
                            continue;
                        }

                        XYZ intersectionPoint;
                        if (!TryGetGridIntersectionPoint(xGrid, yGrid, out intersectionPoint))
                        {
                            warnings.Add($"Could not find the intersection of grids '{xName}' and '{tsu}'.");
                            continue;
                        }

                        double leftOffsetMm;
                        double rightOffsetMm;
                        double topOffsetMm;
                        double bottomOffsetMm;
                        double offsetXmm;
                        double offsetYmm;
                        string offsetError;
                        if (!TryGetColumnPlacementOffsetsFromSegment(
                            segment,
                            out leftOffsetMm,
                            out rightOffsetMm,
                            out topOffsetMm,
                            out bottomOffsetMm,
                            out offsetXmm,
                            out offsetYmm,
                            out offsetError))
                        {
                            warnings.Add($"{kaiName} {tsu}-{xName} {columnCode}: {offsetError}");
                            continue;
                        }

                        XYZ adjustedPoint = new XYZ(
                            intersectionPoint.X + offsetXmm * FeetPerMillimeter,
                            intersectionPoint.Y + offsetYmm * FeetPerMillimeter,
                            intersectionPoint.Z);

                        ColumnPlacementTarget segmentTarget = CreateColumnSegmentTarget(
                            kaiName,
                            tsu,
                            xName,
                            columnCode,
                            widthMm,
                            depthMm,
                            adjustedPoint,
                            baseLevel,
                            topLevel,
                            leftOffsetMm,
                            rightOffsetMm,
                            topOffsetMm,
                            bottomOffsetMm,
                            offsetXmm,
                            offsetYmm);

                        targets.Add(segmentTarget);
                    }
                }
            }

            return targets;
        }

        private static ColumnPlacementTarget CreateColumnSegmentTarget(
            string kaiName,
            string tsu,
            string xName,
            string columnCode,
            double widthMm,
            double depthMm,
            XYZ intersectionPoint,
            Level baseLevel,
            Level topLevel,
            double leftOffsetMm,
            double rightOffsetMm,
            double topOffsetMm,
            double bottomOffsetMm,
            double placementOffsetXmm,
            double placementOffsetYmm)
        {
            // Each kai creates one independent structural column segment:
            // 1F -> 2F uses the 1F segment size, 2F -> 3F uses the 2F segment size, etc.
            return new ColumnPlacementTarget(
                $"{kaiName}->{topLevel.Name} {tsu}-{xName}",
                columnCode,
                widthMm,
                depthMm,
                new XYZ(intersectionPoint.X, intersectionPoint.Y, baseLevel.Elevation),
                baseLevel,
                topLevel,
                leftOffsetMm,
                rightOffsetMm,
                topOffsetMm,
                bottomOffsetMm,
                placementOffsetXmm,
                placementOffsetYmm);
        }

        private static bool TryGetSegmentSize(柱セグメント segment, out double widthMm, out double depthMm, out string errorMessage)
        {
            widthMm = 0.0;
            depthMm = 0.0;
            errorMessage = null;

            double leftMm;
            if (!TryParseMillimeters(segment.左側のズレ, out leftMm))
            {
                errorMessage = $"left offset '{segment.左側のズレ}' is invalid.";
                return false;
            }

            double rightMm;
            if (!TryParseMillimeters(segment.右側のズレ, out rightMm))
            {
                errorMessage = $"right offset '{segment.右側のズレ}' is invalid.";
                return false;
            }

            double topMm;
            if (!TryParseMillimeters(segment.上側のズレ, out topMm))
            {
                errorMessage = $"top offset '{segment.上側のズレ}' is invalid.";
                return false;
            }

            double bottomMm;
            if (!TryParseMillimeters(segment.下側のズレ, out bottomMm))
            {
                errorMessage = $"bottom offset '{segment.下側のズレ}' is invalid.";
                return false;
            }

            widthMm = leftMm + rightMm;
            depthMm = topMm + bottomMm;

            if (widthMm <= 0.0 || depthMm <= 0.0)
            {
                errorMessage = $"computed size {FormatSize(widthMm)}x{FormatSize(depthMm)} is not positive.";
                return false;
            }

            return true;
        }

        private static bool TryGetColumnPlacementOffsetsFromSegment(
            柱セグメント segment,
            out double leftMm,
            out double rightMm,
            out double topMm,
            out double bottomMm,
            out double offsetXmm,
            out double offsetYmm,
            out string errorMessage)
        {
            leftMm = 0.0;
            rightMm = 0.0;
            topMm = 0.0;
            bottomMm = 0.0;
            offsetXmm = 0.0;
            offsetYmm = 0.0;
            errorMessage = null;

            if (!TryParseMillimeters(segment?.左側のズレ, out leftMm)
                || !TryParseMillimeters(segment?.右側のズレ, out rightMm)
                || !TryParseMillimeters(segment?.上側のズレ, out topMm)
                || !TryParseMillimeters(segment?.下側のズレ, out bottomMm))
            {
                errorMessage = "column placement offset is invalid.";
                return false;
            }

            offsetXmm = (rightMm - leftMm) / 2.0;
            offsetYmm = (bottomMm - topMm) / 2.0;
            return true;
        }

        private static bool TryGetColumnSizeFromColumnList(
            ProjectData projectData,
            string kaiName,
            string columnCode,
            out double widthMm,
            out double depthMm,
            out string errorMessage)
        {
            widthMm = 0.0;
            depthMm = 0.0;
            errorMessage = null;

            柱リスト floorColumnList = projectData?.リスト?.柱リスト?
                .FirstOrDefault(list => string.Equals(list?.各階?.Trim(), kaiName, StringComparison.OrdinalIgnoreCase));
            柱 columnData = floorColumnList?.柱?
                .FirstOrDefault(column => string.Equals(column?.Name?.Trim(), columnCode, StringComparison.OrdinalIgnoreCase));
            Z柱の配置 columnLayout = columnData?.柱の配置;
            if (columnLayout == null)
            {
                errorMessage = "column size was not found in 柱リスト.";
                return false;
            }

            // Prefer 柱頭 dimensions for the current screen linkage; fall back to 柱脚 dimensions only when top data is blank.
            string widthText = string.IsNullOrWhiteSpace(columnLayout.柱幅1) ? columnLayout.柱幅 : columnLayout.柱幅1;
            string depthText = string.IsNullOrWhiteSpace(columnLayout.柱成1) ? columnLayout.柱成 : columnLayout.柱成1;

            if (!TryParseMillimeters(widthText, out widthMm) || !TryParseMillimeters(depthText, out depthMm) || widthMm <= 0.0 || depthMm <= 0.0)
            {
                errorMessage = "column size was not found in 柱リスト.";
                widthMm = 0.0;
                depthMm = 0.0;
                return false;
            }

            return true;
        }

        private static bool TryGetColumnSizeSource(
            ProjectData projectData,
            string kaiName,
            string columnCode,
            out string widthText,
            out string depthText,
            out double widthMm,
            out double depthMm)
        {
            widthText = null;
            depthText = null;
            widthMm = 0.0;
            depthMm = 0.0;

            柱リスト floorColumnList = projectData?.リスト?.柱リスト?
                .FirstOrDefault(list => string.Equals(list?.各階?.Trim(), kaiName, StringComparison.OrdinalIgnoreCase));
            柱 columnData = floorColumnList?.柱?
                .FirstOrDefault(column => string.Equals(column?.Name?.Trim(), columnCode, StringComparison.OrdinalIgnoreCase));
            Z柱の配置 columnLayout = columnData?.柱の配置;
            if (columnLayout == null)
            {
                return false;
            }

            widthText = string.IsNullOrWhiteSpace(columnLayout.柱幅1) ? columnLayout.柱幅 : columnLayout.柱幅1;
            depthText = string.IsNullOrWhiteSpace(columnLayout.柱成1) ? columnLayout.柱成 : columnLayout.柱成1;

            return TryParseMillimeters(widthText, out widthMm)
                && TryParseMillimeters(depthText, out depthMm)
                && widthMm > 0.0
                && depthMm > 0.0;
        }

        private static bool TryParseMillimeters(string rawValue, out double valueMm)
        {
            valueMm = 0.0;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            return double.TryParse(rawValue.Trim(), out valueMm);
        }

        private static bool TryGetGridIntersectionPoint(Grid xGrid, Grid yGrid, out XYZ point)
        {
            point = null;

            if (xGrid?.Curve == null || yGrid?.Curve == null)
            {
                return false;
            }

            IntersectionResultArray results;
            SetComparisonResult comparison = xGrid.Curve.Intersect(yGrid.Curve, out results);
            if ((comparison == SetComparisonResult.Overlap
                || comparison == SetComparisonResult.Subset
                || comparison == SetComparisonResult.Superset)
                && results != null
                && results.Size > 0)
            {
                point = results.get_Item(0).XYZPoint;
                return point != null;
            }

            return false;
        }

        private static Dictionary<string, FamilyInstance> CollectExistingColumnsByKey(
            Document doc,
            List<string> warnings,
            out List<FamilyInstance> duplicateManagedColumnsToDelete)
        {
            Dictionary<string, FamilyInstance> columnsByKey = new Dictionary<string, FamilyInstance>(StringComparer.Ordinal);
            duplicateManagedColumnsToDelete = new List<FamilyInstance>();

            foreach (FamilyInstance column in new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>())
            {
                string columnKey;
                if (!TryGetColumnKey(column, out columnKey))
                {
                    continue;
                }

                if (columnsByKey.ContainsKey(columnKey))
                {
                    warnings.Add($"Multiple structural columns share key '{columnKey}'. Duplicate managed columns will be deleted.");
                    duplicateManagedColumnsToDelete.Add(column);
                    continue;
                }

                columnsByKey.Add(columnKey, column);
            }

            return columnsByKey;
        }

        private static bool TryGetColumnKey(FamilyInstance column, out string columnKey)
        {
            columnKey = null;

            if (!IsManagedStructuralColumn(column))
            {
                return false;
            }

            return TryExtractStableKeyFromComment(GetColumnComment(column), out columnKey);
        }

        private static bool TryDeleteColumn(Document doc, FamilyInstance column, out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                doc.Delete(column.Id);
                subTransaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                if (subTransaction.GetStatus() == TransactionStatus.Started)
                {
                    subTransaction.RollBack();
                }

                failureReason = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
                return false;
            }
        }

        private static bool TryBuildExistingColumnTarget(FamilyInstance column, out ColumnPlacementTarget target)
        {
            target = null;

            LocationPoint locationPoint = column?.Location as LocationPoint;
            if (locationPoint == null)
            {
                return false;
            }

            Level baseLevel = column.Document.GetElement(GetElementIdParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)) as Level;
            Level topLevel = column.Document.GetElement(GetElementIdParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)) as Level;
            if (baseLevel == null || topLevel == null)
            {
                return false;
            }

            FamilySymbol symbol = column.Symbol;
            if (symbol == null)
            {
                return false;
            }

            double widthMm;
            double depthMm;
            if (!TryGetColumnSize(symbol, out widthMm, out depthMm))
            {
                return false;
            }

            string comment = GetColumnComment(column);
            string columnCode = ExtractColumnCodeFromComment(comment);
            string label = string.IsNullOrWhiteSpace(comment) ? $"{baseLevel.Name} existing-column" : comment;

            target = new ColumnPlacementTarget(
                label,
                columnCode,
                widthMm,
                depthMm,
                locationPoint.Point,
                baseLevel,
                topLevel,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0);

            return true;
        }

        private static bool TryCreateColumn(
            Document doc,
            FamilySymbol columnSymbol,
            ColumnPlacementTarget target,
            out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                FamilyInstance column = doc.Create.NewFamilyInstance(
                    target.Point,
                    columnSymbol,
                    target.BaseLevel,
                    StructuralType.Column);

                string parameterFailure;
                if (!ApplyColumnParameters(column, target, out parameterFailure))
                {
                    throw new InvalidOperationException(parameterFailure);
                }

                Parameter commentsParameter = column.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParameter != null && !commentsParameter.IsReadOnly)
                {
                    commentsParameter.Set(BuildColumnComment(target));
                }

                subTransaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                if (subTransaction.GetStatus() == TransactionStatus.Started)
                {
                    subTransaction.RollBack();
                }

                failureReason = ex.Message;
                if (string.IsNullOrWhiteSpace(failureReason))
                {
                    failureReason = ex.GetType().Name;
                }

                return false;
            }
        }

        private static bool TryReplaceColumn(
            Document doc,
            FamilyInstance existingColumn,
            FamilySymbol columnSymbol,
            ColumnPlacementTarget target,
            out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                doc.Delete(existingColumn.Id);

                FamilyInstance recreatedColumn = doc.Create.NewFamilyInstance(
                    target.Point,
                    columnSymbol,
                    target.BaseLevel,
                    StructuralType.Column);

                string parameterFailure;
                if (!ApplyColumnParameters(recreatedColumn, target, out parameterFailure))
                {
                    throw new InvalidOperationException(parameterFailure);
                }

                Parameter commentsParameter = recreatedColumn.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (commentsParameter != null && !commentsParameter.IsReadOnly)
                {
                    commentsParameter.Set(BuildColumnComment(target));
                }

                subTransaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                if (subTransaction.GetStatus() == TransactionStatus.Started)
                {
                    subTransaction.RollBack();
                }

                failureReason = ex.Message;
                if (string.IsNullOrWhiteSpace(failureReason))
                {
                    failureReason = ex.GetType().Name;
                }

                return false;
            }
        }

        private static bool IsColumnInSync(FamilyInstance existingColumn, FamilySymbol desiredSymbol, ColumnPlacementTarget target)
        {
            if (existingColumn == null || desiredSymbol == null)
            {
                return false;
            }

            if (existingColumn.Symbol == null || existingColumn.Symbol.Id != desiredSymbol.Id)
            {
                return false;
            }

            string existingComment = GetColumnComment(existingColumn);
            return string.Equals(existingComment, BuildColumnComment(target), StringComparison.Ordinal)
                && HasDesiredColumnLevelTreatment(existingColumn, target)
                && HasDesiredColumnLocation(existingColumn, target);
        }

        private static bool HasDesiredColumnLocation(FamilyInstance column, ColumnPlacementTarget target)
        {
            LocationPoint locationPoint = column?.Location as LocationPoint;
            if (locationPoint == null || target == null || target.Point == null)
            {
                return false;
            }

            XYZ currentPoint = locationPoint.Point;
            XYZ targetPoint = target.Point;
            return Math.Abs(currentPoint.X - targetPoint.X) <= LocationToleranceFeet
                && Math.Abs(currentPoint.Y - targetPoint.Y) <= LocationToleranceFeet
                && Math.Abs(currentPoint.Z - targetPoint.Z) <= LocationToleranceFeet;
        }

        private static string BuildExistingColumnLabel(FamilyInstance column)
        {
            string comment = GetColumnComment(column);
            if (!string.IsNullOrWhiteSpace(comment))
            {
                return comment;
            }

            string typeLabel = column?.Symbol == null
                ? "(unknown type)"
                : $"{column.Symbol.FamilyName} : {column.Symbol.Name}";

            LocationPoint locationPoint = column?.Location as LocationPoint;
            if (locationPoint == null)
            {
                return typeLabel;
            }

            XYZ point = locationPoint.Point;
            return $"{typeLabel} [{RoundToLocationTolerance(point.X)},{RoundToLocationTolerance(point.Y)}]";
        }

        private static string BuildColumnComment(ColumnPlacementTarget target)
        {
            return $"{target.Label} | {target.ColumnCode} | {FormatSize(target.WidthMm)}x{FormatSize(target.DepthMm)} | Offset L={FormatSize(target.LeftOffsetMm)} R={FormatSize(target.RightOffsetMm)} T={FormatSize(target.TopOffsetMm)} B={FormatSize(target.BottomOffsetMm)} | MoveX={FormatSize(target.PlacementOffsetXmm)} MoveY={FormatSize(target.PlacementOffsetYmm)}";
        }

        private static string GetColumnComment(FamilyInstance column)
        {
            Parameter commentsParameter = column?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return commentsParameter?.StorageType == StorageType.String
                ? commentsParameter.AsString() ?? string.Empty
                : string.Empty;
        }

        private static string ExtractColumnCodeFromComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return string.Empty;
            }

            string[] parts = comment.Split(new[] { " | " }, StringSplitOptions.None);
            return parts.Length >= 2 ? parts[1].Trim() : string.Empty;
        }

        private static bool TryExtractStableKeyFromComment(string comment, out string stableKey)
        {
            stableKey = null;
            if (string.IsNullOrWhiteSpace(comment))
            {
                return false;
            }

            string[] parts = comment.Split(new[] { " | " }, StringSplitOptions.None);
            if (parts.Length < 2)
            {
                return false;
            }

            string label = parts[0].Trim();
            string columnCode = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(columnCode))
            {
                return false;
            }

            stableKey = $"{label} | {columnCode}";
            return true;
        }

        private static bool IsManagedStructuralColumn(FamilyInstance column)
        {
            if (column == null || column.StructuralType != StructuralType.Column)
            {
                return false;
            }

            string stableKey;
            return TryExtractStableKeyFromComment(GetColumnComment(column), out stableKey);
        }

        private static bool TrySetElementIdParameter(
            Element element,
            ElementId value,
            out string failureReason,
            params BuiltInParameter[] parameterIds)
        {
            failureReason = null;
            Parameter parameter = FindWritableParameter(element, parameterIds);
            if (parameter == null)
            {
                failureReason = $"Could not find a writable level parameter ({string.Join(", ", parameterIds.Select(id => id.ToString()))}).";
                return false;
            }

            parameter.Set(value);
            return true;
        }

        private static bool TrySetDoubleParameter(
            Element element,
            double value,
            out string failureReason,
            params BuiltInParameter[] parameterIds)
        {
            failureReason = null;
            Parameter parameter = FindWritableParameter(element, parameterIds);
            if (parameter == null)
            {
                failureReason = $"Could not find a writable offset parameter ({string.Join(", ", parameterIds.Select(id => id.ToString()))}).";
                return false;
            }

            parameter.Set(value);
            return true;
        }

        private static bool TrySetIntegerParameter(
            Element element,
            int value,
            out string failureReason,
            params BuiltInParameter[] parameterIds)
        {
            failureReason = null;
            Parameter parameter = FindWritableParameter(element, parameterIds);
            if (parameter == null)
            {
                failureReason = $"Could not find a writable integer parameter ({string.Join(", ", parameterIds.Select(id => id.ToString()))}).";
                return false;
            }

            parameter.Set(value);
            return true;
        }

        private static void TrySetOptionalDoubleParameter(Element element, double value, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element?.get_Parameter(parameterId);
                if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Double)
                {
                    parameter.Set(value);
                }
            }
        }

        private static void TrySetOptionalIntegerParameter(Element element, int value, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element?.get_Parameter(parameterId);
                if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Integer)
                {
                    parameter.Set(value);
                }
            }
        }

        private static Parameter FindWritableParameter(Element element, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element.get_Parameter(parameterId);
                if (parameter != null && !parameter.IsReadOnly)
                {
                    return parameter;
                }
            }

            return null;
        }

        private static ElementId GetElementIdParameterValue(Element element, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element.get_Parameter(parameterId);
                if (parameter != null && parameter.StorageType == StorageType.ElementId)
                {
                    ElementId value = parameter.AsElementId();
                    if (value != null && value != ElementId.InvalidElementId)
                    {
                        return value;
                    }
                }
            }

            return ElementId.InvalidElementId;
        }

        private static bool ApplyColumnParameters(FamilyInstance column, ColumnPlacementTarget target, out string failureReason)
        {
            failureReason = null;

            if (!TrySetElementIdParameter(column, target.BaseLevel.Id, out failureReason,
                    BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                    BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)
                || !TrySetElementIdParameter(column, target.TopLevel.Id, out failureReason,
                    BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
                    BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)
                || !TrySetDoubleParameter(column, 0.0, out failureReason,
                    BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,
                    BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM)
                || !TrySetDoubleParameter(column, 0.0, out failureReason,
                    BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM,
                    BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM))
            {
                return false;
            }

            TrySetOptionalDoubleParameter(column, 0.0,
                BuiltInParameter.COLUMN_BASE_ATTACHMENT_OFFSET_PARAM,
                BuiltInParameter.COLUMN_TOP_ATTACHMENT_OFFSET_PARAM);
            TrySetOptionalIntegerParameter(column, 0,
                BuiltInParameter.COLUMN_BASE_ATTACHED_PARAM,
                BuiltInParameter.COLUMN_TOP_ATTACHED_PARAM);

            return true;
        }

        private static bool HasDesiredColumnLevelTreatment(FamilyInstance column, ColumnPlacementTarget target)
        {
            if (column == null)
            {
                return false;
            }

            ElementId baseLevelId = GetElementIdParameterValue(column,
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM);
            ElementId topLevelId = GetElementIdParameterValue(column,
                BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM);

            if (baseLevelId != target.BaseLevel.Id || topLevelId != target.TopLevel.Id)
            {
                return false;
            }

            return GetIntegerParameterValue(column, BuiltInParameter.COLUMN_BASE_ATTACHED_PARAM) == 0
                && GetIntegerParameterValue(column, BuiltInParameter.COLUMN_TOP_ATTACHED_PARAM) == 0
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM)) <= LocationToleranceFeet
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM)) <= LocationToleranceFeet
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM)) <= LocationToleranceFeet
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM)) <= LocationToleranceFeet
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.COLUMN_BASE_ATTACHMENT_OFFSET_PARAM)) <= LocationToleranceFeet
                && Math.Abs(GetDoubleParameterValue(column, BuiltInParameter.COLUMN_TOP_ATTACHMENT_OFFSET_PARAM)) <= LocationToleranceFeet;
        }

        private static int GetIntegerParameterValue(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            return parameter != null && parameter.StorageType == StorageType.Integer
                ? parameter.AsInteger()
                : 0;
        }

        private static double GetDoubleParameterValue(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            return parameter != null && parameter.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0.0;
        }

        private static bool HasWritableSizeParameters(FamilySymbol symbol, out ColumnSizeParameters sizeParameters)
        {
            return TryGetSizeParameters(symbol, true, out sizeParameters);
        }

        private static bool TryGetColumnSize(FamilySymbol symbol, out double widthMm, out double depthMm)
        {
            widthMm = 0.0;
            depthMm = 0.0;

            ColumnSizeParameters sizeParameters;
            if (!TryGetSizeParameters(symbol, false, out sizeParameters))
            {
                return false;
            }

            widthMm = sizeParameters.Width.AsDouble() / FeetPerMillimeter;
            depthMm = sizeParameters.Depth.AsDouble() / FeetPerMillimeter;
            return true;
        }

        private static bool TrySetColumnSize(FamilySymbol symbol, double widthMm, double depthMm, out string failureReason)
        {
            failureReason = null;

            ColumnSizeParameters sizeParameters;
            if (!TryGetSizeParameters(symbol, true, out sizeParameters))
            {
                failureReason = $"Type '{symbol.FamilyName} : {symbol.Name}' does not expose writable width/depth parameters.";
                return false;
            }

            sizeParameters.Width.Set(ToFeet(widthMm));
            sizeParameters.Depth.Set(ToFeet(depthMm));
            return true;
        }

        private static bool TryGetSizeParameters(FamilySymbol symbol, bool requireWritable, out ColumnSizeParameters sizeParameters)
        {
            sizeParameters = null;

            Parameter widthParameter = FindSizeParameter(
                symbol,
                ColumnWidthGuid,
                requireWritable,
                new[] { "Width", "width", "B", "b" },
                new[] { "width" });

            Parameter depthParameter = FindSizeParameter(
                symbol,
                ColumnDepthGuid,
                requireWritable,
                new[] { "Depth", "depth", "H", "h" },
                new[] { "depth" });

            if (widthParameter == null || depthParameter == null)
            {
                return false;
            }

            sizeParameters = new ColumnSizeParameters(widthParameter, depthParameter);
            return true;
        }

        private static Parameter FindSizeParameter(
            Element element,
            Guid sharedParameterGuid,
            bool requireWritable,
            IEnumerable<string> exactNames,
            IEnumerable<string> containsTokens)
        {
            Parameter sharedParameter = element.get_Parameter(sharedParameterGuid);
            if (IsUsableSizeParameter(sharedParameter, requireWritable))
            {
                return sharedParameter;
            }

            List<Parameter> parameters = element.Parameters
                .Cast<Parameter>()
                .Where(parameter => IsUsableSizeParameter(parameter, requireWritable))
                .ToList();

            Parameter exactMatch = parameters.FirstOrDefault(parameter =>
            {
                string name = parameter.Definition?.Name;
                return exactNames.Any(candidate => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
            });

            if (exactMatch != null)
            {
                return exactMatch;
            }

            return parameters.FirstOrDefault(parameter =>
            {
                string name = parameter.Definition?.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return false;
                }

                return containsTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
            });
        }

        private static bool IsUsableSizeParameter(Parameter parameter, bool requireWritable)
        {
            return parameter != null
                && parameter.StorageType == StorageType.Double
                && (!requireWritable || !parameter.IsReadOnly);
        }

        private static string BuildColumnKey(XYZ point, ElementId baseLevelId, ElementId topLevelId)
        {
            XYZ normalizedPoint = new XYZ(point.X, point.Y, 0.0);
            return $"{BuildPointKey(normalizedPoint)}|{baseLevelId.IntegerValue}|{topLevelId.IntegerValue}";
        }

        private static string BuildPointKey(XYZ point)
        {
            return $"{RoundToLocationTolerance(point.X)}|{RoundToLocationTolerance(point.Y)}";
        }

        private static string BuildSizeKey(double widthMm, double depthMm)
        {
            return $"{RoundToSizeTolerance(widthMm)}|{RoundToSizeTolerance(depthMm)}";
        }

        private static double RoundToLocationTolerance(double value)
        {
            return Math.Round(value / LocationToleranceFeet) * LocationToleranceFeet;
        }

        private static double RoundToSizeTolerance(double value)
        {
            return Math.Round(value / SizeToleranceMillimeters) * SizeToleranceMillimeters;
        }

        private static double ToFeet(double valueMm)
        {
            return valueMm * FeetPerMillimeter;
        }

        private static string FormatSize(double valueMm)
        {
            double rounded = RoundToSizeTolerance(valueMm);
            return Math.Abs(rounded - Math.Round(rounded)) < SizeToleranceMillimeters
                ? Math.Round(rounded).ToString("0")
                : rounded.ToString("0.0");
        }

        private static string BuildTypeName(string columnCode, double widthMm, double depthMm)
        {
            string normalizedCode = string.IsNullOrWhiteSpace(columnCode) ? "COL" : columnCode.Trim();
            return $"{normalizedCode} ({FormatSize(widthMm)}x{FormatSize(depthMm)})";
        }

        private static string BuildResultMessage(
            int createdCount,
            int updatedCount,
            int unchangedCount,
            int deletedCount,
            int targetCount,
            FamilySymbol templateSymbol,
            IReadOnlyCollection<string> createdTypeNames,
            List<string> failedSyncs,
            List<string> failedDeletes,
            List<string> warnings)
        {
            string templateLabel = templateSymbol == null
                ? "(none)"
                : $"{templateSymbol.FamilyName} : {templateSymbol.Name}";

            string resultMessage =
                $"Targets from data: {targetCount}" + Environment.NewLine +
                $"Created: {createdCount}" + Environment.NewLine +
                $"Updated: {updatedCount}" + Environment.NewLine +
                $"Unchanged: {unchangedCount}" + Environment.NewLine +
                $"Deleted orphan columns: {deletedCount}" + Environment.NewLine +
                $"Template symbol: {templateLabel}";

            if (createdTypeNames != null && createdTypeNames.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Created types ({createdTypeNames.Count}): {string.Join(", ", createdTypeNames)}";
            }

            if (failedSyncs.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Failed syncs ({failedSyncs.Count}): {string.Join(", ", failedSyncs)}";
            }

            if (failedDeletes != null && failedDeletes.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Failed deletes ({failedDeletes.Count}): {string.Join(", ", failedDeletes)}";
            }

            if (warnings.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Warnings ({warnings.Count}): {string.Join(", ", warnings)}";
            }

            return resultMessage;
        }

        private sealed class ColumnPlacementTarget
        {
            public ColumnPlacementTarget(
                string label,
                string columnCode,
                double widthMm,
                double depthMm,
                XYZ point,
                Level baseLevel,
                Level topLevel,
                double leftOffsetMm,
                double rightOffsetMm,
                double topOffsetMm,
                double bottomOffsetMm,
                double placementOffsetXmm,
                double placementOffsetYmm)
            {
                Label = label;
                ColumnCode = columnCode;
                StableKey = $"{label} | {columnCode}";
                WidthMm = widthMm;
                DepthMm = depthMm;
                Point = point;
                BaseLevel = baseLevel;
                TopLevel = topLevel;
                LeftOffsetMm = leftOffsetMm;
                RightOffsetMm = rightOffsetMm;
                TopOffsetMm = topOffsetMm;
                BottomOffsetMm = bottomOffsetMm;
                PlacementOffsetXmm = placementOffsetXmm;
                PlacementOffsetYmm = placementOffsetYmm;
            }

            public string Label { get; }

            public string ColumnCode { get; }

            public string StableKey { get; }

            public double WidthMm { get; }

            public double DepthMm { get; }

            public XYZ Point { get; }

            public Level BaseLevel { get; }

            public Level TopLevel { get; }

            public double LeftOffsetMm { get; }

            public double RightOffsetMm { get; }

            public double TopOffsetMm { get; }

            public double BottomOffsetMm { get; }

            public double PlacementOffsetXmm { get; }

            public double PlacementOffsetYmm { get; }
        }

        private sealed class ColumnSizeParameters
        {
            public ColumnSizeParameters(Parameter width, Parameter depth)
            {
                Width = width;
                Depth = depth;
            }

            public Parameter Width { get; }

            public Parameter Depth { get; }
        }

        private sealed class ColumnTypeResolver
        {
            private readonly Family templateFamily;
            private readonly List<FamilySymbol> allSymbols;
            private readonly Dictionary<string, FamilySymbol> sizeCache;

            public ColumnTypeResolver(FamilySymbol templateSymbol, List<FamilySymbol> symbols)
            {
                TemplateSymbol = templateSymbol;
                templateFamily = templateSymbol.Family;
                allSymbols = symbols;
                sizeCache = new Dictionary<string, FamilySymbol>(StringComparer.Ordinal);
                CreatedTypeNames = new List<string>();

                foreach (FamilySymbol symbol in symbols)
                {
                    double widthMm;
                    double depthMm;
                    if (!TryGetColumnSize(symbol, out widthMm, out depthMm))
                    {
                        continue;
                    }

                    string key = BuildSizeKey(widthMm, depthMm);
                    if (!sizeCache.ContainsKey(key))
                    {
                        sizeCache.Add(key, symbol);
                    }
                }
            }

            public FamilySymbol TemplateSymbol { get; }

            public List<string> CreatedTypeNames { get; }

            public bool TryGetOrCreateSymbol(
                Document doc,
                double widthMm,
                double depthMm,
                string columnCode,
                out FamilySymbol symbol,
                out string failureReason)
            {
                failureReason = null;
                string sizeKey = BuildSizeKey(widthMm, depthMm);

                if (sizeCache.TryGetValue(sizeKey, out symbol) && symbol != null)
                {
                    return true;
                }

                symbol = FindMatchingSymbol(widthMm, depthMm);
                if (symbol != null)
                {
                    sizeCache[sizeKey] = symbol;
                    return true;
                }

                string desiredName = BuildTypeName(columnCode, widthMm, depthMm);
                string uniqueName = GetUniqueTypeName(desiredName);

                FamilySymbol duplicatedSymbol = TemplateSymbol.Duplicate(uniqueName) as FamilySymbol;
                if (duplicatedSymbol == null)
                {
                    failureReason = $"Could not duplicate template symbol '{TemplateSymbol.Name}' for size {FormatSize(widthMm)}x{FormatSize(depthMm)}.";
                    return false;
                }

                string sizeSetFailure;
                if (!TrySetColumnSize(duplicatedSymbol, widthMm, depthMm, out sizeSetFailure))
                {
                    doc.Delete(duplicatedSymbol.Id);
                    failureReason = sizeSetFailure;
                    return false;
                }

                allSymbols.Add(duplicatedSymbol);
                sizeCache[sizeKey] = duplicatedSymbol;
                CreatedTypeNames.Add($"{duplicatedSymbol.FamilyName} : {duplicatedSymbol.Name}");
                symbol = duplicatedSymbol;
                return true;
            }

            private FamilySymbol FindMatchingSymbol(double widthMm, double depthMm)
            {
                IEnumerable<FamilySymbol> candidates = allSymbols;
                if (templateFamily != null)
                {
                    candidates = allSymbols
                        .OrderByDescending(symbol => symbol.Family != null && symbol.Family.Id == templateFamily.Id);
                }

                return candidates.FirstOrDefault(symbol =>
                {
                    double existingWidthMm;
                    double existingDepthMm;
                    return TryGetColumnSize(symbol, out existingWidthMm, out existingDepthMm)
                        && Math.Abs(existingWidthMm - widthMm) <= SizeToleranceMillimeters
                        && Math.Abs(existingDepthMm - depthMm) <= SizeToleranceMillimeters;
                });
            }

            private string GetUniqueTypeName(string desiredName)
            {
                HashSet<string> existingNames = new HashSet<string>(
                    allSymbols.Select(symbol => symbol.Name),
                    StringComparer.OrdinalIgnoreCase);

                if (!existingNames.Contains(desiredName))
                {
                    return desiredName;
                }

                int suffix = 2;
                while (existingNames.Contains($"{desiredName} ({suffix})"))
                {
                    suffix++;
                }

                return $"{desiredName} ({suffix})";
            }
        }
    }
}
