using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace RevitProjectDataAddin
{
    public static class PathBinder
    {
        public static void Bind(Control control, object context, int? subIndex = null, int? taskIndex = null, string fieldName = null)
        {
            if (control == null || context == null) return;

            if (fieldName != null)
            {
                string path = "";
                if (subIndex != null)
                {
                    path += $"SubProjects[{subIndex}]";
                    if (taskIndex != null)
                        path += $"/Tasks[{taskIndex}]";
                    path += $"/Field:{fieldName}";
                }
                else
                {
                    path = $"Field:{fieldName}";
                }
                control.Tag = path;
            }

            Bind(control, context);
        }

        public static void Bind(Control control, object context)
        {
            if (control == null || control.Tag == null) return;

            string path = control.Tag.ToString();
            if (string.IsNullOrWhiteSpace(path)) return;

            if (control is TextBox tb)
            {
                tb.TextChanged += (s, e) => PathAccessor.SetByPath(context, path, tb.Text);
            }
            else if (control is CheckBox cb)
            {
                RoutedEventHandler handler = (s, e) =>
                    PathAccessor.SetByPath(context, path, cb.IsChecked == true);
                cb.Checked += handler;
                cb.Unchecked += handler;
            }
            else if (control is Slider slider)
            {
                slider.ValueChanged += (s, e) =>
                {
                    var currentValue = PathAccessor.GetByPath(context, path);
                    if (currentValue is int)
                        PathAccessor.SetByPath(context, path, (int)slider.Value);
                    else
                        PathAccessor.SetByPath(context, path, slider.Value);
                };
            }
            else if (control is ComboBox combo)
            {
                combo.SelectionChanged += (s, e) =>
                {
                    var selected = combo.SelectedItem;
                    PathAccessor.SetByPath(context, path, selected);
                };
            }
            else if (control is DatePicker dp)
            {
                dp.SelectedDateChanged += (s, e) =>
                {
                    PathAccessor.SetByPath(context, path, dp.SelectedDate);
                };
            }
            else if (control is RadioButton rb)
            {
                rb.Checked += (s, e) =>
                {
                    if (rb.GroupName != null)
                    {
                        PathAccessor.SetByPath(context, path, rb.Content?.ToString());
                    }
                    else
                    {
                        PathAccessor.SetByPath(context, path, true);
                    }
                };
                rb.Unchecked += (s, e) =>
                {
                    if (rb.GroupName == null)
                        PathAccessor.SetByPath(context, path, false);
                };
            }
            else if (control is ToggleButton toggle)
            {
                RoutedEventHandler handler = (s, e) =>
                    PathAccessor.SetByPath(context, path, toggle.IsChecked == true);
                toggle.Checked += handler;
                toggle.Unchecked += handler;
            }
        }


        public static void BindAndFill(Control control, object context, int? subIndex = null, int? taskIndex = null, string fieldName = null)
        {
            Bind(control, context, subIndex, taskIndex, fieldName);

            string path = control.Tag?.ToString();
            if (string.IsNullOrEmpty(path)) return;

            var value = PathAccessor.GetByPath(context, path);
            if (value == null) return;

            if (control is TextBox tb)
                tb.Text = value.ToString();
            else if (control is CheckBox cb && value is bool b)
                cb.IsChecked = b;
            else if (control is Slider slider)
            {
                if (value is int i)
                    slider.Value = i;
                else if (value is double d)
                    slider.Value = d;
            }
            else if (control is ComboBox combo)
            {
                combo.SelectedItem = value;
            }
            else if (control is DatePicker dp && value is DateTime dt)
            {
                dp.SelectedDate = dt;
            }
            else if (control is RadioButton rb)
            {
                if (rb.GroupName != null && value is string selected && rb.Content?.ToString() == selected)
                {
                    rb.IsChecked = true;
                }
                else if (value is bool bval)
                {
                    rb.IsChecked = bval;
                }
            }
            else if (control is ToggleButton toggle && value is bool toggleVal)
            {
                toggle.IsChecked = toggleVal;
            }
        }


        /// <summary>
        /// Quét toàn bộ visual tree của UIElement, tìm các control có Tag và tự động bind + fill.
        /// </summary>
        public static void BindAllFromVisualTree(DependencyObject root, object context)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is Control ctrl && ctrl.Tag != null)
                {
                    BindAndFill(ctrl, context);
                }

                // Đệ quy
                BindAllFromVisualTree(child, context);
            }
        }
    }
}
