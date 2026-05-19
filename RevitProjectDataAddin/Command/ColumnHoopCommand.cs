using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnHoopCommand : IExternalCommand
    {
        private const double FeetPerMillimeter = 1.0 / 304.8;
        private const double LocationToleranceFeet = 1e-4;
        private const string TargetSectionName = "柱頭";
        private const string HookType1356DName = "DBS_HOOK_135_6D";
        private const string HookType908DName = "DBS_HOOK_90_8D";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Column HOOP", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Column HOOP", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Column HOOP", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(projectData.Kihon?.NameX?.Select(axis => axis?.Name));
            List<string> yNames = GetAxisNames(projectData.Kihon?.NameY?.Select(axis => axis?.Name));
            List<string> kaiNames = GetAxisNames(projectData.Kihon?.NameKai?.Select(kai => kai?.Name));

            if (xNames == null || yNames == null || kaiNames == null || kaiNames.Count < 2)
            {
                TaskDialog.Show("Column HOOP", "ProjectData Kihon axis data is missing or invalid.");
                return Result.Cancelled;
            }

            柱配置図 columnLayout;
            string layoutError;
            if (!TryGetColumnLayout(projectData, out columnLayout, out layoutError))
            {
                TaskDialog.Show("Column HOOP", layoutError);
                return Result.Cancelled;
            }

            Dictionary<string, Grid> gridsByName;
            string gridError;
            if (!TryCollectGridsByName(doc, out gridsByName, out gridError))
            {
                TaskDialog.Show("Column HOOP", gridError);
                return Result.Cancelled;
            }

            Dictionary<string, Level> levelsByName;
            string levelError;
            if (!TryCollectLevelsByName(doc, out levelsByName, out levelError))
            {
                TaskDialog.Show("Column HOOP", levelError);
                return Result.Cancelled;
            }

            List<string> warnings = new List<string>();
            List<ColumnHoopSpec> hoopSpecs = BuildColumnHoopSpecs(
                projectData,
                columnLayout,
                kaiNames,
                yNames,
                xNames,
                gridsByName,
                levelsByName,
                warnings);

            if (hoopSpecs.Count == 0)
            {
                string emptyResult = $"No valid {TargetSectionName} HOOP specs were found.";
                if (warnings.Count > 0)
                {
                    emptyResult += "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20));
                }

                TaskDialog.Show("Column HOOP", emptyResult);
                return Result.Cancelled;
            }

            Dictionary<string, FamilyInstance> existingColumnsByKey = CollectExistingColumnsByKey(doc);
            Dictionary<string, RebarBarType> barTypeCache = new Dictionary<string, RebarBarType>(StringComparer.OrdinalIgnoreCase);

            int created = 0;
            List<string> failed = new List<string>();

            using (Transaction tx = new Transaction(doc, "Create Column HOOP"))
            {
                tx.Start();
                DeleteExistingColumnHoops(doc, TargetSectionName);

                bool usedFallbackHookType1356D;
                RebarHookType hookType1356D = FindOrCreateHookType135_6D(doc, out usedFallbackHookType1356D);
                if (usedFallbackHookType1356D)
                {
                    warnings.Add("Could not create DBS_HOOK_135_6D; fallback hook type was used.");
                }

                bool usedFallbackHookType908D;
                RebarHookType hookType908D = FindOrCreateHookType90_8D(doc, out usedFallbackHookType908D);
                if (usedFallbackHookType908D)
                {
                    warnings.Add("Could not create DBS_HOOK_90_8D; fallback hook type was used.");
                }

                foreach (ColumnHoopSpec spec in hoopSpecs)
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
                            warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: no RebarBarType matched HOOP径 '{spec.HoopDia}'.");
                            continue;
                        }

                        created += CreateHoopsForSpec(doc, hostColumn, spec, barType, hookType1356D, hookType908D, warnings);
                    }
                    catch (Exception ex)
                    {
                        failed.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            string result = $"Created HOOP count: {created}";
            if (warnings.Count > 0)
            {
                result += "\n\nWarnings:\n" + string.Join("\n", warnings.Take(20));
            }

            if (failed.Count > 0)
            {
                result += "\n\nFailed:\n" + string.Join("\n", failed.Take(10));
            }

            TaskDialog.Show("Column HOOP", result);
            return Result.Succeeded;
        }

        private static List<ColumnHoopSpec> BuildColumnHoopSpecs(
            ProjectData projectData,
            柱配置図 columnLayout,
            List<string> kaiNames,
            List<string> yNames,
            List<string> xNames,
            Dictionary<string, Grid> gridsByName,
            Dictionary<string, Level> levelsByName,
            List<string> warnings)
        {
            List<ColumnHoopSpec> specs = new List<ColumnHoopSpec>();
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
                            warnings.Add($"{kaiName} {yName}-{xNames[xIndex]}: ColumnCode is empty.");
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

                        double leftMm;
                        double rightMm;
                        double topMm;
                        double bottomMm;
                        double offsetXmm;
                        double offsetYmm;
                        string offsetError;
                        if (!TryGetColumnPlacementOffsetsFromSegment(
                            segment,
                            out leftMm,
                            out rightMm,
                            out topMm,
                            out bottomMm,
                            out offsetXmm,
                            out offsetYmm,
                            out offsetError))
                        {
                            warnings.Add($"{kaiName} {yName}-{xName} {columnCode}: {offsetError}");
                            continue;
                        }

                        XYZ adjustedPoint = new XYZ(
                            intersectionPoint.X + offsetXmm * FeetPerMillimeter,
                            intersectionPoint.Y + offsetYmm * FeetPerMillimeter,
                            baseLevel.Elevation);

                        ColumnHoopSpec spec;
                        string specError;
                        if (!TryBuildColumnHoopSpec(
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

        private static bool TryBuildColumnHoopSpec(
            string kaiName,
            string xName,
            string yName,
            string columnCode,
            柱 columnData,
            XYZ point,
            Level baseLevel,
            Level topLevel,
            out ColumnHoopSpec spec,
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

            GridBotDataHashira sectionData = GetSectionData(layout, TargetSectionName);
            if (sectionData == null)
            {
                errorMessage = $"{TargetSectionName} data was not found.";
                return false;
            }

            double widthMm;
            string widthText = GetSectionProperty(layout, TargetSectionName, "柱幅");
            if (!TryParseMillimeters(widthText, out widthMm) || widthMm <= 0.0)
            {
                errorMessage = $"{TargetSectionName} 柱幅 is invalid: '{widthText}'.";
                return false;
            }

            double depthMm;
            string depthText = GetSectionProperty(layout, TargetSectionName, "柱成");
            if (!TryParseMillimeters(depthText, out depthMm) || depthMm <= 0.0)
            {
                errorMessage = $"{TargetSectionName} 柱成 is invalid: '{depthText}'.";
                return false;
            }

            double pitchMm;
            string pitchText = GetSectionProperty(layout, TargetSectionName, "ピッチ");
            if (!TryParseMillimeters(pitchText, out pitchMm) || pitchMm <= 0.0)
            {
                errorMessage = $"{TargetSectionName} ピッチ is invalid: '{pitchText}'.";
                return false;
            }

            double coverTopMm;
            if (!TryParseMillimeters(sectionData.上 ?? GetStringProperty(sectionData, "上"), out coverTopMm) || coverTopMm < 0.0)
            {
                errorMessage = $"{TargetSectionName} 上 cover is invalid: '{sectionData.上}'.";
                return false;
            }

            double coverBottomMm;
            if (!TryParseMillimeters(sectionData.下 ?? GetStringProperty(sectionData, "下"), out coverBottomMm) || coverBottomMm < 0.0)
            {
                errorMessage = $"{TargetSectionName} 下 cover is invalid: '{sectionData.下}'.";
                return false;
            }

            double coverLeftMm;
            if (!TryParseMillimeters(sectionData.左 ?? GetStringProperty(sectionData, "左"), out coverLeftMm) || coverLeftMm < 0.0)
            {
                errorMessage = $"{TargetSectionName} 左 cover is invalid: '{sectionData.左}'.";
                return false;
            }

            double coverRightMm;
            if (!TryParseMillimeters(sectionData.右 ?? GetStringProperty(sectionData, "右"), out coverRightMm) || coverRightMm < 0.0)
            {
                errorMessage = $"{TargetSectionName} 右 cover is invalid: '{sectionData.右}'.";
                return false;
            }

            string hoopDia = GetSectionProperty(layout, TargetSectionName, "HOOP径")?.Trim();
            if (string.IsNullOrWhiteSpace(hoopDia))
            {
                errorMessage = $"{TargetSectionName} HOOP径 is empty.";
                return false;
            }

            string hoopShape = GetSectionProperty(layout, TargetSectionName, "HOOP形")?.Trim() ?? string.Empty;
            string hoopMaterial = GetSectionProperty(layout, TargetSectionName, "HOOP材質")?.Trim() ?? string.Empty;
            string hookPosition = (sectionData.フックの位置 ?? GetStringProperty(sectionData, "フックの位置"))?.Trim() ?? string.Empty;

            spec = new ColumnHoopSpec(
                kaiName,
                xName,
                yName,
                columnCode,
                widthMm,
                depthMm,
                coverTopMm,
                coverBottomMm,
                coverLeftMm,
                coverRightMm,
                hoopDia,
                hoopShape,
                hoopMaterial,
                pitchMm,
                hookPosition,
                TargetSectionName,
                point,
                baseLevel,
                topLevel);

            return true;
        }

        private static int CreateHoopsForSpec(
            Document doc,
            FamilyInstance column,
            ColumnHoopSpec spec,
            RebarBarType barType,
            RebarHookType hookType1356D,
            RebarHookType hookType908D,
            List<string> warnings)
        {
            XYZ center = GetColumnCenter(column) ?? spec.Point;
            ColumnHoopGeneratedData generatedData = BuildColumnHoopGeneratedData(spec, center, warnings);
            if (generatedData?.OffsetPointsMm == null || generatedData.OffsetPointsMm.Count < 2)
            {
                throw new InvalidOperationException("Invalid HOOP geometry from width/depth/cover data.");
            }

            return CreateRebarsFromGeneratedData(doc, column, generatedData, barType, hookType1356D, hookType908D, warnings);
        }

        private static ColumnHoopGeneratedData BuildColumnHoopGeneratedData(
            ColumnHoopSpec spec,
            XYZ columnCenter,
            List<string> warnings)
        {
            List<UV> offsetPointsMm = null;
            string startHook = "0";
            string endHook = "0";

            if (string.Equals(spec.HoopShape, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(spec.HoopShape, "2", StringComparison.OrdinalIgnoreCase))
            {
                double actualDiaMm = GetActualHoopDiameterMm(spec.HoopDia);
                offsetPointsMm = BuildQuadrant12OffsetPoints(spec, actualDiaMm);

                if (offsetPointsMm != null)
                {
                    if (string.Equals(spec.HoopShape, "1", StringComparison.OrdinalIgnoreCase))
                    {
                        startHook = "135_6D";
                        endHook = "135_6D";
                    }
                    else
                    {
                        switch (spec.HookPosition)
                        {
                            case "1":
                            case "3":
                                startHook = "90_8D";
                                endHook = "135_6D";
                                break;
                            case "2":
                            case "4":
                                startHook = "135_6D";
                                endHook = "90_8D";
                                break;
                        }
                    }
                }
            }
            else if (string.Equals(spec.HoopShape, "3", StringComparison.OrdinalIgnoreCase))
            {
                offsetPointsMm = BuildShape3OffsetPoints(spec);
                startHook = "0";
                endHook = "0";
            }

            if (offsetPointsMm == null)
            {
                if (!string.IsNullOrWhiteSpace(spec.HoopShape) || !string.IsNullOrWhiteSpace(spec.HookPosition))
                {
                    warnings.Add($"{spec.Kai} {spec.YName}-{spec.XName} {spec.ColumnCode}: HOOP形='{spec.HoopShape}', フックの位置='{spec.HookPosition}' is not handled yet, using basic closed rectangle.");
                }

                double topY = -spec.CoverTopMm;
                double bottomYFallback = -(spec.DepthMm - spec.CoverBottomMm);
                double leftXFallback = -(spec.WidthMm / 2.0 - spec.CoverLeftMm);
                double rightXFallback = spec.WidthMm / 2.0 - spec.CoverRightMm;

                offsetPointsMm = new List<UV>
                {
                    new UV(rightXFallback, topY),
                    new UV(leftXFallback, topY),
                    new UV(leftXFallback, bottomYFallback),
                    new UV(rightXFallback, bottomYFallback),
                    new UV(rightXFallback, topY)
                };
            }

            return new ColumnHoopGeneratedData(
                spec.Kai,
                spec.XName,
                spec.YName,
                spec.ColumnCode,
                spec.SectionName,
                spec.HoopDia,
                spec.HoopShape,
                spec.Material,
                spec.PitchMm,
                spec.DepthMm,
                new[]
                {
                    spec.CoverTopMm,
                    spec.CoverBottomMm,
                    spec.CoverLeftMm,
                    spec.CoverRightMm
                }.Where(value => value >= 0.0).DefaultIfEmpty(0.0).Min(),
                offsetPointsMm,
                startHook,
                endHook,
                columnCenter,
                spec.BaseLevel,
                spec.TopLevel);
        }

        private static List<UV> BuildQuadrant12OffsetPoints(ColumnHoopSpec spec, double actualDiaMm)
        {
            double leftX = -spec.WidthMm / 2.0 + spec.CoverLeftMm;
            double rightX = spec.WidthMm / 2.0 - spec.CoverRightMm;
            double topY = -spec.CoverTopMm;
            double bottomY = -spec.DepthMm + spec.CoverBottomMm;
            double rHalf = actualDiaMm / 2.0;

            switch (spec.HookPosition)
            {
                case "1":
                    return new List<UV>
                    {
                        new UV(rightX,                             topY - rHalf),
                        new UV(leftX + rHalf,                      topY - rHalf),
                        new UV(leftX + rHalf,                      bottomY + rHalf),
                        new UV(rightX - rHalf,                     bottomY + rHalf),
                        new UV(rightX - rHalf,                     topY)
                    };

                case "2":
                    return new List<UV>
                    {
                        new UV(leftX + rHalf,                      topY),
                        new UV(leftX + rHalf,                      bottomY + rHalf),
                        new UV(rightX - rHalf,                     bottomY + rHalf),
                        new UV(rightX - rHalf,                     topY - rHalf),
                        new UV(leftX,                              topY - rHalf)
                    };

                case "3":
                    return new List<UV>
                    {
                        new UV(leftX,                              bottomY + rHalf),
                        new UV(rightX - rHalf,                     bottomY + rHalf),
                        new UV(rightX - rHalf,                     topY - rHalf),
                        new UV(leftX + rHalf,                      topY - rHalf),
                        new UV(leftX + rHalf,                      bottomY)
                    };

                case "4":
                    return new List<UV>
                    {
                        new UV(rightX - rHalf,                     bottomY),
                        new UV(rightX - rHalf,                     topY - rHalf),
                        new UV(leftX + rHalf,                      topY - rHalf),
                        new UV(leftX + rHalf,                      bottomY + rHalf),
                        new UV(rightX,                             bottomY + rHalf)
                    };
            }

            return null;
        }

        private static List<UV> BuildShape3OffsetPoints(ColumnHoopSpec spec)
        {
            double leftX = -spec.WidthMm / 2.0 + spec.CoverLeftMm;
            double rightX = spec.WidthMm / 2.0 - spec.CoverRightMm;
            double topY = -spec.CoverTopMm;
            double bottomY = -spec.DepthMm + spec.CoverBottomMm;
            double midX = (leftX + rightX) / 2.0;
            double midY = (topY + bottomY) / 2.0;

            switch (spec.HookPosition)
            {
                case "1":
                    return new List<UV>
                    {
                        new UV(leftX, midY),
                        new UV(leftX, bottomY),
                        new UV(rightX, bottomY),
                        new UV(rightX, topY),
                        new UV(leftX, topY),
                        new UV(leftX, midY)
                    };

                case "2":
                    return new List<UV>
                    {
                        new UV(midX, bottomY),
                        new UV(rightX, bottomY),
                        new UV(rightX, topY),
                        new UV(leftX, topY),
                        new UV(leftX, bottomY),
                        new UV(midX, bottomY)
                    };

                case "3":
                    return new List<UV>
                    {
                        new UV(rightX, midY),
                        new UV(rightX, topY),
                        new UV(leftX, topY),
                        new UV(leftX, bottomY),
                        new UV(rightX, bottomY),
                        new UV(rightX, midY)
                    };

                case "4":
                    return new List<UV>
                    {
                        new UV(midX, topY),
                        new UV(leftX, topY),
                        new UV(leftX, bottomY),
                        new UV(rightX, bottomY),
                        new UV(rightX, topY),
                        new UV(midX, topY)
                    };
            }

            return null;
        }

        private static List<UV> AddStraightHookSegments(List<UV> bodyPointsMm, double hookLengthMm)
        {
            if (bodyPointsMm == null || bodyPointsMm.Count < 2 || hookLengthMm <= 0.0)
            {
                return bodyPointsMm;
            }

            UV startPoint = bodyPointsMm[0];
            UV secondPoint = bodyPointsMm[1];
            UV previousPoint = bodyPointsMm[bodyPointsMm.Count - 2];
            UV endPoint = bodyPointsMm[bodyPointsMm.Count - 1];

            UV startHookPoint = CreateOffsetPointAlongDirection(startPoint, startPoint.U - secondPoint.U, startPoint.V - secondPoint.V, hookLengthMm);
            UV endHookPoint = CreateOffsetPointAlongDirection(endPoint, endPoint.U - previousPoint.U, endPoint.V - previousPoint.V, hookLengthMm);

            List<UV> result = new List<UV>(bodyPointsMm.Count + 2)
            {
                startHookPoint
            };
            result.AddRange(bodyPointsMm);
            result.Add(endHookPoint);
            return result;
        }

        private static UV CreateOffsetPointAlongDirection(UV origin, double directionU, double directionV, double lengthMm)
        {
            double magnitude = Math.Sqrt(directionU * directionU + directionV * directionV);
            if (magnitude <= 1e-9 || lengthMm <= 0.0)
            {
                return origin;
            }

            double scale = lengthMm / magnitude;
            return new UV(
                origin.U + directionU * scale,
                origin.V + directionV * scale);
        }

        private static RebarBarType GetOrFindRebarBarType(
            Document doc,
            ColumnHoopSpec spec,
            Dictionary<string, RebarBarType> cache)
        {
            string cacheKey = $"{spec.HoopDia}|{spec.Material}";
            RebarBarType barType;
            if (cache.TryGetValue(cacheKey, out barType))
            {
                return barType;
            }

            barType = FindRebarBarType(doc, spec.HoopDia, spec.Material);
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

            RebarBarType diameterOnly = types.FirstOrDefault(type => NameContains(type.Name, diameterToken));
            return diameterOnly;
        }

        private static RebarHookType FindOrCreateHookType135_6D(Document doc)
        {
            bool usedFallback;
            return FindOrCreateHookType135_6D(doc, out usedFallback);
        }

        private static RebarHookType FindOrCreateHookType135_6D(Document doc, out bool usedFallback)
        {
            usedFallback = false;

            List<RebarHookType> hooks = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToList();

            RebarHookType existing = hooks.FirstOrDefault(hook =>
                string.Equals(hook.Name, HookType1356DName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            try
            {
                RebarHookType created = RebarHookType.Create(doc, 135.0 * Math.PI / 180.0, 6.0);
                if (created != null)
                {
                    created.Style = RebarStyle.StirrupTie;
                    created.HookAngle = 135.0 * Math.PI / 180.0;
                    created.StraightLineMultiplier = 6.0;
                    created.Name = HookType1356DName;
                    return created;
                }
            }
            catch
            {
                // Fall back to an existing 135-degree hook type when the API cannot create a dedicated 6D type.
            }

            RebarHookType fallback = hooks.FirstOrDefault(hook =>
                hook.Name.IndexOf("135", StringComparison.OrdinalIgnoreCase) >= 0);
            usedFallback = fallback != null;
            return fallback;
        }

        private static RebarHookType FindOrCreateHookType90_8D(Document doc, out bool usedFallback)
        {
            usedFallback = false;

            List<RebarHookType> hooks = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToList();

            RebarHookType existing = hooks.FirstOrDefault(hook =>
                string.Equals(hook.Name, HookType908DName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            try
            {
                RebarHookType created = RebarHookType.Create(doc, 90.0 * Math.PI / 180.0, 8.0);
                if (created != null)
                {
                    created.Style = RebarStyle.StirrupTie;
                    created.HookAngle = 90.0 * Math.PI / 180.0;
                    created.StraightLineMultiplier = 8.0;
                    created.Name = HookType908DName;
                    return created;
                }
            }
            catch
            {
                // Fall back to an existing 90-degree hook type when the API cannot create a dedicated 8D type.
            }

            RebarHookType fallback = hooks.FirstOrDefault(hook =>
                hook.Name.IndexOf("90", StringComparison.OrdinalIgnoreCase) >= 0);
            usedFallback = fallback != null;
            return fallback;
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
                errorMessage = "Model contains duplicate grid names. Please resolve them before running Column HOOP: "
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
                errorMessage = "Model contains duplicate level names. Please resolve them before running Column HOOP: "
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

        private static double GetDoubleParameterValue(Element element, BuiltInParameter parameterId)
        {
            Parameter parameter = element?.get_Parameter(parameterId);
            return parameter != null && parameter.StorageType == StorageType.Double
                ? parameter.AsDouble()
                : 0.0;
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

        private static List<Curve> BuildRectangularHoopCurves(
            double left,
            double right,
            double bottom,
            double top,
            double z)
        {
            XYZ p1 = new XYZ(right, top, z);
            XYZ p2 = new XYZ(left, top, z);
            XYZ p3 = new XYZ(left, bottom, z);
            XYZ p4 = new XYZ(right, bottom, z);

            return new List<Curve>
            {
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p4),
                Line.CreateBound(p4, p1)
            };
        }

        private static int CreateRebarsFromGeneratedData(
            Document doc,
            FamilyInstance hostColumn,
            ColumnHoopGeneratedData data,
            RebarBarType barType,
            RebarHookType hookType1356D,
            RebarHookType hookType908D,
            List<string> warnings)
        {
            double minZ;
            double maxZ;
            if (!TryGetHostVerticalRange(hostColumn, out minZ, out maxZ))
            {
                throw new InvalidOperationException("Could not resolve host column vertical range.");
            }

            double endInsetFt = data.EndInsetMm * FeetPerMillimeter;
            double pitchFt = data.PitchMm * FeetPerMillimeter;
            if (pitchFt <= 0.0)
            {
                throw new InvalidOperationException("Pitch must be greater than zero.");
            }

            double startZ = minZ + endInsetFt;
            double endZ = maxZ - endInsetFt;
            if (endZ < startZ)
            {
                warnings.Add($"{data.Kai} {data.YName}-{data.XName} {data.ColumnCode}: host column height is too small for the requested cover.");
                return 0;
            }

            RebarHookType startHookType = ResolveHookType(data.StartHook, hookType1356D, hookType908D);
            RebarHookType endHookType = ResolveHookType(data.EndHook, hookType1356D, hookType908D);

            int count = 0;
            for (double z = startZ; z <= endZ + 1e-9; z += pitchFt)
            {
                List<Curve> curves = BuildHoopCurvesFromGeneratedData(data, z);
                Rebar rebar = Rebar.CreateFromCurves(
                    doc,
                    RebarStyle.StirrupTie,
                    barType,
                    startHookType,
                    endHookType,
                    hostColumn,
                    XYZ.BasisZ,
                    curves,
                    RebarHookOrientation.Left,
                    RebarHookOrientation.Right,
                    true,
                    true);

                if (rebar == null)
                {
                    continue;
                }

                Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                if (comments != null && !comments.IsReadOnly)
                {
                    comments.Set($"COLUMN_HOOP {data.Kai} {data.YName}-{data.XName} {data.ColumnCode} {data.SectionName} D={data.HoopDia} Shape={data.HoopShape} Pitch={data.PitchMm} Hook={GetHookTypeSummary(startHookType, endHookType)} HookStart={data.StartHook} HookEnd={data.EndHook}");
                }

                count++;
            }

            return count;
        }

        private static void DeleteExistingColumnHoops(Document doc, string sectionName)
        {
            List<ElementId> rebarIdsToDelete = new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .Where(rebar =>
                {
                    Parameter comments = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    string comment = comments?.AsString();
                    if (string.IsNullOrWhiteSpace(comment) || !comment.StartsWith("COLUMN_HOOP ", StringComparison.Ordinal))
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

        private static List<Curve> BuildHoopCurvesFromGeneratedData(ColumnHoopGeneratedData data, double z)
        {
            List<Curve> curves = new List<Curve>();
            if (data?.OffsetPointsMm == null || data.OffsetPointsMm.Count < 2)
            {
                return curves;
            }

            for (int i = 0; i < data.OffsetPointsMm.Count - 1; i++)
            {
                XYZ start = ConvertLocalPointMmToModelPoint(data.OffsetPointsMm[i], data.ColumnCenter, data.DepthMm, z);
                XYZ end = ConvertLocalPointMmToModelPoint(data.OffsetPointsMm[i + 1], data.ColumnCenter, data.DepthMm, z);
                curves.Add(Line.CreateBound(start, end));
            }

            return curves;
        }

        private static RebarHookType ResolveHookType(string hookCode, RebarHookType hookType1356D, RebarHookType hookType908D)
        {
            switch (hookCode)
            {
                case "135_6D":
                    return hookType1356D;
                case "90_8D":
                    return hookType908D;
                default:
                    return null;
            }
        }

        private static string GetHookTypeSummary(RebarHookType startHookType, RebarHookType endHookType)
        {
            string startName = startHookType?.Name ?? "None";
            string endName = endHookType?.Name ?? "None";
            return string.Equals(startName, endName, StringComparison.OrdinalIgnoreCase)
                ? startName
                : $"{startName}/{endName}";
        }

        private static XYZ ConvertLocalPointMmToModelPoint(UV pointMm, XYZ center, double depthMm, double z)
        {
            double x = center.X + pointMm.U * FeetPerMillimeter;
            double y = center.Y + (depthMm / 2.0 + pointMm.V) * FeetPerMillimeter;
            return new XYZ(x, y, z);
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

        private static string GetSectionProperty(object layout, string sectionName, string baseName)
        {
            if (layout == null || string.IsNullOrWhiteSpace(sectionName) || string.IsNullOrWhiteSpace(baseName))
            {
                return null;
            }

            switch (sectionName)
            {
                case "柱脚":
                    return GetFirstNonEmptyString(
                        GetStringProperty(layout, baseName));

                case "柱頭":
                    return GetStringProperty(layout, baseName + "1");

                case "仕口部":
                    switch (baseName)
                    {
                        case "HOOP径":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "仕口部_HOOP径"),
                                GetStringProperty(layout, "HOOP径"));
                        case "HOOP形":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "仕口部_HOOP形"),
                                GetStringProperty(layout, "HOOP形"));
                        case "HOOP材質":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "仕口部_HOOP材質"),
                                GetStringProperty(layout, "HOOP材質"));
                        case "ピッチ":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "仕口部_ピッチ"),
                                GetStringProperty(layout, "ピッチ"));
                        case "柱幅":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "柱幅"),
                                GetStringProperty(layout, "柱幅1"));
                        case "柱成":
                            return GetFirstNonEmptyString(
                                GetStringProperty(layout, "柱成"),
                                GetStringProperty(layout, "柱成1"));
                        default:
                            return GetStringProperty(layout, baseName);
                    }

                default:
                    return GetFirstNonEmptyString(
                        GetStringProperty(layout, baseName));
            }
        }

        private static string GetFirstNonEmptyString(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static bool TryParseMillimeters(string input, out double valueMm)
        {
            valueMm = 0.0;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string normalized = input.Trim().Replace(",", string.Empty);

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

        private static string ExtractDigits(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return new string(input.Where(char.IsDigit).ToArray());
        }

        private static double GetActualHoopDiameterMm(string hoopDia)
        {
            switch (ExtractDigits(hoopDia))
            {
                case "10": return 11.0;
                case "13": return 14.0;
                case "16": return 18.0;
                case "19": return 21.0;
                case "22": return 25.0;
                case "25": return 28.0;
                case "29": return 33.0;
                case "32": return 36.0;
                case "35": return 40.0;
                case "38": return 43.0;
                default:
                    double parsedMm;
                    return TryParseMillimeters(ExtractDigits(hoopDia), out parsedMm) && parsedMm > 0.0
                        ? parsedMm
                        : 0.0;
            }
        }

        private static bool NameContains(string source, string token)
        {
            return !string.IsNullOrWhiteSpace(source)
                && !string.IsNullOrWhiteSpace(token)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class ColumnHoopSpec
        {
            public ColumnHoopSpec(
                string kai,
                string xName,
                string yName,
                string columnCode,
                double widthMm,
                double depthMm,
                double coverTopMm,
                double coverBottomMm,
                double coverLeftMm,
                double coverRightMm,
                string hoopDia,
                string hoopShape,
                string material,
                double pitchMm,
                string hookPosition,
                string sectionName,
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
                CoverTopMm = coverTopMm;
                CoverBottomMm = coverBottomMm;
                CoverLeftMm = coverLeftMm;
                CoverRightMm = coverRightMm;
                HoopDia = hoopDia;
                HoopShape = hoopShape;
                Material = material;
                PitchMm = pitchMm;
                HookPosition = hookPosition;
                SectionName = sectionName;
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
            public double CoverTopMm { get; }
            public double CoverBottomMm { get; }
            public double CoverLeftMm { get; }
            public double CoverRightMm { get; }
            public string HoopDia { get; }
            public string HoopShape { get; }
            public string Material { get; }
            public double PitchMm { get; }
            public string HookPosition { get; }
            public string SectionName { get; }
            public XYZ Point { get; }
            public Level BaseLevel { get; }
            public Level TopLevel { get; }
        }

        internal sealed class ColumnHoopGeneratedData
        {
            public ColumnHoopGeneratedData(
                string kai,
                string xName,
                string yName,
                string columnCode,
                string sectionName,
                string hoopDia,
                string hoopShape,
                string hoopMaterial,
                double pitchMm,
                double depthMm,
                double endInsetMm,
                List<UV> offsetPointsMm,
                string startHook,
                string endHook,
                XYZ columnCenter,
                Level baseLevel,
                Level topLevel)
            {
                Kai = kai;
                XName = xName;
                YName = yName;
                ColumnCode = columnCode;
                SectionName = sectionName;
                HoopDia = hoopDia;
                HoopShape = hoopShape;
                HoopMaterial = hoopMaterial;
                PitchMm = pitchMm;
                DepthMm = depthMm;
                EndInsetMm = endInsetMm;
                OffsetPointsMm = offsetPointsMm ?? new List<UV>();
                StartHook = startHook ?? "0";
                EndHook = endHook ?? "0";
                ColumnCenter = columnCenter;
                BaseLevel = baseLevel;
                TopLevel = topLevel;
            }

            public string Kai { get; }
            public string XName { get; }
            public string YName { get; }
            public string ColumnCode { get; }
            public string SectionName { get; }
            public string HoopDia { get; }
            public string HoopShape { get; }
            public string HoopMaterial { get; }
            public double PitchMm { get; }
            public double DepthMm { get; }
            public double EndInsetMm { get; }
            public List<UV> OffsetPointsMm { get; }
            public string StartHook { get; }
            public string EndHook { get; }
            public XYZ ColumnCenter { get; }
            public Level BaseLevel { get; }
            public Level TopLevel { get; }
        }
    }
}
