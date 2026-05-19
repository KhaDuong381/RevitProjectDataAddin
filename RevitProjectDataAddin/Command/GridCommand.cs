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
    public class GridCommand : IExternalCommand
    {
        private const double MmToFeet = 1.0 / 304.8;
        private const double GridMarginMm = 2000.0;
        private const double CoordinateToleranceFeet = 1e-9;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Grid", "Please select a project first.");
                return Result.Cancelled;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("Grid", "No active Revit document was found.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            ProjectData projectData = StorageUtils.LoadProject(doc, ProjectManager.SelectedProjectName);
            if (projectData == null)
            {
                TaskDialog.Show("Grid", $"Could not load ProjectData for project '{ProjectManager.SelectedProjectName}'.");
                return Result.Cancelled;
            }

            KihonData kihon = projectData.Kihon;
            if (kihon == null)
            {
                TaskDialog.Show("Grid", "ProjectData.Kihon is null.");
                return Result.Cancelled;
            }

            List<string> xNames = GetAxisNames(kihon.NameX?.Select(x => x?.Name));
            if (xNames == null)
            {
                TaskDialog.Show("Grid", "NameX data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> yNames = GetAxisNames(kihon.NameY?.Select(y => y?.Name));
            if (yNames == null)
            {
                TaskDialog.Show("Grid", "NameY data is missing or invalid.");
                return Result.Cancelled;
            }

            List<string> duplicateInputNames = xNames
                .Concat(yNames)
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (duplicateInputNames.Count > 0)
            {
                TaskDialog.Show("Grid", "Duplicate grid names were found in input data: " + string.Join(", ", duplicateInputNames));
                return Result.Cancelled;
            }

            List<double> xCoordinatesMm;
            string xError;
            if (!TryBuildCoordinatesFromSpans(
                xNames,
                kihon.ListSpanX?.Select(span => span?.Span).ToList(),
                "X",
                out xCoordinatesMm,
                out xError))
            {
                TaskDialog.Show("Grid", xError);
                return Result.Cancelled;
            }

            List<double> yCoordinatesMm;
            string yError;
            if (!TryBuildCoordinatesFromSpans(
                yNames,
                kihon.ListSpanY?.Select(span => span?.Span).ToList(),
                "Y",
                out yCoordinatesMm,
                out yError))
            {
                TaskDialog.Show("Grid", yError);
                return Result.Cancelled;
            }

            List<GridTarget> targets = BuildGridTargets(xNames, xCoordinatesMm, yCoordinatesMm.LastOrDefault(), true)
                .Concat(BuildGridTargets(yNames, yCoordinatesMm, xCoordinatesMm.LastOrDefault(), false))
                .ToList();

            Dictionary<string, Grid> existingGridsByName = CollectExistingGridsByName(doc);
            List<Grid> orphanGrids = FindOrphanGrids(existingGridsByName, targets);
            List<string> orphanGridNames = orphanGrids
                .Select(grid => grid.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int createdCount = 0;
            int updatedCount = 0;
            int unchangedCount = 0;
            int deletedOrphanCount = 0;

            using (Transaction transaction = new Transaction(doc, "Sync Grid"))
            {
                transaction.Start();

                List<GridTarget> targetsToReplace = new List<GridTarget>();
                List<GridTarget> targetsToCreate = new List<GridTarget>();

                foreach (GridTarget target in targets)
                {
                    Grid existingGrid;
                    if (!existingGridsByName.TryGetValue(target.Name, out existingGrid))
                    {
                        targetsToCreate.Add(target);
                        continue;
                    }

                    if (HasSameGeometry(existingGrid, target))
                    {
                        unchangedCount++;
                        continue;
                    }

                    targetsToReplace.Add(target);
                }

                foreach (GridTarget target in targetsToReplace)
                {
                    doc.Delete(existingGridsByName[target.Name].Id);
                }

                if (targetsToReplace.Count > 0)
                {
                    doc.Regenerate();
                }

                foreach (GridTarget target in targetsToReplace)
                {
                    CreateGrid(doc, target);
                    updatedCount++;
                }

                foreach (GridTarget target in targetsToCreate)
                {
                    CreateGrid(doc, target);
                    createdCount++;
                }

                transaction.Commit();
            }

            if (orphanGridNames.Count > 0)
            {
                deletedOrphanCount = DeleteOrphanGrids(doc, orphanGrids);
            }

            TaskDialog.Show("Grid", BuildResultMessage(createdCount, updatedCount, unchangedCount, orphanGridNames, deletedOrphanCount));
            return Result.Succeeded;
        }

        private static Dictionary<string, Grid> CollectExistingGridsByName(Document doc)
        {
            Dictionary<string, Grid> gridsByName = new Dictionary<string, Grid>(StringComparer.OrdinalIgnoreCase);

            foreach (Grid grid in new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>())
            {
                if (string.IsNullOrWhiteSpace(grid.Name))
                {
                    continue;
                }

                if (!gridsByName.ContainsKey(grid.Name))
                {
                    gridsByName.Add(grid.Name, grid);
                }
            }

            return gridsByName;
        }

        private static IEnumerable<GridTarget> BuildGridTargets(
            List<string> axisNames,
            List<double> coordinatesMm,
            double perpendicularExtentMm,
            bool isXAxis)
        {
            List<GridTarget> targets = new List<GridTarget>();

            for (int i = 0; i < axisNames.Count; i++)
            {
                targets.Add(new GridTarget(axisNames[i], coordinatesMm[i], perpendicularExtentMm, isXAxis));
            }

            return targets;
        }

        private static List<Grid> FindOrphanGrids(
            Dictionary<string, Grid> existingGridsByName,
            List<GridTarget> targets)
        {
            HashSet<string> targetNames = new HashSet<string>(
                targets.Select(target => target.Name),
                StringComparer.OrdinalIgnoreCase);

            return existingGridsByName
                .Where(entry => !targetNames.Contains(entry.Key))
                .Select(entry => entry.Value)
                .ToList();
        }

        private static int DeleteOrphanGrids(Document doc, List<Grid> orphanGrids)
        {
            List<ElementId> orphanIds = orphanGrids
                .Where(grid => grid != null && grid.IsValidObject)
                .Select(grid => grid.Id)
                .ToList();

            if (orphanIds.Count == 0)
            {
                return 0;
            }

            using (Transaction transaction = new Transaction(doc, "Delete Orphan Grids"))
            {
                transaction.Start();
                doc.Delete(orphanIds);
                transaction.Commit();
                return orphanIds.Count;
            }
        }

        private static string BuildResultMessage(
            int createdCount,
            int updatedCount,
            int unchangedCount,
            List<string> orphanGridNames,
            int deletedOrphanCount)
        {
            string resultMessage =
                $"Created: {createdCount}" + Environment.NewLine +
                $"Updated: {updatedCount}" + Environment.NewLine +
                $"Unchanged: {unchangedCount}";

            if (orphanGridNames.Count == 0)
            {
                return resultMessage;
            }

            resultMessage += Environment.NewLine +
                $"Orphan grids detected: {orphanGridNames.Count}";

            if (deletedOrphanCount > 0)
            {
                resultMessage += Environment.NewLine +
                    $"Deleted orphan grids: {deletedOrphanCount}";
            }
            else
            {
                resultMessage += Environment.NewLine +
                    "Orphan grids kept in model.";
            }

            resultMessage += Environment.NewLine +
                $"Orphan names: {string.Join(", ", orphanGridNames)}";

            return resultMessage;
        }

        private static Grid CreateGrid(Document doc, GridTarget target)
        {
            Grid grid = Grid.Create(doc, target.BuildLine());
            grid.Name = target.Name;
            return grid;
        }

        private static bool HasSameGeometry(Grid grid, GridTarget target)
        {
            Line existingLine = grid.Curve as Line;
            if (existingLine == null)
            {
                return false;
            }

            return ArePointsEqual(existingLine.GetEndPoint(0), target.StartPoint)
                && ArePointsEqual(existingLine.GetEndPoint(1), target.EndPoint)
                || ArePointsEqual(existingLine.GetEndPoint(0), target.EndPoint)
                && ArePointsEqual(existingLine.GetEndPoint(1), target.StartPoint);
        }

        private static bool ArePointsEqual(XYZ first, XYZ second)
        {
            return Math.Abs(first.X - second.X) <= CoordinateToleranceFeet
                && Math.Abs(first.Y - second.Y) <= CoordinateToleranceFeet
                && Math.Abs(first.Z - second.Z) <= CoordinateToleranceFeet;
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

        private static bool TryBuildCoordinatesFromSpans(
            List<string> axisNames,
            List<string> spanInputs,
            string axisLabel,
            out List<double> coordinatesMm,
            out string errorMessage)
        {
            coordinatesMm = new List<double>();
            errorMessage = null;

            if (axisNames == null || axisNames.Count == 0)
            {
                errorMessage = $"Name{axisLabel} is empty.";
                return false;
            }

            if (spanInputs == null)
            {
                errorMessage = $"ListSpan{axisLabel} is null.";
                return false;
            }

            if (spanInputs.Count != axisNames.Count - 1)
            {
                errorMessage = $"ListSpan{axisLabel}.Count must equal Name{axisLabel}.Count - 1.";
                return false;
            }

            coordinatesMm.Add(0.0);
            double currentCoordinateMm = 0.0;

            for (int i = 0; i < spanInputs.Count; i++)
            {
                double spanMm;
                if (!TryParseSpan(spanInputs[i], out spanMm))
                {
                    errorMessage = $"Span {axisLabel}{i + 1} is invalid: '{spanInputs[i]}'.";
                    return false;
                }

                if (spanMm <= 0)
                {
                    errorMessage = $"Span {axisLabel}{i + 1} must be a positive number: '{spanInputs[i]}'.";
                    return false;
                }

                currentCoordinateMm += spanMm;
                coordinatesMm.Add(currentCoordinateMm);
            }

            return true;
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

        private sealed class GridTarget
        {
            public GridTarget(string name, double coordinateMm, double perpendicularExtentMm, bool isXAxis)
            {
                Name = name;
                CoordinateMm = coordinateMm;
                PerpendicularExtentMm = perpendicularExtentMm;
                IsXAxis = isXAxis;
            }

            public string Name { get; }

            public double CoordinateMm { get; }

            public double PerpendicularExtentMm { get; }

            public bool IsXAxis { get; }

            public XYZ StartPoint
            {
                get
                {
                    double coordinateFeet = CoordinateMm * MmToFeet;
                    double startExtentFeet = -GridMarginMm * MmToFeet;

                    return IsXAxis
                        ? new XYZ(coordinateFeet, startExtentFeet, 0.0)
                        : new XYZ(startExtentFeet, coordinateFeet, 0.0);
                }
            }

            public XYZ EndPoint
            {
                get
                {
                    double coordinateFeet = CoordinateMm * MmToFeet;
                    double endExtentFeet = (PerpendicularExtentMm + GridMarginMm) * MmToFeet;

                    return IsXAxis
                        ? new XYZ(coordinateFeet, endExtentFeet, 0.0)
                        : new XYZ(endExtentFeet, coordinateFeet, 0.0);
                }
            }

            public Line BuildLine()
            {
                return Line.CreateBound(StartPoint, EndPoint);
            }
        }
    }
}
