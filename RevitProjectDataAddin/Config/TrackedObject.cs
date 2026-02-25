using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RevitProjectDataAddin
{
    public class TrackedObject<T> where T : class
    {
        private T _original; // Bản sao dữ liệu ban đầu để so sánh thay đổi
        private T _current;  // Dữ liệu hiện tại đang được theo dõi
        private INotifyPropertyChanged _currentNotifyChanged; // Để lắng nghe sự thay đổi của _current

        public event Action Changed; // Sự kiện fired khi dữ liệu bị thay đổi

        // Constructor: khởi tạo với dữ liệu ban đầu
        public TrackedObject(T initialData)
        {
            SetCurrent(initialData); // Gán dữ liệu hiện tại và đăng ký lắng nghe thay đổi
            SaveOriginal();          // Lưu snapshot ban đầu để sau này so sánh
        }

        // Property Current: dữ liệu hiện tại
        public T Current
        {
            get => _current;
            private set
            {
                if (!EqualityComparer<T>.Default.Equals(_current, value))
                {
                    // Nếu đổi Current, phải hủy đăng ký object cũ
                    UnsubscribePropertyChanged(_currentNotifyChanged);

                    _current = value;

                    // Đăng ký lắng nghe object mới
                    SubscribePropertyChanged(_current);

                    // Báo sự kiện Changed
                    Changed?.Invoke();
                }
            }
        }

        // Property Value: alias để lấy dữ liệu hiện tại (giống Current nhưng public và readonly)
        public T Value => _current;

        // Hàm để cập nhật dữ liệu hiện tại
        public void UpdateCurrent(T newData)
        {
            Current = newData;
        }

        // Hàm kiểm tra xem dữ liệu hiện tại có khác với ban đầu không
        public bool HasChanged()
        {
            if (PropertyChangeTracker.HasChanges)
            {
                return true;
            }
            var originalJson = JsonSerializer.Serialize(_original, JsonHelper.Options);
            var currentJson = JsonSerializer.Serialize(_current, JsonHelper.Options);

            if (originalJson != currentJson)
            {
                var originalObj = JsonSerializer.Deserialize<Dictionary<string, object>>(originalJson, JsonHelper.Options);
                var currentObj = JsonSerializer.Deserialize<Dictionary<string, object>>(currentJson, JsonHelper.Options);

                var differences = new List<string>();
                foreach (var key in originalObj.Keys)
                {
                    var originalValue = originalObj[key]?.ToString();
                    var currentValue = currentObj.ContainsKey(key) ? currentObj[key]?.ToString() : null;
                    if (originalValue != currentValue)
                    {
                        differences.Add($"Property {key}: Original = {originalValue}, Current = {currentValue}");
                    }
                }

                if (differences.Any())
                {
                    File.AppendAllText("Differences_Log.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Differences found:\n{string.Join("\n", differences)}\n----------------------------------------\n", Encoding.UTF8);
                    return true;
                }
            }

            File.AppendAllText("Differences_Log.txt",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No changes detected.\n----------------------------------------\n");
            return false;
        }

        public void SaveOriginal()
        {
            PropertyChangeTracker.IsTrackingEnabled = false;
            try
            {
                var serialized = JsonSerializer.Serialize(_current, JsonHelper.Options);
                _original = JsonSerializer.Deserialize<T>(serialized, JsonHelper.Options);
            }
            finally
            {
                PropertyChangeTracker.IsTrackingEnabled = true;
                PropertyChangeTracker.Reset();
            }
        }

        // Đăng ký lắng nghe sự kiện PropertyChanged nếu object hỗ trợ
        private void SubscribePropertyChanged(T data)
        {
            if (data is INotifyPropertyChanged npc)
            {
                _currentNotifyChanged = npc;
                _currentNotifyChanged.PropertyChanged += Current_PropertyChanged;
            }
        }

        // Hủy đăng ký sự kiện PropertyChanged nếu có
        private void UnsubscribePropertyChanged(INotifyPropertyChanged npc)
        {
            if (npc != null)
            {
                npc.PropertyChanged -= Current_PropertyChanged;
            }
            _currentNotifyChanged = null;
        }

        // Khi thuộc tính bên trong object thay đổi thì gọi sự kiện Changed
        private void Current_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Changed?.Invoke();
        }

        // Hàm riêng để vừa gán Current vừa đăng ký lắng nghe
        private void SetCurrent(T data)
        {
            _current = data;
            SubscribePropertyChanged(data);
        }
        public void RestoreOriginal()
        {
            if (_original == null || _current == null) return;
            //var type = typeof(T);
            //foreach (var prop in type.GetProperties())
            PropertyChangeTracker.IsTrackingEnabled = false;
            try
            {
                //if (prop.CanWrite)
                var type = typeof(T);
                foreach (var prop in type.GetProperties())
                {
                    //var value = prop.GetValue(_original);
                    //prop.SetValue(_current, value);
                    if (prop.CanWrite)
                    {
                        var value = prop.GetValue(_original);
                        prop.SetValue(_current, value);
                    }
                }
            }
            finally
            {
                PropertyChangeTracker.IsTrackingEnabled = true;
                PropertyChangeTracker.Reset();
            }
        }
    }
    public static class PropertyChangeTracker
    {
        public static bool IsTrackingEnabled = true;
        public static bool HasChanges { get; private set; }

        public static void MarkChanged()
        {
            if (!IsTrackingEnabled)
                return;
            HasChanges = true;
        }

        public static void Reset()
        {
            HasChanges = false;
        }
    }

}
