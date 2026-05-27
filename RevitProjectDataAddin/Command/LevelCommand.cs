using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class LevelCommand : IExternalCommand
    {
        private const double MmToFeet = 1.0 / 304.8;
        // Treat differences smaller than 1 mm as unchanged.
        private const double ElevationToleranceFeet = 1.0 / 304.8;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Level", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Level", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Level", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            KihonData kihon = projectData.Kihon;
            if (kihon == null)
            {
                TaskDialog.Show("Level", "ProjectData.Kihon is null.");
                return Result.Cancelled;
            }

            List<string> levelNames = GetLevelNames(kihon.NameKai?.Select(kai => kai?.Name));
            if (levelNames == null)
            {
                TaskDialog.Show("Level", "NameKai data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> duplicateInputNames = levelNames
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (duplicateInputNames.Count > 0)
            {
                TaskDialog.Show("Level", "Duplicate level names were found in input data: " + string.Join(", ", duplicateInputNames));
                return Result.Cancelled;
            }

            List<double> elevationsMm;
            string elevationError;
            if (!TryBuildElevationsFromSpans(
                levelNames,
                kihon.ListSpanKai?.Select(span => span?.Span).ToList(),
                out elevationsMm,
                out elevationError))
            {
                TaskDialog.Show("Level", elevationError);
                return Result.Cancelled;
            }

            List<LevelTarget> targets = BuildLevelTargets(levelNames, elevationsMm).ToList();
            Dictionary<string, Level> existingLevelsByName;
            string existingLevelError;
            if (!TryCollectExistingLevelsByName(doc, out existingLevelsByName, out existingLevelError))
            {
                TaskDialog.Show("Level", existingLevelError);
                return Result.Cancelled;
            }

            LevelCurveTemplate levelCurveTemplate;
            string templateWarning;
            bool hasTemplateCurve = TryGetTemplateLevelCurve(doc, existingLevelsByName, targets, out levelCurveTemplate, out templateWarning);
            ViewFamilyType structuralPlanType = GetStructuralPlanViewFamilyType(doc);
            HashSet<ElementId> structuralPlanLevelIds = CollectStructuralPlanLevelIds(doc);

            LevelSyncPlan syncPlan = BuildLevelSyncPlan(existingLevelsByName.Values, targets);
            List<string> orphanLevelNames = syncPlan.UnmatchedExistingLevels
                .Select(level => level.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int createdCount = 0;
            int updatedCount = 0;
            int unchangedCount = 0;
            int deletedOrphanCount = 0;
            List<string> failedUpdates = new List<string>();
            List<string> failedDeletes = new List<string>();
            List<string> warnings = new List<string>();

            if (!hasTemplateCurve && !string.IsNullOrWhiteSpace(templateWarning))
            {
                warnings.Add(templateWarning);
            }

            using (Transaction transaction = new Transaction(doc, "Sync Level"))
            {
                transaction.Start();

                foreach (LevelSyncMatch match in syncPlan.Matches)
                {
                    if (match.ExistingLevel == null)
                    {
                        Level createdLevel = CreateLevel(doc, match.Target, hasTemplateCurve ? levelCurveTemplate : null, warnings);
                        EnsureStructuralPlanExists(doc, createdLevel, structuralPlanType, structuralPlanLevelIds, warnings);
                        createdCount++;
                        continue;
                    }

                    string failureReason;
                    bool changed;
                    if (TrySyncLevel(doc, match.ExistingLevel, match.Target, out changed, out failureReason))
                    {
                        if (changed)
                        {
                            updatedCount++;
                        }
                        else
                        {
                            unchangedCount++;
                        }
                    }
                    else
                    {
                        failedUpdates.Add($"{match.Target.Name} ({failureReason})");
                    }
                }

                transaction.Commit();
            }

            if (orphanLevelNames.Count > 0)
            {
                deletedOrphanCount = DeleteOrphanLevels(doc, syncPlan.UnmatchedExistingLevels, failedDeletes);
            }

            TaskDialog.Show("Level", BuildResultMessage(createdCount, updatedCount, unchangedCount, orphanLevelNames, deletedOrphanCount, failedUpdates, failedDeletes, warnings));
            return Result.Succeeded;
        }

        private static List<string> GetLevelNames(IEnumerable<string> names)
        {
            if (names == null)
            {
                return null;
            }

            List<string> normalizedNames = names
                .Select(name => name?.Trim())
                .ToList();

            if (normalizedNames.Count == 0)
            {
                return null;
            }

            if (normalizedNames.Any(string.IsNullOrWhiteSpace))
            {
                return null;
            }

            return normalizedNames;
        }

        private static bool TryBuildElevationsFromSpans(
            List<string> levelNames,
            List<string> spanInputs,
            out List<double> elevationsMm,
            out string errorMessage)
        {
            elevationsMm = new List<double>();
            errorMessage = null;

            if (levelNames == null || levelNames.Count == 0)
            {
                errorMessage = "NameKai is empty.";
                return false;
            }

            if (spanInputs == null)
            {
                errorMessage = "ListSpanKai is null.";
                return false;
            }

            if (spanInputs.Count != levelNames.Count - 1)
            {
                errorMessage = "ListSpanKai.Count must equal NameKai.Count - 1.";
                return false;
            }

            elevationsMm.Add(0.0);
            double currentElevationMm = 0.0;

            for (int i = 0; i < spanInputs.Count; i++)
            {
                double spanMm;
                if (!TryParseSpan(spanInputs[i], out spanMm))
                {
                    errorMessage = $"Span Kai{i + 1} is invalid: '{spanInputs[i]}'.";
                    return false;
                }

                if (spanMm <= 0)
                {
                    errorMessage = $"Span Kai{i + 1} must be a positive number: '{spanInputs[i]}'.";
                    return false;
                }

                currentElevationMm += spanMm;
                elevationsMm.Add(currentElevationMm);
            }

            return true;
        }

        private static bool TryCollectExistingLevelsByName(
            Document doc,
            out Dictionary<string, Level> levelsByName,
            out string errorMessage)
        {
            levelsByName = new Dictionary<string, Level>(StringComparer.OrdinalIgnoreCase);
            errorMessage = null;
            List<string> duplicateNames = new List<string>();

            foreach (Level level in new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>())
            {
                if (string.IsNullOrWhiteSpace(level.Name))
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
                errorMessage = "Model contains duplicate level names. Please resolve them before syncing: "
                    + string.Join(", ", duplicateNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
                return false;
            }

            return true;
        }

        private static IEnumerable<LevelTarget> BuildLevelTargets(List<string> levelNames, List<double> elevationsMm)
        {
            List<LevelTarget> targets = new List<LevelTarget>();

            for (int i = 0; i < levelNames.Count; i++)
            {
                targets.Add(new LevelTarget(levelNames[i], elevationsMm[i]));
            }

            return targets;
        }

        private static bool TryGetTemplateLevelCurve(
            Document doc,
            Dictionary<string, Level> existingLevelsByName,
            List<LevelTarget> targets,
            out LevelCurveTemplate template,
            out string warningMessage)
        {
            template = null;
            warningMessage = null;

            List<Level> candidateLevels = new List<Level>();
            HashSet<ElementId> seenLevelIds = new HashSet<ElementId>();

            Level firstFloorLevel;
            if (existingLevelsByName.TryGetValue("1F", out firstFloorLevel))
            {
                candidateLevels.Add(firstFloorLevel);
                seenLevelIds.Add(firstFloorLevel.Id);
            }

            if (targets.Count > 0)
            {
                Level firstTargetLevel;
                if (existingLevelsByName.TryGetValue(targets[0].Name, out firstTargetLevel))
                {
                    candidateLevels.Add(firstTargetLevel);
                    seenLevelIds.Add(firstTargetLevel.Id);
                }
            }

            foreach (Level level in existingLevelsByName.Values.OrderBy(level => level.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (seenLevelIds.Add(level.Id))
                {
                    candidateLevels.Add(level);
                }
            }

            LevelCurveTemplate viewSpecificFallbackTemplate = null;

            foreach (Level candidateLevel in candidateLevels)
            {
                LevelCurveTemplate candidateTemplate;
                if (!TryBuildTemplateFromLevel(doc, candidateLevel, out candidateTemplate))
                {
                    continue;
                }

                if (candidateTemplate.HasModelCurve)
                {
                    template = candidateTemplate;
                    return true;
                }

                if (viewSpecificFallbackTemplate == null && candidateTemplate.HasViewSpecificCurve)
                {
                    viewSpecificFallbackTemplate = candidateTemplate;
                }
            }

            if (viewSpecificFallbackTemplate != null)
            {
                template = viewSpecificFallbackTemplate;
                warningMessage =
                    $"Using view-specific template line from '{template.TemplateLevelName}' in view '{template.ViewSpecificCurveView.Name}'; model extents could not be read.";
                return true;
            }

            if (candidateLevels.Count == 0)
            {
                warningMessage = "No existing level was found to use as a template; new levels used default line placement.";
            }
            else
            {
                warningMessage = "Could not read a template level line from the current model/views; new levels used default line placement.";
            }

            return false;
        }

        private static IEnumerable<View> GetCandidateViews(Document doc)
        {
            HashSet<ElementId> yieldedViewIds = new HashSet<ElementId>();
            View activeView = doc.ActiveView;

            if (IsSupportedLevelDatumView(activeView) && yieldedViewIds.Add(activeView.Id))
            {
                yield return activeView;
            }

            foreach (View view in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
            {
                if (!IsSupportedLevelDatumView(view))
                {
                    continue;
                }

                if (yieldedViewIds.Add(view.Id))
                {
                    yield return view;
                }
            }
        }

        private static bool IsSupportedLevelDatumView(View view)
        {
            if (view == null || view.IsTemplate)
            {
                return false;
            }

            switch (view.ViewType)
            {
                case ViewType.Section:
                case ViewType.Elevation:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryBuildTemplateFromLevel(Document doc, Level templateLevel, out LevelCurveTemplate template)
        {
            template = null;

            View activeView = doc.ActiveView;
            if (IsSupportedLevelDatumView(activeView))
            {
                if (TryBuildTemplateFromSpecificView(templateLevel, activeView, out template))
                {
                    return true;
                }
            }

            Line modelLine = null;
            View modelView = null;
            Line viewSpecificLine = null;
            View viewSpecificView = null;

            foreach (View candidateView in GetCandidateViews(doc))
            {
                if (modelLine == null)
                {
                    Line candidateModelLine;
                    if (TryGetCurveLineInView(templateLevel, DatumExtentType.Model, candidateView, out candidateModelLine))
                    {
                        modelLine = candidateModelLine;
                        modelView = candidateView;
                    }
                }

                if (viewSpecificLine == null)
                {
                    Line candidateViewSpecificLine;
                    if (TryGetCurveLineInView(templateLevel, DatumExtentType.ViewSpecific, candidateView, out candidateViewSpecificLine))
                    {
                        viewSpecificLine = candidateViewSpecificLine;
                        viewSpecificView = candidateView;
                    }
                }

                if (modelLine != null && viewSpecificLine != null)
                {
                    break;
                }
            }

            if (modelLine == null && viewSpecificLine == null)
            {
                return false;
            }

            template = new LevelCurveTemplate(templateLevel, templateLevel.Name, modelView, modelLine, viewSpecificView, viewSpecificLine);
            return true;
        }

        private static bool TryBuildTemplateFromSpecificView(Level templateLevel, View view, out LevelCurveTemplate template)
        {
            template = null;

            Line modelLine = null;
            Line viewSpecificLine = null;

            TryGetCurveLineInView(templateLevel, DatumExtentType.Model, view, out modelLine);
            TryGetCurveLineInView(templateLevel, DatumExtentType.ViewSpecific, view, out viewSpecificLine);

            if (modelLine == null && viewSpecificLine == null)
            {
                return false;
            }

            template = new LevelCurveTemplate(templateLevel, templateLevel.Name, modelLine != null ? view : null, modelLine, viewSpecificLine != null ? view : null, viewSpecificLine);
            return true;
        }

        private static bool TryGetCurveLineInView(
            Level level,
            DatumExtentType extentType,
            View view,
            out Line line)
        {
            line = null;

            IList<Curve> curves;
            if (!TryGetCurvesInView(level, extentType, view, out curves))
            {
                return false;
            }

            line = curves.OfType<Line>().FirstOrDefault();
            return line != null;
        }

        private static bool TryGetCurvesInView(
            Level level,
            DatumExtentType extentType,
            View view,
            out IList<Curve> curves)
        {
            curves = null;

            try
            {
                curves = level.GetCurvesInView(extentType, view);
                return curves != null && curves.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static LevelSyncPlan BuildLevelSyncPlan(
            IEnumerable<Level> existingLevels,
            List<LevelTarget> targets)
        {
            List<Level> unmatchedExistingLevels = existingLevels
                .OrderBy(level => level.Elevation)
                .ThenBy(level => level.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<LevelSyncMatch> matches = new List<LevelSyncMatch>();
            List<LevelTarget> unmatchedTargets = new List<LevelTarget>();

            foreach (LevelTarget target in targets)
            {
                Level existingLevel = unmatchedExistingLevels.FirstOrDefault(
                    level => string.Equals(level.Name, target.Name, StringComparison.OrdinalIgnoreCase));

                if (existingLevel == null)
                {
                    unmatchedTargets.Add(target);
                    continue;
                }

                matches.Add(new LevelSyncMatch(target, existingLevel));
                unmatchedExistingLevels.Remove(existingLevel);
            }

            foreach (LevelTarget target in unmatchedTargets)
            {
                Level existingLevel = unmatchedExistingLevels.FirstOrDefault(level => HasSameElevation(level, target));
                matches.Add(new LevelSyncMatch(target, existingLevel));

                if (existingLevel != null)
                {
                    unmatchedExistingLevels.Remove(existingLevel);
                }
            }

            return new LevelSyncPlan(matches, unmatchedExistingLevels);
        }

        private static ViewFamilyType GetStructuralPlanViewFamilyType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(viewFamilyType => viewFamilyType.ViewFamily == ViewFamily.StructuralPlan);
        }

        private static HashSet<ElementId> CollectStructuralPlanLevelIds(Document doc)
        {
            HashSet<ElementId> levelIds = new HashSet<ElementId>();

            foreach (ViewPlan viewPlan in new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>())
            {
                if (viewPlan.IsTemplate || viewPlan.GenLevel == null)
                {
                    continue;
                }

                ViewFamilyType viewFamilyType = doc.GetElement(viewPlan.GetTypeId()) as ViewFamilyType;
                if (viewFamilyType == null || viewFamilyType.ViewFamily != ViewFamily.StructuralPlan)
                {
                    continue;
                }

                levelIds.Add(viewPlan.GenLevel.Id);
            }

            return levelIds;
        }

        private static Level CreateLevel(Document doc, LevelTarget target, LevelCurveTemplate template, List<string> curveWarnings)
        {
            if (template != null)
            {
                string copyWarning;
                Level copiedLevel;
                if (TryCreateLevelByCopyingTemplate(doc, target, template, out copiedLevel, out copyWarning))
                {
                    if (!string.IsNullOrWhiteSpace(copyWarning))
                    {
                        curveWarnings.Add(copyWarning);
                    }

                    copiedLevel.Name = target.Name;
                    return copiedLevel;
                }

                if (!string.IsNullOrWhiteSpace(copyWarning))
                {
                    curveWarnings.Add(copyWarning);
                }
            }

            Level level = Level.Create(doc, target.ElevationFeet);
            string curveWarning;
            if (template != null)
            {
                bool appliedTemplate = TryApplyLevelCurveFromTemplate(doc, level, target, template, out curveWarning);
                if (!string.IsNullOrWhiteSpace(curveWarning))
                {
                    curveWarnings.Add(curveWarning);
                }

                if (!appliedTemplate && string.IsNullOrWhiteSpace(curveWarning))
                {
                    curveWarnings.Add($"{target.Name} could not apply the template line.");
                }
            }

            level.Name = target.Name;
            return level;
        }

        private static void EnsureStructuralPlanExists(
            Document doc,
            Level level,
            ViewFamilyType structuralPlanType,
            HashSet<ElementId> structuralPlanLevelIds,
            List<string> warnings)
        {
            if (level == null || structuralPlanLevelIds.Contains(level.Id))
            {
                return;
            }

            if (structuralPlanType == null)
            {
                warnings.Add($"Could not create Structural Plan for '{level.Name}' because no Structural Plan view family type was found.");
                return;
            }

            try
            {
                ViewPlan.Create(doc, structuralPlanType.Id, level.Id);
                structuralPlanLevelIds.Add(level.Id);
            }
            catch (Exception ex)
            {
                warnings.Add($"Could not create Structural Plan for '{level.Name}': {GetExceptionMessage(ex)}");
            }
        }

        private static int DeleteOrphanLevels(Document doc, List<Level> orphanLevels, List<string> failedDeletes)
        {
            int deletedCount = 0;

            using (Transaction transaction = new Transaction(doc, "Delete Orphan Levels"))
            {
                transaction.Start();

                foreach (Level orphanLevel in orphanLevels.Where(level => level != null && level.IsValidObject))
                {
                    if (TryDeleteOrphanLevel(doc, orphanLevel, out string failureReason))
                    {
                        deletedCount++;
                    }
                    else
                    {
                        failedDeletes.Add($"{orphanLevel.Name} ({failureReason})");
                    }
                }

                transaction.Commit();
            }

            return deletedCount;
        }

        private static bool TryDeleteOrphanLevel(Document doc, Level level, out string failureReason)
        {
            failureReason = null;

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                List<ElementId> structuralPlanIds = GetStructuralPlanIdsForLevel(doc, level.Id);
                if (structuralPlanIds.Count > 0)
                {
                    doc.Delete(structuralPlanIds);
                    doc.Regenerate();
                }

                doc.Delete(level.Id);
                subTransaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                if (subTransaction.GetStatus() == TransactionStatus.Started)
                {
                    subTransaction.RollBack();
                }

                failureReason = GetExceptionMessage(ex);
                return false;
            }
        }

        private static List<ElementId> GetStructuralPlanIdsForLevel(Document doc, ElementId levelId)
        {
            List<ElementId> structuralPlanIds = new List<ElementId>();

            foreach (ViewPlan viewPlan in new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>())
            {
                if (viewPlan.IsTemplate || viewPlan.GenLevel == null || viewPlan.GenLevel.Id != levelId)
                {
                    continue;
                }

                ViewFamilyType viewFamilyType = doc.GetElement(viewPlan.GetTypeId()) as ViewFamilyType;
                if (viewFamilyType == null || viewFamilyType.ViewFamily != ViewFamily.StructuralPlan)
                {
                    continue;
                }

                structuralPlanIds.Add(viewPlan.Id);
            }

            return structuralPlanIds;
        }

        private static bool TryCreateLevelByCopyingTemplate(
            Document doc,
            LevelTarget target,
            LevelCurveTemplate template,
            out Level level,
            out string warningMessage)
        {
            level = null;
            warningMessage = null;

            if (template.TemplateLevel == null)
            {
                warningMessage = $"Template '{template.TemplateLevelName}' is missing its source level; falling back to Level.Create.";
                return false;
            }

            double elevationOffset = target.ElevationFeet - template.TemplateLevel.Elevation;
            XYZ translation = new XYZ(0.0, 0.0, elevationOffset);

            try
            {
                ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElement(doc, template.TemplateLevel.Id, translation);
                if (copiedIds == null || copiedIds.Count == 0)
                {
                    warningMessage = $"Could not copy template level '{template.TemplateLevelName}'; falling back to Level.Create.";
                    return false;
                }

                level = copiedIds
                    .Select(id => doc.GetElement(id))
                    .OfType<Level>()
                    .FirstOrDefault();

                if (level == null)
                {
                    warningMessage = $"Copying template level '{template.TemplateLevelName}' did not return a level element; falling back to Level.Create.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                warningMessage =
                    $"Could not copy template level '{template.TemplateLevelName}' ({GetExceptionMessage(ex)}); falling back to Level.Create.";
                return false;
            }
        }

        private static bool TryApplyLevelCurveFromTemplate(
            Document doc,
            Level level,
            LevelTarget target,
            LevelCurveTemplate template,
            out string warningMessage)
        {
            warningMessage = null;
            List<string> failures = new List<string>();
            bool appliedModelCurve = false;
            bool appliedViewSpecificCurve = false;

            if (template.HasModelCurve)
            {
                string modelFailureMessage;
                if (TryApplyCurveInCandidateViews(
                    doc,
                    level,
                    target,
                    DatumExtentType.Model,
                    template.ModelCurveLine,
                    template.ModelCurveView,
                    out modelFailureMessage))
                {
                    appliedModelCurve = true;
                }
                else
                {
                    failures.Add("model extents: " + modelFailureMessage);
                }
            }

            if (template.HasViewSpecificCurve)
            {
                string viewSpecificFailureMessage;
                if (TryApplyViewSpecificCurve(level, target, template.ViewSpecificCurveView, template.ViewSpecificCurveLine, out viewSpecificFailureMessage))
                {
                    appliedViewSpecificCurve = true;
                }
                else
                {
                    failures.Add($"view-specific extents in '{template.ViewSpecificCurveView.Name}': {viewSpecificFailureMessage}");
                }
            }

            if (appliedModelCurve || appliedViewSpecificCurve)
            {
                if (appliedModelCurve && appliedViewSpecificCurve)
                {
                    return true;
                }

                if (appliedViewSpecificCurve)
                {
                    warningMessage =
                        $"{target.Name} matched the visible size of template '{template.TemplateLevelName}' in '{template.ViewSpecificCurveView.Name}', but model extents could not be copied.";
                    return true;
                }

                if (template.HasViewSpecificCurve)
                {
                    warningMessage =
                        $"{target.Name} copied model extents from '{template.TemplateLevelName}', but could not match the visible size in '{template.ViewSpecificCurveView.Name}': {string.Join("; ", failures)}";
                }

                return true;
            }

            if (failures.Count == 0)
            {
                warningMessage = $"no usable template line was found for '{template.TemplateLevelName}'";
            }
            else
            {
                warningMessage = $"could not copy template line from '{template.TemplateLevelName}': {string.Join("; ", failures)}";
            }

            return false;
        }

        private static bool TryApplyCurveInCandidateViews(
            Document doc,
            Level level,
            LevelTarget target,
            DatumExtentType extentType,
            Line sourceLine,
            View preferredView,
            out string failureMessage)
        {
            failureMessage = null;

            List<string> failures = new List<string>();

            foreach (View candidateView in GetOrderedCandidateViews(doc, preferredView))
            {
                string candidateFailureMessage;
                if (TryApplyCurve(level, target, candidateView, extentType, sourceLine, out candidateFailureMessage))
                {
                    return true;
                }

                failures.Add($"'{candidateView.Name}': {candidateFailureMessage}");
            }

            if (failures.Count == 0)
            {
                failureMessage = "no supported datum view was available";
            }
            else
            {
                failureMessage = string.Join("; ", failures);
            }

            return false;
        }

        private static IEnumerable<View> GetOrderedCandidateViews(Document doc, View preferredView)
        {
            HashSet<ElementId> yieldedViewIds = new HashSet<ElementId>();

            if (IsSupportedLevelDatumView(preferredView) && yieldedViewIds.Add(preferredView.Id))
            {
                yield return preferredView;
            }

            foreach (View view in GetCandidateViews(doc))
            {
                if (yieldedViewIds.Add(view.Id))
                {
                    yield return view;
                }
            }
        }

        private static bool TryApplyCurve(
            Level level,
            LevelTarget target,
            View view,
            DatumExtentType extentType,
            Line sourceLine,
            out string failureMessage)
        {
            failureMessage = null;

            XYZ start = sourceLine.GetEndPoint(0);
            XYZ end = sourceLine.GetEndPoint(1);
            Line translatedLine = Line.CreateBound(
                new XYZ(start.X, start.Y, target.ElevationFeet),
                new XYZ(end.X, end.Y, target.ElevationFeet));

            try
            {
                level.SetCurveInView(extentType, view, translatedLine);
                return true;
            }
            catch (Exception ex)
            {
                failureMessage = GetExceptionMessage(ex);
                return false;
            }
        }

        private static bool TryApplyViewSpecificCurve(
            Level level,
            LevelTarget target,
            View view,
            Line sourceLine,
            out string failureMessage)
        {
            failureMessage = null;

            try
            {
                level.SetDatumExtentType(DatumEnds.End0, view, DatumExtentType.ViewSpecific);
                level.SetDatumExtentType(DatumEnds.End1, view, DatumExtentType.ViewSpecific);
            }
            catch (Exception ex)
            {
                failureMessage = "could not switch datum extents to view-specific: " + GetExceptionMessage(ex);
                return false;
            }

            return TryApplyCurve(level, target, view, DatumExtentType.ViewSpecific, sourceLine, out failureMessage);
        }

        private static bool HasSameElevation(Level level, LevelTarget target)
        {
            return Math.Abs(level.Elevation - target.ElevationFeet) <= ElevationToleranceFeet;
        }

        private static bool TrySyncLevel(
            Document doc,
            Level level,
            LevelTarget target,
            out bool changed,
            out string failureReason)
        {
            changed = false;
            failureReason = null;

            bool elevationChanged = !HasSameElevation(level, target);
            bool nameChanged = !string.Equals(level.Name, target.Name, StringComparison.Ordinal);

            if (!elevationChanged && !nameChanged)
            {
                return true;
            }

            SubTransaction subTransaction = new SubTransaction(doc);
            subTransaction.Start();

            try
            {
                if (elevationChanged)
                {
                    level.Elevation = target.ElevationFeet;
                }

                if (nameChanged)
                {
                    level.Name = target.Name;
                }

                subTransaction.Commit();
                changed = true;
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

                failureReason += "; level sync could not apply the requested elevation/name changes";
                return false;
            }
        }

        private static string BuildResultMessage(
            int createdCount,
            int updatedCount,
            int unchangedCount,
            List<string> orphanLevelNames,
            int deletedOrphanCount,
            List<string> failedUpdates,
            List<string> failedDeletes,
            List<string> warnings)
        {
            string resultMessage =
                $"Created: {createdCount}" + Environment.NewLine +
                $"Updated: {updatedCount}" + Environment.NewLine +
                $"Unchanged: {unchangedCount}";

            if (failedUpdates.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Failed updates ({failedUpdates.Count}): {string.Join(", ", failedUpdates)}";
            }

            if (orphanLevelNames.Count > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Orphan levels ({orphanLevelNames.Count}): {string.Join(", ", orphanLevelNames)}";
            }

            if (deletedOrphanCount > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Deleted orphan levels: {deletedOrphanCount}";
            }

            if (failedDeletes.Count > 0)
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

        private static string GetExceptionMessage(Exception ex)
        {
            if (ex == null)
            {
                return "unknown error";
            }

            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                return ex.Message;
            }

            return ex.GetType().Name;
        }

        private static bool TryParseSpan(string input, out double valueMm)
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

        private sealed class LevelTarget
        {
            public LevelTarget(string name, double elevationMm)
            {
                Name = name;
                ElevationMm = elevationMm;
            }

            public string Name { get; }

            public double ElevationMm { get; }

            public double ElevationFeet => ElevationMm * MmToFeet;
        }

        private sealed class LevelSyncMatch
        {
            public LevelSyncMatch(LevelTarget target, Level existingLevel)
            {
                Target = target;
                ExistingLevel = existingLevel;
            }

            public LevelTarget Target { get; }

            public Level ExistingLevel { get; }
        }

        private sealed class LevelSyncPlan
        {
            public LevelSyncPlan(List<LevelSyncMatch> matches, List<Level> unmatchedExistingLevels)
            {
                Matches = matches;
                UnmatchedExistingLevels = unmatchedExistingLevels;
            }

            public List<LevelSyncMatch> Matches { get; }

            public List<Level> UnmatchedExistingLevels { get; }
        }

        private sealed class LevelCurveTemplate
        {
            public LevelCurveTemplate(
                Level templateLevel,
                string templateLevelName,
                View modelCurveView,
                Line modelCurveLine,
                View viewSpecificCurveView,
                Line viewSpecificCurveLine)
            {
                TemplateLevel = templateLevel;
                TemplateLevelName = templateLevelName;
                ModelCurveView = modelCurveView;
                ViewSpecificCurveView = viewSpecificCurveView;
                ModelCurveLine = modelCurveLine;
                ViewSpecificCurveLine = viewSpecificCurveLine;
            }

            public Level TemplateLevel { get; }

            public string TemplateLevelName { get; }

            public View ModelCurveView { get; }

            public View ViewSpecificCurveView { get; }

            public Line ModelCurveLine { get; }

            public Line ViewSpecificCurveLine { get; }

            public bool HasModelCurve => ModelCurveLine != null && ModelCurveView != null;

            public bool HasViewSpecificCurve => ViewSpecificCurveLine != null && ViewSpecificCurveView != null;

            public bool HasAnyCurve => HasModelCurve || HasViewSpecificCurve;
        }
    }
}
