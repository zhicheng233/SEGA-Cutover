using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SEGA_Cutover;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private int _selectIndex = 4;
    private readonly List<string> _selectList = new();
    private int _selected;
    private Config _config = Config.GetConfig();
    private CancellationTokenSource _hidCts = new();
    public MainWindow()
    {
        InitializeComponent();

        _selectList = _config.GetAppList();
        
        DiskLabel1.Content = "";
        DiskLabel2.Content = "";
        DiskLabel3.Content = "";

        if (_selectList.Count > 0)
        {
            DiskLabel1.Content = _selectList[0];
            if (_config.TargetGameId == _selectList[0])
            {
                DiskLabel1.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
            }
        }
        if (_selectList.Count > 1)
        {
            DiskLabel2.Content = _selectList[1];
            if (_config.TargetGameId == _selectList[1])
            {
                DiskLabel2.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
            }
        }
        if (_selectList.Count > 2)
        {
            DiskLabel3.Content = _selectList[2];
            if (_config.TargetGameId == _selectList[2])
            {
                DiskLabel3.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
            }
        }

        if (_selectList.Count == 0)
        {
            DiskLabel1.Content = "未配置";
            DiskLabel1.Foreground = new SolidColorBrush(Color.FromRgb(143, 143, 143));
            DiskLabel2.Content = "";
            DiskLabel3.Content = "";
            Grid.SetRow(CursorCanvas,5);
        }
        
        StartHid();
        
    }

    /// <summary>
    /// 启动 HID 监听功能，用于处理 HID 输入设备的按键反馈。
    /// 该方法在后台启动一个异步任务以监听 HID 按键事件，并相应触发指定的操作。
    /// 
    /// 当检测到按键事件时：
    /// - 如果按下按钮 4，则触发下一个选项的选择操作。
    /// - 如果按下按钮 5，则触发确认操作。
    /// 
    /// 捕获任何可能的异常并在控制台中记录错误信息。
    /// </summary>
    private void StartHid()
    {
        Task.Run(() =>
        {
            try
            {
                Hid.ReadHid(_hidCts.Token, buttons =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (buttons.HasFlag(HidButtons.Button4))
                        {
                            NextSelect();
                        }

                        if (buttons.HasFlag(HidButtons.Button5))
                        {
                            Confirm();
                        }
                    });
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"HID Error: {e}");
            }
        });
    }
    
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            // 在 Preview 事件中处理可以更早地拦截到按键
            e.Handled = true;
            NextSelect();
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm();
        }
    }

    public void NextSelect()
    {
        _selectIndex = _selectIndex + 1;

        if (_selectIndex >= 5)
        {
            _selectIndex = 0;
        }else if (_selectIndex >= _selectList.Count)
        {
            _selectIndex = 4;   //进入确定
        }

        Grid.SetRow(CursorCanvas, _selectIndex);
        
    }
    public void Confirm()
    {
        if (_selectIndex == 4)
        {
            Config.SaveConfig();
            Console.Out.WriteLine("保存配置文件成功");
            Win32API.RebootWindows();
        }

        _selected = _selectIndex;
        
        if (_selectIndex != 4)
        {
            DiskLabel1.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoExtraLight"];;
            DiskLabel2.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoExtraLight"];;
            DiskLabel3.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoExtraLight"];;
        }

        
        switch (_selected)
        {
            case 0:
                DiskLabel1.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
                _config.TargetGameId = _selectList[0];
                break;
            case 1:
                DiskLabel2.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
                _config.TargetGameId = _selectList[1];
                break;
            case 2:
                DiskLabel3.FontFamily = (FontFamily)Application.Current.Resources["JetBrainsMonoBold"];
                _config.TargetGameId = _selectList[2];
                break;
        }
        
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
    }
}