using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnYokoNakagoCommand : IExternalCommand
    {
        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double LocationToleranceFeet = 1e-4;
        private const string SectionName = "柱頭";
        private const string RebarCommentPrefix = "COLUMN_YOKO_NAKAGO ";
        private const string HookType1804DName = "DBS_HOOK_180_4D";
        private const string HookType1356DName = "DBS_HOOK_135_6D";
        private const string HookType908DName = "DBS_HOOK_90_8D";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Column Yoko Nakago", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Column Yoko Nakago", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Column Yoko Nakago", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(projectData.Kihon?.NameX?.Select(axis => axis?.Name));
            List<string> yNames = GetAxisNames(projectData.Kihon?.NameY?.Select(axis => axis?.Name));
            List<string> kaiNames = GetAxisNames(projectData.Kihon?.NameKai?.Select(kai => kai?.Name));

            if (xNames == null || yNames == null || kaiNames == null || kaiNames.Count < 2)
            {
                TaskDialog.Show("Column Yoko Nakago", "ProjectData Kihon axis data is missing or invalid.");
                return Result.Cancelled;
            }

            柱配置図 columnLayout;
            string layoutError;
            if (!TryGetColumnLayout(projectData, out columnLayout, out layoutError))
            {
                TaskDialog.Show("Column Yoko Nakago", layoutError);
                return Result.Cancelled;
            }

            Dictionary<string, Grid> gridsByName;
            string gridError;
            if (!TryCollectGridsByName(doc, out gridsByName, out gridError))
            {
                TaskDialog.Show("Column Yoko Nakago", gridError);
                return Result.Cancelled;
            }

            Dictionary<string, Level> levelsByName;
            string levelError;
            if (!TryCollectLevelsByName(doc, out levelsByName, out levelError))
            {
                TaskDialog.Show("Column Yoko Nakago", levelError);
                return Result.Cancelled;
            }

            List<string> warnings = new List<string>();
            List<YokoNakagoSpec> specs = BuildYokoNakagoSpecs(
                projectData,
                columnLayout,
                kaiNames,
                yNames,
                xNames,
                gridsByName,
                levelsByName,
                warnings);

            if (specs.Count == 0)
            {
                string emptyResult = $"No valid {SectionName} Yoko Nakago specs were found.";
                if (warnings.Count > 0)
                {
                    emptyResult += "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20));
                }

                TaskDialog.Show("Column Yoko Nakago", emptyResult);
                return Result.Cancelled;
            }

            Dictionary<string, FamilyInstance> existingColumnsByKey = CollectExistingColumnsByKey(doc);
            Dictionary<string, RebarBarType> barTypeCache = new Dictionary<string, RebarBarType>(StringComparer.OrdinalIgnoreCase);

            int created = 0;
            List<string> failed = new List<string>();

            using (Transaction tx = new Transaction(doc, "Create Column Yoko Nakago"))
            {
                tx.Start();
                DeleteExistingYokoNakago(doc, SectionName);

                HookTypes hookTypes = FindOrCreateHookTypes(doc, warnings);

                foreach (YokoNakagoSpec spec in specs)
                {
                    try
                    {
                        string columnKey = BuildColumnKey(spec.Point, spec.BaseLevel.Id, spec.TopLevel.Id);
                        FamilyInstance hostColumn;
                        if (!existingColumnsByKey.TryGetValue(columnKey, out hostColumn))
                        {
                            warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: host column was not found in the model.");
                            continue;
                        }

                        RebarBarType barType = GetOrFindRebarBarType(doc, spec, barTypeCache);
                        if (barType == null)
                        {
                            warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: no RebarBarType matched 横向き中子径 '{spec.Diameter}'.");
                            continue;
                        }

                        created += CreateYokoNakagoForSpec(doc, hostColumn, spec, barType, hookTypes, warnings);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: {GetExceptionMessage(ex)}");
                    }
                }

                tx.Commit();
            }

            string result = $"Created Yoko Nakago count: {created}";
            if (warnings.Count > 0)
            {
                result += "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20));
            }

            if (failed.Count > 0)
            {
                result += "\n\nFailed:\n" + string.Join("\n", failed.Take(10));
            }

            TaskDialog.Show("Column Yoko Nakago", result);
            return Result.Succeeded;
        }

        private static List<YokoNakagoSpec> BuildYokoNakagoSpecs(
            ProjectData projectData,
            柱配置図 columnLayout,
            List<string> kaiNames,
            List<string> yNames,
            List<string> xNames,
            Dictionary<string, Grid> gridsByName,
            Dictionary<string, Level> levelsByName,
            List<string> warnings)
        {
            List<YokoNakagoSpec> specs = new List<YokoNakagoSpec>();
            Dictionary<string, 柱リスト> floorListsByKai = (projectData?.リスト?.柱リスト ?? new ObservableCollection<柱リスト>())
                .Where(list => list != null && !string.IsNullOrWhiteSpace(list.各階))
                .GroupBy(list => list.各階, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

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

                柱リスト floorColumnList;
                if (!floorListsByKai.TryGetValue(kaiName, out floorColumnList) || floorColumnList?.柱 == null)
                {
                    warnings.Add($"ProjectData.リスト.柱リスト does not contain floor '{kaiName}'.");
                    continue;
                }

                foreach (string yName in yNames)
                {
                    string mapKey = $"{kaiName}::{yName}";
                    ObservableCollection<柱セグメント> segments;
                    if (!columnLayout.BeamSegmentsMap.TryGetValue(mapKey, out segments) || segments == null)
                    {
                        warnings.Add($"Column layout key '{mapKey}' was not found.");
                        continue;
                    }

                    Grid yGrid;
                    if (!gridsByName.TryGetValue(yName, out yGrid))
                    {
                        warnings.Add($"Grid '{yName}' was not found in the model.");
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

                        柱 columnData = floorColumnList.柱?.FirstOrDefault(column =>
                            column != null && string.Equals(column.Name?.Trim(), columnCode, StringComparison.OrdinalIgnoreCase));
                        if (columnData == null)
                        {
                            warnings.Add($"{kaiName} {yName}-{xNames[xIndex]} {columnCode}: column data was not found in ProjectData.リスト.柱リスト.");
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
                            warnings.Add($"Could not find the intersection of grids '{xName}' and '{yName}'.");
                            continue;
                        }

                        double offsetXmm;
                        double offsetYmm;
                        string offsetError;
                        if (!TryGetColumnPlacementOffsetFromSegment(segment, out offsetXmm, out offsetYmm, out offsetError))
                        {
                            warnings.Add($"{kaiName} {yName}-{xName} {columnCode}: {offsetError}");
                            continue;
                        }

                        XYZ adjustedPoint = new XYZ(
                            intersectionPoint.X + offsetXmm * FeetPerMillimeter,
                            intersectionPoint.Y + offsetYmm * FeetPerMillimeter,
                            baseLevel.Elevation);

                        YokoNakagoSpec spec;
                        string specError;
                        if (!TryBuildYokoNakagoSpec(
                            kaiName,
                            xName,
                            yName,
                            columnCode,
                            columnData,
                            adjustedPoint,
                            baseLevel,
                            topLevel,
                            out spec,
                            out specError))
                        {
                            warnings.Add($"{kaiName} {yName}-{xName} {columnCode}: {specError}");
                            continue;
                        }

                        specs.Add(spec);
                    }
                }
            }

            return specs;
        }

        private static bool TryBuildYokoNakagoSpec(
            string kaiName,
            string xName,
            string yName,
            string columnCode,
            柱 columnData,
            XYZ point,
            Level baseLevel,
            Level topLevel,
            out YokoNakagoSpec spec,
            out string errorMessage)
        {
            spec = null;
            errorMessage = null;

            Z柱の配置 layout = columnData?.柱の配置;
            if (layout == null)
            {
                errorMessage = "柱の配置 is null.";
                return false;
            }

            GridBotDataHashira sectionData = GetSectionData(layout, SectionName);
            if (sectionData == null)
            {
                errorMessage = $"{SectionName} data was not found.";
                return false;
            }

            double widthMm;
            string widthText = GetSectionProperty(layout, SectionName, "柱幅", "幅");
            if (!TryParseMillimeters(widthText, out widthMm) || widthMm <= 0.0)
            {
                errorMessage = $"{SectionName} 柱幅 is invalid: '{widthText}'.";
                return false;
            }

            double depthMm;
            string depthText = GetSectionProperty(layout, SectionName, "柱成", "成");
            if (!TryParseMillimeters(depthText, out depthMm) || depthMm <= 0.0)
            {
                errorMessage = $"{SectionName} 柱成 is invalid: '{depthText}'.";
                return false;
            }

            double mainDiaMm;
            string mainDiaText = GetSectionProperty(layout, SectionName, "主筋径");
            if (!TryParseMillimeters(mainDiaText, out mainDiaMm) || mainDiaMm <= 0.0)
            {
                errorMessage = $"{SectionName} 主筋径 is invalid: '{mainDiaText}'.";
                return false;
            }

            double hoopDiaMm;
            string hoopDiaText = GetSectionProperty(layout, SectionName, "HOOP径");
            if (!TryParseMillimeters(hoopDiaText, out hoopDiaMm) || hoopDiaMm <= 0.0)
            {
                errorMessage = $"{SectionName} HOOP径 is invalid: '{hoopDiaText}'.";
                return false;
            }

            double pitchMm;
            string pitchText = GetSectionProperty(layout, SectionName, "横向き中子ピッチ");
            if (!TryParseMillimeters(pitchText, out pitchMm) || pitchMm <= 0.0)
            {
                pitchText = GetSectionProperty(layout, SectionName, "ピッチ");
                if (!TryParseMillimeters(pitchText, out pitchMm) || pitchMm <= 0.0)
                {
                    errorMessage = $"{SectionName} 横向き中子ピッチ is invalid: '{pitchText}'.";
                    return false;
                }
            }

            double coverTopMm;
            if (!TryParseMillimeters(sectionData.上, out coverTopMm) || coverTopMm < 0.0)
            {
                errorMessage = $"{SectionName} 上 cover is invalid: '{sectionData.上}'.";
                return false;
            }

            double coverBottomMm;
            if (!TryParseMillimeters(sectionData.下, out coverBottomMm) || coverBottomMm < 0.0)
            {
                errorMessage = $"{SectionName} 下 cover is invalid: '{sectionData.下}'.";
                return false;
            }

            double coverLeftMm;
            if (!TryParseMillimeters(sectionData.左, out coverLeftMm) || coverLeftMm < 0.0)
            {
                errorMessage = $"{SectionName} 左 cover is invalid: '{sectionData.左}'.";
                return false;
            }

            double coverRightMm;
            if (!TryParseMillimeters(sectionData.右, out coverRightMm) || coverRightMm < 0.0)
            {
                errorMessage = $"{SectionName} 右 cover is invalid: '{sectionData.右}'.";
                return false;
            }

            string diameter = GetSectionProperty(layout, SectionName, "横向き中子径")?.Trim();
            if (string.IsNullOrWhiteSpace(diameter))
            {
                errorMessage = $"{SectionName} 横向き中子径 is empty.";
                return false;
            }

            double diameterMm;
            if (!TryParseMillimeters(diameter, out diameterMm) || diameterMm <= 0.0)
            {
                errorMessage = $"{SectionName} 横向き中子径 is invalid: '{diameter}'.";
                return false;
            }

            string shape = GetSectionProperty(layout, SectionName, "横向き中子形")?.Trim();
            if (string.IsNullOrWhiteSpace(shape))
            {
                errorMessage = $"{SectionName} 横向き中子形 is empty.";
                return false;
            }

            int count;
            string countText = GetFirstNonEmptyString(
                GetSectionProperty(layout, SectionName, "横向き中子本数", "柱頭横向き中子本数"),
                sectionData.横向き中子本数);
            if (!TryParsePositiveInt(countText, out count))
            {
                errorMessage = $"{SectionName} 横向き中子本数 is invalid: '{countText}'.";
                return false;
            }

            spec = new YokoNakagoSpec(
                kaiName,
                xName,
                yName,
                columnCode,
                widthMm,
                depthMm,
                mainDiaMm,
                hoopDiaMm,
                diameter,
                diameterMm,
                shape,
                GetSectionProperty(layout, SectionName, "横向き中子材質")?.Trim() ?? string.Empty,
                pitchMm,
                count,
                coverTopMm,
                coverBottomMm,
                coverLeftMm,
                coverRightMm,
                sectionData.YokogaoNakagoCustomPositions,
                sectionData.YokogaoNakagoDirections,
                sectionData.横向き中子_方向,
                point,
                baseLevel,
                topLevel);

            return true;
        }

        private static int CreateYokoNakagoForSpec(
            Document doc,
            FamilyInstance column,
            YokoNakagoSpec spec,
            RebarBarType barType,
            HookTypes hookTypes,
            List<string> warnings)
        {
            XYZ center = GetColumnCenter(column) ?? spec.Point;
            List<YokoNakagoBarData> barData = BuildBarData(spec, center, warnings);
            if (barData.Count == 0)
            {
                return 0;
            }

            double minZ;
            double maxZ;
            if (!TryGetHostVerticalRange(column, out minZ, out maxZ))
            {
                throw new InvalidOperationException("Could not resolve host column vertical range.");
            }

            double pitchFt = spec.PitchMm * FeetPerMillimeter;
            double startZ = minZ + spec.CoverBottomMm * FeetPerMillimeter;
            double endZ = maxZ - spec.CoverTopMm * FeetPerMillimeter;          
            double yokoNakagoLiftFt = GetYokoNakagoClearanceMm(spec.HoopDiaMm, spec.DiameterMm) * FeetPerMillimeter;
            if (endZ < startZ)
                if (endZ < startZ)
            {
                warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: host column height is too small for the requested cover.");
                return 0;
            }

            int created = 0;
            for (double z = startZ; z <= endZ + 1e-9; z += pitchFt)
            {
                foreach (YokoNakagoBarData data in barData)
                {
                    double barZ = z + yokoNakagoLiftFt;
                    if (barZ > endZ + 1e-9)
                    {
                        continue;
                    }

                    RebarHookType startHook = hookTypes.Resolve(data.StartHook);
                    RebarHookType endHook = hookTypes.Resolve(data.EndHook);
                    if (data.RequiresMissingHook(startHook, endHook))
                    {
                        warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: hook type for Shape={spec.Shape} was not found.");
                        continue;
                    }

                    XYZ start = ConvertLocalPointMmToModelPoint(data.StartMm, center, spec.DepthMm, barZ);
                    XYZ end = ConvertLocalPointMmToModelPoint(data.EndMm, center, spec.DepthMm, barZ);
                    if (start.IsAlmostEqualTo(end))
                    {
                        continue;
                    }

                    Rebar rebar = Rebar.CreateFromCurves(
                        doc,
                        RebarStyle.StirrupTie,
                        barType,
                        startHook,
                        endHook,
                        column,
                        XYZ.BasisZ,
                        new List<Curve> { Line.CreateBound(start, end) },
                        data.StartHookOrientation,
                        data.EndHookOrientation,
                        true,
                        true);

                    if (rebar == null)
                    {
                        continue;
                    }

                    Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (comments != null && !comments.IsReadOnly)
                    {
                        comments.Set($"{RebarCommentPrefix}{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode} {spec.SectionName} D={spec.Diameter} Shape={spec.Shape} Pitch={spec.PitchMm} Bar={data.Index + 1} HookStart={data.StartHook} HookEnd={data.EndHook}");
                    }

                    created++;
                }
            }

            return created;
        }

        private static List<YokoNakagoBarData> BuildBarData(YokoNakagoSpec spec, XYZ columnCenter, List<string> warnings)
        {
            List<YokoNakagoBarData> bars = new List<YokoNakagoBarData>();
            int shape;
            if (!int.TryParse(ExtractDigits(spec.Shape), out shape) || shape < 1 || shape > 5)
            {
                warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: 横向き中子形='{spec.Shape}' is not handled yet; supported shapes are 1..5.");
                return bars;
            }

            double actualMainDiaMm = GetActualBarDiameter(spec.MainDiaMm);
            double actualHoopDiaMm = GetActualBarDiameter(spec.HoopDiaMm);
            double actualYokoDiaMm = GetActualBarDiameter(spec.DiameterMm);
            double yokoNakagoOffsetMm = GetYokoNakagoClearanceMm(spec.HoopDiaMm, spec.DiameterMm);
            double leftX = -spec.WidthMm / 2.0 + spec.CoverLeftMm + actualHoopDiaMm - actualYokoDiaMm;
            double rightX = spec.WidthMm / 2.0 - spec.CoverRightMm - actualHoopDiaMm + actualYokoDiaMm;
            double centerY = -(spec.DepthMm / 2.0 + actualMainDiaMm);
            double halfRange = Math.Min(
                spec.DepthMm / 2.0 - spec.CoverTopMm - actualHoopDiaMm - actualYokoDiaMm / 2.0,
                spec.DepthMm / 2.0 - spec.CoverBottomMm - actualHoopDiaMm - actualYokoDiaMm / 2.0) / 2.0;
            if (halfRange < 0.0)
            {
                halfRange = 0.0;
            }

            if (rightX <= leftX)
            {
                warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: calculated Yoko Nakago length is not positive.");
                return bars;
            }

            for (int i = 0; i < spec.Count; i++)
            {
                int directionIndex = spec.Count - 1 - i;
                bool isReversed = GetIndexedBoolValue(spec.Directions, directionIndex, false);
                HookPair hooks = GetYokoNakagoHookData(shape, false);
                double y = spec.Count == 1
                    ? centerY
                    : centerY - halfRange + 2.0 * halfRange * i / (spec.Count - 1);
                UV startMm = new UV(leftX, y);
                UV endMm = new UV(rightX, y);
                string startHook = hooks.Start;
                string endHook = hooks.End;
                RebarHookOrientation startHookOrientation = RebarHookOrientation.Left;
                RebarHookOrientation endHookOrientation = RebarHookOrientation.Left;

                if (isReversed)
                {
                    UV originalStartMm = startMm;
                    startMm = endMm;
                    endMm = originalStartMm;

                    string originalStartHook = startHook;
                    startHook = endHook;
                    endHook = originalStartHook;

                    startHookOrientation = RebarHookOrientation.Right;
                    endHookOrientation = RebarHookOrientation.Right;
                }

                bars.Add(new YokoNakagoBarData(
                    i,
                    startMm,
                    endMm,
                    startHook,
                    endHook,
                    startHookOrientation,
                    endHookOrientation));
            }

            return bars;
        }

        private static Dictionary<string, FamilyInstance> CollectExistingColumnsByKey(Document doc)
        {
            Dictionary<string, FamilyInstance> columnsByKey = new Dictionary<string, FamilyInstance>(StringComparer.Ordinal);

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

                if (!columnsByKey.ContainsKey(columnKey))
                {
                    columnsByKey.Add(columnKey, column);
                }
            }

            return columnsByKey;
        }

        private static bool TryGetColumnKey(FamilyInstance column, out string columnKey)
        {
            columnKey = null;

            LocationPoint locationPoint = column?.Location as LocationPoint;
            if (locationPoint == null)
            {
                return false;
            }

            ElementId baseLevelId = GetElementIdParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM);
            ElementId topLevelId = GetElementIdParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM);
            if (baseLevelId == ElementId.InvalidElementId || topLevelId == ElementId.InvalidElementId)
            {
                return false;
            }

            columnKey = BuildColumnKey(locationPoint.Point, baseLevelId, topLevelId);
            return true;
        }

        private static RebarBarType GetOrFindRebarBarType(Document doc, YokoNakagoSpec spec, Dictionary<string, RebarBarType> cache)
        {
            string cacheKey = $"{spec.Diameter}|{spec.Material}";
            RebarBarType barType;
            if (cache.TryGetValue(cacheKey, out barType))
            {
                return barType;
            }

            barType = FindRebarBarType(doc, spec.Diameter, spec.Material);
            cache[cacheKey] = barType;
            return barType;
        }

        private static RebarBarType FindRebarBarType(Document doc, string nominalDiameter, string material)
        {
            string diameterToken = ExtractDigits(nominalDiameter);
            string materialToken = material?.Trim();

            List<RebarBarType> types = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();

            if (types.Count == 0)
            {
                return null;
            }

            RebarBarType exact = types.FirstOrDefault(type =>
                NameContains(type.Name, diameterToken)
                && NameContains(type.Name, materialToken));
            if (exact != null)
            {
                return exact;
            }

            return types.FirstOrDefault(type => NameContains(type.Name, diameterToken));
        }

        private static HookTypes FindOrCreateHookTypes(Document doc, List<string> warnings)
        {
            HookTypes hookTypes = new HookTypes
            {
                Hook180 = FindOrCreateHookType(doc, HookType1804DName, 180.0, 4.0, warnings),
                Hook135 = FindOrCreateHookType(doc, HookType1356DName, 135.0, 6.0, warnings),
                Hook90 = FindOrCreateHookType(doc, HookType908DName, 90.0, 8.0, warnings)
            };

            return hookTypes;
        }

        private static RebarHookType FindOrCreateHookType(Document doc, string name, double angleDegrees, double multiplier, List<string> warnings)
        {
            List<RebarHookType> hooks = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToList();

            RebarHookType existing = hooks.FirstOrDefault(hook =>
                string.Equals(hook.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            double angleRadians = angleDegrees * Math.PI / 180.0;
            try
            {
                RebarHookType created = RebarHookType.Create(doc, angleRadians, multiplier);
                if (created != null)
                {
                    created.Style = RebarStyle.StirrupTie;
                    created.HookAngle = angleRadians;
                    created.StraightLineMultiplier = multiplier;
                    created.Name = name;
                    return created;
                }
            }
            catch
            {
                // Revit may reject creating hook types in some templates; fall back by name below.
            }

            RebarHookType fallback = hooks.FirstOrDefault(hook =>
                hook.Name.IndexOf(((int)Math.Round(angleDegrees)).ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) >= 0);
            if (fallback == null)
            {
                warnings.Add($"Could not create or find hook type {name}.");
            }

            return fallback;
        }

        private static void DeleteExistingYokoNakago(Document doc, string sectionName)
        {
            List<ElementId> rebarIdsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(rebar =>
                {
                    Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    string comment = comments?.AsString();
                    if (string.IsNullOrWhiteSpace(comment) || !comment.StartsWith(RebarCommentPrefix, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return string.IsNullOrWhiteSpace(sectionName)
                        || comment.IndexOf(" " + sectionName, StringComparison.Ordinal) >= 0;
                })
                .Select(rebar => rebar.Id)
                .ToList();

            if (rebarIdsToDelete.Count > 0)
            {
                doc.Delete(rebarIdsToDelete);
            }
        }

        private static bool TryGetColumnPlacementOffsetFromSegment(
            柱セグメント segment,
            out double offsetXmm,
            out double offsetYmm,
            out string errorMessage)
        {
            offsetXmm = 0.0;
            offsetYmm = 0.0;
            errorMessage = null;

            double leftMm;
            double rightMm;
            double topMm;
            double bottomMm;
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

        private static bool TryCollectGridsByName(Document doc, out Dictionary<string, Grid> gridsByName, out string errorMessage)
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
                errorMessage = "Model contains duplicate grid names. Please resolve them before running Column Yoko Nakago: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
        }

        private static bool TryCollectLevelsByName(Document doc, out Dictionary<string, Level> levelsByName, out string errorMessage)
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
                errorMessage = "Model contains duplicate level names. Please resolve them before running Column Yoko Nakago: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
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

        private static GridBotDataHashira GetSectionData(Z柱の配置 layout, string sectionName)
        {
            if (layout?.gridbotdata == null || string.IsNullOrWhiteSpace(sectionName))
            {
                return null;
            }

            GridBotDataHashira sectionData;
            return layout.gridbotdata.TryGetValue(sectionName, out sectionData) ? sectionData : null;
        }

        private static string GetSectionProperty(object layout, string sectionName, params string[] baseNames)
        {
            if (layout == null || string.IsNullOrWhiteSpace(sectionName) || baseNames == null)
            {
                return null;
            }

            foreach (string baseName in baseNames.Where(name => !string.IsNullOrWhiteSpace(name)))
            {
                if (sectionName == "柱頭")
                {
                    string headValue = GetStringProperty(layout, baseName + "1");
                    if (!string.IsNullOrWhiteSpace(headValue))
                    {
                        return headValue;
                    }

                    string prefixedValue = GetStringProperty(layout, "柱頭" + baseName);
                    if (!string.IsNullOrWhiteSpace(prefixedValue))
                    {
                        return prefixedValue;
                    }
                }

                string value = GetStringProperty(layout, baseName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string GetStringProperty(object obj, string propertyName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            PropertyInfo property = obj.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.PropertyType != typeof(string))
            {
                return null;
            }

            return property.GetValue(obj) as string;
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

        private static XYZ GetColumnCenter(FamilyInstance column)
        {
            LocationPoint locationPoint = column?.Location as LocationPoint;
            return locationPoint?.Point;
        }

        private static bool TryGetHostVerticalRange(FamilyInstance column, out double minZ, out double maxZ)
        {
            minZ = 0.0;
            maxZ = 0.0;

            Level baseLevel = column.Document.GetElement(GetElementIdParameterValue(
                column,
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)) as Level;
            Level topLevel = column.Document.GetElement(GetElementIdParameterValue(
                column,
                BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)) as Level;

            if (baseLevel != null && topLevel != null)
            {
                minZ = baseLevel.Elevation
                    + GetFirstDoubleParameterValue(
                        column,
                        BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,
                        BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM);
                maxZ = topLevel.Elevation
                    + GetFirstDoubleParameterValue(
                        column,
                        BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM,
                        BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM);

                if (maxZ > minZ)
                {
                    return true;
                }
            }

            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            if (bbox == null)
            {
                return false;
            }

            minZ = bbox.Min.Z;
            maxZ = bbox.Max.Z;
            return maxZ > minZ;
        }

        private static XYZ ConvertLocalPointMmToModelPoint(UV pointMm, XYZ center, double depthMm, double z)
        {
            double x = center.X + pointMm.U * FeetPerMillimeter;
            double y = center.Y + (depthMm / 2.0 + pointMm.V) * FeetPerMillimeter;
            return new XYZ(x, y, z);
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

        private static double GetFirstDoubleParameterValue(Element element, params BuiltInParameter[] parameterIds)
        {
            foreach (BuiltInParameter parameterId in parameterIds)
            {
                Parameter parameter = element?.get_Parameter(parameterId);
                if (parameter != null && parameter.StorageType == StorageType.Double)
                {
                    return parameter.AsDouble();
                }
            }

            return 0.0;
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

        private static double RoundToLocationTolerance(double value)
        {
            return Math.Round(value / LocationToleranceFeet) * LocationToleranceFeet;
        }

        private static HookPair GetYokoNakagoHookData(int shape, bool isReversed)
        {
            switch (shape)
            {
                case 1:
                    return new HookPair("180", "180");
                case 2:
                    return isReversed ? new HookPair("180", "90") : new HookPair("90", "180");
                case 3:
                    return new HookPair("90", "90");
                case 4:
                    return new HookPair("135", "135");
                case 5:
                    return isReversed ? new HookPair("135", "90") : new HookPair("90", "135");
                default:
                    return new HookPair("0", "0");
            }
        }

        private static bool TryParseMillimeters(string input, out double valueMm)
        {
            valueMm = 0.0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string normalized = ExtractNumberText(input);

            return double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out valueMm)
                || double.TryParse(
                normalized,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.CurrentCulture,
                out valueMm);
        }

        private static bool TryParsePositiveInt(string input, out int value)
        {
            value = 0;
            double parsed;
            if (!TryParseMillimeters(input, out parsed))
            {
                return false;
            }

            value = (int)Math.Round(parsed);
            return value > 0;
        }

        private static string ExtractDigits(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return new string(input.Where(char.IsDigit).ToArray());
        }

        private static string ExtractNumberText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            string text = input.Trim().Replace(",", string.Empty);
            return new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray());
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
        private static double GetYokoNakagoClearanceMm(double nominalHoopDiameter, double nominalYokoNakagoDiameter)
        {
            double actualHoopDiaMm = GetActualBarDiameter(nominalHoopDiameter);
            double actualYokoDiaMm = GetActualBarDiameter(nominalYokoNakagoDiameter);
            return (actualHoopDiaMm + actualYokoDiaMm) / 2.0;
        }

        private static bool GetIndexedBoolValue(Dictionary<int, bool> dictionary, int index, bool fallback)
        {
            bool value;
            return dictionary != null && dictionary.TryGetValue(index, out value) ? value : fallback;
        }

        private static string GetFirstNonEmptyString(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static bool NameContains(string source, string token)
        {
            return !string.IsNullOrWhiteSpace(source)
                && !string.IsNullOrWhiteSpace(token)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetExceptionMessage(Exception ex)
        {
            if (ex == null)
            {
                return "unknown error";
            }

            return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
        }

        private sealed class YokoNakagoSpec
        {
            public YokoNakagoSpec(
                string kai,
                string xName,
                string yName,
                string columnCode,
                double widthMm,
                double depthMm,
                double mainDiaMm,
                double hoopDiaMm,
                string diameter,
                double diameterMm,
                string shape,
                string material,
                double pitchMm,
                int count,
                double coverTopMm,
                double coverBottomMm,
                double coverLeftMm,
                double coverRightMm,
                Dictionary<int, int> customPositions,
                Dictionary<int, bool> directions,
                bool _,
                XYZ point,
                Level baseLevel,
                Level topLevel)
            {
                Kai = kai;
                XName = xName;
                YName = yName;
                ColumnCode = columnCode;
                WidthMm = widthMm;
                DepthMm = depthMm;
                MainDiaMm = mainDiaMm;
                HoopDiaMm = hoopDiaMm;
                Diameter = diameter;
                DiameterMm = diameterMm;
                Shape = shape;
                Material = material;
                PitchMm = pitchMm;
                Count = count;
                CoverTopMm = coverTopMm;
                CoverBottomMm = coverBottomMm;
                CoverLeftMm = coverLeftMm;
                CoverRightMm = coverRightMm;
                CustomPositions = customPositions ?? new Dictionary<int, int>();
                Directions = directions ?? new Dictionary<int, bool>();
                SectionName = ColumnYokoNakagoCommand.SectionName;
                Point = point;
                BaseLevel = baseLevel;
                TopLevel = topLevel;
            }

            public string Kai { get; }
            public string XName { get; }
            public string YName { get; }
            public string ColumnCode { get; }
            public double WidthMm { get; }
            public double DepthMm { get; }
            public double MainDiaMm { get; }
            public double HoopDiaMm { get; }
            public string Diameter { get; }
            public double DiameterMm { get; }
            public string Shape { get; }
            public string Material { get; }
            public double PitchMm { get; }
            public int Count { get; }
            public double CoverTopMm { get; }
            public double CoverBottomMm { get; }
            public double CoverLeftMm { get; }
            public double CoverRightMm { get; }
            public Dictionary<int, int> CustomPositions { get; }
            public Dictionary<int, bool> Directions { get; }
            public string SectionName { get; }
            public XYZ Point { get; }
            public Level BaseLevel { get; }
            public Level TopLevel { get; }
        }

        private sealed class YokoNakagoBarData
        {
            public YokoNakagoBarData(
                int index,
                UV startMm,
                UV endMm,
                string startHook,
                string endHook,
                RebarHookOrientation startHookOrientation,
                RebarHookOrientation endHookOrientation)
            {
                Index = index;
                StartMm = startMm;
                EndMm = endMm;
                StartHook = startHook;
                EndHook = endHook;
                StartHookOrientation = startHookOrientation;
                EndHookOrientation = endHookOrientation;
            }

            public int Index { get; }
            public UV StartMm { get; }
            public UV EndMm { get; }
            public string StartHook { get; }
            public string EndHook { get; }
            public RebarHookOrientation StartHookOrientation { get; }
            public RebarHookOrientation EndHookOrientation { get; }

            public bool RequiresMissingHook(RebarHookType startHookType, RebarHookType endHookType)
            {
                return StartHook != "0" && startHookType == null
                    || EndHook != "0" && endHookType == null;
            }
        }

        private sealed class HookPair
        {
            public HookPair(string start, string end)
            {
                Start = start;
                End = end;
            }

            public string Start { get; }
            public string End { get; }
        }

        private sealed class HookTypes
        {
            public RebarHookType Hook180 { get; set; }
            public RebarHookType Hook135 { get; set; }
            public RebarHookType Hook90 { get; set; }

            public RebarHookType Resolve(string hookCode)
            {
                switch (hookCode)
                {
                    case "180":
                        return Hook180;
                    case "135":
                        return Hook135;
                    case "90":
                        return Hook90;
                    default:
                        return null;
                }
            }
        }
    }
}
