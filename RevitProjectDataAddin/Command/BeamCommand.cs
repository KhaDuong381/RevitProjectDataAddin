using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class BeamCommand : IExternalCommand
    {
        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double LocationToleranceFeet = 1e-4;
        private const double OffsetToleranceFeet = 1e-6;
        private const double SizeToleranceMillimeters = 0.1;
        private static readonly Guid BeamWidthGuid = new Guid("97588ae9-d518-4479-b90f-454db8294b24");
        private static readonly Guid BeamDepthGuid = new Guid("9e46f218-ddd2-4f10-900b-75ff734a9ac6");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Beam", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Beam", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Beam", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            if (projectData.Kihon == null)
            {
                TaskDialog.Show("Beam", "ProjectData.Kihon is null.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(projectData.Kihon.NameX?.Select(axis => axis?.Name));
            if (xNames == null)
            {
                TaskDialog.Show("Beam", "NameX data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> yNames = GetAxisNames(projectData.Kihon.NameY?.Select(axis => axis?.Name));
            if (yNames == null)
            {
                TaskDialog.Show("Beam", "NameY data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> kaiNames = GetAxisNames(projectData.Kihon.NameKai?.Select(kai => kai?.Name));
            if (kaiNames == null || kaiNames.Count == 0)
            {
                TaskDialog.Show("Beam", "At least one valid NameKai entry is required.");
                return Result.Cancelled;
            }

            柱配置図 columnLayout;
            string columnLayoutError;
            if (!TryGetColumnLayout(projectData, out columnLayout, out columnLayoutError))
            {
                TaskDialog.Show("Beam", columnLayoutError);
                return Result.Cancelled;
            }

            梁配置図 beamLayout;
            string beamLayoutError;
            if (!TryGetBeamLayout(projectData, out beamLayout, out beamLayoutError))
            {
                TaskDialog.Show("Beam", beamLayoutError);
                return Result.Cancelled;
            }

            Dictionary<string, Level> levelsByName;
            string levelError;
            if (!TryCollectLevelsByName(doc, out levelsByName, out levelError))
            {
                TaskDialog.Show("Beam", levelError);
                return Result.Cancelled;
            }

            Dictionary<string, double> xCoordinatesByName;
            string xCoordinateError;
            if (!TryBuildAxisCoordinates(
                xNames,
                projectData.Kihon.ListSpanX?.Select(span => span?.Span),
                "X",
                out xCoordinatesByName,
                out xCoordinateError))
            {
                TaskDialog.Show("Beam", xCoordinateError);
                return Result.Cancelled;
            }

            Dictionary<string, double> yCoordinatesByName;
            string yCoordinateError;
            if (!TryBuildAxisCoordinates(
                yNames,
                projectData.Kihon.ListSpanY?.Select(span => span?.Span),
                "Y",
                out yCoordinatesByName,
                out yCoordinateError))
            {
                TaskDialog.Show("Beam", yCoordinateError);
                return Result.Cancelled;
            }

            List<string> warnings = new List<string>();

            BeamTypeResolver typeResolver;
            string resolverError;
            if (!TryCreateBeamTypeResolver(doc, out typeResolver, out resolverError))
            {
                TaskDialog.Show("Beam", resolverError);
                return Result.Cancelled;
            }

            List<BeamPlacementTarget> targets = BuildBeamTargets(
                kaiNames,
                xNames,
                yNames,
                beamLayout.BeamSegmentsMap,
                columnLayout.BeamSegmentsMap,
                xCoordinatesByName,
                yCoordinatesByName,
                levelsByName,
                warnings);

            Dictionary<string, FamilyInstance> existingBeamsByKey = CollectExistingBeamsByKey(doc, warnings);
            HashSet<string> matchedExistingKeys = new HashSet<string>(StringComparer.Ordinal);
            List<string> failedSyncs = new List<string>();
            List<string> failedDeletes = new List<string>();
            int createdCount = 0;
            int updatedCount = 0;
            int unchangedCount = 0;
            int deletedCount = 0;

            using (Transaction transaction = new Transaction(doc, "Sync Beams"))
            {
                transaction.Start();

                foreach (BeamPlacementTarget target in targets)
                {
                    FamilySymbol beamSymbol;
                    string symbolFailureReason;
                    if (!typeResolver.TryGetOrCreateSymbol(doc, target.DepthMm, out beamSymbol, out symbolFailureReason))
                    {
                        failedSyncs.Add($"{target.Label} ({symbolFailureReason})");
                        continue;
                    }

                    if (!beamSymbol.IsActive)
                    {
                        beamSymbol.Activate();
                        doc.Regenerate();
                    }

                    string beamKey = BuildBeamKey(target.Line, target.Level.Id);
                    FamilyInstance existingBeam;
                    if (!existingBeamsByKey.TryGetValue(beamKey, out existingBeam))
                    {
                        string createFailureReason;
                        if (TryCreateBeam(doc, beamSymbol, target, out createFailureReason))
                        {
                            createdCount++;
                        }
                        else
                        {
                            failedSyncs.Add($"{target.Label} ({createFailureReason})");
                        }

                        continue;
                    }

                    matchedExistingKeys.Add(beamKey);

                    if (IsBeamInSync(existingBeam, beamSymbol, target))
                    {
                        unchangedCount++;
                        continue;
                    }

                    string updateFailureReason;
                    if (TryReplaceBeam(doc, existingBeam, beamSymbol, target, out updateFailureReason))
                    {
                        updatedCount++;
                    }
                    else
                    {
                        failedSyncs.Add($"{target.Label} ({updateFailureReason})");
                    }
                }

                foreach (KeyValuePair<string, FamilyInstance> orphanEntry in existingBeamsByKey)
                {
                    if (matchedExistingKeys.Contains(orphanEntry.Key))
                    {
                        continue;
                    }

                    string deleteFailureReason;
                    if (TryDeleteBeam(doc, orphanEntry.Value, out deleteFailureReason))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        failedDeletes.Add($"{BuildExistingBeamLabel(orphanEntry.Value)} ({deleteFailureReason})");
                    }
                }

                transaction.Commit();
            }

            TaskDialog.Show("Beam", BuildResultMessage(
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

        private static bool TryBuildAxisCoordinates(
            IReadOnlyList<string> axisNames,
            IEnumerable<string> spans,
            string axisLabel,
            out Dictionary<string, double> coordinatesByName,
            out string errorMessage)
        {
            coordinatesByName = null;
            errorMessage = null;

            if (axisNames == null || axisNames.Count == 0)
            {
                errorMessage = $"{axisLabel} axis names are missing.";
                return false;
            }

            coordinatesByName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                [axisNames[0]] = 0.0
            };

            if (axisNames.Count == 1)
            {
                return true;
            }

            List<string> spanValues = spans?.ToList() ?? new List<string>();
            if (spanValues.Count < axisNames.Count - 1)
            {
                errorMessage = $"{axisLabel} spans are incomplete. Expected {axisNames.Count - 1} values but found {spanValues.Count}.";
                return false;
            }

            double currentCoordinateMm = 0.0;
            for (int i = 1; i < axisNames.Count; i++)
            {
                double spanMm;
                if (!TryParseMillimeters(spanValues[i - 1], out spanMm))
                {
                    errorMessage = $"{axisLabel} span '{axisNames[i - 1]}-{axisNames[i]}' has invalid value '{spanValues[i - 1]}'.";
                    return false;
                }

                currentCoordinateMm += spanMm;
                coordinatesByName[axisNames[i]] = currentCoordinateMm;
            }

            return true;
        }

        private static bool TryGetBeamLayout(ProjectData projectData, out 梁配置図 beamLayout, out string errorMessage)
        {
            beamLayout = null;
            errorMessage = null;

            if (projectData.Haichi == null)
            {
                errorMessage = "ProjectData.Haichi is null.";
                return false;
            }

            if (projectData.Haichi.梁配置図 == null || projectData.Haichi.梁配置図.Count == 0)
            {
                errorMessage = "ProjectData.Haichi.梁配置図 does not contain any layouts.";
                return false;
            }

            beamLayout = projectData.Haichi.梁配置図[0];
            if (beamLayout == null)
            {
                errorMessage = "ProjectData.Haichi.梁配置図[0] is null.";
                return false;
            }

            if (beamLayout.BeamSegmentsMap == null || beamLayout.BeamSegmentsMap.Count == 0)
            {
                errorMessage = "ProjectData.Haichi.梁配置図.BeamSegmentsMap is empty.";
                return false;
            }

            return true;
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
                errorMessage = "Model contains duplicate grid names. Please resolve them before running Beam: "
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
                errorMessage = "Model contains duplicate level names. Please resolve them before running Beam: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
        }

        private static bool TryCreateBeamTypeResolver(Document doc, out BeamTypeResolver resolver, out string errorMessage)
        {
            resolver = null;
            errorMessage = null;

            List<FamilySymbol> symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .Cast<FamilySymbol>()
                .OrderBy(symbol => symbol.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (symbols.Count == 0)
            {
                errorMessage = "No structural framing family symbol was found in the model.";
                return false;
            }

            FamilySymbol templateSymbol = symbols.FirstOrDefault(symbol => HasWritableBeamDepthParameter(symbol));
            if (templateSymbol == null)
            {
                errorMessage = "No structural framing family symbol exposes a writable beam depth type parameter.";
                return false;
            }

            resolver = new BeamTypeResolver(templateSymbol, symbols);
            return true;
        }

        private static List<BeamPlacementTarget> BuildBeamTargets(
            List<string> kaiNames,
            List<string> xNames,
            List<string> yNames,
            Dictionary<string, ObservableCollection<梁セグメント>> beamSegmentsMap,
            Dictionary<string, ObservableCollection<柱セグメント>> columnSegmentsMap,
            IReadOnlyDictionary<string, double> xCoordinatesByName,
            IReadOnlyDictionary<string, double> yCoordinatesByName,
            Dictionary<string, Level> levelsByName,
            List<string> warnings)
        {
            List<BeamPlacementTarget> targets = new List<BeamPlacementTarget>();
            HashSet<string> xNameSet = new HashSet<string>(xNames, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> xNameIndices = xNames
                .Select((name, index) => new { name, index })
                .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
            List<string> tsuNames = xNames.Concat(yNames).ToList();

            for (int kaiIndex = 0; kaiIndex < kaiNames.Count; kaiIndex++)
            {
                string kaiName = kaiNames[kaiIndex];
                string columnKaiName = GetColumnKaiNameForBeam(kaiNames, kaiIndex);

                Level level;
                if (!levelsByName.TryGetValue(kaiName, out level))
                {
                    warnings.Add($"Level '{kaiName}' was not found in the model.");
                    continue;
                }

                foreach (string tsu in tsuNames)
                {
                    string mapKey = $"{kaiName}::{tsu}";
                    ObservableCollection<梁セグメント> segments;
                    if (!beamSegmentsMap.TryGetValue(mapKey, out segments) || segments == null)
                    {
                        warnings.Add($"Beam layout key '{mapKey}' was not found.");
                        continue;
                    }

                    bool tsuIsX = xNameSet.Contains(tsu);
                    int expectedSegmentCount = tsuIsX ? Math.Max(0, yNames.Count - 1) : Math.Max(0, xNames.Count - 1);
                    if (segments.Count < expectedSegmentCount)
                    {
                        warnings.Add($"Beam layout key '{mapKey}' has {segments.Count} segments but expected {expectedSegmentCount}.");
                    }

                    foreach (梁セグメント segment in segments)
                    {
                        if (segment == null)
                        {
                            continue;
                        }

                        string beamCode = segment.梁の符号?.Trim();
                        if (string.IsNullOrWhiteSpace(beamCode))
                        {
                            continue;
                        }

                        string leftName = segment.左側?.Trim();
                        string rightName = segment.右側?.Trim();
                        if (string.IsNullOrWhiteSpace(leftName) || string.IsNullOrWhiteSpace(rightName))
                        {
                            warnings.Add($"Beam segment '{segment.タイトル}' in '{mapKey}' is missing 左側/右側.");
                            continue;
                        }

                        XYZ startPoint;
                        XYZ endPoint;
                        string coordinateError;
                        if (!TryGetBeamEndpointsFromData(
                            tsu,
                            leftName,
                            rightName,
                            tsuIsX,
                            xCoordinatesByName,
                            yCoordinatesByName,
                            out startPoint,
                            out endPoint,
                            out coordinateError))
                        {
                            warnings.Add($"Skipped beam segment '{segment.梁の符号}' in '{mapKey}' because {coordinateError}");
                            continue;
                        }

                        if (startPoint.IsAlmostEqualTo(endPoint))
                        {
                            warnings.Add($"Beam segment '{segment.タイトル}' in '{mapKey}' has zero length.");
                            continue;
                        }

                        double topMm;
                        double bottomMm;
                        double depthMm;
                        string depthError;
                        if (!TryGetBeamDepthMm(segment, out topMm, out bottomMm, out depthMm, out depthError))
                        {
                            warnings.Add($"Skipped beam segment '{segment.タイトル}' in '{mapKey}' because {depthError}");
                            continue;
                        }

                        double beamOffsetFeet = 0.0;
                        double beamOffsetMm;
                        if (TryParseMillimeters(segment.梁の段差, out beamOffsetMm))
                        {
                            beamOffsetFeet = beamOffsetMm * FeetPerMillimeter;
                        }
                        else if (!string.IsNullOrWhiteSpace(segment.梁の段差))
                        {
                            warnings.Add($"Beam segment '{segment.タイトル}' in '{mapKey}' has invalid 梁の段差 '{segment.梁の段差}'. Using 0.");
                        }

                        Line beamLine;
                        string lineError;
                        if (!TryBuildBeamLine(
                            kaiName,
                            columnKaiName,
                            tsu,
                            leftName,
                            rightName,
                            tsuIsX,
                            topMm,
                            bottomMm,
                            startPoint,
                            endPoint,
                            columnSegmentsMap,
                            xNameIndices,
                            level,
                            out beamLine,
                            out lineError))
                        {
                            warnings.Add($"Skipped beam segment '{segment.タイトル}' in '{mapKey}' because {lineError}");
                            continue;
                        }

                        string label = string.IsNullOrWhiteSpace(segment.タイトル)
                            ? $"{kaiName} {tsu} {leftName}-{rightName}"
                            : segment.タイトル;

                        targets.Add(new BeamPlacementTarget(label, beamCode, beamLine, level, beamOffsetFeet, depthMm));
                    }
                }
            }

            return targets;
        }

        private static string GetColumnKaiNameForBeam(IReadOnlyList<string> kaiNames, int beamKaiIndex)
        {
            if (kaiNames == null || kaiNames.Count == 0)
            {
                return string.Empty;
            }

            if (beamKaiIndex <= 0)
            {
                return kaiNames[0];
            }

            return kaiNames[Math.Min(beamKaiIndex - 1, kaiNames.Count - 1)];
        }

        private static bool TryGetBeamEndpointsFromData(
            string tsu,
            string leftName,
            string rightName,
            bool tsuIsX,
            IReadOnlyDictionary<string, double> xCoordinatesByName,
            IReadOnlyDictionary<string, double> yCoordinatesByName,
            out XYZ startPoint,
            out XYZ endPoint,
            out string errorMessage)
        {
            startPoint = null;
            endPoint = null;
            errorMessage = null;

            double tsuCoordinateMm;
            double leftCoordinateMm;
            double rightCoordinateMm;

            if (tsuIsX)
            {
                if (!xCoordinatesByName.TryGetValue(tsu, out tsuCoordinateMm))
                {
                    errorMessage = $"X grid '{tsu}' was not found in Kihon.NameX/ListSpanX.";
                    return false;
                }

                if (!yCoordinatesByName.TryGetValue(leftName, out leftCoordinateMm))
                {
                    errorMessage = $"Y grid '{leftName}' was not found in Kihon.NameY/ListSpanY.";
                    return false;
                }

                if (!yCoordinatesByName.TryGetValue(rightName, out rightCoordinateMm))
                {
                    errorMessage = $"Y grid '{rightName}' was not found in Kihon.NameY/ListSpanY.";
                    return false;
                }

                startPoint = new XYZ(ToFeet(tsuCoordinateMm), ToFeet(leftCoordinateMm), 0.0);
                endPoint = new XYZ(ToFeet(tsuCoordinateMm), ToFeet(rightCoordinateMm), 0.0);
                return true;
            }

            if (!yCoordinatesByName.TryGetValue(tsu, out tsuCoordinateMm))
            {
                errorMessage = $"Y grid '{tsu}' was not found in Kihon.NameY/ListSpanY.";
                return false;
            }

            if (!xCoordinatesByName.TryGetValue(leftName, out leftCoordinateMm))
            {
                errorMessage = $"X grid '{leftName}' was not found in Kihon.NameX/ListSpanX.";
                return false;
            }

            if (!xCoordinatesByName.TryGetValue(rightName, out rightCoordinateMm))
            {
                errorMessage = $"X grid '{rightName}' was not found in Kihon.NameX/ListSpanX.";
                return false;
            }

            startPoint = new XYZ(ToFeet(leftCoordinateMm), ToFeet(tsuCoordinateMm), 0.0);
            endPoint = new XYZ(ToFeet(rightCoordinateMm), ToFeet(tsuCoordinateMm), 0.0);
            return true;
        }

        private static bool TryGetBeamDepthMm(
            梁セグメント segment,
            out double topMm,
            out double bottomMm,
            out double depthMm,
            out string errorMessage)
        {
            topMm = 0.0;
            bottomMm = 0.0;
            depthMm = 0.0;
            errorMessage = null;

            if (!TryParseMillimeters(segment.上側のズレ寸法, out topMm))
            {
                errorMessage = $"top offset '{segment.上側のズレ寸法}' is invalid.";
                return false;
            }

            if (!TryParseMillimeters(segment.下側のズレ寸法, out bottomMm))
            {
                errorMessage = $"bottom offset '{segment.下側のズレ寸法}' is invalid.";
                return false;
            }

            depthMm = topMm + bottomMm;
            if (depthMm <= 0.0)
            {
                errorMessage = $"calculated depth is {FormatSize(depthMm)} mm.";
                return false;
            }

            return true;
        }

        private static bool TryBuildBeamLine(
            string kaiName,
            string columnKaiName,
            string tsu,
            string leftName,
            string rightName,
            bool tsuIsX,
            double beamTopMm,
            double beamBottomMm,
            XYZ startGridPoint,
            XYZ endGridPoint,
            Dictionary<string, ObservableCollection<柱セグメント>> columnSegmentsMap,
            IReadOnlyDictionary<string, int> xNameIndices,
            Level level,
            out Line beamLine,
            out string errorMessage)
        {
            beamLine = null;
            errorMessage = null;

            柱セグメント startColumn;
            柱セグメント endColumn;
            string columnError;

            if (tsuIsX)
            {
                if (!TryGetColumnSegment(columnSegmentsMap, xNameIndices, columnKaiName, leftName, tsu, out startColumn, out columnError))
                {
                    errorMessage = columnError;
                    return false;
                }

                if (!TryGetColumnSegment(columnSegmentsMap, xNameIndices, columnKaiName, rightName, tsu, out endColumn, out columnError))
                {
                    errorMessage = columnError;
                    return false;
                }

                double startInsetMm;
                if (!TryGetColumnOffsetMm(startColumn.上側のズレ, $"{columnKaiName} {leftName}-{tsu}", "上側のズレ", out startInsetMm, out errorMessage))
                {
                    return false;
                }

                double endInsetMm;
                if (!TryGetColumnOffsetMm(endColumn.下側のズレ, $"{columnKaiName} {rightName}-{tsu}", "下側のズレ", out endInsetMm, out errorMessage))
                {
                    return false;
                }

                double xShiftFeet = ((beamBottomMm - beamTopMm) / 2.0) * FeetPerMillimeter;
                XYZ startPoint = new XYZ(
                    startGridPoint.X + xShiftFeet,
                    startGridPoint.Y + startInsetMm * FeetPerMillimeter,
                    level.Elevation);
                XYZ endPoint = new XYZ(
                    endGridPoint.X + xShiftFeet,
                    endGridPoint.Y - endInsetMm * FeetPerMillimeter,
                    level.Elevation);

                if (startPoint.IsAlmostEqualTo(endPoint))
                {
                    errorMessage = "calculated beam line has zero length after applying column faces.";
                    return false;
                }

                beamLine = Line.CreateBound(startPoint, endPoint);
                return true;
            }

            if (!TryGetColumnSegment(columnSegmentsMap, xNameIndices, columnKaiName, tsu, leftName, out startColumn, out columnError))
            {
                errorMessage = columnError;
                return false;
            }

            if (!TryGetColumnSegment(columnSegmentsMap, xNameIndices, columnKaiName, tsu, rightName, out endColumn, out columnError))
            {
                errorMessage = columnError;
                return false;
            }

            double startInsetXmm;
            if (!TryGetColumnOffsetMm(startColumn.右側のズレ, $"{columnKaiName} {tsu}-{leftName}", "右側のズレ", out startInsetXmm, out errorMessage))
            {
                return false;
            }

            double endInsetXmm;
            if (!TryGetColumnOffsetMm(endColumn.左側のズレ, $"{columnKaiName} {tsu}-{rightName}", "左側のズレ", out endInsetXmm, out errorMessage))
            {
                return false;
            }

            double yShiftFeet = ((beamTopMm - beamBottomMm) / 2.0) * FeetPerMillimeter;
            XYZ startPointX = new XYZ(
                startGridPoint.X + startInsetXmm * FeetPerMillimeter,
                startGridPoint.Y + yShiftFeet,
                level.Elevation);
            XYZ endPointX = new XYZ(
                endGridPoint.X - endInsetXmm * FeetPerMillimeter,
                endGridPoint.Y + yShiftFeet,
                level.Elevation);

            if (startPointX.IsAlmostEqualTo(endPointX))
            {
                errorMessage = "calculated beam line has zero length after applying column faces.";
                return false;
            }

            beamLine = Line.CreateBound(startPointX, endPointX);
            return true;
        }

        private static bool TryGetColumnSegment(
            Dictionary<string, ObservableCollection<柱セグメント>> columnSegmentsMap,
            IReadOnlyDictionary<string, int> xNameIndices,
            string kaiName,
            string yName,
            string xName,
            out 柱セグメント columnSegment,
            out string errorMessage)
        {
            columnSegment = null;
            errorMessage = null;

            string mapKey = $"{kaiName}::{yName}";
            ObservableCollection<柱セグメント> segments;
            if (!columnSegmentsMap.TryGetValue(mapKey, out segments) || segments == null)
            {
                errorMessage = $"column layout key '{mapKey}' was not found.";
                return false;
            }

            int xIndex;
            if (!xNameIndices.TryGetValue(xName, out xIndex))
            {
                errorMessage = $"column grid '{xName}' was not found in NameX.";
                return false;
            }

            if (xIndex < 0 || xIndex >= segments.Count)
            {
                errorMessage = $"column layout key '{mapKey}' does not contain X grid '{xName}'.";
                return false;
            }

            columnSegment = segments[xIndex];
            if (columnSegment == null)
            {
                errorMessage = $"column layout segment '{kaiName} {yName}-{xName}' is null.";
                return false;
            }

            return true;
        }

        private static bool TryGetColumnOffsetMm(
            string rawValue,
            string columnLabel,
            string offsetLabel,
            out double offsetMm,
            out string errorMessage)
        {
            errorMessage = null;
            if (TryParseMillimeters(rawValue, out offsetMm))
            {
                return true;
            }

            errorMessage = $"column '{columnLabel}' has invalid {offsetLabel} '{rawValue}'.";
            return false;
        }

        private static bool TryGetGridIntersectionPoint(Grid gridA, Grid gridB, out XYZ point)
        {
            point = null;

            if (gridA?.Curve == null || gridB?.Curve == null)
            {
                return false;
            }

            IntersectionResultArray results;
            SetComparisonResult comparison = gridA.Curve.Intersect(gridB.Curve, out results);
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

        private static bool TryParseMillimeters(string rawValue, out double valueMm)
        {
            valueMm = 0.0;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            string normalized = rawValue.Trim();
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out valueMm)
                || double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out valueMm);
        }

        private static Dictionary<string, FamilyInstance> CollectExistingBeamsByKey(Document doc, List<string> warnings)
        {
            Dictionary<string, FamilyInstance> beamsByKey = new Dictionary<string, FamilyInstance>(StringComparer.Ordinal);

            foreach (FamilyInstance beam in new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>())
            {
                string beamKey;
                if (!TryGetBeamKey(beam, out beamKey))
                {
                    continue;
                }

                if (beamsByKey.ContainsKey(beamKey))
                {
                    warnings.Add($"Multiple framing instances share beam key '{beamKey}'. One of them will be reported as orphan.");
                    continue;
                }

                beamsByKey.Add(beamKey, beam);
            }

            return beamsByKey;
        }

        private static bool TryGetBeamKey(FamilyInstance beam, out string beamKey)
        {
            beamKey = null;

            if (!IsStructuralBeam(beam))
            {
                return false;
            }

            LocationCurve locationCurve = beam?.Location as LocationCurve;
            Line beamLine = locationCurve?.Curve as Line;
            if (beamLine == null)
            {
                return false;
            }

            ElementId levelId = GetElementIdParameterValue(
                beam,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                BuiltInParameter.FAMILY_LEVEL_PARAM);

            if (levelId == ElementId.InvalidElementId)
            {
                return false;
            }

            beamKey = BuildBeamKey(beamLine, levelId);
            return true;
        }

        private static bool TryDeleteBeam(Document doc, FamilyInstance beam, out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                doc.Delete(beam.Id);
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

        private static bool TryReplaceBeam(
            Document doc,
            FamilyInstance existingBeam,
            FamilySymbol beamSymbol,
            BeamPlacementTarget target,
            out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                doc.Delete(existingBeam.Id);

                FamilyInstance recreatedBeam = doc.Create.NewFamilyInstance(
                    target.Line,
                    beamSymbol,
                    target.Level,
                    StructuralType.Beam);

                ApplyBeamParameters(recreatedBeam, target);
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

        private static bool IsBeamInSync(FamilyInstance existingBeam, FamilySymbol desiredSymbol, BeamPlacementTarget target)
        {
            if (existingBeam == null || desiredSymbol == null)
            {
                return false;
            }

            if (existingBeam.Symbol == null || existingBeam.Symbol.Id != desiredSymbol.Id)
            {
                return false;
            }

            string existingComment = GetBeamComment(existingBeam);
            if (!string.Equals(existingComment, BuildBeamComment(target), StringComparison.Ordinal))
            {
                return false;
            }

            LocationCurve locationCurve = existingBeam.Location as LocationCurve;
            Line existingLine = locationCurve?.Curve as Line;
            if (!AreLinesEquivalent(existingLine, target.Line))
            {
                return false;
            }

            double end0Offset = GetDoubleParameterValue(existingBeam, BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
            double end1Offset = GetDoubleParameterValue(existingBeam, BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
            return Math.Abs(end0Offset - target.EndOffsetFeet) <= OffsetToleranceFeet
                && Math.Abs(end1Offset - target.EndOffsetFeet) <= OffsetToleranceFeet
                && HasDesiredBeamEndTreatment(existingBeam);
        }

        private static string BuildExistingBeamLabel(FamilyInstance beam)
        {
            string comment = GetBeamComment(beam);
            if (!string.IsNullOrWhiteSpace(comment))
            {
                return comment;
            }

            string typeLabel = beam?.Symbol == null
                ? "(unknown type)"
                : $"{beam.Symbol.FamilyName} : {beam.Symbol.Name}";

            LocationCurve locationCurve = beam?.Location as LocationCurve;
            Line beamLine = locationCurve?.Curve as Line;
            if (beamLine == null)
            {
                return typeLabel;
            }

            XYZ p0 = beamLine.GetEndPoint(0);
            XYZ p1 = beamLine.GetEndPoint(1);
            return $"{typeLabel} [{FormatCoordinate(p0.X)},{FormatCoordinate(p0.Y)} -> {FormatCoordinate(p1.X)},{FormatCoordinate(p1.Y)}]";
        }

        private static bool IsStructuralBeam(FamilyInstance beam)
        {
            return beam != null && beam.StructuralType == StructuralType.Beam;
        }

        private static bool TryCreateBeam(
            Document doc,
            FamilySymbol beamSymbol,
            BeamPlacementTarget target,
            out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                FamilyInstance beam = doc.Create.NewFamilyInstance(
                    target.Line,
                    beamSymbol,
                    target.Level,
                    StructuralType.Beam);

                ApplyBeamParameters(beam, target);
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

        private static void ApplyBeamParameters(FamilyInstance beam, BeamPlacementTarget target)
        {
            TrySetDoubleParameter(beam, target.EndOffsetFeet,
                BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION,
                BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);

            // The legacy streamlit tool uses clear-span geometry between column faces.
            // Reset Revit framing end treatment so the solid does not extend past that data-driven line.
            TrySetDoubleParameter(beam, 0.0,
                BuiltInParameter.START_EXTENSION,
                BuiltInParameter.END_EXTENSION,
                BuiltInParameter.START_JOIN_CUTBACK,
                BuiltInParameter.END_JOIN_CUTBACK,
                BuiltInParameter.STRUCTURAL_BEAM_CUTBACK_FOR_COLUMN);

            TryDisallowBeamJoin(beam, 0);
            TryDisallowBeamJoin(beam, 1);

            Parameter analyticalParameter = beam.get_Parameter(BuiltInParameter.STRUCTURAL_ANALYTICAL_MODEL);
            if (analyticalParameter != null && !analyticalParameter.IsReadOnly && analyticalParameter.StorageType == StorageType.Integer)
            {
                analyticalParameter.Set(0);
            }

            Parameter commentsParameter = beam.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (commentsParameter != null && !commentsParameter.IsReadOnly)
            {
                commentsParameter.Set(BuildBeamComment(target));
            }
        }

        private static string BuildBeamComment(BeamPlacementTarget target)
        {
            return $"{target.Label} | {target.BeamCode} | {BuildBeamTypeName(target.DepthMm)}";
        }

        private static string GetBeamComment(FamilyInstance beam)
        {
            Parameter commentsParameter = beam?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            return commentsParameter?.StorageType == StorageType.String
                ? commentsParameter.AsString() ?? string.Empty
                : string.Empty;
        }

        private static void TrySetDoubleParameter(Element element, double value, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element.get_Parameter(parameterId);
                if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Double)
                {
                    parameter.Set(value);
                }
            }
        }

        private static double GetDoubleParameterValue(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            return parameter != null && parameter.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0.0;
        }

        private static bool HasDesiredBeamEndTreatment(FamilyInstance beam)
        {
            if (beam == null)
            {
                return false;
            }

            return Math.Abs(GetDoubleParameterValue(beam, BuiltInParameter.START_EXTENSION)) <= OffsetToleranceFeet
                && Math.Abs(GetDoubleParameterValue(beam, BuiltInParameter.END_EXTENSION)) <= OffsetToleranceFeet
                && Math.Abs(GetDoubleParameterValue(beam, BuiltInParameter.START_JOIN_CUTBACK)) <= OffsetToleranceFeet
                && Math.Abs(GetDoubleParameterValue(beam, BuiltInParameter.END_JOIN_CUTBACK)) <= OffsetToleranceFeet
                && Math.Abs(GetDoubleParameterValue(beam, BuiltInParameter.STRUCTURAL_BEAM_CUTBACK_FOR_COLUMN)) <= OffsetToleranceFeet
                && !IsBeamJoinAllowed(beam, 0)
                && !IsBeamJoinAllowed(beam, 1);
        }

        private static bool IsBeamJoinAllowed(FamilyInstance beam, int end)
        {
            try
            {
                return StructuralFramingUtils.IsJoinAllowedAtEnd(beam, end);
            }
            catch
            {
                return false;
            }
        }

        private static void TryDisallowBeamJoin(FamilyInstance beam, int end)
        {
            try
            {
                StructuralFramingUtils.DisallowJoinAtEnd(beam, end);
            }
            catch
            {
                // Some framing families/end states may reject join edits; ignore and keep syncing.
            }
        }

        private static ElementId GetElementIdParameterValue(Element element, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element?.get_Parameter(parameterId);
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

        private static bool HasWritableBeamDepthParameter(FamilySymbol symbol)
        {
            BeamSizeParameters sizeParameters;
            return TryGetBeamSizeParameters(symbol, true, out sizeParameters);
        }

        private static bool TryGetBeamDepth(FamilySymbol symbol, out double depthMm)
        {
            depthMm = 0.0;

            BeamSizeParameters sizeParameters;
            if (!TryGetBeamSizeParameters(symbol, false, out sizeParameters))
            {
                return false;
            }

            depthMm = sizeParameters.Depth.AsDouble() / FeetPerMillimeter;
            return true;
        }

        private static bool TryGetBeamWidth(FamilySymbol symbol, out double widthMm)
        {
            widthMm = 0.0;

            BeamSizeParameters sizeParameters;
            if (!TryGetBeamSizeParameters(symbol, false, out sizeParameters) || sizeParameters.Width == null)
            {
                return false;
            }

            widthMm = sizeParameters.Width.AsDouble() / FeetPerMillimeter;
            return true;
        }

        private static bool TrySetBeamSize(FamilySymbol symbol, double depthMm, out string failureReason)
        {
            failureReason = null;

            BeamSizeParameters sizeParameters;
            if (!TryGetBeamSizeParameters(symbol, true, out sizeParameters))
            {
                failureReason = $"Type '{symbol.FamilyName} : {symbol.Name}' does not expose a writable beam depth parameter.";
                return false;
            }

            sizeParameters.Depth.Set(ToFeet(depthMm));
            return true;
        }

        private static bool TryGetBeamSizeParameters(FamilySymbol symbol, bool requireWritable, out BeamSizeParameters sizeParameters)
        {
            sizeParameters = null;

            Parameter widthParameter = FindSizeParameter(
                symbol,
                BeamWidthGuid,
                requireWritable,
                new[] { "Width", "width", "B", "b" },
                new[] { "width" });

            Parameter depthParameter = FindSizeParameter(
                symbol,
                BeamDepthGuid,
                requireWritable,
                new[] { "Depth", "depth", "Height", "height", "H", "h" },
                new[] { "depth", "height" });

            if (depthParameter == null)
            {
                return false;
            }

            sizeParameters = new BeamSizeParameters(widthParameter, depthParameter);
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
                $"Deleted orphan beams: {deletedCount}" + Environment.NewLine +
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

        private sealed class BeamPlacementTarget
        {
            public BeamPlacementTarget(
                string label,
                string beamCode,
                Line line,
                Level level,
                double endOffsetFeet,
                double depthMm)
            {
                Label = label;
                BeamCode = beamCode;
                Line = line;
                Level = level;
                EndOffsetFeet = endOffsetFeet;
                DepthMm = depthMm;
            }

            public string Label { get; }

            public string BeamCode { get; }

            public Line Line { get; }

            public Level Level { get; }

            public double EndOffsetFeet { get; }

            public double DepthMm { get; }
        }

        private sealed class BeamSizeParameters
        {
            public BeamSizeParameters(Parameter width, Parameter depth)
            {
                Width = width;
                Depth = depth;
            }

            public Parameter Width { get; }

            public Parameter Depth { get; }
        }

        private sealed class BeamTypeResolver
        {
            private readonly Family templateFamily;
            private readonly List<FamilySymbol> allSymbols;
            private readonly Dictionary<string, FamilySymbol> depthCache;
            private readonly double? templateWidthMm;

            public BeamTypeResolver(FamilySymbol templateSymbol, List<FamilySymbol> symbols)
            {
                TemplateSymbol = templateSymbol;
                templateFamily = templateSymbol.Family;
                allSymbols = symbols;
                depthCache = new Dictionary<string, FamilySymbol>(StringComparer.Ordinal);
                CreatedTypeNames = new List<string>();

                double widthMm;
                templateWidthMm = TryGetBeamWidth(templateSymbol, out widthMm) ? widthMm : (double?)null;

                foreach (FamilySymbol symbol in symbols)
                {
                    double depthMm;
                    if (!TryGetBeamDepth(symbol, out depthMm) || !MatchesTemplateWidth(symbol))
                    {
                        continue;
                    }

                    string key = BuildBeamDepthKey(depthMm);
                    if (!depthCache.ContainsKey(key))
                    {
                        depthCache.Add(key, symbol);
                    }
                }
            }

            public FamilySymbol TemplateSymbol { get; }

            public List<string> CreatedTypeNames { get; }

            public bool TryGetOrCreateSymbol(
                Document doc,
                double depthMm,
                out FamilySymbol symbol,
                out string failureReason)
            {
                failureReason = null;
                string depthKey = BuildBeamDepthKey(depthMm);

                if (depthCache.TryGetValue(depthKey, out symbol) && symbol != null)
                {
                    return true;
                }

                symbol = FindMatchingSymbol(depthMm);
                if (symbol != null)
                {
                    depthCache[depthKey] = symbol;
                    return true;
                }

                string desiredName = BuildBeamTypeName(depthMm);
                string uniqueName = GetUniqueTypeName(desiredName);

                FamilySymbol duplicatedSymbol = TemplateSymbol.Duplicate(uniqueName) as FamilySymbol;
                if (duplicatedSymbol == null)
                {
                    failureReason = $"Could not duplicate template symbol '{TemplateSymbol.Name}' for depth {FormatSize(depthMm)}.";
                    return false;
                }

                string sizeSetFailure;
                if (!TrySetBeamSize(duplicatedSymbol, depthMm, out sizeSetFailure))
                {
                    doc.Delete(duplicatedSymbol.Id);
                    failureReason = sizeSetFailure;
                    return false;
                }

                allSymbols.Add(duplicatedSymbol);
                depthCache[depthKey] = duplicatedSymbol;
                CreatedTypeNames.Add($"{duplicatedSymbol.FamilyName} : {duplicatedSymbol.Name}");
                symbol = duplicatedSymbol;
                return true;
            }

            private FamilySymbol FindMatchingSymbol(double depthMm)
            {
                IEnumerable<FamilySymbol> candidates = allSymbols;
                if (templateFamily != null)
                {
                    candidates = allSymbols
                        .OrderByDescending(symbol => symbol.Family != null && symbol.Family.Id == templateFamily.Id);
                }

                return candidates.FirstOrDefault(symbol =>
                {
                    double existingDepthMm;
                    return MatchesTemplateWidth(symbol)
                        && TryGetBeamDepth(symbol, out existingDepthMm)
                        && Math.Abs(existingDepthMm - depthMm) <= SizeToleranceMillimeters;
                });
            }

            private bool MatchesTemplateWidth(FamilySymbol symbol)
            {
                if (!templateWidthMm.HasValue)
                {
                    return true;
                }

                double existingWidthMm;
                return TryGetBeamWidth(symbol, out existingWidthMm)
                    && Math.Abs(existingWidthMm - templateWidthMm.Value) <= SizeToleranceMillimeters;
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

        private static string BuildBeamKey(Line line, ElementId levelId)
        {
            XYZ p0 = NormalizePoint(line.GetEndPoint(0));
            XYZ p1 = NormalizePoint(line.GetEndPoint(1));

            string pointKey0 = BuildPointKey(p0);
            string pointKey1 = BuildPointKey(p1);
            string startKey = string.CompareOrdinal(pointKey0, pointKey1) <= 0 ? pointKey0 : pointKey1;
            string endKey = string.CompareOrdinal(pointKey0, pointKey1) <= 0 ? pointKey1 : pointKey0;
            return $"{startKey}|{endKey}|{levelId.IntegerValue}";
        }

        private static bool AreLinesEquivalent(Line lineA, Line lineB)
        {
            if (lineA == null || lineB == null)
            {
                return false;
            }

            XYZ a0 = NormalizePoint(lineA.GetEndPoint(0));
            XYZ a1 = NormalizePoint(lineA.GetEndPoint(1));
            XYZ b0 = NormalizePoint(lineB.GetEndPoint(0));
            XYZ b1 = NormalizePoint(lineB.GetEndPoint(1));

            return PointsMatch(a0, b0) && PointsMatch(a1, b1)
                || PointsMatch(a0, b1) && PointsMatch(a1, b0);
        }

        private static bool PointsMatch(XYZ pointA, XYZ pointB)
        {
            return Math.Abs(pointA.X - pointB.X) <= LocationToleranceFeet
                && Math.Abs(pointA.Y - pointB.Y) <= LocationToleranceFeet;
        }

        private static XYZ NormalizePoint(XYZ point)
        {
            return new XYZ(point.X, point.Y, 0.0);
        }

        private static string BuildPointKey(XYZ point)
        {
            return $"{RoundToLocationTolerance(point.X)}|{RoundToLocationTolerance(point.Y)}";
        }

        private static string BuildBeamDepthKey(double depthMm)
        {
            return RoundToSizeTolerance(depthMm).ToString("0.###", CultureInfo.InvariantCulture);
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

        private static string FormatCoordinate(double value)
        {
            return RoundToLocationTolerance(value).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatSize(double valueMm)
        {
            double rounded = RoundToSizeTolerance(valueMm);
            return Math.Abs(rounded - Math.Round(rounded)) < SizeToleranceMillimeters
                ? Math.Round(rounded).ToString("0", CultureInfo.InvariantCulture)
                : rounded.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string BuildBeamTypeName(double depthMm)
        {
            return $"H{FormatSize(depthMm)}";
        }
    }
}
