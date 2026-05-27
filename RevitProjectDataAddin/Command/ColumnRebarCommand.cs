using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class ColumnRebarCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Column", "Please select a project first.");
                return Result.Cancelled;
            }

            List<(string Label, IExternalCommand Command)> steps = new List<(string, IExternalCommand)>
            {
                ("Column", new ColumnCommand()),
                ("Column HOOP", new ColumnHoopCommand()),
                ("Column Main", new ColumnMainRebarCommand()),
                ("Yoko Nakago", new ColumnYokoNakagoCommand()),
                ("TaTe Nakago", new ColumnTaTeNakagoCommand())
            };

            ProjectManager.SuppressColumnCompletionDialogs = true;
            try
            {
                foreach ((string label, IExternalCommand command) in steps)
                {
                    string stepMessage = string.Empty;
                    Result stepResult;

                    try
                    {
                        stepResult = command.Execute(commandData, ref stepMessage, elements);
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Column", $"Stopped at {label}.\n\n{ex.Message}");
                        return Result.Failed;
                    }

                    if (stepResult == Result.Succeeded)
                    {
                        continue;
                    }

                    string detail = string.IsNullOrWhiteSpace(stepMessage) ? "The step did not complete." : stepMessage;
                    TaskDialog.Show("Column", $"Stopped at {label}.\n\n{detail}");
                    return stepResult;
                }

                return Result.Succeeded;
            }
            finally
            {
                ProjectManager.SuppressColumnCompletionDialogs = false;
            }
        }
    }
}
