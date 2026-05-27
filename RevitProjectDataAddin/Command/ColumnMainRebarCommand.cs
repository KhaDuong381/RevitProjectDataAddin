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
    public class ColumnMainRebarCommand : IExternalCommand
    {
        private static void ShowTaskDialog(string title, string message)
        {
            TaskDialog.Show(title, message);
        }

        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double RebarVerticalShiftMillimeters = 1000.0;
        private const double LocationToleranceFeet = 1e-4;
        private const string SectionName = "柱頭";
        private const string RebarCommentPrefix = "COLUMN_MAIN_REBAR ";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                ShowTaskDialog("Column Main Rebar", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                ShowTaskDialog("Column Main Rebar", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                ShowTaskDialog("Column Main Rebar", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(projectData.Kihon?.NameX?.Select(axis => axis?.Name));
            List<string> yNames = GetAxisNames(projectData.Kihon?.NameY?.Select(axis => axis?.Name));
            List<string> kaiNames = GetAxisNames(projectData.Kihon?.NameKai?.Select(kai => kai?.Name));
            if (xNames == null || yNames == null || kaiNames == null || kaiNames.Count < 2)
            {
                ShowTaskDialog("Column Main Rebar", "ProjectData Kihon axis data is missing or invalid.");
                return Result.Cancelled;
            }

            柱配置図 columnLayout;
            string layoutError;
            if (!TryGetColumnLayout(projectData, out columnLayout, out layoutError))
            {
                ShowTaskDialog("Column Main Rebar", layoutError);
                return Result.Cancelled;
            }

            Dictionary<string, Grid> gridsByName;
            string gridError;
            if (!TryCollectGridsByName(doc, out gridsByName, out gridError))
            {
                ShowTaskDialog("Column Main Rebar", gridError);
                return Result.Cancelled;
            }

            Dictionary<string, Level> levelsByName;
            string levelError;
            if (!TryCollectLevelsByName(doc, out levelsByName, out levelError))
            {
                ShowTaskDialog("Column Main Rebar", levelError);
                return Result.Cancelled;
            }

            List<string> warnings = new List<string>();
            List<ColumnMainRebarSpec> specs = BuildSpecs(projectData, columnLayout, kaiNames, yNames, xNames, gridsByName, levelsByName, warnings);
            if (specs.Count == 0)
            {
                ShowTaskDialog("Column Main Rebar", "No valid column main rebar specs were found."
                    + (warnings.Count > 0 ? "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20)) : string.Empty));
                return Result.Cancelled;
            }

            Dictionary<string, FamilyInstance> existingColumnsByKey = CollectExistingColumnsByKey(doc);
            Dictionary<string, RebarBarType> barTypeCache = new Dictionary<string, RebarBarType>(StringComparer.OrdinalIgnoreCase);
            int created = 0;
            List<string> failed = new List<string>();

            using (Transaction tx = new Transaction(doc, "Create Column Main Rebar"))
            {
                tx.Start();
                DeleteExistingMainRebars(doc, SectionName);

                foreach (ColumnMainRebarSpec spec in specs)
                {
                    try
                    {
                        FamilyInstance hostColumn;
                        string columnKey = BuildColumnKey(spec.Point, spec.BaseLevel.Id, spec.TopLevel.Id);
                        if (!existingColumnsByKey.TryGetValue(columnKey, out hostColumn))
                        {
                            warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: host column was not found in the model.");
                            continue;
                        }

                        if (!HasAnyRebarGeometry(spec))
                        {
                            warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: no main/core rebar count was greater than zero.");
                            continue;
                        }

                        created += CreateMainRebarsForSpec(doc, hostColumn, spec, barTypeCache, warnings);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            string result = $"Created column main rebar count: {created}";
            if (warnings.Count > 0)
            {
                result += "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20));
            }

            if (failed.Count > 0)
            {
                result += "\n\nFailed:\n" + string.Join("\n", failed.Take(10));
            }

            if (!ProjectManager.SuppressColumnCompletionDialogs || created <= 0 || warnings.Count > 0)
            {
                ShowTaskDialog("Column Main Rebar", result);
            }
            return Result.Succeeded;
        }

        private static List<ColumnMainRebarSpec> BuildSpecs(
            ProjectData projectData,
            柱配置図 columnLayout,
            List<string> kaiNames,
            List<string> yNames,
            List<string> xNames,
            Dictionary<string, Grid> gridsByName,
            Dictionary<string, Level> levelsByName,
            List<string> warnings)
        {
            List<ColumnMainRebarSpec> specs = new List<ColumnMainRebarSpec>();
            Dictionary<string, 柱リスト> floorListsByKai = (projectData?.リスト?.柱リスト ?? new ObservableCollection<柱リスト>())
                .Where(list => list != null && !string.IsNullOrWhiteSpace(list.各階))
                .GroupBy(list => list.各階, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            for (int kaiIndex = 0; kaiIndex < kaiNames.Count - 1; kaiIndex++)
            {
                string kaiName = kaiNames[kaiIndex];
                string topKaiName = kaiNames[kaiIndex + 1];

                Level baseLevel;
                Level topLevel;
                if (!levelsByName.TryGetValue(kaiName, out baseLevel) || !levelsByName.TryGetValue(topKaiName, out topLevel))
                {
                    warnings.Add($"Level pair '{kaiName}'-'{topKaiName}' was not found in the model.");
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
                    ObservableCollection<柱セグメント> segments;
                    string mapKey = $"{kaiName}::{yName}";
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
                    for (int xIndex = 0; xIndex < maxIndex; xIndex++)
                    {
                        柱セグメント segment = segments[xIndex];
                        string columnCode = segment?.柱の符号?.Trim();
                        if (string.IsNullOrWhiteSpace(columnCode))
                        {
                            continue;
                        }

                        柱 columnData = floorColumnList.柱.FirstOrDefault(column =>
                            column != null && string.Equals(column.Name?.Trim(), columnCode, StringComparison.OrdinalIgnoreCase));
                        if (columnData == null)
                        {
                            warnings.Add($"{kaiName} {yName}-{xNames[xIndex]} {columnCode}: column data was not found.");
                            continue;
                        }

                        Grid xGrid;
                        string xName = xNames[xIndex];
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

                        ColumnMainRebarSpec spec;
                        string specError;
                        if (!TryBuildSpec(kaiName, xName, yName, columnCode, columnData, adjustedPoint, baseLevel, topLevel, out spec, out specError))
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

        private static bool TryBuildSpec(
            string kaiName,
            string xName,
            string yName,
            string columnCode,
            柱 columnData,
            XYZ point,
            Level baseLevel,
            Level topLevel,
            out ColumnMainRebarSpec spec,
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
            double depthMm;
            double mainDiaMm;
            double coreDiaMm;
            double hoopDiaMm;
            int topCount;
            int bottomCount;
            int leftCount;
            int rightCount;
            int coreTopCount;
            int coreBottomCount;
            int coreLeftCount;
            int coreRightCount;
            double coverTopMm;
            double coverBottomMm;
            double coverLeftMm;
            double coverRightMm;

            string widthText = GetSectionProperty(layout, SectionName, "柱幅", "幅");
            string depthText = GetSectionProperty(layout, SectionName, "柱成", "成");
            string mainDiaText = GetSectionProperty(layout, SectionName, "主筋径");
            string coreDiaText = GetSectionProperty(layout, SectionName, "芯筋径");
            string hoopDiaText = GetSectionProperty(layout, SectionName, "HOOP径");
            if (!TryParseMillimeters(widthText, out widthMm) || widthMm <= 0.0
                || !TryParseMillimeters(depthText, out depthMm) || depthMm <= 0.0
                || !TryParseMillimeters(mainDiaText, out mainDiaMm) || mainDiaMm <= 0.0
                || !TryParseMillimeters(coreDiaText, out coreDiaMm) || coreDiaMm <= 0.0
                || !TryParseMillimeters(hoopDiaText, out hoopDiaMm) || hoopDiaMm <= 0.0)
            {
                errorMessage = "column size, 主筋径, 芯筋径, or HOOP径 is invalid.";
                return false;
            }

            if (!TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "上側主筋本数"), out topCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "下側主筋本数"), out bottomCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "左側主筋本数"), out leftCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "右側主筋本数"), out rightCount))
            {
                errorMessage = "main rebar count is invalid.";
                return false;
            }

            if (!TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "上側芯筋本数"), out coreTopCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "下側芯筋本数"), out coreBottomCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "左側芯筋本数"), out coreLeftCount)
                || !TryParseNonNegativeInt(GetSectionProperty(layout, SectionName, "右側芯筋本数"), out coreRightCount))
            {
                errorMessage = "core rebar count is invalid.";
                return false;
            }

            if (!TryParseMillimeters(sectionData.上, out coverTopMm) || coverTopMm < 0.0
                || !TryParseMillimeters(sectionData.下, out coverBottomMm) || coverBottomMm < 0.0
                || !TryParseMillimeters(sectionData.左, out coverLeftMm) || coverLeftMm < 0.0
                || !TryParseMillimeters(sectionData.右, out coverRightMm) || coverRightMm < 0.0)
            {
                errorMessage = "cover data is invalid.";
                return false;
            }

            spec = new ColumnMainRebarSpec(
                kaiName,
                xName,
                yName,
                columnCode,
                widthMm,
                depthMm,
                mainDiaText?.Trim() ?? string.Empty,
                mainDiaMm,
                GetSectionProperty(layout, SectionName, "主筋材質")?.Trim() ?? string.Empty,
                coreDiaText?.Trim() ?? string.Empty,
                coreDiaMm,
                GetSectionProperty(layout, SectionName, "芯筋材質")?.Trim() ?? string.Empty,
                hoopDiaMm,
                topCount,
                bottomCount,
                leftCount,
                rightCount,
                coreTopCount,
                coreBottomCount,
                coreLeftCount,
                coreRightCount,
                coverTopMm,
                coverBottomMm,
                coverLeftMm,
                coverRightMm,
                sectionData,
                point,
                baseLevel,
                topLevel);

            return true;
        }

        private static int CreateMainRebarsForSpec(
            Document doc,
            FamilyInstance column,
            ColumnMainRebarSpec spec,
            Dictionary<string, RebarBarType> barTypeCache,
            List<string> warnings)
        {
            XYZ center = GetColumnCenter(column) ?? spec.Point;
            List<MainRebarPoint> points = BuildMainRebarPoints(spec);
            if (points.Count == 0)
            {
                warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: no main/core rebar coordinates were generated.");
                return 0;
            }

            double minZ;
            double maxZ;
            if (!TryGetHostVerticalRange(column, out minZ, out maxZ))
            {
                throw new InvalidOperationException("Could not resolve host column vertical range.");
            }

            double shiftFt = RebarVerticalShiftMillimeters * FeetPerMillimeter;
            double startZ = minZ + spec.CoverBottomMm * FeetPerMillimeter + shiftFt;
            double endZ = maxZ - spec.CoverTopMm * FeetPerMillimeter + shiftFt;
            if (endZ <= startZ)
            {
                warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: host column height is too small for main rebar.");
                return 0;
            }

            int created = 0;
            foreach (MainRebarPoint point in points)
            {
                RebarBarType barType = GetOrFindRebarBarType(doc, point.DiameterText, point.Material, barTypeCache);
                if (barType == null)
                {
                    warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: no RebarBarType matched {point.KindLabel}径 '{point.DiameterText}'.");
                    continue;
                }

                XYZ start = ConvertCanvasPointToModelPoint(point.XMm, point.YMm, center, spec.DepthMm, startZ);
                XYZ end = ConvertCanvasPointToModelPoint(point.XMm, point.YMm, center, spec.DepthMm, endZ);
                Rebar rebar = Rebar.CreateFromCurves(
                    doc,
                    RebarStyle.Standard,
                    barType,
                    null,
                    null,
                    column,
                    XYZ.BasisX,
                    new List<Curve> { Line.CreateBound(start, end) },
                    RebarHookOrientation.Left,
                    RebarHookOrientation.Left,
                    true,
                    true);

                if (rebar == null)
                {
                    continue;
                }

                Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comments != null && !comments.IsReadOnly)
                {
                    comments.Set($"{RebarCommentPrefix}{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode} {SectionName} Kind={point.KindLabel} D={point.DiameterText} Pos={point.Label}");
                }

                created++;
            }

            return created;
        }

        private static List<MainRebarPoint> BuildMainRebarPoints(ColumnMainRebarSpec spec)
        {
            List<MainRebarPoint> points = new List<MainRebarPoint>();
            double left = -spec.WidthMm / 2.0;
            double right = spec.WidthMm / 2.0;
            double top = 0.0;
            double bottom = spec.DepthMm;
            double actualHoopDia = GetActualBarDiameter(spec.HoopDiaMm);
            double actualMainDia = GetActualBarDiameter(spec.MainDiaMm);
            double actualCoreDia = GetActualBarDiameter(spec.CoreDiaMm);
            double cornerRadius = spec.HoopDiaMm * 2.0;

            double cxRightTop = right - spec.CoverRightMm - actualHoopDia - cornerRadius;
            double cyRightTop = top + spec.CoverTopMm + actualHoopDia + cornerRadius;
            double cxLeftTop = left + spec.CoverLeftMm + actualHoopDia + cornerRadius;
            double cyLeftTop = top + spec.CoverTopMm + actualHoopDia + cornerRadius;
            double cxRightBottom = right - spec.CoverRightMm - actualHoopDia - cornerRadius;
            double cyRightBottom = bottom - spec.CoverBottomMm - actualHoopDia - cornerRadius;
            double cxLeftBottom = left + spec.CoverLeftMm + actualHoopDia + cornerRadius;
            double cyLeftBottom = bottom - spec.CoverBottomMm - actualHoopDia - cornerRadius;

            double distance = cornerRadius - actualMainDia / 2.0;
            double angle = 315.0 * Math.PI / 180.0;

            double xRightTop = cxRightTop + distance * Math.Cos(angle);
            double yRightTop = cyRightTop + distance * Math.Sin(angle);
            double xLeftTop = cxLeftTop - distance * Math.Cos(angle);
            double yLeftTop = cyLeftTop + distance * Math.Sin(angle);
            double xRightBottom = cxRightBottom + distance * Math.Cos(angle);
            double yRightBottom = cyRightBottom - distance * Math.Sin(angle);
            double xLeftBottom = cxLeftBottom - distance * Math.Cos(angle);
            double yLeftBottom = cyLeftBottom - distance * Math.Sin(angle);

            AddUniquePoint(points, xLeftTop, yLeftTop, "corner-left-top", spec.MainDia, spec.Material, "主筋");
            AddUniquePoint(points, xRightTop, yRightTop, "corner-right-top", spec.MainDia, spec.Material, "主筋");
            AddUniquePoint(points, xLeftBottom, yLeftBottom, "corner-left-bottom", spec.MainDia, spec.Material, "主筋");
            AddUniquePoint(points, xRightBottom, yRightBottom, "corner-right-bottom", spec.MainDia, spec.Material, "主筋");

            List<double> topXs = new List<double> { left, xLeftTop };
            double spacingTop = (xRightTop - xLeftTop) / (spec.TopCount + 1);
            double yTopRebar = top + spec.CoverTopMm + actualHoopDia + actualMainDia / 2.0;
            for (int i = 0; i < spec.TopCount; i++)
            {
                double x = xLeftTop + spacingTop * (i + 1) + GetOffset(spec.Data.左右Offsets, i);
                double y = yTopRebar + GetOffset(spec.Data.上下Offsets, i);
                topXs.Add(x);
                AddUniquePoint(points, x, y, $"top-{i + 1}", spec.MainDia, spec.Material, "主筋");
            }

            topXs.Add(xRightTop);
            topXs.Add(right);

            List<double> bottomXs = new List<double> { left, xLeftBottom };
            double spacingBottom = (xRightBottom - xLeftBottom) / (spec.BottomCount + 1);
            for (int i = 0; i < spec.BottomCount; i++)
            {
                bottomXs.Add(xLeftBottom + spacingBottom * (i + 1));
            }

            bottomXs.Add(xRightBottom);
            bottomXs.Add(right);

            List<double> adjustedBottomXs = AswapB(topXs, bottomXs);
            double yBottomRebar = bottom - spec.CoverBottomMm - actualHoopDia - actualMainDia / 2.0;
            for (int i = 0; i < adjustedBottomXs.Count; i++)
            {
                double x = adjustedBottomXs[i] + GetOffset(spec.Data.左右Offsets, i) + GetOffset(spec.Data.左右1Offsets, i);
                double y = yBottomRebar + GetOffset(spec.Data.上下1Offsets, i);
                AddUniquePoint(points, x, y, $"bottom-{i + 1}", spec.MainDia, spec.Material, "主筋");
            }

            List<double> leftYs = new List<double> { top, yLeftTop };
            double spacingLeft = (yLeftBottom - yLeftTop) / (spec.LeftCount + 1);
            double xLeftRebar = left + spec.CoverLeftMm + actualHoopDia + actualMainDia / 2.0;
            for (int i = 0; i < spec.LeftCount; i++)
            {
                double y = yLeftTop + spacingLeft * (i + 1) + GetOffset(spec.Data.上下2Offsets, i);
                double x = xLeftRebar + GetOffset(spec.Data.左右2Offsets, i);
                leftYs.Add(y);
                AddUniquePoint(points, x, y, $"left-{i + 1}", spec.MainDia, spec.Material, "主筋");
            }

            leftYs.Add(yLeftBottom);
            leftYs.Add(bottom);

            List<double> rightYs = new List<double> { top, yRightTop };
            double spacingRight = (yRightBottom - yRightTop) / (spec.RightCount + 1);
            for (int i = 0; i < spec.RightCount; i++)
            {
                rightYs.Add(yRightTop + spacingRight * (i + 1));
            }

            rightYs.Add(yRightBottom);
            rightYs.Add(bottom);

            List<double> adjustedRightYs = AswapB(leftYs, rightYs);
            double xRightRebar = right - spec.CoverRightMm - actualHoopDia - actualMainDia / 2.0;
            for (int i = 0; i < adjustedRightYs.Count; i++)
            {
                double x = xRightRebar + GetOffset(spec.Data.左右3Offsets, i);
                double y = adjustedRightYs[i] + GetOffset(spec.Data.上下2Offsets, i) + GetOffset(spec.Data.上下3Offsets, i);
                AddUniquePoint(points, x, y, $"right-{i + 1}", spec.MainDia, spec.Material, "主筋");
            }

            if (actualCoreDia > 0.0
                && (spec.CoreTopCount > 0 || spec.CoreBottomCount > 0 || spec.CoreLeftCount > 0 || spec.CoreRightCount > 0))
            {
                double xCoreLeftTop = -(xRightTop - xLeftTop) / 4.0;
                double yCoreLeftTop = (yLeftBottom - yLeftTop) / 4.0 + yLeftTop;
                double xCoreRightTop = (xRightTop - xLeftTop) / 4.0;
                double yCoreRightTop = yCoreLeftTop;
                double xCoreLeftBottom = xCoreLeftTop;
                double yCoreLeftBottom = bottom - yCoreLeftTop;
                double xCoreRightBottom = xCoreRightTop;
                double yCoreRightBottom = yCoreLeftBottom;

                AddUniquePoint(points, xCoreLeftTop, yCoreLeftTop, "core-corner-left-top", spec.CoreDia, spec.CoreMaterial, "芯筋");
                AddUniquePoint(points, xCoreRightTop, yCoreRightTop, "core-corner-right-top", spec.CoreDia, spec.CoreMaterial, "芯筋");
                AddUniquePoint(points, xCoreLeftBottom, yCoreLeftBottom, "core-corner-left-bottom", spec.CoreDia, spec.CoreMaterial, "芯筋");
                AddUniquePoint(points, xCoreRightBottom, yCoreRightBottom, "core-corner-right-bottom", spec.CoreDia, spec.CoreMaterial, "芯筋");

                double spacingCoreTop = (xCoreRightTop - xCoreLeftTop) / (spec.CoreTopCount + 1);
                for (int i = 0; i < spec.CoreTopCount; i++)
                {
                    double x = xCoreLeftTop + spacingCoreTop * (i + 1) + GetOffset(spec.Data.左右4Offsets, i);
                    double y = yCoreLeftTop + GetOffset(spec.Data.上下4Offsets, i);
                    AddUniquePoint(points, x, y, $"core-top-{i + 1}", spec.CoreDia, spec.CoreMaterial, "芯筋");
                }

                double spacingCoreBottom = (xCoreRightBottom - xCoreLeftBottom) / (spec.CoreBottomCount + 1);
                for (int i = 0; i < spec.CoreBottomCount; i++)
                {
                    double x = xCoreLeftBottom + spacingCoreBottom * (i + 1) + GetOffset(spec.Data.左右5Offsets, i);
                    double y = yCoreLeftBottom + GetOffset(spec.Data.上下5Offsets, i);
                    AddUniquePoint(points, x, y, $"core-bottom-{i + 1}", spec.CoreDia, spec.CoreMaterial, "芯筋");
                }

                double spacingCoreLeft = (yCoreLeftBottom - yCoreLeftTop) / (spec.CoreLeftCount + 1);
                for (int i = 0; i < spec.CoreLeftCount; i++)
                {
                    double x = xCoreLeftTop + GetOffset(spec.Data.左右6Offsets, i);
                    double y = yCoreLeftTop + spacingCoreLeft * (i + 1) + GetOffset(spec.Data.上下6Offsets, i);
                    AddUniquePoint(points, x, y, $"core-left-{i + 1}", spec.CoreDia, spec.CoreMaterial, "芯筋");
                }

                double spacingCoreRight = (yCoreRightBottom - yCoreRightTop) / (spec.CoreRightCount + 1);
                for (int i = 0; i < spec.CoreRightCount; i++)
                {
                    double x = xCoreRightTop + GetOffset(spec.Data.左右7Offsets, i);
                    double y = yCoreRightTop + spacingCoreRight * (i + 1) + GetOffset(spec.Data.上下7Offsets, i);
                    AddUniquePoint(points, x, y, $"core-right-{i + 1}", spec.CoreDia, spec.CoreMaterial, "芯筋");
                }
            }

            return points;
        }

        private static void AddUniquePoint(List<MainRebarPoint> points, double x, double y, string label, string diameterText, string material, string kindLabel)
        {
            if (points.Any(point => Math.Abs(point.XMm - x) < 0.001 && Math.Abs(point.YMm - y) < 0.001))
            {
                return;
            }

            points.Add(new MainRebarPoint(x, y, label, diameterText, material, kindLabel));
        }

        private static List<double> AswapB(List<double> a, List<double> b)
        {
            List<double> listA = a.Skip(2).Take(Math.Max(0, a.Count - 4)).ToList();
            List<double> listB = b.Skip(2).Take(Math.Max(0, b.Count - 4)).ToList();
            int countA = listA.Count;
            int countB = listB.Count;
            if (countA <= countB)
            {
                return listB;
            }

            List<double> result = new List<double>();
            if (countB == 0)
            {
                return result;
            }

            if (countA % 2 == 1 && countB % 2 == 1)
            {
                int numSides = (countB - 1) / 2;
                result.AddRange(listA.Take(numSides));
                result.Add(listA[countA / 2]);
                result.AddRange(listA.Skip(countA - numSides));
            }
            else if (countA % 2 == 1 && countB % 2 == 0)
            {
                int numSides = countB / 2;
                result.AddRange(listA.Take(numSides));
                result.AddRange(listA.Skip(countA - numSides));
            }
            else if (countA % 2 == 0 && countB % 2 == 1)
            {
                int numSides = (countB - 1) / 2;
                result.AddRange(listA.Take(numSides));
                result.Add(listA[countA / 2 - 1]);
                result.AddRange(listA.Skip(countA - numSides));
            }
            else
            {
                int numSides = countB / 2;
                result.AddRange(listA.Take(numSides));
                result.AddRange(listA.Skip(countA - numSides));
            }

            return result;
        }

        private static double GetOffset(Dictionary<int, string> offsets, int index)
        {
            string value;
            double parsed;
            return offsets != null && offsets.TryGetValue(index, out value) && TryParseMillimeters(value, out parsed) ? parsed : 0.0;
        }

        private static XYZ ConvertCanvasPointToModelPoint(double xMm, double yMm, XYZ center, double depthMm, double z)
        {
            return new XYZ(
                center.X + xMm * FeetPerMillimeter,
                center.Y + (depthMm / 2.0 - yMm) * FeetPerMillimeter,
                z);
        }

        private static RebarBarType GetOrFindRebarBarType(Document doc, ColumnMainRebarSpec spec, Dictionary<string, RebarBarType> cache)
        {
            return GetOrFindRebarBarType(doc, spec.MainDia, spec.Material, cache);
        }

        private static RebarBarType GetOrFindRebarBarType(Document doc, string diameterText, string material, Dictionary<string, RebarBarType> cache)
        {
            string cacheKey = $"{diameterText}|{material}";
            RebarBarType barType;
            if (cache.TryGetValue(cacheKey, out barType))
            {
                return barType;
            }

            string diameterToken = ExtractDigits(diameterText);
            string materialToken = material?.Trim();
            barType = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .FirstOrDefault(type =>
                    NameContains(type.Name, diameterToken)
                    && (string.IsNullOrWhiteSpace(materialToken) || NameContains(type.Name, materialToken)));
            if (barType == null)
            {
                barType = new FilteredElementCollector(doc)
                    .OfClass(typeof(RebarBarType))
                    .Cast<RebarBarType>()
                    .FirstOrDefault(type => NameContains(type.Name, diameterToken));
            }

            cache[cacheKey] = barType;
            return barType;
        }

        private static void DeleteExistingMainRebars(Document doc, string sectionName)
        {
            List<ElementId> idsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(rebar =>
                {
                    Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    string value = comments?.AsString();
                    return !string.IsNullOrWhiteSpace(value)
                        && value.StartsWith(RebarCommentPrefix, StringComparison.Ordinal)
                        && value.IndexOf(sectionName, StringComparison.OrdinalIgnoreCase) >= 0;
                })
                .Select(rebar => rebar.Id)
                .ToList();

            if (idsToDelete.Count > 0)
            {
                doc.Delete(idsToDelete);
            }
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
                if (TryGetColumnKey(column, out columnKey) && !columnsByKey.ContainsKey(columnKey))
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

        private static bool TryGetHostVerticalRange(FamilyInstance column, out double minZ, out double maxZ)
        {
            minZ = 0.0;
            maxZ = 0.0;
            Level baseLevel = column.Document.GetElement(GetElementIdParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_PARAM, BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)) as Level;
            Level topLevel = column.Document.GetElement(GetElementIdParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_PARAM, BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)) as Level;
            if (baseLevel != null && topLevel != null)
            {
                minZ = baseLevel.Elevation + GetFirstDoubleParameterValue(column, BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM, BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM);
                maxZ = topLevel.Elevation + GetFirstDoubleParameterValue(column, BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM, BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM);
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

        private static XYZ GetColumnCenter(FamilyInstance column)
        {
            LocationPoint locationPoint = column?.Location as LocationPoint;
            return locationPoint?.Point;
        }

        private static bool TryGetColumnLayout(ProjectData projectData, out 柱配置図 columnLayout, out string errorMessage)
        {
            columnLayout = projectData?.Haichi?.柱配置図?.FirstOrDefault();
            errorMessage = columnLayout == null ? "ProjectData.Haichi.柱配置図 is empty." : null;
            return columnLayout != null;
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
            PropertyInfo property = obj?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            return property != null && property.PropertyType == typeof(string) ? property.GetValue(obj) as string : null;
        }

        private static bool TryGetColumnPlacementOffsetFromSegment(柱セグメント segment, out double offsetXmm, out double offsetYmm, out string errorMessage)
        {
            offsetXmm = 0.0;
            offsetYmm = 0.0;
            errorMessage = null;
            if (!TryParseOptionalMillimeters(segment?.左側のズレ, out double leftOffset)
                || !TryParseOptionalMillimeters(segment?.右側のズレ, out double rightOffset)
                || !TryParseOptionalMillimeters(segment?.上側のズレ, out double topOffset)
                || !TryParseOptionalMillimeters(segment?.下側のズレ, out double bottomOffset))
            {
                errorMessage = "column placement offset is invalid.";
                return false;
            }

            offsetXmm = rightOffset - leftOffset;
            offsetYmm = topOffset - bottomOffset;
            return true;
        }

        private static bool TryCollectGridsByName(Document doc, out Dictionary<string, Grid> gridsByName, out string errorMessage)
        {
            gridsByName = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .Cast<Grid>()
                .Where(grid => !string.IsNullOrWhiteSpace(grid.Name))
                .GroupBy(grid => grid.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            errorMessage = gridsByName.Count == 0 ? "No grids were found in the active document." : null;
            return gridsByName.Count > 0;
        }

        private static bool TryCollectLevelsByName(Document doc, out Dictionary<string, Level> levelsByName, out string errorMessage)
        {
            levelsByName = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Where(level => !string.IsNullOrWhiteSpace(level.Name))
                .GroupBy(level => level.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            errorMessage = levelsByName.Count == 0 ? "No levels were found in the active document." : null;
            return levelsByName.Count > 0;
        }

        private static bool TryGetGridIntersectionPoint(Grid xGrid, Grid yGrid, out XYZ point)
        {
            point = null;
            Line xLine = xGrid?.Curve as Line;
            Line yLine = yGrid?.Curve as Line;
            if (xLine == null || yLine == null)
            {
                return false;
            }

            XYZ p = xLine.GetEndPoint(0);
            XYZ r = xLine.GetEndPoint(1) - xLine.GetEndPoint(0);
            XYZ q = yLine.GetEndPoint(0);
            XYZ s = yLine.GetEndPoint(1) - yLine.GetEndPoint(0);
            double cross = r.X * s.Y - r.Y * s.X;
            if (Math.Abs(cross) < 1e-9)
            {
                return false;
            }

            XYZ qp = q - p;
            double t = (qp.X * s.Y - qp.Y * s.X) / cross;
            point = p + t * r;
            return true;
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

        private static bool TryParseOptionalMillimeters(string input, out double valueMm)
        {
            valueMm = 0.0;
            return string.IsNullOrWhiteSpace(input) || TryParseMillimeters(input, out valueMm);
        }

        private static bool TryParseMillimeters(string input, out double valueMm)
        {
            valueMm = 0.0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string normalized = ExtractNumberText(input);
            return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out valueMm)
                || double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out valueMm);
        }

        private static bool TryParseNonNegativeInt(string input, out int value)
        {
            value = 0;
            double parsed;
            if (!TryParseMillimeters(input, out parsed))
            {
                return false;
            }

            value = (int)Math.Round(parsed);
            return value >= 0;
        }

        private static bool HasAnyRebarGeometry(ColumnMainRebarSpec spec)
        {
            return spec.TopCount > 0
                || spec.BottomCount > 0
                || spec.LeftCount > 0
                || spec.RightCount > 0
                || spec.CoreTopCount > 0
                || spec.CoreBottomCount > 0
                || spec.CoreLeftCount > 0
                || spec.CoreRightCount > 0;
        }

        private static string ExtractDigits(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? string.Empty : new string(input.Where(char.IsDigit).ToArray());
        }

        private static string ExtractNumberText(string input)
        {
            string text = input.Trim().Replace(",", string.Empty);
            return new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray());
        }

        private static List<string> GetAxisNames(IEnumerable<string> names)
        {
            if (names == null)
            {
                return null;
            }

            List<string> normalizedNames = names.Select(name => name?.Trim()).ToList();
            return normalizedNames.Count == 0 || normalizedNames.Any(string.IsNullOrWhiteSpace) ? null : normalizedNames;
        }

        private static bool NameContains(string source, string token)
        {
            return !string.IsNullOrWhiteSpace(source)
                && !string.IsNullOrWhiteSpace(token)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class ColumnMainRebarSpec
        {
            public ColumnMainRebarSpec(
                string kai,
                string xName,
                string yName,
                string columnCode,
                double widthMm,
                double depthMm,
                string mainDia,
                double mainDiaMm,
                string material,
                string coreDia,
                double coreDiaMm,
                string coreMaterial,
                double hoopDiaMm,
                int topCount,
                int bottomCount,
                int leftCount,
                int rightCount,
                int coreTopCount,
                int coreBottomCount,
                int coreLeftCount,
                int coreRightCount,
                double coverTopMm,
                double coverBottomMm,
                double coverLeftMm,
                double coverRightMm,
                GridBotDataHashira data,
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
                MainDia = mainDia;
                MainDiaMm = mainDiaMm;
                Material = material;
                CoreDia = coreDia;
                CoreDiaMm = coreDiaMm;
                CoreMaterial = coreMaterial;
                HoopDiaMm = hoopDiaMm;
                TopCount = topCount;
                BottomCount = bottomCount;
                LeftCount = leftCount;
                RightCount = rightCount;
                CoreTopCount = coreTopCount;
                CoreBottomCount = coreBottomCount;
                CoreLeftCount = coreLeftCount;
                CoreRightCount = coreRightCount;
                CoverTopMm = coverTopMm;
                CoverBottomMm = coverBottomMm;
                CoverLeftMm = coverLeftMm;
                CoverRightMm = coverRightMm;
                Data = data;
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
            public string MainDia { get; }
            public double MainDiaMm { get; }
            public string Material { get; }
            public string CoreDia { get; }
            public double CoreDiaMm { get; }
            public string CoreMaterial { get; }
            public double HoopDiaMm { get; }
            public int TopCount { get; }
            public int BottomCount { get; }
            public int LeftCount { get; }
            public int RightCount { get; }
            public int CoreTopCount { get; }
            public int CoreBottomCount { get; }
            public int CoreLeftCount { get; }
            public int CoreRightCount { get; }
            public double CoverTopMm { get; }
            public double CoverBottomMm { get; }
            public double CoverLeftMm { get; }
            public double CoverRightMm { get; }
            public GridBotDataHashira Data { get; }
            public XYZ Point { get; }
            public Level BaseLevel { get; }
            public Level TopLevel { get; }
        }

        private sealed class MainRebarPoint
        {
            public MainRebarPoint(double xMm, double yMm, string label, string diameterText, string material, string kindLabel)
            {
                XMm = xMm;
                YMm = yMm;
                Label = label;
                DiameterText = diameterText;
                Material = material;
                KindLabel = kindLabel;
            }

            public double XMm { get; }
            public double YMm { get; }
            public string Label { get; }
            public string DiameterText { get; }
            public string Material { get; }
            public string KindLabel { get; }
        }
    }
}
