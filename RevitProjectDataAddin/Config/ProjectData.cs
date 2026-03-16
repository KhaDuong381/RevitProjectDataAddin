using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.DB;
using RevitProjectDataAddin;


public class ProjectData : INotifyPropertyChanged
{
    private string _id;
    private string _projectName;
    private KihonData _kihon;
    private KesanData _kesan;
    private リスト入力 _list;
    private HaichiList _haichilist;
    private Secozu _secozu;
    public ProjectData()
    {
        リスト = new リスト入力
        {
            基礎リスト = new ObservableCollection<基礎リスト>(),
            柱リスト = new ObservableCollection<柱リスト>(),
            梁リスト = new ObservableCollection<梁リスト>()
        };
        Haichi = new HaichiList
        {
            柱配置図 = new ObservableCollection<柱配置図>(),
            梁配置図 = new ObservableCollection<梁配置図>(),
            //基礎配置図 = new ObservableCollection<基礎配置図>()
        };
        Secozu = new Secozu
        {
            柱施工図 = new ObservableCollection<柱施工図>(),
            梁施工図 = new ObservableCollection<梁施工図>()
        };
        // ====== ★ TỪ ĐÂY ↓ ======
        if (Kihon == null) Kihon = new KihonData();                 // bảo đảm có 1 bộ X/Y/Kai mặc định
        SyncHarikaiWithNameKai();                                   // bảo đảm リスト.梁リスト khớp các tầng
        if (Haichi.梁配置図 == null) Haichi.梁配置図 = new ObservableCollection<梁配置図>();
        if (Haichi.梁配置図.Count == 0) Haichi.梁配置図.Add(new 梁配置図()); // có 1 layout để chứa map

        // ★★ 柱配置図: eager init ↓
        SyncHashiraWithNameKai();                                // đảm bảo リスト.柱リスト khớp các tầng (có C0)
        if (Haichi.柱配置図 == null) Haichi.柱配置図 = new ObservableCollection<柱配置図>();
        if (Haichi.柱配置図.Count == 0) Haichi.柱配置図.Add(new 柱配置図()); // một layout để chứa map
        BuildDefaultColumnLayouts();                              // sinh đủ (Kai × Y通), mỗi key có segs theo NameX
                                                                  // ★★ 柱配置図: eager init ↑
        BuildDefaultBeamLayouts();                                   // sinh sẵn toàn bộ (Kai × (X∪Y))
        // ====== ★ END ↑ ======
    }
    //????????????????????????????????????????????????????????????????????
    private static string MakeKeyForMap(string kai, string tsu) => $"{kai}::{tsu}";
    private void BuildDefaultBeamLayouts()
    {
        // Guards
        if (Kihon == null) return;
        if (Haichi?.梁配置図 == null || Haichi.梁配置図.Count == 0) return;

        var layout = Haichi.梁配置図[0];
        if (layout.BeamSegmentsMap == null)
            layout.BeamSegmentsMap = new Dictionary<string, ObservableCollection<梁セグメント>>();

        // Danh sách tên
        var kaiList = Kihon.NameKai?.Select(x => x.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        var xList = Kihon.NameX?.Select(x => x.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        var yList = Kihon.NameY?.Select(y => y.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

        var tsuAll = xList.Concat(yList).ToList();

        foreach (var kai in kaiList)
        {
            foreach (var tsu in tsuAll)
            {
                var key = MakeKeyForMap(kai, tsu);
                if (layout.BeamSegmentsMap.ContainsKey(key))
                    continue; // không đè dữ liệu đang có

                // Quy tắc: nếu tsu thuộc X → tạo theo Y; nếu tsu thuộc Y → tạo theo X (đúng như UI)
                var along = xList.Contains(tsu) ? yList : xList;

                var segments = new ObservableCollection<梁セグメント>();
                for (int i = 0; i < Math.Max(0, along.Count - 1); i++)
                {
                    var left = along[i];
                    var right = along[i + 1];

                    segments.Add(new 梁セグメント
                    {
                        梁の符号 = null, // chuẩn hoá sau theo 梁候補リスト (y như UI)
                        上側のズレ寸法 = "300",
                        下側のズレ寸法 = "300",
                        梁の段差 = "-200",
                        タイトル = $"{kai} {tsu}の{left}-{right}",
                        左側 = left,
                        右側 = right
                    });
                }

                // Lấy danh sách ứng viên 梁 theo tầng; nếu rỗng → ["G0"] (đúng UI)
                var floorBeamList = リスト?.梁リスト?.FirstOrDefault(r => r.各階 == kai);
                var candidates = (floorBeamList?.梁 != null && floorBeamList.梁.Any())
                    ? floorBeamList.梁.Select(b => b.Name).ToList()
                    : new List<string> { "G0" };

                var first = candidates.FirstOrDefault() ?? "G0";

                // Chuẩn hoá 梁の符号 về phần tử đầu nếu null/invalid (đúng bước (5) của UI)
                foreach (var s in segments)
                {
                    if (string.IsNullOrEmpty(s.梁の符号) || !candidates.Contains(s.梁の符号))
                        s.梁の符号 = first;
                }

                layout.BeamSegmentsMap[key] = segments;
            }
        }
    }
    private void BuildDefaultColumnLayouts()
    {
        if (Kihon == null) return;
        if (Haichi?.柱配置図 == null || Haichi.柱配置図.Count == 0) return;

        var layout = Haichi.柱配置図[0];
        if (layout.BeamSegmentsMap == null)
            layout.BeamSegmentsMap = new Dictionary<string, ObservableCollection<柱セグメント>>();

        // Danh sách tên
        var kaiList = Kihon.NameKai?.Select(x => x.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        var xList = Kihon.NameX?.Select(x => x.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
        var yList = Kihon.NameY?.Select(y => y.Name).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

        foreach (var kai in kaiList)
        {
            foreach (var tsu in yList) // 通 = Y
            {
                var key = MakeKeyForMap(kai, tsu);
                if (!layout.BeamSegmentsMap.TryGetValue(key, out var segs))
                {
                    segs = new ObservableCollection<柱セグメント>();
                    layout.BeamSegmentsMap[key] = segs;
                }

                // Đồng bộ số lượng theo NameX (giống SyncSegments trong UI)
                SyncColumnSegments(segs, xList, kai, tsu);

                // Lấy ứng viên cột theo tầng; nếu rỗng → ["C0"]
                var floorColList = リスト?.柱リスト?.FirstOrDefault(r => r.各階 == kai);
                var candidates = (floorColList?.柱 != null && floorColList.柱.Any())
                    ? floorColList.柱.Select(c => c.Name).ToList()
                    : new List<string> { "C0" };
                var first = candidates.FirstOrDefault() ?? "C0";

                // Chuẩn hoá 柱の符号
                foreach (var s in segs)
                {
                    if (string.IsNullOrEmpty(s.柱の符号) || !candidates.Contains(s.柱の符号))
                        s.柱の符号 = first;
                }
            }
        }
    }
    private static void SyncColumnSegments(ObservableCollection<柱セグメント> segs, List<string> xNames, string kai, string tsu)
    {
        if (segs == null) return;

        if (segs.Count != (xNames?.Count ?? 0))
        {
            segs.Clear();
            if (xNames != null)
            {
                for (int i = 0; i < xNames.Count; i++)
                {
                    segs.Add(new 柱セグメント
                    {
                        柱の符号 = null,
                        上側のズレ = "500",
                        下側のズレ = "500",
                        左側のズレ = "500",
                        右側のズレ = "500",
                        位置表示 = $"{kai} {tsu}-{xNames[i]}",
                    });
                }
            }
            return;
        }

        // Số lượng khớp → cập nhật 位置表示 in-place
        for (int i = 0; i < segs.Count; i++)
            segs[i].位置表示 = $"{kai} {tsu}-{xNames[i]}";
    }
    //????????????????????????????????????????????????????????????????????
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value);
    }

    public KihonData Kihon
    {
        get => _kihon;
        set => SetField(ref _kihon, value);
    }

    public KesanData Kesan
    {
        get => _kesan;
        set => SetField(ref _kesan, value);
    }
    public リスト入力 リスト
    {
        get => _list;
        set => SetField(ref _list, value);
    }
    public HaichiList Haichi
    {
        get => _haichilist;
        set => SetField(ref _haichilist, value);
    }
    public Secozu Secozu
    {
        get => _secozu;
        set => SetField(ref _secozu, value);
    }

    public void SyncKisokaiWithNameKai()
    {
        // Đảm bảo số lượng của 基礎リスト và NameKai đồng bộ
        while (リスト.基礎リスト.Count < Kihon.NameKai.Count)
        {
            リスト.基礎リスト.Add(new 基礎リスト
            {
                各階 = Kihon.NameKai[リスト.基礎リスト.Count].Name,
                基礎 = new ObservableCollection<基礎> {
                    new 基礎 {
                        Name = $"F0",//{Kihon.NameKai[リスト.基礎リスト.Count].Index}
                    }
                }
            });
        }
        while (リスト.基礎リスト.Count > Kihon.NameKai.Count)
        {
            リスト.基礎リスト.RemoveAt(リスト.基礎リスト.Count - 1);
        }

        // Đồng bộ giá trị 各階 với Name trong NameKai
        for (int i = 0; i < リスト.基礎リスト.Count; i++)
        {
            リスト.基礎リスト[i].各階 = Kihon.NameKai[i].Name;
        }
    }
    public void SyncHashiraWithNameKai()
    {
        // Đảm bảo số lượng của 柱リスト và NameKai đồng bộ
        while (リスト.柱リスト.Count < Kihon.NameKai.Count)
        {
            リスト.柱リスト.Add(new 柱リスト
            {
                各階 = Kihon.NameKai[リスト.柱リスト.Count].Name,
                柱 = new ObservableCollection<柱>
                {
                    new 柱
                    {
                        Name = $"C0",//{Kihon.NameKai[リスト.柱リスト.Count].Index}
                    }
                }
            });
        }
        while (リスト.柱リスト.Count > Kihon.NameKai.Count)
        {
            リスト.柱リスト.RemoveAt(リスト.柱リスト.Count - 1);
        }

        // Đồng bộ giá trị 各階 với Name trong NameKai
        for (int i = 0; i < リスト.柱リスト.Count; i++)
        {
            リスト.柱リスト[i].各階 = Kihon.NameKai[i].Name;
        }
    }
    public void SyncHarikaiWithNameKai()
    {
        // Đảm bảo số lượng của 梁リスト và NameKai đồng bộ
        while (リスト.梁リスト.Count < Kihon.NameKai.Count)
        {
            リスト.梁リスト.Add(new 梁リスト
            {
                各階 = Kihon.NameKai[リスト.梁リスト.Count].Name,
                梁 = new ObservableCollection<梁> {
                    new 梁 {
                        Name = $"G0",//{Kihon.NameKai[リスト.梁リスト.Count].Index}
                    }
                }
            });
        }
        while (リスト.梁リスト.Count > Kihon.NameKai.Count)
        {
            リスト.梁リスト.RemoveAt(リスト.梁リスト.Count - 1);
        }

        // Đồng bộ giá trị 各階 với Name trong NameKai
        for (int i = 0; i < リスト.梁リスト.Count; i++)
        {
            リスト.梁リスト[i].各階 = Kihon.NameKai[i].Name;
        }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propName = "")
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }
}
public class Secozu : INotifyPropertyChanged
{
    public ObservableCollection<柱施工図> 柱施工図 { get; set; } = new ObservableCollection<柱施工図>();
    public ObservableCollection<梁施工図> 梁施工図 { get; set; } = new ObservableCollection<梁施工図>();

    public event PropertyChangedEventHandler PropertyChanged;

}
public class 柱施工図 : INotifyPropertyChanged
{
    private string _X通を選択;
    public string X通を選択
    {
        get => _X通を選択;
        set => SetProperty(ref _X通を選択, value);
    }

    private string _Y通を選択;
    public string Y通を選択
    {
        get => _Y通を選択;
        set => SetProperty(ref _Y通を選択, value);
    }
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }

}

public class 梁施工図 : INotifyPropertyChanged
{

    private string _階を選択;
    public string 階を選択
    {
        get => _階を選択;
        set => SetProperty(ref _階を選択, value);
    }

    private string _通を選択;
    public string 通を選択
    {
        get => _通を選択;
        set => SetProperty(ref _通を選択, value);
    }

    private ObservableCollection<GridBotsecozu> _gridbotsecozu = new ObservableCollection<GridBotsecozu>();
    public ObservableCollection<GridBotsecozu> gridbotsecozu
    {
        get => _gridbotsecozu;
        set => SetProperty(ref _gridbotsecozu, value);
    }
    public Dictionary<string, ObservableCollection<GridBotsecozu>> GridBotsecozuMap
    { get; set; } = new Dictionary<string, ObservableCollection<GridBotsecozu>>();

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }


}

public class OrangeSegOverridePersistData
{
    public double X1 { get; set; }
    public double X2 { get; set; }
}

public class GridBotsecozu : INotifyPropertyChanged
{
    private Dictionary<string, double> _tanbuHookOverrides = new Dictionary<string, double>();
    public Dictionary<string, double> TanbuHookOverrides
    {
        get => _tanbuHookOverrides;
        set => SetProperty(ref _tanbuHookOverrides, value ?? new Dictionary<string, double>());
    }

    private Dictionary<string, string> _tanbuDiameterOverrides = new Dictionary<string, string>();
    public Dictionary<string, string> TanbuDiameterOverrides
    {
        get => _tanbuDiameterOverrides;
        set => SetProperty(ref _tanbuDiameterOverrides, value ?? new Dictionary<string, string>());
    }

    private Dictionary<string, string> _orangeDimTextOverrides = new Dictionary<string, string>();
    public Dictionary<string, string> OrangeDimTextOverrides
    {
        get => _orangeDimTextOverrides;
        set => SetProperty(ref _orangeDimTextOverrides, value ?? new Dictionary<string, string>());
    }

    private Dictionary<string, double> _ankaOverrides = new Dictionary<string, double>();
    public Dictionary<string, double> AnkaOverrides
    {
        get => _ankaOverrides;
        set => SetProperty(ref _ankaOverrides, value ?? new Dictionary<string, double>());
    }

    private Dictionary<string, double> _ankaSegOverrides = new Dictionary<string, double>();
    public Dictionary<string, double> AnkaSegOverrides
    {
        get => _ankaSegOverrides;
        set => SetProperty(ref _ankaSegOverrides, value ?? new Dictionary<string, double>());
    }

    private HashSet<string> _deletedOrangeSegs = new HashSet<string>();
    public HashSet<string> DeletedOrangeSegs
    {
        get => _deletedOrangeSegs;
        set => SetProperty(ref _deletedOrangeSegs, value ?? new HashSet<string>());
    }

    private Dictionary<string, OrangeSegOverridePersistData> _orangeSegOverrides = new Dictionary<string, OrangeSegOverridePersistData>();
    public Dictionary<string, OrangeSegOverridePersistData> OrangeSegOverrides
    {
        get => _orangeSegOverrides;
        set => SetProperty(ref _orangeSegOverrides, value ?? new Dictionary<string, OrangeSegOverridePersistData>());
    }

    private Dictionary<string, List<double>> _orangeSegEqualCutPoints = new Dictionary<string, List<double>>();
    public Dictionary<string, List<double>> OrangeSegEqualCutPoints
    {
        get => _orangeSegEqualCutPoints;
        set => SetProperty(ref _orangeSegEqualCutPoints, value ?? new Dictionary<string, List<double>>());
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            OnPropertyChanged(propertyName);
        }
        return true;
    }
}
public class HaichiList : INotifyPropertyChanged
{
    public ObservableCollection<柱配置図> 柱配置図 { get; set; } = new ObservableCollection<柱配置図>();
    public ObservableCollection<梁配置図> 梁配置図 { get; set; } = new ObservableCollection<梁配置図>();

    public event PropertyChangedEventHandler PropertyChanged;

}
public class 梁配置図 : INotifyPropertyChanged
{


    private string _階を選択;
    public string 階を選択
    {
        get => _階を選択;
        set => SetProperty(ref _階を選択, value);
    }

    private string _通を選択;
    public string 通を選択
    {
        get => _通を選択;
        set => SetProperty(ref _通を選択, value);
    }

    private ObservableCollection<string> _梁候補リスト = new ObservableCollection<string> { "G0", "G1", "G2" };
    public ObservableCollection<string> 梁候補リスト
    {
        get => _梁候補リスト;
        set => SetProperty(ref _梁候補リスト, value);
    }

    private ObservableCollection<梁セグメント> _beamSegments = new ObservableCollection<梁セグメント>();
    public ObservableCollection<梁セグメント> BeamSegments
    {
        get => _beamSegments;
        set => SetProperty(ref _beamSegments, value);
    }

    public Dictionary<string, ObservableCollection<梁セグメント>> BeamSegmentsMap
    { get; set; } = new Dictionary<string, ObservableCollection<梁セグメント>>();

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }

}
public class 梁セグメント : INotifyPropertyChanged
{

    private string _梁の符号;
    public string 梁の符号
    {
        get => _梁の符号;
        set => SetProperty(ref _梁の符号, value);
    }

    private string _上側のズレ寸法;
    public string 上側のズレ寸法
    {
        get => _上側のズレ寸法;
        set => SetProperty(ref _上側のズレ寸法, value);
    }

    private string _下側のズレ寸法;
    public string 下側のズレ寸法
    {
        get => _下側のズレ寸法;
        set => SetProperty(ref _下側のズレ寸法, value);
    }

    private string _梁の段差;
    public string 梁の段差
    {
        get => _梁の段差;
        set => SetProperty(ref _梁の段差, value);
    }

    private string _タイトル;
    public string タイトル
    {
        get => _タイトル;
        set => SetProperty(ref _タイトル, value);
    }

    private string _左側;
    public string 左側
    {
        get => _左側;
        set => SetProperty(ref _左側, value);
    }

    private string _右側;
    public string 右側
    {
        get => _右側;
        set => SetProperty(ref _右側, value);
    }


    public 梁セグメント()
    {
        梁の符号 = "G0";
        上側のズレ寸法 = "300";
        下側のズレ寸法 = "300";
        梁の段差 = "-200";
        タイトル = "";
        左側 = "";
        右側 = "";
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }
}
public class 柱配置図 : INotifyPropertyChanged
{

    private string _階を選択;
    public string 階を選択
    {
        get => _階を選択;
        set => SetProperty(ref _階を選択, value);
    }

    private string _通を選択;
    public string 通を選択
    {
        get => _通を選択;
        set => SetProperty(ref _通を選択, value);
    }

    private ObservableCollection<string> _ColumnNames = new ObservableCollection<string> { "C0", "C1", "C2" };
    public ObservableCollection<string> ColumnNames
    {
        get => _ColumnNames;
        set => SetProperty(ref _ColumnNames, value);
    }

    private ObservableCollection<柱セグメント> _beamSegments = new ObservableCollection<柱セグメント>();
    public ObservableCollection<柱セグメント> BeamSegments
    {
        get => _beamSegments;
        set => SetProperty(ref _beamSegments, value);
    }
    public Dictionary<string, ObservableCollection<柱セグメント>> BeamSegmentsMap
    { get; set; } = new Dictionary<string, ObservableCollection<柱セグメント>>();

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }

}
public class 柱セグメント : INotifyPropertyChanged
{

    private string _柱の符号;
    public string 柱の符号
    {
        get => _柱の符号;
        set => SetProperty(ref _柱の符号, value);
    }

    private string _上側のズレ;
    public string 上側のズレ
    {
        get => _上側のズレ;
        set => SetProperty(ref _上側のズレ, value);
    }

    private string _下側のズレ;
    public string 下側のズレ
    {
        get => _下側のズレ;
        set => SetProperty(ref _下側のズレ, value);
    }
    private string _左側のズレ;
    public string 左側のズレ
    {
        get => _左側のズレ;
        set => SetProperty(ref _左側のズレ, value);
    }
    private string _右側のズレ;
    public string 右側のズレ
    {
        get => _右側のズレ;
        set => SetProperty(ref _右側のズレ, value);
    }

    private string _位置表示;
    public string 位置表示
    {
        get => _位置表示;
        set => SetProperty(ref _位置表示, value);
    }

    public 柱セグメント()
    {
        柱の符号 = "C0";
        上側のズレ = "500";
        下側のズレ = "500";
        左側のズレ = "500";
        右側のズレ = "500";
        位置表示 = null;

    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            // LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }
}

public class KihonData : INotifyPropertyChanged
{
    private int _numberX;
    private int _numberY;
    private int _numberKai;
    public ObservableCollection<NameX> _namex;
    public ObservableCollection<NameX> NameX
    {
        get => _namex;
        set
        {
            if (SetField(ref _namex, value))
            {
                ListenNestedList(NameX, nameof(NameX));
            }
        }
    }
    private ObservableCollection<ListSpanX> _listSpanX;
    public ObservableCollection<ListSpanX> ListSpanX
    {
        get => _listSpanX;
        set
        {
            if (SetField(ref _listSpanX, value))
            {
                ListenNestedList(ListSpanX, nameof(ListSpanX));
            }
        }
    }

    public ObservableCollection<NameY> _namey;
    public ObservableCollection<NameY> NameY
    {
        get => _namey;
        set
        {
            if (SetField(ref _namey, value))
            {
                ListenNestedList(NameY, nameof(NameY));
            }
        }
    }

    public ObservableCollection<ListSpanY> _listspany;
    public ObservableCollection<ListSpanY> ListSpanY
    {
        get => _listspany;
        set
        {
            if (SetField(ref _listspany, value))
            {
                ListenNestedList(ListSpanY, nameof(ListSpanY));
            }
        }
    }

    public ObservableCollection<NameKai> _namekai;
    public ObservableCollection<NameKai> NameKai
    {
        get => _namekai;
        set
        {
            if (SetField(ref _namekai, value))
            {
                ListenNestedList(ListSpanY, nameof(ListSpanY));
            }
        }
    }
    public ObservableCollection<ListSpanKai> _listspankai;
    public ObservableCollection<ListSpanKai> ListSpanKai
    {
        get => _listspankai;
        set
        {
            if (SetField(ref _listspankai, value))
            {
                ListenNestedList(ListSpanY, nameof(ListSpanY));
            }
        }
    }
    public KihonData()
    {
        _numberX = 3;
        _numberY = 2;
        _numberKai = 2;
        NameX = new ObservableCollection<NameX>
        {
            new NameX{ Name = "X1", Index = 1},
            new NameX{ Name = "X2", Index = 2},
            new NameX{ Name = "X3", Index = 3}
        };
        NameY = new ObservableCollection<NameY>
        {
            new NameY{ Name = "Y1", Index = 1},
            new NameY{ Name = "Y2", Index = 2},
        };
        NameKai = new ObservableCollection<NameKai>
        {
            new NameKai{ Name = "1F", Index = 1},
            new NameKai{ Name = "2F", Index = 2},
        };
        ListSpanX = new ObservableCollection<ListSpanX>
        {
            new ListSpanX{ Name = "X1-X2", Span= "6500", Index = 1},
            new ListSpanX{ Name = "X2-X3", Span= "6500", Index = 2}
        };
        ListSpanY = new ObservableCollection<ListSpanY>
        {
            new ListSpanY{ Name = "Y1-Y2", Span= "9700", Index = 1},
        };
        ListSpanKai = new ObservableCollection<ListSpanKai>
        {
            new ListSpanKai{ Name = "1F-2F", Span= "3500", Index = 1},
        };

    }
    public int NumberX
    {
        get => _numberX;
        set
        {
            if (_numberX != value)
            {
                _numberX = value;

                UpdateNameXAndSpanX();
                OnPropertyChanged(nameof(NumberX));
            }
        }
    }

    public int NumberY
    {
        get { return _numberY; }
        set
        {
            if (_numberY != value)
            {
                _numberY = value;
                UpdateNameYAndSpanY();
                OnPropertyChanged(nameof(NumberY));
            }
        }
    }

    public int NumberKai
    {
        get { return _numberKai; }
        set
        {
            if (_numberKai != value)
            {
                _numberKai = value;
                UpdateNameKaiAndSpanKai();
                OnPropertyChanged(nameof(NumberKai));
            }
        }
    }
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propName = "")
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }
    private void ListenNestedList<T>(ObservableCollection<T> list, string propertyName) where T : INotifyPropertyChanged
    {
        if (list == null) return;

        // Lắng nghe sự kiện PropertyChanged cho các phần tử trong danh sách
        foreach (var item in list)
        {
            item.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(propertyName);
            };
        }

        // Lắng nghe sự kiện CollectionChanged cho ObservableCollection
        list.CollectionChanged += (sender, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (T newItem in e.NewItems)
                {
                    newItem.PropertyChanged += (s, args) =>
                    {
                        OnPropertyChanged(propertyName);
                    };
                }
            }

            if (e.OldItems != null)
            {
                foreach (T oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= (s, args) =>
                    {
                        OnPropertyChanged(propertyName);
                    };
                }
            }

            OnPropertyChanged(propertyName);
        };
    }
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateNameXAndSpanX()
    {
        //if (!int.TryParse(NumberX, out int numberX)) return;

        // Cập nhật NameX
        while (NameX.Count < NumberX)
        {
            NameX.Add(new NameX { Name = $"X{NameX.Count + 1}" });
        }
        while (NameX.Count > NumberX)
        {
            NameX.RemoveAt(NameX.Count - 1);
        }

        // Cập nhật ListSpanX
        while (ListSpanX.Count < NumberX - 1)
        {
            int currentIndex = ListSpanX.Count + 1;

            ListSpanX.Add(new ListSpanX { Name = $"X{currentIndex}-X{currentIndex + 1}", Span = "6500" });
        }
        while (ListSpanX.Count > NumberX - 1)
        {
            ListSpanX.RemoveAt(ListSpanX.Count - 1);
        }

        // Đồng bộ Name trong ListSpanX với NameX
        for (int i = 0; i < ListSpanX.Count; i++)
        {
            ListSpanX[i].Index = i + 1;
        }
        // Cập nhật Index cho NameX
        for (int i = 0; i < NameX.Count; i++)
        {
            NameX[i].Index = i + 1;
        }
        // Kích hoạt sự kiện PropertyChanged
        //OnPropertyChanged(nameof(NameX));
        //OnPropertyChanged(nameof(ListSpanX));
    }
    public void SyncListSpanXNames()
    {
        for (int i = 0; i < ListSpanX.Count; i++)
        {
            if (i < NameX.Count - 1)
            {
                ListSpanX[i].Name = $"{NameX[i].Name}-{NameX[i + 1].Name}";
            }
            ListSpanX[i].Index = i + 1;
        }
        //OnPropertyChanged(nameof(ListSpanX));
    }
    public void SyncListSpanYNames()
    {
        for (int i = 0; i < ListSpanY.Count; i++)
        {
            if (i < NameY.Count - 1)
            {
                ListSpanY[i].Name = $"{NameY[i].Name}-{NameY[i + 1].Name}";
            }
            ListSpanY[i].Index = i + 1;
        }
        //OnPropertyChanged(nameof(ListSpanY));
    }
    private void UpdateNameYAndSpanY()
    {
        while (NameY.Count < NumberY)
        {
            NameY.Add(new NameY { Name = $"Y{NameY.Count + 1}" });

        }
        while (NameY.Count > NumberY)
        {
            NameY.RemoveAt(NameY.Count - 1);
        }
        while (ListSpanY.Count < NumberY - 1)
        {
            int currentIndex = ListSpanY.Count + 1;
            ListSpanY.Add(new ListSpanY { Name = $"Y{currentIndex}-Y{currentIndex + 1}", Span = "9700" });
        }
        while (ListSpanY.Count > NumberY - 1)
        {
            ListSpanY.RemoveAt(ListSpanY.Count - 1);
        }
        for (int i = 0; i < NameY.Count; i++)
        {
            NameY[i].Index = i + 1;
        }
        for (int i = 0; i < ListSpanY.Count; i++)
        {
            ListSpanY[i].Index = i + 1;
        }
        //OnPropertyChanged(nameof(NameY));
        //OnPropertyChanged(nameof(ListSpanY));
    }

    private void UpdateNameKaiAndSpanKai()
    {
        while (NameKai.Count < NumberKai)
        {
            NameKai.Add(new NameKai { Name = $"{NameKai.Count + 1}F" });
        }
        while (NameKai.Count > NumberKai)
        {
            NameKai.RemoveAt(NameKai.Count - 1);
        }
        while (ListSpanKai.Count < NumberKai - 1)
        {
            int currentIndex = ListSpanKai.Count + 1;
            ListSpanKai.Add(new ListSpanKai { Name = $"{currentIndex}F-{currentIndex + 1}F", Span = "3500" });
        }
        while (ListSpanKai.Count > NumberKai - 1)
        {
            ListSpanKai.RemoveAt(ListSpanKai.Count - 1);
        }
        for (int i = 0; i < NameKai.Count; i++)
        {
            NameKai[i].Index = i + 1;
        }
        for (int i = 0; i < ListSpanKai.Count; i++)
        {
            ListSpanKai[i].Index = i + 1;
        }

        //OnPropertyChanged(nameof(NameKai));
        //OnPropertyChanged(nameof(ListSpanKai));
    }

}

public class NameX : INotifyPropertyChanged
{
    private int _index;
    private string _name;

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ListSpanX : INotifyPropertyChanged
{
    private int _index;
    private string _name;
    private string _span;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Span
    {
        get { return _span; }
        set
        {
            if (_span != value)
            {
                _span = value;
                OnPropertyChanged(nameof(Span));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class NameY : INotifyPropertyChanged
{
    private int _index;
    private string _name;

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ListSpanY : INotifyPropertyChanged
{
    private int _index;
    private string _name;
    private string _span;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Span
    {
        get { return _span; }
        set
        {
            if (_span != value)
            {
                _span = value;
                OnPropertyChanged(nameof(Span));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class NameKai : INotifyPropertyChanged
{
    private int _index;
    private string _name;

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ListSpanKai : INotifyPropertyChanged
{
    private string _name;
    private string _span;
    private int _index;

    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged(nameof(Index));
            }
        }
    }
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public string Span
    {
        get { return _span; }
        set
        {
            if (_span != value)
            {
                _span = value;
                OnPropertyChanged(nameof(Span));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class KesanData : INotifyPropertyChanged
{
    // TextBox properties
    private string _tsunaga1;
    private string _tsunaga2;
    private string _teichakuuwa;
    private string _teichakushita;
    private string _nigeuwa;
    private string _nigeuwachu1;
    private string _nigeuwachu2;
    private string _nigeshitachu1;
    private string _nigeshitachu2;
    private string _nigeshita;
    private string _ankanagauwa;
    private string _ankanagauwachu;
    private string _ankanagashitachu;
    private string _ankanagashita;
    private string _ankanagahara;
    private string _nigehara;
    private string _ankanagakiriage;
    private string _ankanagasaite;
    private string _ankanagarf;
    private string _shukinkankaku;
    private string _tsunaga16;
    private string _tsunaga19;
    private string _tonarioo;
    private string _tonariko;
    private string _ikeasetsu;
    private string _tsukauwa;
    private string _tsukashita;
    private string _harihabazure;
    private string _kakushukinzure;
    private string _dansakoteisa;
    private string _chichuuwadashi;
    private string _chichuuwanakatan;
    private string _chichuuwanakacho;
    private string _chichuuwashimai;
    private string _chichushitanakatan;
    private string _chichushitanakacho;
    private string _chichushitadashi;
    private string _chichushitashimai;
    private string _ippanuwadashi;
    private string _ippanuwanakatan;
    private string _ippanuwanakacho;
    private string _ippanuwashimai;
    private string _ippanshitadashi;
    private string _ippanshitanakatan;
    private string _ippanshitanakacho;
    private string _ippanshitashimai;
    private string _dashikiriage;
    private string _dashiLmiman;
    private string _dashiLijo;
    private string _nakakiriage;
    private string _shimaikiriage;
    private string _shimaiLmiman;
    private string _shimaiLijo;
    private string _d6d16ryoanka;
    private string _d19d51ryoanka;
    private string _ryoankakiriage;
    private string _ryoankaLmiman;
    private string _ryoankaLijo;
    private string _d6d16ryotei;
    private string _d19d51ryotei;
    private string _topkiriage;
    private string _topLmiman;
    private string _topLijo;
    private string _hanchihataraki;
    private string _hanchikiriage;
    private string _hanchiLmiman;
    private string _hanchiLijo;
    private string _chichuoojogekaburi;
    private string _chichuoosayukaburi;
    private string _chichukojogekaburi;
    private string _chichukosayukaburi;
    private string _chichukataoojogekaburi;
    private string _chichukataoosayukaburi;
    private string _chichukatakojogekaburi;
    private string _chichukatakosayukaburi;
    private string _ippanoojogekaburi;
    private string _ippanoosayukaburi;
    private string _ippankojogekaburi;
    private string _ippankosayukaburi;
    private string _ippankataoojogekaburi;
    private string _ippankataoosayukaburi;
    private string _ippankatakojogekaburi;
    private string _ippankatakosayukaburi;
    private string _kabechichujogekaburi;
    private string _kabechichusayukaburi;
    private string _kabeippanjogekaburi;
    private string _kabeippansayukaburi;
    private string _STPhabaherisun;
    private string _STPseiherisun;
    private string _STPtsu;
    private string _nakagotsu;
    private string _STPhani;
    private string _nakagohataraki;
    private string _harateinaga;
    private string _haranagasei;
    private string _harakiriage;
    private string _haraLmiman;
    private string _haraLijo;
    private string _habadomehani;
    private string _fukashiSTPteinaga;
    private string _fukashishukinteinaga;
    private string _外端端部;
    private string _內端端部;
    private string _外端中央部;
    private string _內端中央部;
    private string _外端中央3;
    private string _內端中央3;
    private string _外端端部3;
    private string _內端端部3;

    // CheckBox properties
    private bool _teiuwa;
    private bool _teiuwachu;
    private bool _teishita;
    private bool _teishitachu;
    private bool _teiharigaibu;

    // ComboBox properties
    private string _tsuuwa;
    private string _tsushita;

    private Matomeprint matomeprint = Matomeprint.鉄筋１本ずつのまま印刷する;

    private Matomeprint MatomeprintInput
    {
        get => matomeprint;
        set
        {
            if (SetField(ref matomeprint, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(matomeprint1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(matomeprint2)));
            }
        }

    }

    public bool matomeprint1
    {
        get => MatomeprintInput == Matomeprint.鉄筋１本ずつのまま印刷する;
        set { if (value) MatomeprintInput = Matomeprint.鉄筋１本ずつのまま印刷する; }
    }

    public bool matomeprint2
    {
        get => MatomeprintInput == Matomeprint.同配筋の鉄筋を本数でまとめて印刷する;
        set { if (value) MatomeprintInput = Matomeprint.同配筋の鉄筋を本数でまとめて印刷する; }
    }

    // Enum và thuộc tính cho Saiteanka
    public enum SaiteankaOption
    {
        最低アンカ最低アンカ長さを必ず取る,
        直筋定着可能であれば最低アンカは取らない
    }

    private SaiteankaOption _saiteanka = SaiteankaOption.最低アンカ最低アンカ長さを必ず取る;

    private SaiteankaOption SaiteankaInput
    {
        get => _saiteanka;
        set
        {
            if (SetField(ref _saiteanka, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaiteankaOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaiteankaOption2)));
            }
        }
    }

    public bool SaiteankaOption1
    {
        get => SaiteankaInput == SaiteankaOption.最低アンカ最低アンカ長さを必ず取る;
        set { if (value) SaiteankaInput = SaiteankaOption.最低アンカ最低アンカ長さを必ず取る; }
    }

    public bool SaiteankaOption2
    {
        get => SaiteankaInput == SaiteankaOption.直筋定着可能であれば最低アンカは取らない;
        set { if (value) SaiteankaInput = SaiteankaOption.直筋定着可能であれば最低アンカは取らない; }
    }

    // Enum và thuộc tính cho Harinaitei
    public enum HarinaiteiOption
    {
        梁内定着可とする,
        梁内定着不可_アンカ定着とする,
        大梁から小梁への梁内定着は不可とする
    }

    private HarinaiteiOption _harinaitei = HarinaiteiOption.梁内定着可とする;

    private HarinaiteiOption HarinaiteiInput
    {
        get => _harinaitei;
        set
        {
            if (SetField(ref _harinaitei, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinaiteiOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinaiteiOption2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinaiteiOption3)));
            }
        }
    }

    public bool HarinaiteiOption1
    {
        get => HarinaiteiInput == HarinaiteiOption.梁内定着可とする;
        set { if (value) HarinaiteiInput = HarinaiteiOption.梁内定着可とする; }
    }

    public bool HarinaiteiOption2
    {
        get => HarinaiteiInput == HarinaiteiOption.梁内定着不可_アンカ定着とする;
        set { if (value) HarinaiteiInput = HarinaiteiOption.梁内定着不可_アンカ定着とする; }
    }

    public bool HarinaiteiOption3
    {
        get => HarinaiteiInput == HarinaiteiOption.大梁から小梁への梁内定着は不可とする;
        set { if (value) HarinaiteiInput = HarinaiteiOption.大梁から小梁への梁内定着は不可とする; }
    }

    // Enum và thuộc tính cho Shukintei
    public enum ShukinteiOption
    {
        柱せいの3_4まで飲み込んで定着する,
        設定したにげまで飲み込んで定着する
    }

    private ShukinteiOption _shukintei = ShukinteiOption.柱せいの3_4まで飲み込んで定着する;

    private ShukinteiOption ShukinteiInput
    {
        get => _shukintei;
        set
        {
            if (SetField(ref _shukintei, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShukinteiOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShukinteiOption2)));
            }
        }
    }

    public bool ShukinteiOption1
    {
        get => ShukinteiInput == ShukinteiOption.柱せいの3_4まで飲み込んで定着する;
        set { if (value) ShukinteiInput = ShukinteiOption.柱せいの3_4まで飲み込んで定着する; }
    }

    public bool ShukinteiOption2
    {
        get => ShukinteiInput == ShukinteiOption.設定したにげまで飲み込んで定着する;
        set { if (value) ShukinteiInput = ShukinteiOption.設定したにげまで飲み込んで定着する; }
    }

    // Enum và thuộc tính cho Harinainaga
    public enum HarinainagaOption
    {
        必ず柱せい_15dの定着を取る,
        定着寸法分のみでよい,
        最上階は柱せい_15dとし_一般階は定着寸法分のみ
    }

    private HarinainagaOption _harinainaga = HarinainagaOption.必ず柱せい_15dの定着を取る;

    private HarinainagaOption HarinainagaInput
    {
        get => _harinainaga;
        set
        {
            if (SetField(ref _harinainaga, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinainagaOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinainagaOption2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HarinainagaOption3)));
            }
        }
    }

    public bool HarinainagaOption1
    {
        get => HarinainagaInput == HarinainagaOption.必ず柱せい_15dの定着を取る;
        set { if (value) HarinainagaInput = HarinainagaOption.必ず柱せい_15dの定着を取る; }
    }

    public bool HarinainagaOption2
    {
        get => HarinainagaInput == HarinainagaOption.定着寸法分のみでよい;
        set { if (value) HarinainagaInput = HarinainagaOption.定着寸法分のみでよい; }
    }

    public bool HarinainagaOption3
    {
        get => HarinainagaInput == HarinainagaOption.最上階は柱せい_15dとし_一般階は定着寸法分のみ;
        set { if (value) HarinainagaInput = HarinainagaOption.最上階は柱せい_15dとし_一般階は定着寸法分のみ; }
    }

    // Enum và thuộc tính cho Tsugite
    public enum TsugiteOption
    {
        全数継手,
        半数継手
    }

    private TsugiteOption _tsugite = TsugiteOption.全数継手;

    private TsugiteOption TsugiteInput
    {
        get => _tsugite;
        set
        {
            if (SetField(ref _tsugite, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TsugiteOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TsugiteOption2)));
            }
        }
    }

    public bool TsugiteOption1
    {
        get => TsugiteInput == TsugiteOption.全数継手;
        set { if (value) TsugiteInput = TsugiteOption.全数継手; }
    }

    public bool TsugiteOption2
    {
        get => TsugiteInput == TsugiteOption.半数継手;
        set { if (value) TsugiteInput = TsugiteOption.半数継手; }
    }

    // Enum và thuộc tính cho Shitatsuhook
    public enum ShitatsuhookOption
    {
        フックを付けない,
        フックを付ける
    }

    private ShitatsuhookOption _shitatsuhook = ShitatsuhookOption.フックを付けない;

    private ShitatsuhookOption ShitatsuhookInput
    {
        get => _shitatsuhook;
        set
        {
            if (SetField(ref _shitatsuhook, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShitatsuhookOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShitatsuhookOption2)));
            }
        }
    }

    public bool ShitatsuhookOption1
    {
        get => ShitatsuhookInput == ShitatsuhookOption.フックを付けない;
        set { if (value) ShitatsuhookInput = ShitatsuhookOption.フックを付けない; }
    }

    public bool ShitatsuhookOption2
    {
        get => ShitatsuhookInput == ShitatsuhookOption.フックを付ける;
        set { if (value) ShitatsuhookInput = ShitatsuhookOption.フックを付ける; }
    }

    // Enum và thuộc tính cho Jogetsuzurasu1
    public enum Jogetsuzurasu1Option
    {
        しない,
        する
    }

    private Jogetsuzurasu1Option _jogetsuzurasu1 = Jogetsuzurasu1Option.しない;

    private Jogetsuzurasu1Option Jogetsuzurasu1Input
    {
        get => _jogetsuzurasu1;
        set
        {
            if (SetField(ref _jogetsuzurasu1, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Jogetsuzurasu1Option1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Jogetsuzurasu1Option2)));
            }
        }
    }

    public bool Jogetsuzurasu1Option1
    {
        get => Jogetsuzurasu1Input == Jogetsuzurasu1Option.しない;
        set { if (value) Jogetsuzurasu1Input = Jogetsuzurasu1Option.しない; }
    }

    public bool Jogetsuzurasu1Option2
    {
        get => Jogetsuzurasu1Input == Jogetsuzurasu1Option.する;
        set { if (value) Jogetsuzurasu1Input = Jogetsuzurasu1Option.する; }
    }

    // Enum và thuộc tính cho Jogetsuzurasu2
    public enum Jogetsuzurasu2Option
    {
        しない,
        する
    }

    private Jogetsuzurasu2Option _jogetsuzurasu2 = Jogetsuzurasu2Option.しない;

    private Jogetsuzurasu2Option Jogetsuzurasu2Input
    {
        get => _jogetsuzurasu2;
        set
        {
            if (SetField(ref _jogetsuzurasu2, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Jogetsuzurasu2Option1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Jogetsuzurasu2Option2)));
            }
        }
    }

    public bool Jogetsuzurasu2Option1
    {
        get => Jogetsuzurasu2Input == Jogetsuzurasu2Option.しない;
        set { if (value) Jogetsuzurasu2Input = Jogetsuzurasu2Option.しない; }
    }

    public bool Jogetsuzurasu2Option2
    {
        get => Jogetsuzurasu2Input == Jogetsuzurasu2Option.する;
        set { if (value) Jogetsuzurasu2Input = Jogetsuzurasu2Option.する; }
    }

    // Enum và thuộc tính cho Sizedifferent
    public enum SizedifferentOption
    {
        圧接ない,
        圧接する
    }

    private SizedifferentOption _sizedifferent = SizedifferentOption.圧接ない;

    private SizedifferentOption SizedifferentInput
    {
        get => _sizedifferent;
        set
        {
            if (SetField(ref _sizedifferent, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizedifferentOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SizedifferentOption2)));
            }
        }
    }

    public bool SizedifferentOption1
    {
        get => SizedifferentInput == SizedifferentOption.圧接ない;
        set { if (value) SizedifferentInput = SizedifferentOption.圧接ない; }
    }

    public bool SizedifferentOption2
    {
        get => SizedifferentInput == SizedifferentOption.圧接する;
        set { if (value) SizedifferentInput = SizedifferentOption.圧接する; }
    }

    // Enum và thuộc tính cho Tanbutsuryoiki
    public enum TanbutsuryoikiOption
    {
        考慮しない,
        一般階のみ考慮する,
        考慮する
    }

    private TanbutsuryoikiOption _tanbutsuryoiki = TanbutsuryoikiOption.考慮しない;

    private TanbutsuryoikiOption TanbutsuryoikiInput
    {
        get => _tanbutsuryoiki;
        set
        {
            if (SetField(ref _tanbutsuryoiki, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TanbutsuryoikiOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TanbutsuryoikiOption2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TanbutsuryoikiOption3)));
            }
        }
    }

    public bool TanbutsuryoikiOption1
    {
        get => TanbutsuryoikiInput == TanbutsuryoikiOption.考慮しない;
        set { if (value) TanbutsuryoikiInput = TanbutsuryoikiOption.考慮しない; }
    }

    public bool TanbutsuryoikiOption2
    {
        get => TanbutsuryoikiInput == TanbutsuryoikiOption.一般階のみ考慮する;
        set { if (value) TanbutsuryoikiInput = TanbutsuryoikiOption.一般階のみ考慮する; }
    }

    public bool TanbutsuryoikiOption3
    {
        get => TanbutsuryoikiInput == TanbutsuryoikiOption.考慮する;
        set { if (value) TanbutsuryoikiInput = TanbutsuryoikiOption.考慮する; }
    }

    // Enum và thuộc tính cho Kobaichouten
    public enum KobaichoutenOption
    {
        柱面で曲げて通す,
        延長した交点で曲げて通す
    }

    private KobaichoutenOption _kobaichouten = KobaichoutenOption.柱面で曲げて通す;

    private KobaichoutenOption KobaichoutenInput
    {
        get => _kobaichouten;
        set
        {
            if (SetField(ref _kobaichouten, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KobaichoutenOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KobaichoutenOption2)));
            }
        }
    }

    public bool KobaichoutenOption1
    {
        get => KobaichoutenInput == KobaichoutenOption.柱面で曲げて通す;
        set { if (value) KobaichoutenInput = KobaichoutenOption.柱面で曲げて通す; }
    }

    public bool KobaichoutenOption2
    {
        get => KobaichoutenInput == KobaichoutenOption.延長した交点で曲げて通す;
        set { if (value) KobaichoutenInput = KobaichoutenOption.延長した交点で曲げて通す; }
    }

    // Enum và thuộc tính cho Hanchishitakin
    public enum HanchishitakinOption
    {
        角の下筋は折曲げとする,
        全数切断定着にする
    }

    private HanchishitakinOption _hanchishitakin = HanchishitakinOption.角の下筋は折曲げとする;

    private HanchishitakinOption HanchishitakinInput
    {
        get => _hanchishitakin;
        set
        {
            if (SetField(ref _hanchishitakin, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchishitakinOption1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchishitakinOption2)));
            }
        }
    }

    public bool HanchishitakinOption1
    {
        get => HanchishitakinInput == HanchishitakinOption.角の下筋は折曲げとする;
        set { if (value) HanchishitakinInput = HanchishitakinOption.角の下筋は折曲げとする; }
    }

    public bool HanchishitakinOption2
    {
        get => HanchishitakinInput == HanchishitakinOption.全数切断定着にする;
        set { if (value) HanchishitakinInput = HanchishitakinOption.全数切断定着にする; }
    }

    public enum Chichudashiuwa
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Chichudashiuwa _chichudashiuwa = Chichudashiuwa.最長定尺の長さまで使用;
    private Chichudashiuwa ChichudashiuwaInput
    {
        get => _chichudashiuwa;
        set
        {
            if (SetField(ref _chichudashiuwa, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichudashiuwa1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichudashiuwa2)));
            }
        }
    }

    public bool Chichudashiuwa1
    {
        get => ChichudashiuwaInput == Chichudashiuwa.最長定尺の長さまで使用;
        set { if (value) ChichudashiuwaInput = Chichudashiuwa.最長定尺の長さまで使用; }
    }

    public bool Chichudashiuwa2
    {
        get => ChichudashiuwaInput == Chichudashiuwa.指定のLmmまでとする;
        set { if (value) ChichudashiuwaInput = Chichudashiuwa.指定のLmmまでとする; }
    }

    public enum Chichushimaiuwa
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Chichushimaiuwa _hichushimaiuwa = Chichushimaiuwa.最長定尺の長さまで使用;
    private Chichushimaiuwa ChichushimaiuwaInput
    {
        get => _hichushimaiuwa;
        set
        {
            if (SetField(ref _hichushimaiuwa, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichushimaiuwa1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichushimaiuwa2)));
            }
        }
    }

    public bool Chichushimaiuwa1
    {
        get => ChichushimaiuwaInput == Chichushimaiuwa.最長定尺の長さまで使用;
        set { if (value) ChichushimaiuwaInput = Chichushimaiuwa.最長定尺の長さまで使用; }
    }

    public bool Chichushimaiuwa2
    {
        get => ChichushimaiuwaInput == Chichushimaiuwa.指定のLmmまでとする;
        set { if (value) ChichushimaiuwaInput = Chichushimaiuwa.指定のLmmまでとする; }
    }

    //
    public enum Chichudashishita
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Chichudashishita _Chichudashishita = Chichudashishita.最長定尺の長さまで使用;
    private Chichudashishita ChichudashishitaInput
    {
        get => _Chichudashishita;
        set
        {
            if (SetField(ref _Chichudashishita, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichudashishita1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichudashishita2)));
            }
        }
    }

    public bool Chichudashishita1
    {
        get => ChichudashishitaInput == Chichudashishita.最長定尺の長さまで使用;
        set { if (value) ChichudashishitaInput = Chichudashishita.最長定尺の長さまで使用; }
    }

    public bool Chichudashishita2
    {
        get => ChichudashishitaInput == Chichudashishita.指定のLmmまでとする;
        set { if (value) ChichudashishitaInput = Chichudashishita.指定のLmmまでとする; }
    }
    //

    public enum Chichushimaishita
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Chichushimaishita _Chichushimaishita = Chichushimaishita.最長定尺の長さまで使用;
    private Chichushimaishita ChichushimaishitaInput
    {
        get => _Chichushimaishita;
        set
        {
            if (SetField(ref _Chichushimaishita, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichushimaishita1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Chichushimaishita2)));
            }
        }
    }

    public bool Chichushimaishita1
    {
        get => ChichushimaishitaInput == Chichushimaishita.最長定尺の長さまで使用;
        set { if (value) ChichushimaishitaInput = Chichushimaishita.最長定尺の長さまで使用; }
    }

    public bool Chichushimaishita2
    {
        get => ChichushimaishitaInput == Chichushimaishita.指定のLmmまでとする;
        set { if (value) ChichushimaishitaInput = Chichushimaishita.指定のLmmまでとする; }
    }
    //

    public enum Ippandashiuwa
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Ippandashiuwa _Ippandashiuwa = Ippandashiuwa.最長定尺の長さまで使用;
    private Ippandashiuwa IppandashiuwaInput
    {
        get => _Ippandashiuwa;
        set
        {
            if (SetField(ref _Ippandashiuwa, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippandashiuwa1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippandashiuwa2)));
            }
        }
    }

    public bool Ippandashiuwa1
    {
        get => IppandashiuwaInput == Ippandashiuwa.最長定尺の長さまで使用;
        set { if (value) IppandashiuwaInput = Ippandashiuwa.最長定尺の長さまで使用; }
    }

    public bool Ippandashiuwa2
    {
        get => IppandashiuwaInput == Ippandashiuwa.指定のLmmまでとする;
        set { if (value) IppandashiuwaInput = Ippandashiuwa.指定のLmmまでとする; }
    }
    //

    public enum Ippanshimaiuwa
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Ippanshimaiuwa _Ippanshimaiuwa = Ippanshimaiuwa.最長定尺の長さまで使用;
    private Ippanshimaiuwa IppanshimaiuwaInput
    {
        get => _Ippanshimaiuwa;
        set
        {
            if (SetField(ref _Ippanshimaiuwa, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippanshimaiuwa1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippanshimaiuwa2)));
            }
        }
    }

    public bool Ippanshimaiuwa1
    {
        get => IppanshimaiuwaInput == Ippanshimaiuwa.最長定尺の長さまで使用;
        set { if (value) IppanshimaiuwaInput = Ippanshimaiuwa.最長定尺の長さまで使用; }
    }

    public bool Ippanshimaiuwa2
    {
        get => IppanshimaiuwaInput == Ippanshimaiuwa.指定のLmmまでとする;
        set { if (value) IppanshimaiuwaInput = Ippanshimaiuwa.指定のLmmまでとする; }
    }

    //

    public enum Ippandashishita
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Ippandashishita _Ippandashishita = Ippandashishita.最長定尺の長さまで使用;
    private Ippandashishita IppandashishitaInput
    {
        get => _Ippandashishita;
        set
        {
            if (SetField(ref _Ippandashishita, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippandashishita1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippandashishita2)));
            }
        }
    }

    public bool Ippandashishita1
    {
        get => IppandashishitaInput == Ippandashishita.最長定尺の長さまで使用;
        set { if (value) IppandashishitaInput = Ippandashishita.最長定尺の長さまで使用; }
    }

    public bool Ippandashishita2
    {
        get => IppandashishitaInput == Ippandashishita.指定のLmmまでとする;
        set { if (value) IppandashishitaInput = Ippandashishita.指定のLmmまでとする; }
    }
    //


    public enum Ippanshimaishita
    {
        最長定尺の長さまで使用,
        指定のLmmまでとする
    }

    private Ippanshimaishita _Ippanshimaishita = Ippanshimaishita.最長定尺の長さまで使用;
    private Ippanshimaishita IppanshimaishitaInput
    {
        get => _Ippanshimaishita;
        set
        {
            if (SetField(ref _Ippanshimaishita, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippanshimaishita1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ippanshimaishita2)));
            }
        }
    }

    public bool Ippanshimaishita1
    {
        get => IppanshimaishitaInput == Ippanshimaishita.最長定尺の長さまで使用;
        set { if (value) IppanshimaishitaInput = Ippanshimaishita.最長定尺の長さまで使用; }
    }

    public bool Ippanshimaishita2
    {
        get => IppanshimaishitaInput == Ippanshimaishita.指定のLmmまでとする;
        set { if (value) IppanshimaishitaInput = Ippanshimaishita.指定のLmmまでとする; }
    }

    //


    public enum Topkinuekaburikoryo
    {
        しない,
        する
    }

    private Topkinuekaburikoryo _Topkinuekaburikoryo = Topkinuekaburikoryo.しない;
    private Topkinuekaburikoryo TopkinuekaburikoryoInput
    {
        get => _Topkinuekaburikoryo;
        set
        {
            if (SetField(ref _Topkinuekaburikoryo, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Topkinuekaburikoryo1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Topkinuekaburikoryo2)));
            }
        }
    }

    public bool Topkinuekaburikoryo1
    {
        get => TopkinuekaburikoryoInput == Topkinuekaburikoryo.しない;
        set { if (value) TopkinuekaburikoryoInput = Topkinuekaburikoryo.しない; }
    }

    public bool Topkinuekaburikoryo2
    {
        get => TopkinuekaburikoryoInput == Topkinuekaburikoryo.する;
        set { if (value) TopkinuekaburikoryoInput = Topkinuekaburikoryo.する; }
    }
    // Fix for CS1001: Identifier expected and CS0102: The type 'KesanData.梁の主筋の位置' already contains a definition for ''  
    // The issue is caused by the invalid enum definition `1,2,3,4`. Enum members must have valid identifiers.  
    // Correcting the enum definition for `梁の主筋の位置` with meaningful identifiers.  

    public enum 梁の主筋の位置
    {
        Option1,
        Option2,
        Option3,
        Option4
    }

    private 梁の主筋の位置 _梁の主筋の位置 = 梁の主筋の位置.Option1;
    private 梁の主筋の位置 梁の主筋の位置Input
    {
        get => _梁の主筋の位置;
        set
        {
            if (SetField(ref _梁の主筋の位置, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(梁の主筋の位置1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(梁の主筋の位置2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(梁の主筋の位置3)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(梁の主筋の位置4)));
            }
        }
    }

    public bool 梁の主筋の位置1
    {
        get => 梁の主筋の位置Input == 梁の主筋の位置.Option1;
        set { if (value) 梁の主筋の位置Input = 梁の主筋の位置.Option1; }
    }

    public bool 梁の主筋の位置2
    {
        get => 梁の主筋の位置Input == 梁の主筋の位置.Option2;
        set { if (value) 梁の主筋の位置Input = 梁の主筋の位置.Option2; }
    }
    public bool 梁の主筋の位置3
    {
        get => 梁の主筋の位置Input == 梁の主筋の位置.Option3;
        set { if (value) 梁の主筋の位置Input = 梁の主筋の位置.Option3; }
    }
    public bool 梁の主筋の位置4
    {
        get => 梁の主筋の位置Input == 梁の主筋の位置.Option4;
        set { if (value) 梁の主筋の位置Input = 梁の主筋の位置.Option4; }
    }
    //
    public enum HanchihokyoSTP
    {
        一般部のあばら筋１本掛け,
        一般部のあばら筋２本掛け,
        一般部のあばら筋を１サイズアップして１本掛け,
        一般部のあばら筋を１サイズアップして２本掛け
    }

    private HanchihokyoSTP _HanchihokyoSTP = HanchihokyoSTP.一般部のあばら筋１本掛け;
    private HanchihokyoSTP HanchihokyoSTPInput
    {
        get => _HanchihokyoSTP;
        set
        {
            if (SetField(ref _HanchihokyoSTP, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchihokyoSTP1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchihokyoSTP2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchihokyoSTP3)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HanchihokyoSTP4)));
            }
        }
    }

    public bool HanchihokyoSTP1
    {
        get => HanchihokyoSTPInput == HanchihokyoSTP.一般部のあばら筋１本掛け;
        set { if (value) HanchihokyoSTPInput = HanchihokyoSTP.一般部のあばら筋１本掛け; }
    }

    public bool HanchihokyoSTP2
    {
        get => HanchihokyoSTPInput == HanchihokyoSTP.一般部のあばら筋２本掛け;
        set { if (value) HanchihokyoSTPInput = HanchihokyoSTP.一般部のあばら筋２本掛け; }
    }
    public bool HanchihokyoSTP3
    {
        get => HanchihokyoSTPInput == HanchihokyoSTP.一般部のあばら筋を１サイズアップして１本掛け;
        set { if (value) HanchihokyoSTPInput = HanchihokyoSTP.一般部のあばら筋を１サイズアップして１本掛け; }
    }
    public bool HanchihokyoSTP4
    {
        get => HanchihokyoSTPInput == HanchihokyoSTP.一般部のあばら筋を１サイズアップして２本掛け;
        set { if (value) HanchihokyoSTPInput = HanchihokyoSTP.一般部のあばら筋を１サイズアップして２本掛け; }
    }
    //
    public enum FukashiSTPteichakutenba
    {
        Option1,
        Option2,
    }

    private FukashiSTPteichakutenba _FukashiSTPteichakutenba = FukashiSTPteichakutenba.Option1;
    private FukashiSTPteichakutenba FukashiSTPteichakutenbaInput
    {
        get => _FukashiSTPteichakutenba;
        set
        {
            if (SetField(ref _FukashiSTPteichakutenba, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FukashiSTPteichakutenba1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FukashiSTPteichakutenba2)));
            }
        }
    }

    public bool FukashiSTPteichakutenba1
    {
        get => FukashiSTPteichakutenbaInput == FukashiSTPteichakutenba.Option1;
        set { if (value) FukashiSTPteichakutenbaInput = FukashiSTPteichakutenba.Option1; }
    }

    public bool FukashiSTPteichakutenba2
    {
        get => FukashiSTPteichakutenbaInput == FukashiSTPteichakutenba.Option2;
        set { if (value) FukashiSTPteichakutenbaInput = FukashiSTPteichakutenba.Option2; }
    }
    //

    public enum Fukashishukinteichaku
    {
        Option1,
        Option2,
        Option3
    }

    private Fukashishukinteichaku _Fukashishukinteichaku = Fukashishukinteichaku.Option1;
    private Fukashishukinteichaku FukashishukinteichakuInput
    {
        get => _Fukashishukinteichaku;
        set
        {
            if (SetField(ref _Fukashishukinteichaku, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fukashishukinteichaku1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fukashishukinteichaku2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fukashishukinteichaku3)));
            }
        }
    }

    public bool Fukashishukinteichaku1
    {
        get => FukashishukinteichakuInput == Fukashishukinteichaku.Option1;
        set { if (value) FukashishukinteichakuInput = Fukashishukinteichaku.Option1; }
    }

    public bool Fukashishukinteichaku2
    {
        get => FukashishukinteichakuInput == Fukashishukinteichaku.Option2;
        set { if (value) FukashishukinteichakuInput = Fukashishukinteichaku.Option2; }
    }
    public bool Fukashishukinteichaku3
    {
        get => FukashishukinteichakuInput == Fukashishukinteichaku.Option3;
        set { if (value) FukashishukinteichakuInput = Fukashishukinteichaku.Option3; }
    }
    //

    public enum Hatarakion
    {
        Option1,
        Option2,
    }

    private Hatarakion _Hatarakion = Hatarakion.Option1;
    private Hatarakion HatarakionInput
    {
        get => _Hatarakion;
        set
        {
            if (SetField(ref _Hatarakion, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hatarakion1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hatarakion2)));
            }
        }
    }

    public bool Hatarakion1
    {
        get => HatarakionInput == Hatarakion.Option1;
        set { if (value) HatarakionInput = Hatarakion.Option1; }
    }

    public bool Hatarakion2
    {
        get => HatarakionInput == Hatarakion.Option2;
        set { if (value) HatarakionInput = Hatarakion.Option2; }
    }
    //

    public enum Ankanagaon
    {
        Option1,
        Option2,
    }

    private Ankanagaon _Ankanagaon = Ankanagaon.Option1;
    private Ankanagaon AnkanagaonInput
    {
        get => _Ankanagaon;
        set
        {
            if (SetField(ref _Ankanagaon, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ankanagaon1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ankanagaon2)));
            }
        }
    }

    public bool Ankanagaon1
    {
        get => AnkanagaonInput == Ankanagaon.Option1;
        set { if (value) AnkanagaonInput = Ankanagaon.Option1; }
    }

    public bool Ankanagaon2
    {
        get => AnkanagaonInput == Ankanagaon.Option2;
        set { if (value) AnkanagaonInput = Ankanagaon.Option2; }
    }
    //
    public enum Nigeon
    {
        Option1,
        Option2,
    }

    private Nigeon _Nigeon = Nigeon.Option1;
    private Nigeon NigeonInput
    {
        get => _Nigeon;
        set
        {
            if (SetField(ref _Nigeon, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nigeon1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nigeon2)));
            }
        }
    }

    public bool Nigeon1
    {
        get => NigeonInput == Nigeon.Option1;
        set { if (value) NigeonInput = Nigeon.Option1; }
    }

    public bool Nigeon2
    {
        get => NigeonInput == Nigeon.Option2;
        set { if (value) NigeonInput = Nigeon.Option2; }
    }
    //
    public enum Topkindimon
    {
        Option1,
        Option2,
    }

    private Topkindimon _Topkindimon = Topkindimon.Option1;
    private Topkindimon TopkindimonInput
    {
        get => _Topkindimon;
        set
        {
            if (SetField(ref _Topkindimon, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Topkindimon1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Topkindimon2)));
            }
        }
    }

    public bool Topkindimon1
    {
        get => TopkindimonInput == Topkindimon.Option1;
        set { if (value) TopkindimonInput = Topkindimon.Option1; }
    }

    public bool Topkindimon2
    {
        get => TopkindimonInput == Topkindimon.Option2;
        set { if (value) TopkindimonInput = Topkindimon.Option2; }
    }
    //
    public enum Nakagozuon
    {
        Option1,
        Option2,
    }

    private Nakagozuon _Nakagozuon = Nakagozuon.Option1;
    private Nakagozuon NakagozuonInput
    {
        get => _Nakagozuon;
        set
        {
            if (SetField(ref _Nakagozuon, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nakagozuon1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nakagozuon2)));
            }
        }
    }

    public bool Nakagozuon1
    {
        get => NakagozuonInput == Nakagozuon.Option1;
        set { if (value) NakagozuonInput = Nakagozuon.Option1; }
    }

    public bool Nakagozuon2
    {
        get => NakagozuonInput == Nakagozuon.Option2;
        set { if (value) NakagozuonInput = Nakagozuon.Option2; }
    }
    //
    public enum Nakago2on
    {
        Option1,
        Option2,
    }

    private Nakago2on _Nakago2on = Nakago2on.Option1;
    private Nakago2on Nakago2onInput
    {
        get => _Nakago2on;
        set
        {
            if (SetField(ref _Nakago2on, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nakago2on1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nakago2on2)));
            }
        }
    }

    public bool Nakago2on1
    {
        get => Nakago2onInput == Nakago2on.Option1;
        set { if (value) Nakago2onInput = Nakago2on.Option1; }
    }

    public bool Nakago2on2
    {
        get => Nakago2onInput == Nakago2on.Option2;
        set { if (value) Nakago2onInput = Nakago2on.Option2; }
    }
    //
    //
    public enum STPzaishitsuon
    {
        Option1,
        Option2,
    }

    private STPzaishitsuon _STPzaishitsuon = STPzaishitsuon.Option1;
    private STPzaishitsuon STPzaishitsuonInput
    {
        get => _STPzaishitsuon;
        set
        {
            if (SetField(ref _STPzaishitsuon, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(STPzaishitsuon1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(STPzaishitsuon2)));
            }
        }
    }

    public bool STPzaishitsuon1
    {
        get => STPzaishitsuonInput == STPzaishitsuon.Option1;
        set { if (value) STPzaishitsuonInput = STPzaishitsuon.Option1; }
    }

    public bool STPzaishitsuon2
    {
        get => STPzaishitsuonInput == STPzaishitsuon.Option2;
        set { if (value) STPzaishitsuonInput = STPzaishitsuon.Option2; }
    }
    //
    public enum Tsuryoikion
    {
        Option1,
        Option2,
    }

    private Tsuryoikion _Tsuryoikion = Tsuryoikion.Option1;
    private Tsuryoikion TsuryoikionInput
    {
        get => _Tsuryoikion;
        set
        {
            if (SetField(ref _Tsuryoikion, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tsuryoikion1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tsuryoikion2)));
            }
        }
    }

    public bool Tsuryoikion1
    {
        get => TsuryoikionInput == Tsuryoikion.Option1;
        set { if (value) TsuryoikionInput = Tsuryoikion.Option1; }
    }

    public bool Tsuryoikion2
    {
        get => TsuryoikionInput == Tsuryoikion.Option2;
        set { if (value) TsuryoikionInput = Tsuryoikion.Option2; }
    }
    //
    public enum Printsize
    {
        Option1,
        Option2,
    }

    private Printsize _Printsize = Printsize.Option1;
    private Printsize PrintsizeInput
    {
        get => _Printsize;
        set
        {
            if (SetField(ref _Printsize, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Printsize1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Printsize2)));
            }
        }
    }

    public bool Printsize1
    {
        get => PrintsizeInput == Printsize.Option1;
        set { if (value) PrintsizeInput = Printsize.Option1; }
    }

    public bool Printsize2
    {
        get => PrintsizeInput == Printsize.Option2;
        set { if (value) PrintsizeInput = Printsize.Option2; }
    }
    //
    public enum Printmuki
    {
        Option1,
        Option2,
    }

    private Printmuki _Printmuki = Printmuki.Option1;
    private Printmuki PrintmukiInput
    {
        get => _Printmuki;
        set
        {
            if (SetField(ref _Printmuki, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Printmuki1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Printmuki2)));
            }
        }
    }

    public bool Printmuki1
    {
        get => PrintmukiInput == Printmuki.Option1;
        set { if (value) PrintmukiInput = Printmuki.Option1; }
    }

    public bool Printmuki2
    {
        get => PrintmukiInput == Printmuki.Option2;
        set { if (value) PrintmukiInput = Printmuki.Option2; }
    }
    //
    public enum Tsuryoikiprint
    {
        Option1,
        Option2,
    }

    private Tsuryoikiprint _Tsuryoikiprint = Tsuryoikiprint.Option1;
    private Tsuryoikiprint TsuryoikiprintInput
    {
        get => _Tsuryoikiprint;
        set
        {
            if (SetField(ref _Tsuryoikiprint, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tsuryoikiprint1)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tsuryoikiprint2)));
            }
        }
    }

    public bool Tsuryoikiprint1
    {
        get => TsuryoikiprintInput == Tsuryoikiprint.Option1;
        set { if (value) TsuryoikiprintInput = Tsuryoikiprint.Option1; }
    }

    public bool Tsuryoikiprint2
    {
        get => TsuryoikiprintInput == Tsuryoikiprint.Option2;
        set { if (value) TsuryoikiprintInput = Tsuryoikiprint.Option2; }
    }
    //

    [JsonIgnore]
    private Document _doc;

    [JsonIgnore]
    private ProjectData _projectData;

    [JsonIgnore]
    public Document Doc
    {
        get => _doc;
        set => _doc = value;
    }

    [JsonIgnore]
    public ProjectData ProjectData
    {
        get => _projectData;
        set => _projectData = value;
    }

    public KesanData()
    {
        // Thiết lập giá trị mặc định cho TextBox
        Tsunaga1 = "自動";
        Tsunaga2 = "自動";
        TeichakuUwa = "40";
        TeichakuShita = "40";
        NigeUwa = "180";
        NigeUwaChu1 = "180";
        NigeUwaChu2 = "180";
        NigeShitaChu1 = "180";
        NigeShitaChu2 = "180";
        NigeShita = "180";
        AnkaNagaUwa = "250";
        AnkaNagaUwaChu = "250";
        AnkaNagaShitaChu = "250";
        AnkaNagaShita = "250";
        AnkaNagaHara = "指定無";
        NigeHara = "指定無";
        AnkaNagaKiriage = "50";
        AnkaNagaSaite = "200";
        AnkaNagaRF = "40";
        ShukinKankaku = "80";
        Tsunaga16 = "40";
        Tsunaga19 = "1";
        TonariOo = "0";
        TonariKo = "0";
        IkeAsetsu = "2";
        TsuKaUwa = "1";
        TsuKaShita = "1";
        HariHabaZure = "99";
        KakuShukinZure = "49";
        DansaKoteisa = "25";
        ChichuUwaDashi = "0";
        ChichuUwaNakaTan = "3500";
        ChichuUwaNakaCho = "12000";
        ChichuUwaShimai = "0";
        ChichuShitaNakaTan = "3500";
        ChichuShitaNakaCho = "12000";
        ChichuShitaDashi = "0";
        ChichuShitaShimai = "0";
        IppanUwaDashi = "0";
        IppanUwaNakaTan = "3500";
        IppanUwaNakaCho = "12000";
        IppanUwaShimai = "0";
        IppanShitaDashi = "0";
        IppanShitaNakaTan = "3500";
        IppanShitaNakaCho = "12000";
        IppanShitaShimai = "0";
        DashiKiriage = "3500";
        DashiLMiman = "250";
        DashiLIjo = "500";
        NakaKiriage = "500";
        ShimaiKiriage = "3500";
        ShimaiLMiman = "250";
        ShimaiLIjo = "500";
        D6D16Ryoanka = "6500";
        D19D51Ryoanka = "8500";
        RyoankaKiriage = "3500";
        RyoankaLMiman = "250";
        RyoankaLIjo = "500";
        D6D16Ryotei = "6500";
        D19D51Ryotei = "8500";
        TopKiriage = "3500";
        TopLMiman = "250";
        TopLIjo = "500";
        HanchiHataraki = "250";
        HanchiKiriage = "3500";
        HanchiLMiman = "250";
        HanchiLIjo = "500";
        ChichuOoJogeKaburi = "100";
        ChichuOoSayuKaburi = "100";
        ChichuKoJogeKaburi = "100";
        ChichuKoSayuKaburi = "100";
        ChichuKataOoJogeKaburi = "100";
        ChichuKataOoSayuKaburi = "100";
        ChichuKataKoJogeKaburi = "100";
        ChichuKataKoSayuKaburi = "100";
        IppanOoJogeKaburi = "100";
        IppanOoSayuKaburi = "100";
        IppanKoJogeKaburi = "100";
        IppanKoSayuKaburi = "100";
        IppanKataOoJogeKaburi = "100";
        IppanKataOoSayuKaburi = "100";
        IppanKataKoJogeKaburi = "100";
        IppanKataKoSayuKaburi = "100";
        KabeChichuJogeKaburi = "100";
        KabeChichuSayuKaburi = "100";
        KabeIppanJogeKaburi = "100";
        KabeIppanSayuKaburi = "100";
        STPHabaHerisun = "25";
        STPSeiHerisun = "25";
        STPTsu = "40";
        NakagoTsu = "40";
        STPHani = "20";
        NakagoHataraki = "0";
        HaraTeiNaga = "200";
        HaraNagaSei = "5500";
        HaraKiriage = "3500";
        HaraLMiman = "250";
        HaraLIjo = "250";
        HabadomeHani = "30";
        FukashiSTPTeiNaga = "40";
        FukashiShukinTeiNaga = "35";
        GaitanTanbuTopYochou = "15";
        NaitanTanbuTopYochou = "15";
        GaitanChubuTopYochou = "15";
        NaitanChubuTopYochou = "15";
        GaitanChubuTopYochou3 = "15";
        NaitanChubuTopYochou3 = "15";
        GaitanTanbuTopYochou3 = "15";
        NaitanTanbuTopYochou3 = "15";

        // Thiết lập giá trị mặc định cho CheckBox
        Teiuwa = true;
        Teiuwachu = true;
        Teishitachu = true;
        Teishita = true;
        

        // Thiết lập giá trị mặc định cho ComboBox "中央部", "端部"
        Tsuuwa = "中央部";
        Tsushita = "端部";
    }

    public KesanData(Document doc, ProjectData projectData) : this()
    {
        _doc = doc;
        _projectData = projectData;

    }

    // TextBox properties
    public string Tsunaga1 { get => _tsunaga1; set => SetField(ref _tsunaga1, value); }
    public string Tsunaga2 { get => _tsunaga2; set => SetField(ref _tsunaga2, value); }
    public string TeichakuUwa { get => _teichakuuwa; set => SetField(ref _teichakuuwa, value); }
    public string TeichakuShita { get => _teichakushita; set => SetField(ref _teichakushita, value); }
    public string NigeUwa { get => _nigeuwa; set => SetField(ref _nigeuwa, value); }
    public string NigeUwaChu1 { get => _nigeuwachu1; set => SetField(ref _nigeuwachu1, value); }
    public string NigeUwaChu2 { get => _nigeuwachu2; set => SetField(ref _nigeuwachu2, value); }
    public string NigeShitaChu1 { get => _nigeshitachu1; set => SetField(ref _nigeshitachu1, value); }
    public string NigeShitaChu2 { get => _nigeshitachu2; set => SetField(ref _nigeshitachu2, value); }
    public string NigeShita { get => _nigeshita; set => SetField(ref _nigeshita, value); }
    public string AnkaNagaUwa { get => _ankanagauwa; set => SetField(ref _ankanagauwa, value); }
    public string AnkaNagaUwaChu { get => _ankanagauwachu; set => SetField(ref _ankanagauwachu, value); }
    public string AnkaNagaShitaChu { get => _ankanagashitachu; set => SetField(ref _ankanagashitachu, value); }
    public string AnkaNagaShita { get => _ankanagashita; set => SetField(ref _ankanagashita, value); }
    public string AnkaNagaHara { get => _ankanagahara; set => SetField(ref _ankanagahara, value); }
    public string NigeHara { get => _nigehara; set => SetField(ref _nigehara, value); }
    public string AnkaNagaKiriage { get => _ankanagakiriage; set => SetField(ref _ankanagakiriage, value); }
    public string AnkaNagaSaite { get => _ankanagasaite; set => SetField(ref _ankanagasaite, value); }
    public string AnkaNagaRF { get => _ankanagarf; set => SetField(ref _ankanagarf, value); }
    public string ShukinKankaku { get => _shukinkankaku; set => SetField(ref _shukinkankaku, value); }
    public string Tsunaga16 { get => _tsunaga16; set => SetField(ref _tsunaga16, value); }
    public string Tsunaga19 { get => _tsunaga19; set => SetField(ref _tsunaga19, value); }
    public string TonariOo { get => _tonarioo; set => SetField(ref _tonarioo, value); }
    public string TonariKo { get => _tonariko; set => SetField(ref _tonariko, value); }
    public string IkeAsetsu { get => _ikeasetsu; set => SetField(ref _ikeasetsu, value); }
    public string TsuKaUwa { get => _tsukauwa; set => SetField(ref _tsukauwa, value); }
    public string TsuKaShita { get => _tsukashita; set => SetField(ref _tsukashita, value); }
    public string HariHabaZure { get => _harihabazure; set => SetField(ref _harihabazure, value); }
    public string KakuShukinZure { get => _kakushukinzure; set => SetField(ref _kakushukinzure, value); }
    public string DansaKoteisa { get => _dansakoteisa; set => SetField(ref _dansakoteisa, value); }
    public string ChichuUwaDashi { get => _chichuuwadashi; set => SetField(ref _chichuuwadashi, value); }
    public string ChichuUwaNakaTan { get => _chichuuwanakatan; set => SetField(ref _chichuuwanakatan, value); }
    public string ChichuUwaNakaCho { get => _chichuuwanakacho; set => SetField(ref _chichuuwanakacho, value); }
    public string ChichuUwaShimai { get => _chichuuwashimai; set => SetField(ref _chichuuwashimai, value); }
    public string ChichuShitaNakaTan { get => _chichushitanakatan; set => SetField(ref _chichushitanakatan, value); }
    public string ChichuShitaNakaCho { get => _chichushitanakacho; set => SetField(ref _chichushitanakacho, value); }
    public string ChichuShitaDashi { get => _chichushitadashi; set => SetField(ref _chichushitadashi, value); }
    public string ChichuShitaShimai { get => _chichushitashimai; set => SetField(ref _chichushitashimai, value); }
    public string IppanUwaDashi { get => _ippanuwadashi; set => SetField(ref _ippanuwadashi, value); }
    public string IppanUwaNakaTan { get => _ippanuwanakatan; set => SetField(ref _ippanuwanakatan, value); }
    public string IppanUwaNakaCho { get => _ippanuwanakacho; set => SetField(ref _ippanuwanakacho, value); }
    public string IppanUwaShimai { get => _ippanuwashimai; set => SetField(ref _ippanuwashimai, value); }
    public string IppanShitaDashi { get => _ippanshitadashi; set => SetField(ref _ippanshitadashi, value); }
    public string IppanShitaNakaTan { get => _ippanshitanakatan; set => SetField(ref _ippanshitanakatan, value); }
    public string IppanShitaNakaCho { get => _ippanshitanakacho; set => SetField(ref _ippanshitanakacho, value); }
    public string IppanShitaShimai { get => _ippanshitashimai; set => SetField(ref _ippanshitashimai, value); }
    public string DashiKiriage { get => _dashikiriage; set => SetField(ref _dashikiriage, value); }
    public string DashiLMiman { get => _dashiLmiman; set => SetField(ref _dashiLmiman, value); }
    public string DashiLIjo { get => _dashiLijo; set => SetField(ref _dashiLijo, value); }
    public string NakaKiriage { get => _nakakiriage; set => SetField(ref _nakakiriage, value); }
    public string ShimaiKiriage { get => _shimaikiriage; set => SetField(ref _shimaikiriage, value); }
    public string ShimaiLMiman { get => _shimaiLmiman; set => SetField(ref _shimaiLmiman, value); }
    public string ShimaiLIjo { get => _shimaiLijo; set => SetField(ref _shimaiLijo, value); }
    public string D6D16Ryoanka { get => _d6d16ryoanka; set => SetField(ref _d6d16ryoanka, value); }
    public string D19D51Ryoanka { get => _d19d51ryoanka; set => SetField(ref _d19d51ryoanka, value); }
    public string RyoankaKiriage { get => _ryoankakiriage; set => SetField(ref _ryoankakiriage, value); }
    public string RyoankaLMiman { get => _ryoankaLmiman; set => SetField(ref _ryoankaLmiman, value); }
    public string RyoankaLIjo { get => _ryoankaLijo; set => SetField(ref _ryoankaLijo, value); }
    public string D6D16Ryotei { get => _d6d16ryotei; set => SetField(ref _d6d16ryotei, value); }
    public string D19D51Ryotei { get => _d19d51ryotei; set => SetField(ref _d19d51ryotei, value); }
    public string TopKiriage { get => _topkiriage; set => SetField(ref _topkiriage, value); }
    public string TopLMiman { get => _topLmiman; set => SetField(ref _topLmiman, value); }
    public string TopLIjo { get => _topLijo; set => SetField(ref _topLijo, value); }
    public string HanchiHataraki { get => _hanchihataraki; set => SetField(ref _hanchihataraki, value); }
    public string HanchiKiriage { get => _hanchikiriage; set => SetField(ref _hanchikiriage, value); }
    public string HanchiLMiman { get => _hanchiLmiman; set => SetField(ref _hanchiLmiman, value); }
    public string HanchiLIjo { get => _hanchiLijo; set => SetField(ref _hanchiLijo, value); }
    public string ChichuOoJogeKaburi { get => _chichuoojogekaburi; set => SetField(ref _chichuoojogekaburi, value); }
    public string ChichuOoSayuKaburi { get => _chichuoosayukaburi; set => SetField(ref _chichuoosayukaburi, value); }
    public string ChichuKoJogeKaburi { get => _chichukojogekaburi; set => SetField(ref _chichukojogekaburi, value); }
    public string ChichuKoSayuKaburi { get => _chichukosayukaburi; set => SetField(ref _chichukosayukaburi, value); }
    public string ChichuKataOoJogeKaburi { get => _chichukataoojogekaburi; set => SetField(ref _chichukataoojogekaburi, value); }
    public string ChichuKataOoSayuKaburi { get => _chichukataoosayukaburi; set => SetField(ref _chichukataoosayukaburi, value); }
    public string ChichuKataKoJogeKaburi { get => _chichukatakojogekaburi; set => SetField(ref _chichukatakojogekaburi, value); }
    public string ChichuKataKoSayuKaburi { get => _chichukatakosayukaburi; set => SetField(ref _chichukatakosayukaburi, value); }
    public string IppanOoJogeKaburi { get => _ippanoojogekaburi; set => SetField(ref _ippanoojogekaburi, value); }
    public string IppanOoSayuKaburi { get => _ippanoosayukaburi; set => SetField(ref _ippanoosayukaburi, value); }
    public string IppanKoJogeKaburi { get => _ippankojogekaburi; set => SetField(ref _ippankojogekaburi, value); }
    public string IppanKoSayuKaburi { get => _ippankosayukaburi; set => SetField(ref _ippankosayukaburi, value); }
    public string IppanKataOoJogeKaburi { get => _ippankataoojogekaburi; set => SetField(ref _ippankataoojogekaburi, value); }
    public string IppanKataOoSayuKaburi { get => _ippankataoosayukaburi; set => SetField(ref _ippankataoosayukaburi, value); }
    public string IppanKataKoJogeKaburi { get => _ippankatakojogekaburi; set => SetField(ref _ippankatakojogekaburi, value); }
    public string IppanKataKoSayuKaburi { get => _ippankatakosayukaburi; set => SetField(ref _ippankatakosayukaburi, value); }
    public string KabeChichuJogeKaburi { get => _kabechichujogekaburi; set => SetField(ref _kabechichujogekaburi, value); }
    public string KabeChichuSayuKaburi { get => _kabechichusayukaburi; set => SetField(ref _kabechichusayukaburi, value); }
    public string KabeIppanJogeKaburi { get => _kabeippanjogekaburi; set => SetField(ref _kabeippanjogekaburi, value); }
    public string KabeIppanSayuKaburi { get => _kabeippansayukaburi; set => SetField(ref _kabeippansayukaburi, value); }
    public string STPHabaHerisun { get => _STPhabaherisun; set => SetField(ref _STPhabaherisun, value); }
    public string STPSeiHerisun { get => _STPseiherisun; set => SetField(ref _STPseiherisun, value); }
    public string STPTsu { get => _STPtsu; set => SetField(ref _STPtsu, value); }
    public string NakagoTsu { get => _nakagotsu; set => SetField(ref _nakagotsu, value); }
    public string STPHani { get => _STPhani; set => SetField(ref _STPhani, value); }
    public string NakagoHataraki { get => _nakagohataraki; set => SetField(ref _nakagohataraki, value); }
    public string HaraTeiNaga { get => _harateinaga; set => SetField(ref _harateinaga, value); }
    public string HaraNagaSei { get => _haranagasei; set => SetField(ref _haranagasei, value); }
    public string HaraKiriage { get => _harakiriage; set => SetField(ref _harakiriage, value); }
    public string HaraLMiman { get => _haraLmiman; set => SetField(ref _haraLmiman, value); }
    public string HaraLIjo { get => _haraLijo; set => SetField(ref _haraLijo, value); }
    public string HabadomeHani { get => _habadomehani; set => SetField(ref _habadomehani, value); }
    public string FukashiSTPTeiNaga { get => _fukashiSTPteinaga; set => SetField(ref _fukashiSTPteinaga, value); }
    public string FukashiShukinTeiNaga { get => _fukashishukinteinaga; set => SetField(ref _fukashishukinteinaga, value); }
    public string GaitanTanbuTopYochou { get => _外端端部; set => SetField(ref _外端端部, value); }
    public string NaitanTanbuTopYochou { get => _內端端部; set => SetField(ref _內端端部, value); }
    public string GaitanChubuTopYochou { get => _外端中央部; set => SetField(ref _外端中央部, value); }
    public string NaitanChubuTopYochou { get => _內端中央部; set => SetField(ref _內端中央部, value); }
    public string GaitanChubuTopYochou3 { get => _外端中央3; set => SetField(ref _外端中央3, value); }
    public string NaitanChubuTopYochou3 { get => _內端中央3; set => SetField(ref _內端中央3, value); }
    public string GaitanTanbuTopYochou3 { get => _外端端部3; set => SetField(ref _外端端部3, value); }
    public string NaitanTanbuTopYochou3 { get => _內端端部3; set => SetField(ref _內端端部3, value); }
    // CheckBox properties
    public bool Teiuwa { get => _teiuwa; set => SetField(ref _teiuwa, value); }
    public bool Teiuwachu { get => _teiuwachu; set => SetField(ref _teiuwachu, value); }
    public bool Teishita { get => _teishita; set => SetField(ref _teishita, value); }
    public bool Teishitachu { get => _teishitachu; set => SetField(ref _teishitachu, value); }
    public bool Teiharigaibu { get => _teiharigaibu; set => SetField(ref _teiharigaibu, value); }

    // ComboBox properties
    public string Tsuuwa { get => _tsuuwa; set => SetField(ref _tsuuwa, value); }
    public string Tsushita { get => _tsushita; set => SetField(ref _tsushita, value); }

    public event PropertyChangedEventHandler PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propName = "")
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }
}

public class リスト入力 : INotifyPropertyChanged
{
    public ObservableCollection<基礎リスト> 基礎リスト { get; set; } = new ObservableCollection<基礎リスト>();
    public ObservableCollection<梁リスト> 梁リスト { get; set; } = new ObservableCollection<梁リスト>();
    public ObservableCollection<柱リスト> 柱リスト { get; set; } = new ObservableCollection<柱リスト>();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class 基礎リスト : INotifyPropertyChanged
{
    private string _name;
    private ObservableCollection<基礎> _基礎;
    public string 各階
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(各階));
            }
        }
    }

    public ObservableCollection<基礎> 基礎
    {
        get => _基礎;
        set
        {
            if (_基礎 != value)
            {
                _基礎 = value;
                OnPropertyChanged(nameof(基礎));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class 基礎 : INotifyPropertyChanged
{
    private string _name;
    private Z基礎の配置 _基礎の配置;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public Z基礎の配置 基礎の配置
    {
        get
        {
            if (_基礎の配置 == null)
            {
                _基礎の配置 = new Z基礎の配置();
            }
            return _基礎の配置;
        }
        set
        {
            if (_基礎の配置 != value)
            {
                _基礎の配置 = value;
                OnPropertyChanged(nameof(基礎の配置));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class Z基礎の配置 : INotifyPropertyChanged
{
    private string _水平方向はかま筋径TEXTBOX;
    private string _水平方向はかま筋形COMBOBOX;
    private string _水平方向はかま筋材質COMBOBOX;
    private string _ピッチTEXTBOX;
    private string _縦向きはかま筋径TEXTBOX;
    private string _縦向きはかま筋本数TEXTBOX;
    private string _縦向きはかま筋形COMBOBOX;
    private string _縦向きはかま筋材質COMBOBOX;
    private string _縦向き下端筋径TEXTBOX;
    private string _縦向き下端筋本数TEXTBOX;
    private string _縦向き下端筋形COMBOBOX;
    private string _縦向き下端筋材質COMBOBOX;
    private string _横向きはかま筋径TEXTBOX;
    private string _横向きはかま筋本数TEXTBOX;
    private string _横向きはかま筋形COMBOBOX;
    private string _横向きはかま筋材質COMBOBOX;
    private string _横向き下端筋径TEXTBOX;
    private string _横向き下端筋本数TEXTBOX;
    private string _横向き下端筋形COMBOBOX;
    private string _横向き下端筋材質COMBOBOX;

    public string 水平方向はかま筋径
    {
        get => _水平方向はかま筋径TEXTBOX;
        set
        {
            if (_水平方向はかま筋径TEXTBOX != value)
            {
                _水平方向はかま筋径TEXTBOX = value;
                OnPropertyChanged(nameof(_水平方向はかま筋径TEXTBOX));
            }
        }
    }
    public string 水平方向はかま筋形
    {
        get => _水平方向はかま筋形COMBOBOX;
        set
        {
            if (_水平方向はかま筋形COMBOBOX != value)
            {
                _水平方向はかま筋形COMBOBOX = value;
                OnPropertyChanged(nameof(_水平方向はかま筋形COMBOBOX));
            }
        }
    }
    public string 水平方向はかま筋材質
    {
        get => _水平方向はかま筋材質COMBOBOX;
        set
        {
            if (_水平方向はかま筋材質COMBOBOX != value)
            {
                _水平方向はかま筋材質COMBOBOX = value;
                OnPropertyChanged(nameof(水平方向はかま筋材質));
            }
        }
    }

    public string ピッチ
    {
        get => _ピッチTEXTBOX;
        set
        {
            if (_ピッチTEXTBOX != value)
            {
                _ピッチTEXTBOX = value;
                OnPropertyChanged(nameof(ピッチ));
            }
        }
    }

    public string 縦向きはかま筋径
    {
        get => _縦向きはかま筋径TEXTBOX;
        set
        {
            if (_縦向きはかま筋径TEXTBOX != value)
            {
                _縦向きはかま筋径TEXTBOX = value;
                OnPropertyChanged(nameof(縦向きはかま筋径));
            }
        }
    }

    public string 縦向きはかま筋本数
    {
        get => _縦向きはかま筋本数TEXTBOX;
        set
        {
            if (_縦向きはかま筋本数TEXTBOX != value)
            {
                _縦向きはかま筋本数TEXTBOX = value;
                OnPropertyChanged(nameof(縦向きはかま筋本数));
            }
        }
    }

    public string 縦向きはかま筋形
    {
        get => _縦向きはかま筋形COMBOBOX;
        set
        {
            if (_縦向きはかま筋形COMBOBOX != value)
            {
                _縦向きはかま筋形COMBOBOX = value;
                OnPropertyChanged(nameof(縦向きはかま筋形));
            }
        }
    }

    public string 縦向きはかま筋材質
    {
        get => _縦向きはかま筋材質COMBOBOX;
        set
        {
            if (_縦向きはかま筋材質COMBOBOX != value)
            {
                _縦向きはかま筋材質COMBOBOX = value;
                OnPropertyChanged(nameof(縦向きはかま筋材質));
            }
        }
    }

    public string 縦向き下端筋径
    {
        get => _縦向き下端筋径TEXTBOX;
        set
        {
            if (_縦向き下端筋径TEXTBOX != value)
            {
                _縦向き下端筋径TEXTBOX = value;
                OnPropertyChanged(nameof(縦向き下端筋径));
            }
        }
    }

    public string 縦向き下端筋本数
    {
        get => _縦向き下端筋本数TEXTBOX;
        set
        {
            if (_縦向き下端筋本数TEXTBOX != value)
            {
                _縦向き下端筋本数TEXTBOX = value;
                OnPropertyChanged(nameof(縦向き下端筋本数));
            }
        }
    }

    public string 縦向き下端筋形
    {
        get => _縦向き下端筋形COMBOBOX;
        set
        {
            if (_縦向き下端筋形COMBOBOX != value)
            {
                _縦向き下端筋形COMBOBOX = value;
                OnPropertyChanged(nameof(縦向き下端筋形));
            }
        }
    }

    public string 縦向き下端筋材質
    {
        get => _縦向き下端筋材質COMBOBOX;
        set
        {
            if (_縦向き下端筋材質COMBOBOX != value)
            {
                _縦向き下端筋材質COMBOBOX = value;
                OnPropertyChanged(nameof(縦向き下端筋材質));
            }
        }
    }

    public string 横向きはかま筋径
    {
        get => _横向きはかま筋径TEXTBOX;
        set
        {
            if (_横向きはかま筋径TEXTBOX != value)
            {
                _横向きはかま筋径TEXTBOX = value;
                OnPropertyChanged(nameof(横向きはかま筋径));
            }
        }
    }

    public string 横向きはかま筋本数
    {
        get => _横向きはかま筋本数TEXTBOX;
        set
        {
            if (_横向きはかま筋本数TEXTBOX != value)
            {
                _横向きはかま筋本数TEXTBOX = value;
                OnPropertyChanged(nameof(横向きはかま筋本数));
            }
        }
    }

    public string 横向きはかま筋形
    {
        get => _横向きはかま筋形COMBOBOX;
        set
        {
            if (_横向きはかま筋形COMBOBOX != value)
            {
                _横向きはかま筋形COMBOBOX = value;
                OnPropertyChanged(nameof(横向きはかま筋形));
            }
        }
    }

    public string 横向きはかま筋材質
    {
        get => _横向きはかま筋材質COMBOBOX;
        set
        {
            if (_横向きはかま筋材質COMBOBOX != value)
            {
                _横向きはかま筋材質COMBOBOX = value;
                OnPropertyChanged(nameof(横向きはかま筋材質));
            }
        }
    }

    public string 横向き下端筋径
    {
        get => _横向き下端筋径TEXTBOX;
        set
        {
            if (_横向き下端筋径TEXTBOX != value)
            {
                _横向き下端筋径TEXTBOX = value;
                OnPropertyChanged(nameof(横向き下端筋径));
            }
        }
    }

    public string 横向き下端筋本数
    {
        get => _横向き下端筋本数TEXTBOX;
        set
        {
            if (_横向き下端筋本数TEXTBOX != value)
            {
                _横向き下端筋本数TEXTBOX = value;
                OnPropertyChanged(nameof(横向き下端筋本数));
            }
        }
    }

    public string 横向き下端筋形
    {
        get => _横向き下端筋形COMBOBOX;
        set
        {
            if (_横向き下端筋形COMBOBOX != value)
            {
                _横向き下端筋形COMBOBOX = value;
                OnPropertyChanged(nameof(横向き下端筋形));
            }
        }
    }

    public string 横向き下端筋材質
    {
        get => _横向き下端筋材質COMBOBOX;
        set
        {
            if (_横向き下端筋材質COMBOBOX != value)
            {
                _横向き下端筋材質COMBOBOX = value;
                OnPropertyChanged(nameof(横向き下端筋材質));
            }
        }
    }


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class 梁リスト : INotifyPropertyChanged
{
    private string _name;
    private ObservableCollection<梁> _梁;
    public event PropertyChangedEventHandler PropertyChanged;
    public string 各階
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(各階));
            }
        }
    }
    public ObservableCollection<梁> 梁
    {
        get => _梁;
        set
        {
            if (_梁 != value)
            {
                _梁 = value;
                OnPropertyChanged(nameof(梁));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class 梁 : INotifyPropertyChanged
{
    private string _name;
    private Z梁の配置 _梁の配置;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public Z梁の配置 梁の配置
    {
        get
        {
            if (_梁の配置 == null)
            {
                _梁の配置 = new Z梁の配置();
            }
            return _梁の配置;
        }
        set
        {
            if (_梁の配置 != value)
            {
                _梁の配置 = value;
                OnPropertyChanged(nameof(梁の配置));
            }
        }
    }
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
public class Z梁の配置 : INotifyPropertyChanged
{

    // Phần đầu 1 - Backing fields
    private string _端部1幅; // Chiều rộng dầm
    private string _端部1成; // Chiều cao dầm
    private string _端部1主筋径; // Đường kính thanh thép chính
    private string _端部1主筋材質; // Chất liệu thanh thép chính
    private string _端部1上筋本数; // Số lượng thanh thép trên
    private string _端部1上宙1; // Thanh thép trên giữa 1
    private string _端部1上宙2; // Thanh thép trên giữa 2
    private string _端部1下宙2; // Thanh thép dưới giữa 2
    private string _端部1下宙1; // Thanh thép dưới giữa 1
    private string _端部1下筋本数; // Số lượng thanh thép dưới
    private string _端部1スタラップ径; // Đường kính thanh thép đai
    private string _端部1ピッチ; // Khoảng cách
    private string _端部1スタラップ材質; // Chất liệu thanh thép đai
    private string _端部1のスタラップ形; // Hình dạng thanh thép đai
    private string _端部1CAP径; // Đường kính CAP
    private string _端部1中子筋径; // Đường kính thanh thép giữa
    private string _端部1中子筋径ピッチ; // Khoảng cách thanh thép giữa
    private string _端部1中子筋材質; // Chất liệu thanh thép giữa
    private string _端部1中子筋形; // Hình dạng thanh thép giữa
    private string _端部1中子筋本数; // Số lượng thanh thép giữa
    private string _端部1腹筋径; // Đường kính thanh thép bụng
    private string _端部1腹筋本数; // Số lượng thanh thép bụng
    private string _端部1幅止筋径; // Đường kính thanh thép giữ chiều rộng
    private string _端部1幅止筋ピッチ; // Khoảng cách thanh thép giữ chiều rộng
    //
    // Phần giữa - Backing fields
    private string _中央幅; // Chiều rộng dầm
    private string _中央成; // Chiều cao dầm
    private string _中央主筋径; // Đường kính thanh thép chính
    private string _中央材質; // Chất liệu
    private string _中央上筋本数; // Số lượng thanh thép trên
    private string _中央上宙1; // Thanh thép trên giữa 1
    private string _中央上宙2; // Thanh thép trên giữa 2
    private string _中央下宙2; // Thanh thép dưới giữa 2
    private string _中央下宙1; // Thanh thép dưới giữa 1
    private string _中央下筋本数; // Số lượng thanh thép dưới
    private string _中央スタラップ径; // Đường kính thanh thép đai
    private string _中央ピッチ; // Khoảng cách
    private string _中央スタラップ材質; // Chất liệu thanh thép đai
    private string _中央スタラップ形; // Hình dạng thanh thép đai
    private string _中央CAP径; // Đường kính CAP
    private string _中央中子筋径; // Đường kính thanh thép giữa
    private string _中央中子筋径ピッチ; // Khoảng cách thanh thép giữa
    private string _中央中子筋材質; // Chất liệu thanh thép giữa
    private string _中央中子筋形; // Hình dạng thanh thép giữa
    private string _中央中子筋本数; // Số lượng thanh thép giữa

    // Phần đầu 2 - Backing fields
    private string _端部2幅; // Chiều rộng dầm
    private string _端部2成; // Chiều cao dầm
    private string _端部2主筋径; // Đường kính thanh thép chính
    private string _端部2主筋材質; // Chất liệu thanh thép chính
    private string _端部2上筋本数; // Số lượng thanh thép trên
    private string _端部2上宙1; // Thanh thép trên giữa 1
    private string _端部2上宙2; // Thanh thép trên giữa 2
    private string _端部2下宙2; // Thanh thép dưới giữa 2
    private string _端部2下宙1; // Thanh thép dưới giữa 1
    private string _端部2下筋本数; // Số lượng thanh thép dưới
    private string _端部2スタラップ径; // Đường kính thanh thép đai
    private string _端部2ピッチ; // Khoảng cách
    private string _端部2スタラップ材質; // Chất liệu thanh thép đai
    private string _端部2スタラップ形; // Hình dạng thanh thép đai
    private string _端部2CAP径; // Đường kính CAP
    private string _端部2中子筋径; // Đường kính thanh thép giữa
    private string _端部2中子筋径ピッチ; // Khoảng cách thanh thép giữa
    private string _端部2中子筋材質; // Chất liệu thanh thép giữa
    private string _端部2中子筋形; // Hình dạng thanh thép giữa
    private string _端部2中子筋本数; // Số lượng thanh thép giữa

    // Tổng quan - Backing field
    private string _梁COMBOBOX;


    private Dictionary<string, GridBotData> _gridbotdata;


    private bool _全断面;
    private bool _地中ON_般OFF;
    private bool _大梁ON_小梁OFF;


    public Z梁の配置()
    {

        // Ghi dòng phân cách vào log
        File.AppendAllText("debugdata.txt", Environment.NewLine + "------------------------------------Z梁の配置 mac dinh----------------------------------" + Environment.NewLine);

        // Sample data for top grid TextBoxes and ComboBoxes
        端部1幅 = "600";
        端部1成 = "900";
        端部1主筋径 = "22";
        端部1主筋材質 = "SD390";
        端部1上筋本数 = "4";
        端部1上宙1 = "2";
        端部1上宙2 = "0";
        端部1下宙2 = "0";
        端部1下宙1 = "2";
        端部1下筋本数 = "4";
        端部1スタラップ径 = "10";
        端部1ピッチ = "100";
        端部1スタラップ材質 = "SD390";
        端部1のスタラップ形 = "1";
        端部1CAP径 = "13";
        端部1中子筋径 = "10";
        端部1中子筋径ピッチ = "150";
        端部1中子筋材質 = "SD390";
        端部1中子筋形 = "1";
        端部1中子筋本数 = "2";

        中央幅 = "600";
        中央成 = "900";
        中央主筋径 = "25";
        中央材質 = "SD390";
        中央上筋本数 = "5";
        中央上宙1 = "3";
        中央上宙2 = "0";
        中央下宙2 = "0";
        中央下宙1 = "3";
        中央下筋本数 = "5";
        中央スタラップ径 = "13";
        中央ピッチ = "120";
        中央スタラップ材質 = "SD390";
        中央スタラップ形 = "1";
        中央CAP径 = "16";
        中央中子筋径 = "13";
        中央中子筋径ピッチ = "180";
        中央中子筋材質 = "SD390";
        中央中子筋形 = "1";
        中央中子筋本数 = "3";
        端部1腹筋径 = "13";
        端部1腹筋本数 = "2";
        端部1幅止筋径 = "10";
        端部1幅止筋ピッチ = "200";

        端部2幅 = "600";
        端部2成 = "900";
        端部2主筋径 = "19";
        端部2主筋材質 = "SD390";
        端部2上筋本数 = "4";
        端部2上宙1 = "2";
        端部2上宙2 = "0";
        端部2下宙2 = "0";
        端部2下宙1 = "2";
        端部2下筋本数 = "4";
        端部2スタラップ径 = "10";
        端部2ピッチ = "110";
        端部2スタラップ材質 = "SD390";
        端部2スタラップ形 = "1";
        端部2CAP径 = "13";
        端部2中子筋径 = "10";
        端部2中子筋径ピッチ = "160";
        端部2中子筋材質 = "SD390";
        端部2中子筋形 = "1";
        端部2中子筋本数 = "2";

        全断面 = false;
        地中ON_般OFF = false;
        大梁ON_小梁OFF = false;

        梁COMBOBOX = "端部1";

        // ---------- Tạo GridBotData và ghi log phân biệt ----------
        gridbotdata = new Dictionary<string, GridBotData>
            {
                { "端部1", new GridBotData("端部1") },
                { "中央", new GridBotData("中央") },
                { "端部2", new GridBotData("端部2") }
            };

        foreach (var key in gridbotdata.Keys)
        {
            File.AppendAllText("debugdata.txt", $"{Environment.NewLine}-------------------- Init GridBotData {key} --------------------{Environment.NewLine}");
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Init] Added GridBotData for key: {key}{Environment.NewLine}");
        }

        // Ghi dòng phân cách vào log
        File.AppendAllText("debugdata.txt", Environment.NewLine + "------------------------------------Z梁の配置 mac dinh----------------------------------" + Environment.NewLine);
    }


    // --- Các Properties viết lại theo kiểu SetProperty ---
    public string 端部1幅 { get => _端部1幅; set => SetProperty(ref _端部1幅, value); }
    public string 端部1成 { get => _端部1成; set => SetProperty(ref _端部1成, value); }
    public string 端部1主筋径 { get => _端部1主筋径; set => SetProperty(ref _端部1主筋径, value); }
    public string 端部1主筋材質 { get => _端部1主筋材質; set => SetProperty(ref _端部1主筋材質, value); }
    public string 端部1上筋本数 { get => _端部1上筋本数; set => SetProperty(ref _端部1上筋本数, value); }
    public string 端部1上宙1 { get => _端部1上宙1; set => SetProperty(ref _端部1上宙1, value); }
    public string 端部1上宙2 { get => _端部1上宙2; set => SetProperty(ref _端部1上宙2, value); }
    public string 端部1下宙2 { get => _端部1下宙2; set => SetProperty(ref _端部1下宙2, value); }
    public string 端部1下宙1 { get => _端部1下宙1; set => SetProperty(ref _端部1下宙1, value); }
    public string 端部1下筋本数 { get => _端部1下筋本数; set => SetProperty(ref _端部1下筋本数, value); }
    public string 端部1スタラップ径 { get => _端部1スタラップ径; set => SetProperty(ref _端部1スタラップ径, value); }
    public string 端部1ピッチ { get => _端部1ピッチ; set => SetProperty(ref _端部1ピッチ, value); }
    public string 端部1スタラップ材質 { get => _端部1スタラップ材質; set => SetProperty(ref _端部1スタラップ材質, value); }
    public string 端部1のスタラップ形 { get => _端部1のスタラップ形; set => SetProperty(ref _端部1のスタラップ形, value); }
    public string 端部1CAP径 { get => _端部1CAP径; set => SetProperty(ref _端部1CAP径, value); }
    public string 端部1中子筋径 { get => _端部1中子筋径; set => SetProperty(ref _端部1中子筋径, value); }
    public string 端部1中子筋径ピッチ { get => _端部1中子筋径ピッチ; set => SetProperty(ref _端部1中子筋径ピッチ, value); }
    public string 端部1中子筋材質 { get => _端部1中子筋材質; set => SetProperty(ref _端部1中子筋材質, value); }
    public string 端部1中子筋形 { get => _端部1中子筋形; set => SetProperty(ref _端部1中子筋形, value); }
    public string 端部1中子筋本数 { get => _端部1中子筋本数; set => SetProperty(ref _端部1中子筋本数, value); }
    public string 端部1腹筋径 { get => _端部1腹筋径; set => SetProperty(ref _端部1腹筋径, value); }
    public string 端部1腹筋本数 { get => _端部1腹筋本数; set => SetProperty(ref _端部1腹筋本数, value); }
    public string 端部1幅止筋径 { get => _端部1幅止筋径; set => SetProperty(ref _端部1幅止筋径, value); }
    public string 端部1幅止筋ピッチ { get => _端部1幅止筋ピッチ; set => SetProperty(ref _端部1幅止筋ピッチ, value); }
    public string 中央幅 { get => _中央幅; set => SetProperty(ref _中央幅, value); }
    public string 中央成 { get => _中央成; set => SetProperty(ref _中央成, value); }
    public string 中央主筋径 { get => _中央主筋径; set => SetProperty(ref _中央主筋径, value); }
    public string 中央材質 { get => _中央材質; set => SetProperty(ref _中央材質, value); }
    public string 中央上筋本数 { get => _中央上筋本数; set => SetProperty(ref _中央上筋本数, value); }
    public string 中央上宙1 { get => _中央上宙1; set => SetProperty(ref _中央上宙1, value); }
    public string 中央上宙2 { get => _中央上宙2; set => SetProperty(ref _中央上宙2, value); }
    public string 中央下宙2 { get => _中央下宙2; set => SetProperty(ref _中央下宙2, value); }
    public string 中央下宙1 { get => _中央下宙1; set => SetProperty(ref _中央下宙1, value); }
    public string 中央下筋本数 { get => _中央下筋本数; set => SetProperty(ref _中央下筋本数, value); }
    public string 中央スタラップ径 { get => _中央スタラップ径; set => SetProperty(ref _中央スタラップ径, value); }
    public string 中央ピッチ { get => _中央ピッチ; set => SetProperty(ref _中央ピッチ, value); }
    public string 中央スタラップ材質 { get => _中央スタラップ材質; set => SetProperty(ref _中央スタラップ材質, value); }
    public string 中央スタラップ形 { get => _中央スタラップ形; set => SetProperty(ref _中央スタラップ形, value); }
    public string 中央CAP径 { get => _中央CAP径; set => SetProperty(ref _中央CAP径, value); }
    public string 中央中子筋径 { get => _中央中子筋径; set => SetProperty(ref _中央中子筋径, value); }
    public string 中央中子筋径ピッチ { get => _中央中子筋径ピッチ; set => SetProperty(ref _中央中子筋径ピッチ, value); }
    public string 中央中子筋材質 { get => _中央中子筋材質; set => SetProperty(ref _中央中子筋材質, value); }
    public string 中央中子筋形 { get => _中央中子筋形; set => SetProperty(ref _中央中子筋形, value); }
    public string 中央中子筋本数 { get => _中央中子筋本数; set => SetProperty(ref _中央中子筋本数, value); }
    public string 端部2幅 { get => _端部2幅; set => SetProperty(ref _端部2幅, value); }
    public string 端部2成 { get => _端部2成; set => SetProperty(ref _端部2成, value); }
    public string 端部2主筋径 { get => _端部2主筋径; set => SetProperty(ref _端部2主筋径, value); }
    public string 端部2主筋材質 { get => _端部2主筋材質; set => SetProperty(ref _端部2主筋材質, value); }
    public string 端部2上筋本数 { get => _端部2上筋本数; set => SetProperty(ref _端部2上筋本数, value); }
    public string 端部2上宙1 { get => _端部2上宙1; set => SetProperty(ref _端部2上宙1, value); }
    public string 端部2上宙2 { get => _端部2上宙2; set => SetProperty(ref _端部2上宙2, value); }
    public string 端部2下宙2 { get => _端部2下宙2; set => SetProperty(ref _端部2下宙2, value); }
    public string 端部2下宙1 { get => _端部2下宙1; set => SetProperty(ref _端部2下宙1, value); }
    public string 端部2下筋本数 { get => _端部2下筋本数; set => SetProperty(ref _端部2下筋本数, value); }
    public string 端部2スタラップ径 { get => _端部2スタラップ径; set => SetProperty(ref _端部2スタラップ径, value); }
    public string 端部2ピッチ { get => _端部2ピッチ; set => SetProperty(ref _端部2ピッチ, value); }
    public string 端部2スタラップ材質 { get => _端部2スタラップ材質; set => SetProperty(ref _端部2スタラップ材質, value); }
    public string 端部2スタラップ形 { get => _端部2スタラップ形; set => SetProperty(ref _端部2スタラップ形, value); }
    public string 端部2CAP径 { get => _端部2CAP径; set => SetProperty(ref _端部2CAP径, value); }
    public string 端部2中子筋径 { get => _端部2中子筋径; set => SetProperty(ref _端部2中子筋径, value); }
    public string 端部2中子筋径ピッチ { get => _端部2中子筋径ピッチ; set => SetProperty(ref _端部2中子筋径ピッチ, value); }
    public string 端部2中子筋材質 { get => _端部2中子筋材質; set => SetProperty(ref _端部2中子筋材質, value); }
    public string 端部2中子筋形 { get => _端部2中子筋形; set => SetProperty(ref _端部2中子筋形, value); }
    public string 端部2中子筋本数 { get => _端部2中子筋本数; set => SetProperty(ref _端部2中子筋本数, value); }

    public string 梁COMBOBOX { get => _梁COMBOBOX; set => SetProperty(ref _梁COMBOBOX, value); }

    public Dictionary<string, GridBotData> gridbotdata { get => _gridbotdata; set => SetProperty(ref _gridbotdata, value); }

    public bool 全断面 { get => _全断面; set => SetProperty(ref _全断面, value); }
    public bool 地中ON_般OFF { get => _地中ON_般OFF; set => SetProperty(ref _地中ON_般OFF, value); }
    public bool 大梁ON_小梁OFF { get => _大梁ON_小梁OFF; set => SetProperty(ref _大梁ON_小梁OFF, value); }

    // Sự kiện PropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;

    // Phương thức thông báo thay đổi thuộc tính
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }
    private void LogPropertyChange<T>(string propertyName, T value)
    {
        string formattedValue;

        if (value is Dictionary<string, GridBotData>)
        {
            formattedValue = "new changed GridBotData dictionary";
        }
        else if (value is IEnumerable<KeyValuePair<int, string>> dictStr)
            formattedValue = string.Join(", ", dictStr.Select(kv => $"{kv.Key}:{kv.Value}"));
        else if (value is IEnumerable<KeyValuePair<int, int>> dictInt)
            formattedValue = string.Join(", ", dictInt.Select(kv => $"{kv.Key}:{kv.Value}"));
        else if (value is IEnumerable<KeyValuePair<int, bool>> dictBool)
            formattedValue = string.Join(", ", dictBool.Select(kv => $"{kv.Key}:{kv.Value.ToString().ToLower()}"));
        else if (value is IEnumerable<string> listStr)
            formattedValue = string.Join(", ", listStr);
        else
            formattedValue = value?.ToString() ?? "null";

        File.AppendAllText("debugdata.txt",
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PropertyChanged] {propertyName} set to: {formattedValue}{Environment.NewLine}");
    }

}

public class GridBotData : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            LogPropertyChange(propertyName, value);
            OnPropertyChanged(propertyName);
        }
        return true;
    }
    private void LogPropertyChange<T>(string propertyName, T value)
    {
        string formattedValue;

        if (value is IEnumerable<KeyValuePair<int, string>> dictStr)
            formattedValue = string.Join(", ", dictStr.Select(kv => $"{kv.Key}:{kv.Value}"));
        else if (value is IEnumerable<KeyValuePair<int, int>> dictInt)
            formattedValue = string.Join(", ", dictInt.Select(kv => $"{kv.Key}:{kv.Value}"));
        else if (value is IEnumerable<KeyValuePair<int, bool>> dictBool)
            formattedValue = string.Join(", ", dictBool.Select(kv => $"{kv.Key}:{kv.Value.ToString().ToLower()}"));
        else if (value is IEnumerable<string> listStr)
            formattedValue = string.Join(", ", listStr);
        else
            formattedValue = value?.ToString() ?? "null";

        File.AppendAllText("debugdata.txt",
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [PropertyChanged] {propertyName} set to: {formattedValue}{Environment.NewLine}");
    }

    public GridBotData Clone()
    {
        var serialized = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<GridBotData>(serialized);
    }
    public string BeamType { get; set; }

    private string _上TEXTBOX;
    public string 上TEXTBOX
    {
        get => _上TEXTBOX;
        set => SetProperty(ref _上TEXTBOX, value);
    }

    private string _下TEXTBOX;
    public string 下TEXTBOX
    {
        get => _下TEXTBOX;
        set => SetProperty(ref _下TEXTBOX, value);
    }

    private string _左TEXTBOX;
    public string 左TEXTBOX
    {
        get => _左TEXTBOX;
        set => SetProperty(ref _左TEXTBOX, value);
    }

    private string _右TEXTBOX;
    public string 右TEXTBOX
    {
        get => _右TEXTBOX;
        set => SetProperty(ref _右TEXTBOX, value);
    }

    private string _上筋COMBOBOX;
    public string 上筋COMBOBOX
    {
        get => _上筋COMBOBOX;
        set => SetProperty(ref _上筋COMBOBOX, value);
    }

    private string _下筋COMBOBOX;
    public string 下筋COMBOBOX
    {
        get => _下筋COMBOBOX;
        set => SetProperty(ref _下筋COMBOBOX, value);
    }

    private string _上宙1COMBOBOX;
    public string 上宙1COMBOBOX
    {
        get => _上宙1COMBOBOX;
        set => SetProperty(ref _上宙1COMBOBOX, value);
    }

    private string _上宙2COMBOBOX;
    public string 上宙2COMBOBOX
    {
        get => _上宙2COMBOBOX;
        set => SetProperty(ref _上宙2COMBOBOX, value);
    }

    private string _下宙1COMBOBOX;
    public string 下宙1COMBOBOX
    {
        get => _下宙1COMBOBOX;
        set => SetProperty(ref _下宙1COMBOBOX, value);
    }

    private string _下宙2COMBOBOX;
    public string 下宙2COMBOBOX
    {
        get => _下宙2COMBOBOX;
        set => SetProperty(ref _下宙2COMBOBOX, value);
    }

    private string _上筋左右;
    public string 上筋左右
    {
        get => _上筋左右;
        set => SetProperty(ref _上筋左右, value);
    }

    private string _上筋上下;
    public string 上筋上下
    {
        get => _上筋上下;
        set => SetProperty(ref _上筋上下, value);
    }

    private string _上宙1左右;
    public string 上宙1左右
    {
        get => _上宙1左右;
        set => SetProperty(ref _上宙1左右, value);
    }

    private string _上宙1上下;
    public string 上宙1上下
    {
        get => _上宙1上下;
        set => SetProperty(ref _上宙1上下, value);
    }

    private string _上宙2左右;
    public string 上宙2左右
    {
        get => _上宙2左右;
        set => SetProperty(ref _上宙2左右, value);
    }

    private string _上宙2上下;
    public string 上宙2上下
    {
        get => _上宙2上下;
        set => SetProperty(ref _上宙2上下, value);
    }

    private string _下宙1左右;
    public string 下宙1左右
    {
        get => _下宙1左右;
        set => SetProperty(ref _下宙1左右, value);
    }

    private string _下宙1上下;
    public string 下宙1上下
    {
        get => _下宙1上下;
        set => SetProperty(ref _下宙1上下, value);
    }

    private string _下宙2左右;
    public string 下宙2左右
    {
        get => _下宙2左右;
        set => SetProperty(ref _下宙2左右, value);
    }

    private string _下宙2上下;
    public string 下宙2上下
    {
        get => _下宙2上下;
        set => SetProperty(ref _下宙2上下, value);
    }

    private string _下筋左右;
    public string 下筋左右
    {
        get => _下筋左右;
        set => SetProperty(ref _下筋左右, value);
    }

    private string _下筋上下;
    public string 下筋上下
    {
        get => _下筋上下;
        set => SetProperty(ref _下筋上下, value);
    }

    private string _フックの位置COMBOBOX;
    public string フックの位置COMBOBOX
    {
        get => _フックの位置COMBOBOX;
        set => SetProperty(ref _フックの位置COMBOBOX, value);
    }

    private string _中子筋の数COMBOBOX;
    public string 中子筋の数COMBOBOX
    {
        get => _中子筋の数COMBOBOX;
        set => SetProperty(ref _中子筋の数COMBOBOX, value);
    }

    private string _中子筋の位置COMBOBOX;
    public string 中子筋の位置COMBOBOX
    {
        get => _中子筋の位置COMBOBOX;
        set => SetProperty(ref _中子筋の位置COMBOBOX, value);
    }

    private bool _中子筋_方向;
    public bool 中子筋_方向
    {
        get => _中子筋_方向;
        set => SetProperty(ref _中子筋_方向, value);
    }


    private Dictionary<int, int> _nakagoCustomPositions;
    public Dictionary<int, int> NakagoCustomPositions
    {
        get => _nakagoCustomPositions;
        set => SetProperty(ref _nakagoCustomPositions, value);
    }

    private Dictionary<int, bool> _nakagoDirections;
    public Dictionary<int, bool> NakagoDirections
    {
        get => _nakagoDirections;
        set => SetProperty(ref _nakagoDirections, value);
    }

    private List<string> _valid上筋本数Values;
    public List<string> Valid上筋本数Values
    {
        get => _valid上筋本数Values;
        set => SetProperty(ref _valid上筋本数Values, value);
    }

    private List<string> _valid下筋本数Values;
    public List<string> Valid下筋本数Values
    {
        get => _valid下筋本数Values;
        set => SetProperty(ref _valid下筋本数Values, value);
    }

    private List<string> _valid上宙1Values;
    public List<string> Valid上宙1Values
    {
        get => _valid上宙1Values;
        set => SetProperty(ref _valid上宙1Values, value);
    }

    private List<string> _valid上宙2Values;
    public List<string> Valid上宙2Values
    {
        get => _valid上宙2Values;
        set => SetProperty(ref _valid上宙2Values, value);
    }

    private List<string> _valid下宙1Values;
    public List<string> Valid下宙1Values
    {
        get => _valid下宙1Values;
        set => SetProperty(ref _valid下宙1Values, value);
    }

    private List<string> _valid下宙2Values;
    public List<string> Valid下宙2Values
    {
        get => _valid下宙2Values;
        set => SetProperty(ref _valid下宙2Values, value);
    }

    private List<string> _valid数Values;
    public List<string> Valid数Values
    {
        get => _valid数Values;
        set => SetProperty(ref _valid数Values, value);
    }

    private List<string> _valid中子筋の位置Values;
    public List<string> Valid中子筋の位置Values
    {
        get => _valid中子筋の位置Values;
        set => SetProperty(ref _valid中子筋の位置Values, value);
    }

    private Dictionary<int, string> _上筋左右Offsets;
    public Dictionary<int, string> 上筋左右Offsets
    {
        get => _上筋左右Offsets;
        set => SetProperty(ref _上筋左右Offsets, value);
    }

    private Dictionary<int, string> _上筋上下Offsets;
    public Dictionary<int, string> 上筋上下Offsets
    {
        get => _上筋上下Offsets;
        set => SetProperty(ref _上筋上下Offsets, value);
    }

    private Dictionary<int, string> _上宙1左右Offsets;
    public Dictionary<int, string> 上宙1左右Offsets
    {
        get => _上宙1左右Offsets;
        set => SetProperty(ref _上宙1左右Offsets, value);
    }

    private Dictionary<int, string> _上宙1上下Offsets;
    public Dictionary<int, string> 上宙1上下Offsets
    {
        get => _上宙1上下Offsets;
        set => SetProperty(ref _上宙1上下Offsets, value);
    }

    private Dictionary<int, string> _上宙2左右Offsets;
    public Dictionary<int, string> 上宙2左右Offsets
    {
        get => _上宙2左右Offsets;
        set => SetProperty(ref _上宙2左右Offsets, value);
    }

    private Dictionary<int, string> _上宙2上下Offsets;
    public Dictionary<int, string> 上宙2上下Offsets
    {
        get => _上宙2上下Offsets;
        set => SetProperty(ref _上宙2上下Offsets, value);
    }

    private Dictionary<int, string> _下宙1左右Offsets;
    public Dictionary<int, string> 下宙1左右Offsets
    {
        get => _下宙1左右Offsets;
        set => SetProperty(ref _下宙1左右Offsets, value);
    }

    private Dictionary<int, string> _下宙1上下Offsets;
    public Dictionary<int, string> 下宙1上下Offsets
    {
        get => _下宙1上下Offsets;
        set => SetProperty(ref _下宙1上下Offsets, value);
    }

    private Dictionary<int, string> _下宙2左右Offsets;
    public Dictionary<int, string> 下宙2左右Offsets
    {
        get => _下宙2左右Offsets;
        set => SetProperty(ref _下宙2左右Offsets, value);
    }

    private Dictionary<int, string> _下宙2上下Offsets;
    public Dictionary<int, string> 下宙2上下Offsets
    {
        get => _下宙2上下Offsets;
        set => SetProperty(ref _下宙2上下Offsets, value);
    }

    private Dictionary<int, string> _下筋左右Offsets;
    public Dictionary<int, string> 下筋左右Offsets
    {
        get => _下筋左右Offsets;
        set => SetProperty(ref _下筋左右Offsets, value);
    }

    private Dictionary<int, string> _下筋上下Offsets;
    public Dictionary<int, string> 下筋上下Offsets
    {
        get => _下筋上下Offsets;
        set => SetProperty(ref _下筋上下Offsets, value);
    }


    // Constructor
    public GridBotData(string beamType)
    {
        BeamType = beamType;
        // Các giá trị mặc định đã gán ở trên hoặc tại đây nếu muốn đồng bộ thêm

        上TEXTBOX = "50";
        下TEXTBOX = "50";
        左TEXTBOX = "50";
        右TEXTBOX = "50";

        上筋COMBOBOX = "1";
        上宙1COMBOBOX = "1";
        上宙2COMBOBOX = "1";
        下筋COMBOBOX = "1";
        下宙1COMBOBOX = "1";
        下宙2COMBOBOX = "1";

        上筋左右 = "0";
        上筋上下 = "0";
        上宙1左右 = "0";
        上宙1上下 = "0";
        上宙2左右 = "0";
        上宙2上下 = "0";
        下宙1左右 = "0";
        下宙1上下 = "0";
        下宙2左右 = "0";
        下宙2上下 = "0";
        下筋左右 = "0";
        下筋上下 = "0";

        フックの位置COMBOBOX = "1";
        中子筋の数COMBOBOX = "1";
        中子筋の位置COMBOBOX = "2";
        中子筋_方向 = false;


        上筋左右Offsets = new Dictionary<int, string>();
        上筋上下Offsets = new Dictionary<int, string>();
        上宙1左右Offsets = new Dictionary<int, string>();
        上宙1上下Offsets = new Dictionary<int, string>();
        上宙2左右Offsets = new Dictionary<int, string>();
        上宙2上下Offsets = new Dictionary<int, string>();
        下宙1左右Offsets = new Dictionary<int, string>();
        下宙1上下Offsets = new Dictionary<int, string>();
        下宙2左右Offsets = new Dictionary<int, string>();
        下宙2上下Offsets = new Dictionary<int, string>();
        下筋左右Offsets = new Dictionary<int, string>();
        下筋上下Offsets = new Dictionary<int, string>();
        // Khởi tạo các dictionary với giá trị mặc định
        NakagoCustomPositions = new Dictionary<int, int>();
        NakagoDirections = new Dictionary<int, bool>();

        for (int i = 0; i < 10; i++)
        {
            NakagoCustomPositions[i] = i;
            NakagoDirections[i] = false;

            上筋左右Offsets[i] = "0";
            上筋上下Offsets[i] = "0";

            上宙1左右Offsets[i] = "0";
            上宙1上下Offsets[i] = "0";

            上宙2左右Offsets[i] = "0";
            上宙2上下Offsets[i] = "0";

            下宙1左右Offsets[i] = "0";
            下宙1上下Offsets[i] = "0";

            下宙2左右Offsets[i] = "0";
            下宙2上下Offsets[i] = "0";

            下筋左右Offsets[i] = "0";
            下筋上下Offsets[i] = "0";
        }
    }

    private Dictionary<string, Func<string>> _getters = new Dictionary<string, Func<string>>();

    public void SetGetter(string key, Func<string> getter)
    {
        if (_getters.ContainsKey(key) && _getters[key] == getter) return;
        _getters[key] = getter;
        UpdateValidValues(key);
    }

    private string GetValue(string key)
    {
        return _getters.TryGetValue(key, out var getter) ? getter() : "";
    }

    private List<string> GenerateRange(string text)
    {
        if (int.TryParse(text, out int count) && count > 0)
        {
            return Enumerable.Range(1, count).Select(i => i.ToString()).ToList();
        }
        return new List<string>();
    }

    private void UpdateValidValues(string key)
    {
        var value = GetValue(key);
        // Log bước 1: Giá trị đầu vào
        File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Start] UpdateValidValues - Key: {key}, Value: {value}{Environment.NewLine}");

        switch (key)
        {
            case "上筋本数":
                Valid上筋本数Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid上筋本数Values: {string.Join(",", Valid上筋本数Values)}{Environment.NewLine}");
                break;
            case "下筋本数":
                Valid下筋本数Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid下筋本数Values: {string.Join(",", Valid下筋本数Values)}{Environment.NewLine}");
                break;
            case "上宙1":
                Valid上宙1Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid上宙1Values: {string.Join(",", Valid上宙1Values)}{Environment.NewLine}");
                break;
            case "上宙2":
                Valid上宙2Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid上宙2Values: {string.Join(",", Valid上宙2Values)}{Environment.NewLine}");
                break;
            case "下宙1":
                Valid下宙1Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid下宙1Values: {string.Join(",", Valid下宙1Values)}{Environment.NewLine}");
                break;
            case "下宙2":
                Valid下宙2Values = GenerateRange(value);
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid下宙2Values: {string.Join(",", Valid下宙2Values)}{Environment.NewLine}");
                break;
            case "中子筋本数":
                Valid数Values = GenerateRange(value);
                if (value == "0")
                {
                    Valid数Values = new List<string>();
                }
                File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid数Values: {string.Join(",", Valid数Values)}{Environment.NewLine}");
                break;
        }

        // Cập nhật Valid中子筋の位置Values khi 上筋本数 thay đổi
        if (key == "上筋本数")
        {
            if (int.TryParse(value, out int count) && count > 2)
            {
                Valid中子筋の位置Values = Enumerable.Range(2, count - 2)
                    .Select(i => i.ToString())
                    .ToList();
            }
            else
            {
                Valid中子筋の位置Values = new List<string>();
            }

            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update] Valid中子筋の位置Values (from 上筋本数): {string.Join(",", Valid中子筋の位置Values)}{Environment.NewLine}");
        }

        // Nếu 中子筋本数 == 0 thì Valid中子筋の位置Values cũng trống
        if (key == "中子筋本数" && value == "0")
        {
            Valid中子筋の位置Values = new List<string>();
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset] Valid中子筋の位置Values cleared due to 中子筋本数 == 0{Environment.NewLine}");
        }
    }


}

public class 柱リスト : INotifyPropertyChanged
{
    private string _name;
    private ObservableCollection<柱> _柱;
    public event PropertyChangedEventHandler PropertyChanged;

    public string 各階
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(各階));
            }
        }
    }

    public ObservableCollection<柱> 柱
    {
        get => _柱;
        set
        {
            if (_柱 != value)
            {
                _柱 = value;
                OnPropertyChanged(nameof(柱));
            }
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class 柱 : INotifyPropertyChanged
{
    private string _name;
    private Z柱の配置 _柱の配置;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }
    public Z柱の配置 柱の配置
    {
        get
        {
            if (_柱の配置 == null)
            {
                _柱の配置 = new Z柱の配置();
            }
            return _柱の配置;
        }
        set
        {
            if (_柱の配置 != value)
            {
                _柱の配置 = value;
                OnPropertyChanged(nameof(柱の配置));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;



    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


}

public class Z柱の配置 : INotifyPropertyChanged
{

    #region Backing Fields

    // 柱全断面1 (Column Section 1)
    private string _柱幅; // Column width
    private string _柱成; // Column height
    private string _主筋径; // Main rebar diameter
    private string _主筋材質; // Main rebar material
    private string _芯筋径; // Core rebar diameter
    private string _芯筋材質; // Core rebar material
    private string _柱脚上側主筋本数; // Number of top main rebars
    private string _柱脚下側主筋本数; // Number of bottom main rebars
    private string _柱脚左側主筋本数; // Number of left main rebars
    private string _柱脚右側主筋本数; // Number of right main rebars
    private string _柱脚上側芯筋本数; // Number of top core rebars
    private string _柱脚下側芯筋本数; // Number of bottom core rebars
    private string _柱脚左側芯筋本数; // Number of left core rebars
    private string _柱脚右側芯筋本数; // Number of right core rebars
    private string _HOOP径; // Hoop diameter
    private string _HOOP形; // Hoop shape
    private string _HOOP材質; // Hoop material
    private string _ピッチ; // Spacing
    private string _縦向き中子径; // Vertical middle rebar diameter
    private string _縦向き中子形; // Vertical middle rebar shape
    private string _縦向き中子材質; // Vertical middle rebar material
    private string _縦向き中子ピッチ; // Vertical middle rebar spacing
    private string _柱脚縦向き中子本数; // Number of vertical middle rebars
    private string _横向き中子径; // Horizontal middle rebar diameter
    private string _横向き中子形; // Horizontal middle rebar shape
    private string _横向き中子材質; // Horizontal middle rebar material
    private string _横向き中子ピッチ; // Horizontal middle rebar spacing
    private string _柱脚横向き中子本数; // Number of horizontal middle rebars

    // 柱全断面2 (Column Section 2)
    private string _柱幅1; // Column width
    private string _柱成1; // Column height
    private string _主筋径1; // Main rebar diameter
    private string _主筋材質1; // Main rebar material
    private string _芯筋径1; // Core rebar diameter
    private string _芯筋材質1; // Core rebar material
    private string _柱頭上側主筋本数; // Number of top main rebars
    private string _柱頭下側主筋本数; // Number of bottom main rebars
    private string _柱頭左側主筋本数; // Number of left main rebars
    private string _柱頭右側主筋本数; // Number of right main rebars
    private string _柱頭上側芯筋本数; // Number of top core rebars
    private string _柱頭下側芯筋本数; // Number of bottom core rebars
    private string _柱頭左側芯筋本数; // Number of left core rebars
    private string _柱頭右側芯筋本数; // Number of right core rebars
    private string _HOOP径1; // Hoop diameter
    private string _HOOP形1; // Hoop shape
    private string _HOOP材質1; // Hoop material
    private string _ピッチ1; // Spacing
    private string _縦向き中子径1; // Vertical middle rebar diameter
    private string _縦向き中子形1; // Vertical middle rebar shape
    private string _縦向き中子材質1; // Vertical middle rebar material
    private string _縦向き中子ピッチ1; // Vertical middle rebar spacing
    private string _柱頭縦向き中子本数; // Number of vertical middle rebars
    private string _横向き中子径1; // Horizontal middle rebar diameter
    private string _横向き中子形1; // Horizontal middle rebar shape
    private string _横向き中子材質1; // Horizontal middle rebar material
    private string _横向き中子ピッチ1; // Horizontal middle rebar spacing
    private string _柱頭横向き中子本数; // Number of horizontal middle rebars

    // 仕口部 (Joint Section)
    private string _仕口部_HOOP形; // Joint hoop shape
    private string _仕口部_HOOP材質; // Joint hoop material
    private string _仕口部_ピッチ; // Joint spacing
    private string _仕口部_縦向き中子径; // Joint vertical middle rebar diameter
    private string _仕口部_縦向き中子形; // Joint vertical middle rebar shape
    private string _仕口部_縦向き中子材質; // Joint vertical middle rebar material
    private string _仕口部_縦向き中子ピッチ; // Joint vertical middle rebar spacing
    private string _仕口部縦向き中子本数; // Number of joint vertical middle rebars
    private string _仕口部_横向き中子径; // Joint horizontal middle rebar diameter
    private string _仕口部_横向き中子形; // Joint horizontal middle rebar shape
    private string _仕口部_横向き中子材質; // Joint horizontal middle rebar material
    private string _仕口部_横向き中子ピッチ; // Joint horizontal middle rebar spacing
    private string _仕口部横向き中子本数; // Number of joint horizontal middle rebars      

    private string _梁COMBOBOX;
    private bool _柱全断面;

    private Dictionary<string, GridBotDataHashira> _gridbotdata;

    #endregion

    #region Properties
    // 柱全断面1 (Column Section 1)

    public Z柱の配置()
    {
        //Trái
        柱幅 = "800";
        柱成 = "800";
        主筋径 = "29";
        主筋材質 = "SD390";
        芯筋径 = "29";
        芯筋材質 = "SD390";
        柱脚上側主筋本数 = "2";
        柱脚下側主筋本数 = "2";
        柱脚左側主筋本数 = "2";
        柱脚右側主筋本数 = "2";
        柱脚上側芯筋本数 = "0";
        柱脚下側芯筋本数 = "0";
        柱脚左側芯筋本数 = "0";
        柱脚右側芯筋本数 = "0";
        HOOP径 = "13";
        HOOP形 = "1";
        HOOP材質 = "SD295";
        ピッチ = "100";
        縦向き中子径 = "13";
        縦向き中子形 = "1";
        縦向き中子材質 = "SD295";
        縦向き中子ピッチ = "100";
        柱脚縦向き中子本数 = "2";
        横向き中子径 = "13";
        横向き中子形 = "1";
        横向き中子材質 = "SD295";
        横向き中子ピッチ = "100";
        柱脚横向き中子本数 = "2";

        //Giữa
        柱幅1 = "900";
        柱成1 = "900";
        主筋径1 = "29";
        主筋材質1 = "SD390";
        芯筋径1 = "29";
        芯筋材質1 = "SD390";
        柱頭上側主筋本数 = "2";
        柱頭下側主筋本数 = "2";
        柱頭左側主筋本数 = "2";
        柱頭右側主筋本数 = "2";
        柱頭上側芯筋本数 = "0";
        柱頭下側芯筋本数 = "0";
        柱頭左側芯筋本数 = "0";
        柱頭右側芯筋本数 = "0";
        HOOP径1 = "13";
        HOOP形1 = "1";
        HOOP材質1 = "SD295";
        ピッチ1 = "100";
        縦向き中子径1 = "13";
        縦向き中子形1 = "1";
        縦向き中子材質1 = "SD295";
        縦向き中子ピッチ1 = "100";
        柱頭縦向き中子本数 = "2";
        横向き中子径1 = "13";
        横向き中子形1 = "1";
        横向き中子材質1 = "SD295";
        横向き中子ピッチ1 = "100";
        柱頭横向き中子本数 = "2";

        //Phải
        仕口部_HOOP形 = "1";
        仕口部_HOOP材質 = "SD295";
        仕口部_ピッチ = "100";
        仕口部_縦向き中子径 = "13";
        仕口部_縦向き中子形 = "1";
        仕口部_縦向き中子材質 = "SD295";
        仕口部_縦向き中子ピッチ = "100";
        仕口部縦向き中子本数 = "1";

        仕口部_横向き中子径 = "13";
        仕口部_横向き中子形 = "1";
        仕口部_横向き中子材質 = "SD295";
        仕口部_横向き中子ピッチ = "100";
        仕口部横向き中子本数 = "1";

        // 全体 (General)
        _柱全断面 = false;
        _梁COMBOBOX = "柱脚";


        gridbotdata = new Dictionary<string, GridBotDataHashira>
        {
            ["柱脚"] = new GridBotDataHashira("柱脚"),
            ["柱頭"] = new GridBotDataHashira("柱頭"),
            ["仕口部"] = new GridBotDataHashira("仕口部")
        };
    }


    public string 柱幅
    {
        get => _柱幅;
        set => SetProperty(ref _柱幅, value);
    }
    public string 柱成
    {
        get => _柱成;
        set => SetProperty(ref _柱成, value);
    }
    public string 主筋径
    {
        get => _主筋径;
        set => SetProperty(ref _主筋径, value);
    }
    public string 主筋材質
    {
        get => _主筋材質;
        set => SetProperty(ref _主筋材質, value);
    }
    public string 芯筋径
    {
        get => _芯筋径;
        set => SetProperty(ref _芯筋径, value);
    }
    public string 芯筋材質
    {
        get => _芯筋材質;
        set => SetProperty(ref _芯筋材質, value);
    }
    public string 柱脚上側主筋本数
    {
        get => _柱脚上側主筋本数;
        set => SetProperty(ref _柱脚上側主筋本数, value);
    }
    public string 柱脚下側主筋本数
    {
        get => _柱脚下側主筋本数;
        set => SetProperty(ref _柱脚下側主筋本数, value);
    }
    public string 柱脚左側主筋本数
    {
        get => _柱脚左側主筋本数;
        set => SetProperty(ref _柱脚左側主筋本数, value);
    }
    public string 柱脚右側主筋本数
    {
        get => _柱脚右側主筋本数;
        set => SetProperty(ref _柱脚右側主筋本数, value);
    }
    public string 柱脚上側芯筋本数
    {
        get => _柱脚上側芯筋本数;
        set => SetProperty(ref _柱脚上側芯筋本数, value);
    }
    public string 柱脚下側芯筋本数
    {
        get => _柱脚下側芯筋本数;
        set => SetProperty(ref _柱脚下側芯筋本数, value);
    }
    public string 柱脚左側芯筋本数
    {
        get => _柱脚左側芯筋本数;
        set => SetProperty(ref _柱脚左側芯筋本数, value);
    }
    public string 柱脚右側芯筋本数
    {
        get => _柱脚右側芯筋本数;
        set => SetProperty(ref _柱脚右側芯筋本数, value);
    }
    public string HOOP径
    {
        get => _HOOP径;
        set => SetProperty(ref _HOOP径, value);
    }
    public string HOOP形
    {
        get => _HOOP形;
        set => SetProperty(ref _HOOP形, value);
    }
    public string HOOP材質
    {
        get => _HOOP材質;
        set => SetProperty(ref _HOOP材質, value);
    }
    public string ピッチ
    {
        get => _ピッチ;
        set => SetProperty(ref _ピッチ, value);
    }
    public string 縦向き中子径
    {
        get => _縦向き中子径;
        set => SetProperty(ref _縦向き中子径, value);
    }
    public string 縦向き中子形
    {
        get => _縦向き中子形;
        set => SetProperty(ref _縦向き中子形, value);
    }
    public string 縦向き中子材質
    {
        get => _縦向き中子材質;
        set => SetProperty(ref _縦向き中子材質, value);
    }
    public string 縦向き中子ピッチ
    {
        get => _縦向き中子ピッチ;
        set => SetProperty(ref _縦向き中子ピッチ, value);
    }
    public string 柱脚縦向き中子本数
    {
        get => _柱脚縦向き中子本数;
        set => SetProperty(ref _柱脚縦向き中子本数, value);
    }
    public string 横向き中子径
    {
        get => _横向き中子径;
        set => SetProperty(ref _横向き中子径, value);
    }
    public string 横向き中子形
    {
        get => _横向き中子形;
        set => SetProperty(ref _横向き中子形, value);
    }
    public string 横向き中子材質
    {
        get => _横向き中子材質;
        set => SetProperty(ref _横向き中子材質, value);
    }
    public string 横向き中子ピッチ
    {
        get => _横向き中子ピッチ;
        set => SetProperty(ref _横向き中子ピッチ, value);
    }
    public string 柱脚横向き中子本数
    {
        get => _柱脚横向き中子本数;
        set => SetProperty(ref _柱脚横向き中子本数, value);
    }
    public string 柱幅1
    {
        get => _柱幅1;
        set => SetProperty(ref _柱幅1, value);
    }
    public string 柱成1
    {
        get => _柱成1;
        set => SetProperty(ref _柱成1, value);
    }
    public string 主筋径1
    {
        get => _主筋径1;
        set => SetProperty(ref _主筋径1, value);
    }
    public string 主筋材質1
    {
        get => _主筋材質1;
        set => SetProperty(ref _主筋材質1, value);
    }
    public string 芯筋径1
    {
        get => _芯筋径1;
        set => SetProperty(ref _芯筋径1, value);
    }
    public string 芯筋材質1
    {
        get => _芯筋材質1;
        set => SetProperty(ref _芯筋材質1, value);
    }
    public string 柱頭上側主筋本数
    {
        get => _柱頭上側主筋本数;
        set => SetProperty(ref _柱頭上側主筋本数, value);
    }
    public string 柱頭下側主筋本数
    {
        get => _柱頭下側主筋本数;
        set => SetProperty(ref _柱頭下側主筋本数, value);
    }
    public string 柱頭左側主筋本数
    {
        get => _柱頭左側主筋本数;
        set => SetProperty(ref _柱頭左側主筋本数, value);
    }
    public string 柱頭右側主筋本数
    {
        get => _柱頭右側主筋本数;
        set => SetProperty(ref _柱頭右側主筋本数, value);
    }
    public string 柱頭上側芯筋本数
    {
        get => _柱頭上側芯筋本数;
        set => SetProperty(ref _柱頭上側芯筋本数, value);
    }
    public string 柱頭下側芯筋本数
    {
        get => _柱頭下側芯筋本数;
        set => SetProperty(ref _柱頭下側芯筋本数, value);
    }
    public string 柱頭左側芯筋本数
    {
        get => _柱頭左側芯筋本数;
        set => SetProperty(ref _柱頭左側芯筋本数, value);
    }
    public string 柱頭右側芯筋本数
    {
        get => _柱頭右側芯筋本数;
        set => SetProperty(ref _柱頭右側芯筋本数, value);
    }
    public string HOOP径1
    {
        get => _HOOP径1;
        set => SetProperty(ref _HOOP径1, value);
    }
    public string HOOP形1
    {
        get => _HOOP形1;
        set => SetProperty(ref _HOOP形1, value);
    }
    public string HOOP材質1
    {
        get => _HOOP材質1;
        set => SetProperty(ref _HOOP材質1, value);
    }
    public string ピッチ1
    {
        get => _ピッチ1;
        set => SetProperty(ref _ピッチ1, value);
    }
    public string 縦向き中子径1
    {
        get => _縦向き中子径1;
        set => SetProperty(ref _縦向き中子径1, value);
    }
    public string 縦向き中子形1
    {
        get => _縦向き中子形1;
        set => SetProperty(ref _縦向き中子形1, value);
    }
    public string 縦向き中子材質1
    {
        get => _縦向き中子材質1;
        set => SetProperty(ref _縦向き中子材質1, value);
    }
    public string 縦向き中子ピッチ1
    {
        get => _縦向き中子ピッチ1;
        set => SetProperty(ref _縦向き中子ピッチ1, value);
    }
    public string 柱頭縦向き中子本数
    {
        get => _柱頭縦向き中子本数;
        set => SetProperty(ref _柱頭縦向き中子本数, value);
    }
    public string 横向き中子径1
    {
        get => _横向き中子径1;
        set => SetProperty(ref _横向き中子径1, value);
    }
    public string 横向き中子形1
    {
        get => _横向き中子形1;
        set => SetProperty(ref _横向き中子形1, value);
    }
    public string 横向き中子材質1
    {
        get => _横向き中子材質1;
        set => SetProperty(ref _横向き中子材質1, value);
    }
    public string 横向き中子ピッチ1
    {
        get => _横向き中子ピッチ1;
        set => SetProperty(ref _横向き中子ピッチ1, value);
    }
    public string 柱頭横向き中子本数
    {
        get => _柱頭横向き中子本数;
        set => SetProperty(ref _柱頭横向き中子本数, value);
    }
    public string 仕口部_HOOP形
    {
        get => _仕口部_HOOP形;
        set => SetProperty(ref _仕口部_HOOP形, value);
    }
    public string 仕口部_HOOP材質
    {
        get => _仕口部_HOOP材質;
        set => SetProperty(ref _仕口部_HOOP材質, value);
    }
    public string 仕口部_ピッチ
    {
        get => _仕口部_ピッチ;
        set => SetProperty(ref _仕口部_ピッチ, value);
    }
    public string 仕口部_縦向き中子径
    {
        get => _仕口部_縦向き中子径;
        set => SetProperty(ref _仕口部_縦向き中子径, value);
    }
    public string 仕口部_縦向き中子形
    {
        get => _仕口部_縦向き中子形;
        set => SetProperty(ref _仕口部_縦向き中子形, value);
    }
    public string 仕口部_縦向き中子材質
    {
        get => _仕口部_縦向き中子材質;
        set => SetProperty(ref _仕口部_縦向き中子材質, value);
    }
    public string 仕口部_縦向き中子ピッチ
    {
        get => _仕口部_縦向き中子ピッチ;
        set => SetProperty(ref _仕口部_縦向き中子ピッチ, value);
    }
    public string 仕口部縦向き中子本数
    {
        get => _仕口部縦向き中子本数;
        set => SetProperty(ref _仕口部縦向き中子本数, value);
    }
    public string 仕口部_横向き中子径
    {
        get => _仕口部_横向き中子径;
        set => SetProperty(ref _仕口部_横向き中子径, value);
    }
    public string 仕口部_横向き中子形
    {
        get => _仕口部_横向き中子形;
        set => SetProperty(ref _仕口部_横向き中子形, value);
    }
    public string 仕口部_横向き中子材質
    {
        get => _仕口部_横向き中子材質;
        set => SetProperty(ref _仕口部_横向き中子材質, value);
    }
    public string 仕口部_横向き中子ピッチ
    {
        get => _仕口部_横向き中子ピッチ;
        set => SetProperty(ref _仕口部_横向き中子ピッチ, value);
    }
    public string 仕口部横向き中子本数
    {
        get => _仕口部横向き中子本数;
        set => SetProperty(ref _仕口部横向き中子本数, value);
    }
    public string 梁COMBOBOX
    {
        get => _梁COMBOBOX;
        set => SetProperty(ref _梁COMBOBOX, value);
    }
    public bool 柱全断面
    {
        get => _柱全断面;
        set => SetProperty(ref _柱全断面, value);
    }
    public Dictionary<string, GridBotDataHashira> gridbotdata
    {
        get => _gridbotdata;
        set => SetProperty(ref _gridbotdata, value);
    }

    #endregion

    #region INotifyPropertyChanged Implementation
    // Sự kiện PropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;

    // Phương thức thông báo thay đổi thuộc tính
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            OnPropertyChanged(propertyName);
        }
        return true;
    }
    #endregion
}
public class GridBotDataHashira : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propName = null)
         => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    // Phương thức tiện ích để set giá trị và gọi OnPropertyChanged
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return false;

        backingField = value;
        if (PropertyChangeTracker.IsTrackingEnabled)
        {
            PropertyChangeTracker.MarkChanged();
            OnPropertyChanged(propertyName);
        }
        return true;
    }

    public GridBotDataHashira Clone()
    {
        var serialized = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<GridBotDataHashira>(serialized);
    }
    public string BeamType { get; set; }
    #region Fields
    private string _上;
    private string _下;
    private string _右;
    private string _左;
    private string _上側主筋本数2;
    private string _下側主筋本数2;
    private string _左側主筋本数2;
    private string _右側主筋本数2;
    private string _上側芯筋本数2;
    private string _下側芯筋本数2;
    private string _左側芯筋本数2;
    private string _右側芯筋本数2;
    private string _左右;
    private string _上下;
    private string _左右1;
    private string _上下1;
    private string _左右2;
    private string _上下2;
    private string _左右3;
    private string _上下3;
    private string _左右4;
    private string _上下4;
    private string _左右5;
    private string _上下5;
    private string _左右6;
    private string _上下6;
    private string _左右7;
    private string _上下7;
    private string _フックの位置;
    private string _縦向き中子本数;
    private string _縦向き中子位置;
    private string _横向き中子本数;
    private string _横向き中子位置;
    private bool _縦向き中子_方向;
    private bool _横向き中子_方向;
    private Dictionary<int, int> _nakagoCustomPositions;
    private Dictionary<int, bool> _nakagoDirections;
    private Dictionary<int, int> _yokogaoNakagoCustomPositions;
    private Dictionary<int, bool> _yokogaoNakagoDirections;
    private List<string> _valid上側主筋本数Values;
    private List<string> _valid下側主筋本数Values;
    private List<string> _valid左側主筋本数Values;
    private List<string> _valid右側主筋本数Values;
    private List<string> _valid上側芯筋本数Values;
    private List<string> _valid下側芯筋本数Values;
    private List<string> _valid左側芯筋本数Values;
    private List<string> _valid右側芯筋本数Values;
    private List<string> _valid縦向き中子の位置Values;
    private List<string> _valid縦向き中子本数Values;
    private List<string> _valid横向き中子本数Values;
    private List<string> _valid横向き中子の位置Values;
    private Dictionary<int, string> _左右Offsets;
    private Dictionary<int, string> _上下Offsets;
    private Dictionary<int, string> _左右1Offsets;
    private Dictionary<int, string> _上下1Offsets;
    private Dictionary<int, string> _左右2Offsets;
    private Dictionary<int, string> _上下2Offsets;
    private Dictionary<int, string> _左右3Offsets;
    private Dictionary<int, string> _上下3Offsets;
    private Dictionary<int, string> _左右4Offsets;
    private Dictionary<int, string> _上下4Offsets;
    private Dictionary<int, string> _左右5Offsets;
    private Dictionary<int, string> _上下5Offsets;
    private Dictionary<int, string> _左右6Offsets;
    private Dictionary<int, string> _上下6Offsets;
    private Dictionary<int, string> _左右7Offsets;
    private Dictionary<int, string> _上下7Offsets;
    #endregion

    #region Properties
    public string 上
    {
        get => _上;
        set => SetProperty(ref _上, value);
    }
    public string 下
    {
        get => _下;
        set => SetProperty(ref _下, value);
    }
    public string 右
    {
        get => _右;
        set => SetProperty(ref _右, value);
    }
    public string 左
    {
        get => _左;
        set => SetProperty(ref _左, value);
    }
    public string 上側主筋本数2
    {
        get => _上側主筋本数2;
        set => SetProperty(ref _上側主筋本数2, value);
    }
    public string 下側主筋本数2
    {
        get => _下側主筋本数2;
        set => SetProperty(ref _下側主筋本数2, value);
    }
    public string 左側主筋本数2
    {
        get => _左側主筋本数2;
        set => SetProperty(ref _左側主筋本数2, value);
    }
    public string 右側主筋本数2
    {
        get => _右側主筋本数2;
        set => SetProperty(ref _右側主筋本数2, value);
    }
    public string 上側芯筋本数2
    {
        get => _上側芯筋本数2;
        set => SetProperty(ref _上側芯筋本数2, value);
    }
    public string 下側芯筋本数2
    {
        get => _下側芯筋本数2;
        set => SetProperty(ref _下側芯筋本数2, value);
    }
    public string 左側芯筋本数2
    {
        get => _左側芯筋本数2;
        set => SetProperty(ref _左側芯筋本数2, value);
    }
    public string 右側芯筋本数2
    {
        get => _右側芯筋本数2;
        set => SetProperty(ref _右側芯筋本数2, value);
    }
    public string 左右
    {
        get => _左右;
        set => SetProperty(ref _左右, value);
    }
    public string 上下
    {
        get => _上下;
        set => SetProperty(ref _上下, value);
    }
    public string 左右1
    {
        get => _左右1;
        set => SetProperty(ref _左右1, value);
    }
    public string 上下1
    {
        get => _上下1;
        set => SetProperty(ref _上下1, value);
    }
    public string 左右2
    {
        get => _左右2;
        set => SetProperty(ref _左右2, value);
    }
    public string 上下2
    {
        get => _上下2;
        set => SetProperty(ref _上下2, value);
    }
    public string 左右3
    {
        get => _左右3;
        set => SetProperty(ref _左右3, value);
    }
    public string 上下3
    {
        get => _上下3;
        set => SetProperty(ref _上下3, value);
    }
    public string 左右4
    {
        get => _左右4;
        set => SetProperty(ref _左右4, value);
    }
    public string 上下4
    {
        get => _上下4;
        set => SetProperty(ref _上下4, value);
    }
    public string 左右5
    {
        get => _左右5;
        set => SetProperty(ref _左右5, value);
    }
    public string 上下5
    {
        get => _上下5;
        set => SetProperty(ref _上下5, value);
    }
    public string 左右6
    {
        get => _左右6;
        set => SetProperty(ref _左右6, value);
    }
    public string 上下6
    {
        get => _上下6;
        set => SetProperty(ref _上下6, value);
    }
    public string 左右7
    {
        get => _左右7;
        set => SetProperty(ref _左右7, value);
    }
    public string 上下7
    {
        get => _上下7;
        set => SetProperty(ref _上下7, value);
    }
    public string フックの位置
    {
        get => _フックの位置;
        set => SetProperty(ref _フックの位置, value);
    }
    public string 縦向き中子本数
    {
        get => _縦向き中子本数;
        set => SetProperty(ref _縦向き中子本数, value);
    }
    public string 縦向き中子位置
    {
        get => _縦向き中子位置;
        set => SetProperty(ref _縦向き中子位置, value);
    }
    public string 横向き中子本数
    {
        get => _横向き中子本数;
        set => SetProperty(ref _横向き中子本数, value);
    }
    public string 横向き中子位置
    {
        get => _横向き中子位置;
        set => SetProperty(ref _横向き中子位置, value);
    }
    public bool 縦向き中子_方向
    {
        get => _縦向き中子_方向;
        set => SetProperty(ref _縦向き中子_方向, value);
    }
    public bool 横向き中子_方向
    {
        get => _横向き中子_方向;
        set => SetProperty(ref _横向き中子_方向, value);
    }
    public Dictionary<int, int> NakagoCustomPositions
    {
        get => _nakagoCustomPositions;
        set => SetProperty(ref _nakagoCustomPositions, value);
    }
    public Dictionary<int, bool> NakagoDirections
    {
        get => _nakagoDirections;
        set => SetProperty(ref _nakagoDirections, value);
    }
    public Dictionary<int, int> YokogaoNakagoCustomPositions
    {
        get => _yokogaoNakagoCustomPositions;
        set => SetProperty(ref _yokogaoNakagoCustomPositions, value);
    }
    public Dictionary<int, bool> YokogaoNakagoDirections
    {
        get => _yokogaoNakagoDirections;
        set => SetProperty(ref _yokogaoNakagoDirections, value);
    }
    public List<string> Valid上側主筋本数Values
    {
        get => _valid上側主筋本数Values;
        set => SetProperty(ref _valid上側主筋本数Values, value);
    }
    public List<string> Valid下側主筋本数Values
    {
        get => _valid下側主筋本数Values;
        set => SetProperty(ref _valid下側主筋本数Values, value);
    }
    public List<string> Valid左側主筋本数Values
    {
        get => _valid左側主筋本数Values;
        set => SetProperty(ref _valid左側主筋本数Values, value);
    }
    public List<string> Valid右側主筋本数Values
    {
        get => _valid右側主筋本数Values;
        set => SetProperty(ref _valid右側主筋本数Values, value);
    }
    public List<string> Valid上側芯筋本数Values
    {
        get => _valid上側芯筋本数Values;
        set => SetProperty(ref _valid上側芯筋本数Values, value);
    }
    public List<string> Valid下側芯筋本数Values
    {
        get => _valid下側芯筋本数Values;
        set => SetProperty(ref _valid下側芯筋本数Values, value);
    }
    public List<string> Valid左側芯筋本数Values
    {
        get => _valid左側芯筋本数Values;
        set => SetProperty(ref _valid左側芯筋本数Values, value);
    }
    public List<string> Valid右側芯筋本数Values
    {
        get => _valid右側芯筋本数Values;
        set => SetProperty(ref _valid右側芯筋本数Values, value);
    }
    public List<string> Valid縦向き中子の位置Values
    {
        get => _valid縦向き中子の位置Values;
        set => SetProperty(ref _valid縦向き中子の位置Values, value);
    }
    public List<string> Valid縦向き中子本数Values
    {
        get => _valid縦向き中子本数Values;
        set => SetProperty(ref _valid縦向き中子本数Values, value);
    }
    public List<string> Valid横向き中子本数Values
    {
        get => _valid横向き中子本数Values;
        set => SetProperty(ref _valid横向き中子本数Values, value);
    }
    public List<string> Valid横向き中子の位置Values
    {
        get => _valid横向き中子の位置Values;
        set => SetProperty(ref _valid横向き中子の位置Values, value);
    }
    public Dictionary<int, string> 左右Offsets
    {
        get => _左右Offsets;
        set => SetProperty(ref _左右Offsets, value);
    }
    public Dictionary<int, string> 上下Offsets
    {
        get => _上下Offsets;
        set => SetProperty(ref _上下Offsets, value);
    }
    public Dictionary<int, string> 左右1Offsets
    {
        get => _左右1Offsets;
        set => SetProperty(ref _左右1Offsets, value);
    }
    public Dictionary<int, string> 上下1Offsets
    {
        get => _上下1Offsets;
        set => SetProperty(ref _上下1Offsets, value);
    }
    public Dictionary<int, string> 左右2Offsets
    {
        get => _左右2Offsets;
        set => SetProperty(ref _左右2Offsets, value);
    }
    public Dictionary<int, string> 上下2Offsets
    {
        get => _上下2Offsets;
        set => SetProperty(ref _上下2Offsets, value);
    }
    public Dictionary<int, string> 左右3Offsets
    {
        get => _左右3Offsets;
        set => SetProperty(ref _左右3Offsets, value);
    }
    public Dictionary<int, string> 上下3Offsets
    {
        get => _上下3Offsets;
        set => SetProperty(ref _上下3Offsets, value);
    }
    public Dictionary<int, string> 左右4Offsets
    {
        get => _左右4Offsets;
        set => SetProperty(ref _左右4Offsets, value);
    }
    public Dictionary<int, string> 上下4Offsets
    {
        get => _上下4Offsets;
        set => SetProperty(ref _上下4Offsets, value);
    }
    public Dictionary<int, string> 左右5Offsets
    {
        get => _左右5Offsets;
        set => SetProperty(ref _左右5Offsets, value);
    }
    public Dictionary<int, string> 上下5Offsets
    {
        get => _上下5Offsets;
        set => SetProperty(ref _上下5Offsets, value);
    }
    public Dictionary<int, string> 左右6Offsets
    {
        get => _左右6Offsets;
        set => SetProperty(ref _左右6Offsets, value);
    }
    public Dictionary<int, string> 上下6Offsets
    {
        get => _上下6Offsets;
        set => SetProperty(ref _上下6Offsets, value);
    }
    public Dictionary<int, string> 左右7Offsets
    {
        get => _左右7Offsets;
        set => SetProperty(ref _左右7Offsets, value);
    }
    public Dictionary<int, string> 上下7Offsets
    {
        get => _上下7Offsets;
        set => SetProperty(ref _上下7Offsets, value);
    }

    #endregion

    public GridBotDataHashira(string beamType)
    {
        BeamType = beamType;
        上 = "50";
        下 = "50";
        右 = "50";
        左 = "50";
        上側主筋本数2 = "1";
        下側主筋本数2 = "1";
        左側主筋本数2 = "1";
        右側主筋本数2 = "1";
        上側芯筋本数2 = "1";
        下側芯筋本数2 = "1";
        左側芯筋本数2 = "1";
        右側芯筋本数2 = "1";
        左右 = "0";
        上下 = "0";
        左右1 = "0";
        上下1 = "0";
        左右2 = "0";
        上下2 = "0";
        左右3 = "0";
        上下3 = "0";
        左右4 = "0";
        上下4 = "0";
        左右5 = "0";
        上下5 = "0";
        左右6 = "0";
        上下6 = "0";
        左右7 = "0";
        上下7 = "0";

        フックの位置 = "1";
        縦向き中子本数 = "1";
        縦向き中子位置 = "2";
        横向き中子本数 = "1";
        横向き中子位置 = "2";
        縦向き中子_方向 = false;
        横向き中子_方向 = false;

        // Khởi tạo các dictionary với giá trị mặc định
        NakagoCustomPositions = new Dictionary<int, int>();
        NakagoDirections = new Dictionary<int, bool>();
        YokogaoNakagoCustomPositions = new Dictionary<int, int>();
        YokogaoNakagoDirections = new Dictionary<int, bool>();
        左右Offsets = new Dictionary<int, string>();
        上下Offsets = new Dictionary<int, string>();
        左右1Offsets = new Dictionary<int, string>();
        上下1Offsets = new Dictionary<int, string>();
        左右2Offsets = new Dictionary<int, string>();
        上下2Offsets = new Dictionary<int, string>();
        左右3Offsets = new Dictionary<int, string>();
        上下3Offsets = new Dictionary<int, string>();
        左右4Offsets = new Dictionary<int, string>();
        上下4Offsets = new Dictionary<int, string>();
        左右5Offsets = new Dictionary<int, string>();
        上下5Offsets = new Dictionary<int, string>();
        左右6Offsets = new Dictionary<int, string>();
        上下6Offsets = new Dictionary<int, string>();
        左右7Offsets = new Dictionary<int, string>();
        上下7Offsets = new Dictionary<int, string>();


        for (int i = 0; i < 10; i++)
        {
            NakagoCustomPositions[i] = i;
            NakagoDirections[i] = false;
            YokogaoNakagoCustomPositions[i] = i;
            YokogaoNakagoDirections[i] = false;

            左右Offsets[i] = "0";
            上下Offsets[i] = "0";

            左右1Offsets[i] = "0";
            上下1Offsets[i] = "0";

            左右2Offsets[i] = "0";
            上下2Offsets[i] = "0";

            左右3Offsets[i] = "0";
            上下3Offsets[i] = "0";

            左右4Offsets[i] = "0";
            上下4Offsets[i] = "0";

            左右5Offsets[i] = "0";
            上下5Offsets[i] = "0";

            左右6Offsets[i] = "0";
            上下6Offsets[i] = "0";

            左右7Offsets[i] = "0";
            上下7Offsets[i] = "0";
        }
    }
    private Dictionary<string, Func<string>> _getters = new Dictionary<string, Func<string>>();

    public void SetGetter(string key, Func<string> getter)
    {
        if (_getters.ContainsKey(key) && _getters[key] == getter) return;
        _getters[key] = getter;
        UpdateValidValues(key);
    }

    private string GetValue(string key)
    {
        return _getters.TryGetValue(key, out var getter) ? getter() : "";
    }

    private List<string> GenerateRange(string text)
    {
        if (int.TryParse(text, out int count) && count > 0)
        {
            return Enumerable.Range(1, count).Select(i => i.ToString()).ToList();
        }
        return new List<string>();
    }

    private void UpdateValidValues(string key)
    {
        switch (key)
        {
            case "上側主筋本数":
                Valid上側主筋本数Values = GenerateRange(GetValue(key));
                break;
            case "下側主筋本数":
                Valid下側主筋本数Values = GenerateRange(GetValue(key));
                break;
            case "左側主筋本数":
                Valid左側主筋本数Values = GenerateRange(GetValue(key));
                break;
            case "右側主筋本数":
                Valid右側主筋本数Values = GenerateRange(GetValue(key));
                break;
            case "上側芯筋本数":
                Valid上側芯筋本数Values = GenerateRange(GetValue(key));
                break;
            case "下側芯筋本数":
                Valid下側芯筋本数Values = GenerateRange(GetValue(key));
                break;
            case "左側芯筋本数":
                Valid左側芯筋本数Values = GenerateRange(GetValue(key));
                break;
            case "右側芯筋本数":
                Valid右側芯筋本数Values = GenerateRange(GetValue(key));
                break;
            case "縦向き中子本数":
                Valid縦向き中子本数Values = GenerateRange(GetValue(key));
                break;
            case "横向き中子本数":
                Valid横向き中子本数Values = GenerateRange(GetValue(key));
                break;
        }
        if (key == "上側主筋本数")
        {
            if (int.TryParse(GetValue(key), out int count) && count > 0)
            {
                Valid縦向き中子の位置Values = Enumerable.Range(2, count).Select(i => i.ToString()).ToList();
            }
            else
            {
                Valid縦向き中子の位置Values = new List<string>();
            }
        }


        if (key == "左側主筋本数")
        {
            if (int.TryParse(GetValue(key), out int count) && count > 0)
            {
                Valid横向き中子の位置Values = Enumerable.Range(2, count).Select(i => i.ToString()).ToList();
            }
            else
            {
                Valid横向き中子の位置Values = new List<string>();
            }
        }
    }

}


public enum Matomeprint
{
    鉄筋１本ずつのまま印刷する,
    同配筋の鉄筋を本数でまとめて印刷する
}
public enum TaskType
{
    Design,
    Fabrication,
    Inspection,
    Delivery
}
