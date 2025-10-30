using Microsoft.Win32;
using System;
using WinForms = System.Windows.Forms;
/**
 * File: FileDialogService.cs
 * Description: 파일 폴더 선택 후 경로를 출력하기위한 모델
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model
{
    public interface IFileDialogService
    {
        /**
         * BRIEF  파일 선택 팝업 호출용 함수
         * PARAM  filter: 파일 선택 필터 기능
         *        initialDir: 팝업 호출 시 초기 디렉토리
         * THROWS System.ArgumentException: folder는 System.Environment.SpecialFolder의 멤버가 아닙니다.
         *        System.PlatformNotSupportedException: 현재 플랫폼이 지원되지 않습니다.
         * RETURN 성공시 파일 경로/파일명.확장자명, 실패시 null
         */
        string OpenFile(string filter = "All Files|*.*", string initialDir = null);
        /**
         * BRIEF  폴더 선택 팝업 호출용 함수
         * PARAM  filter: 파일 선택 필터 기능
         *        initialDir: 팝업 호출 시 초기 디렉토리
         * THROWS System.ArgumentException: folder는 System.Environment.SpecialFolder의 멤버가 아닙니다.
         *        System.PlatformNotSupportedException: 현재 플랫폼이 지원되지 않습니다.
         * RETURN 성공시 파일 경로, 실패시 null
         */
        string OpenFolder(string description = "폴더 선택", string initialDir = null);
    }

    public class FileDialogService : IFileDialogService
    {
        public string OpenFolder(string description = "폴더 선택", string initialDir = null)
        {
            using (var dlg = new WinForms.FolderBrowserDialog())
            {
                dlg.Description = description;
                dlg.ShowNewFolderButton = true;
                dlg.SelectedPath = !string.IsNullOrEmpty(initialDir)
                    ? initialDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                var result = dlg.ShowDialog(); // WPF의 UI 스레드는 STA이므로 OK
                return result == WinForms.DialogResult.OK ? dlg.SelectedPath : null;
            }
        }

        public string OpenFile(string filter = "All Files|*.*", string initialDir = null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true,
                InitialDirectory = initialDir ??
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            return dlg.ShowDialog().GetValueOrDefault() ? dlg.FileName : null;
        }
    }
}
