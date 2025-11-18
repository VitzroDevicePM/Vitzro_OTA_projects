using System.Windows;
using Vitzro_OTA_projects.Model;

/*
 * File: MainView.cs
 * Description: 메인 화면을 담당하는 뷰 클레스
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel(new FileDialogService(), new NetworkScannerService());
        }
    }
}
