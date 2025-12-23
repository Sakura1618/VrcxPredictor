using CommunityToolkit.Mvvm.ComponentModel;

namespace VrcxPredictor.App.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    [ObservableProperty] private string _text =
        "VRCX Predictor 是一个本地桌面工具：读取电脑上的 VRCX.sqlite3（只读）\n" +
        "把 VRChat 的 Online/Offline 记录做成统计与热力图，\n" +
        "并给出“未来 2 小时上线概率”和“未来 24 小时最佳上线时间窗”。\n\n" +
        "隐私：不联网、不上传，只读取本地 SQLite 文件。\n\n" +
        "技术栈：" +
        "WPF UI(wpf-ui + CommunityToolkit.Mvvm) •  DB: SQLite+Dapper  •  MVVM: CommunityToolkit.Mvvm";
}
