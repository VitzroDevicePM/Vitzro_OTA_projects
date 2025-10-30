using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Vitzro_OTA_projects.Model;
using Vitzro_OTA_projects.Model.Object;

/**
 * File: MainViewModel.cs
 * Description: 메인 화면에 바인딩될 데이터를 담당하는 뷰모델
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        /**
         * BRIEF  명령을 현재 상태에서 실행할 수 있는지를 결정하는 함수
         * PARAM  source: 복사 대상 위치 경로
         *        dest: 복사 위치 경로
         *        progress: 복사 중 프로그레시브 바 변수
         *        token: 병렬 작업 지원용 변수
         * THROWS 
         * RETURN void
         */
        public static void CopyDirectoryWithProgress(string source, string dest, IProgress<CopyProgress> progress, CancellationToken token)
        {
            // 전체 파일 목록 수집 (진행률 총량 계산)
            var allFiles = System.IO.Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            var total = allFiles.Length;

            // 대상 루트 생성
            System.IO.Directory.CreateDirectory(dest);

            // 먼저 모든 하위 폴더 생성
            var allDirs = System.IO.Directory.GetDirectories(source, "*", SearchOption.AllDirectories);
            foreach (var dir in allDirs)
            {
                token.ThrowIfCancellationRequested();
                var rel = dir.Substring(source.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                var targetDir = System.IO.Path.Combine(dest, rel);
                if (!System.IO.Directory.Exists(targetDir))
                    System.IO.Directory.CreateDirectory(targetDir);
            }

            // 파일 복사 + 진행률 보고
            int done = 0;
            foreach (var file in allFiles)
            {
                token.ThrowIfCancellationRequested();

                var rel = file.Substring(source.Length).TrimStart(System.IO.Path.DirectorySeparatorChar);
                var targetFile = System.IO.Path.Combine(dest, rel);
                var targetDir = System.IO.Path.GetDirectoryName(targetFile);
                if (!System.IO.Directory.Exists(targetDir))
                    System.IO.Directory.CreateDirectory(targetDir);

                System.IO.File.Copy(file, targetFile, true); // overwrite
                Logger.AppendLine(@"C:\Test", "파일 복사: " + Path.GetFileName(file));

                done++;
                if (progress != null)
                {
                    int percent = (total == 0) ? 100 : (int)(done * 100.0 / total);
                    progress.Report(new CopyProgress
                    {
                        Done = done,
                        Total = total,
                        Percent = percent,
                        CurrentName = System.IO.Path.GetFileName(file)
                    });
                }
            }

            // 빈 폴더만 있는 경우도 100% 보고
            if (total == 0 && progress != null)
                progress.Report(new CopyProgress { Done = 0, Total = 0, Percent = 100, CurrentName = "" });
        }

        private readonly IFileDialogService _dialog;
        private CancellationTokenSource _cts;

        private readonly INetworkScannerService _scanner;
        private CancellationTokenSource _scanCts;

        public MainWindowViewModel(IFileDialogService dialog, INetworkScannerService scanner)
        {
            _dialog = dialog;
            _scanner = scanner;

            // === 기존 커맨드들 유지 ===
            SelectFileCommand = new RelayCommand(_ => SelectFile());
            UploadCommand = new RelayCommand(async _ => await UploadAsync(), _ => !IsBusy);
            CancelCommand = new RelayCommand(_ => { if (_scanCts != null) { _scanCts.Cancel(); } else if (_cts != null) { _cts.Cancel(); } }, _ => IsBusy);

            // === 신규 스캔 커맨드 ===
            DetectSubnetCommand = new RelayCommand(_ => SubnetCidr = _scanner.AutoDetectSubnetCidr(), _ => !IsBusy);
            ScanSubnetCommand = new RelayCommand(async _ => await ScanSubnetAsync(), _ => !IsBusy);

            Hosts = new ObservableCollection<HostRow>();
            SubnetCidr = _scanner.AutoDetectSubnetCidr();
        }

        // ===== 바인딩 속성 =====
        private string _filePath;
        public string FilePath { get => _filePath; set { _filePath = value; OnPropertyChanged(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); RaiseCommandCanExecuteChanged(); } }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate { get => _isIndeterminate; set { _isIndeterminate = value; OnPropertyChanged(); } }

        private int _progressValue;
        public int ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); } }

        private string _busyText = "처리 중…";
        public string BusyText { get => _busyText; set { _busyText = value; OnPropertyChanged(); } }

        // ===== 커맨드 =====
        public ICommand SelectFileCommand { get; }
        public ICommand UploadCommand { get; }
        public ICommand CancelCommand { get; }

        /**
         * BRIEF  폴더 선택 호출 이벤트 처리 함수
         * PARAM  
         * THROWS 
         * RETURN void
         */
        private void SelectFile()
        {
            //var selected = _dialog.OpenFile("All Files|*.*");
            var selected = _dialog.OpenFolder("폴더 선택", null);
            if (!string.IsNullOrEmpty(selected))
            {
                FilePath = selected;
                Logger.AppendLine(@"C:\Test", "사용자가 폴더를 선택했습니다.");
            }
        }

        /**
         * BRIEF  파일 복사 이벤트 처리 함수
         * PARAM  
         * THROWS 
         * RETURN void
         */
        private async Task UploadAsync()
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            if (FilePath == "" || FilePath is null)
            {
                System.Windows.MessageBox.Show("경로를 선택 해주십시오", "Err");

                return;
            }

            /** TODO(유동주)
             *  지금은 복사 기능이지만 펌웨어 업데이트 하는 클레스 제작해서 처리 필요
             *  공통성을 위해서 CMD 커맨드 명령어 형태 필요할듯?
             *  CMD 실행용 기초 셋업도 체크하는 방안 필요
             */
            try
            {
                IsBusy = true;

                IsIndeterminate = false;
                BusyText = "복사 준비 중…";
                ProgressValue = 0;

                var source = FilePath;
                var destRoot = @"C:\Test";
                if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
                    throw new InvalidOperationException("유효한 폴더가 아닙니다.");

                Directory.CreateDirectory(destRoot);

                var dest = System.IO.Path.Combine(destRoot, new DirectoryInfo(source).Name);

                var srcFull = System.IO.Path.GetFullPath(source).TrimEnd('\\');
                var dstFull = System.IO.Path.GetFullPath(dest).TrimEnd('\\');
                if (string.Equals(srcFull, dstFull, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("소스와 대상 경로가 같습니다.");

                var progress = new Progress<CopyProgress>(p =>
                {
                    ProgressValue = p.Percent;
                    BusyText = $"복사 중… {p.Done}/{p.Total}  ({p.CurrentName})";
                });

                await Task.Run(() => CopyDirectoryWithProgress(source, dest, progress, _cts.Token), _cts.Token);
                await Logger.AppendLineAsync(@"C:\Test", string.Format("복사 완료: '{0}' -> '{1}'", source, dest), _cts.Token);

                BusyText = "복사 완료!";
            }
            catch (OperationCanceledException)
            {
                BusyText = "사용자 취소됨";
            }
            catch (Exception ex)
            {
                BusyText = $"오류: {ex.Message}";
            }
            finally
            {
                await Task.Delay(300);
                IsBusy = false;
                ProgressValue = 0;
                IsIndeterminate = true;
            }
        }

        /**
         * BRIEF  복사중 UI 활성화를 처리를 위한 함수
         * PARAM  
         * THROWS 
         * RETURN void
         */
        private void RaiseCommandCanExecuteChanged()
        {
            (UploadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /**
         * BRIEF  MVVM 형식에서 ViewModel에서 변경된 데이터를 View에서 처리 위한 함수
         * PARAM  name: 변경 데이터 명
         * THROWS 
         * RETURN void
         */
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        // ===== 스캔 바인딩 속성 =====
        private string _subnetCidr;
        public string SubnetCidr { get => _subnetCidr; set { _subnetCidr = value; OnPropertyChanged(); } }

        private int _pingTimeoutMs = 250;
        public int PingTimeoutMs { get => _pingTimeoutMs; set { _pingTimeoutMs = value; OnPropertyChanged(); } }

        private int _maxConcurrency = 128;
        public int MaxConcurrency { get => _maxConcurrency; set { _maxConcurrency = value; OnPropertyChanged(); } }

        public ObservableCollection<HostRow> Hosts { get; }

        // ===== 커맨드 =====
        public ICommand DetectSubnetCommand { get; }
        public ICommand ScanSubnetCommand { get; }

        /**
         * BRIEF  네트워크 서칭 버튼 클릭 이벤트 처리용 함수
         * PARAM  
         * THROWS 
         * RETURN void
         */
        private async Task ScanSubnetAsync()
        {
            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();

            try
            {
                IsBusy = true;
                IsIndeterminate = false;
                ProgressValue = 0;
                BusyText = $"스캔 시작: {SubnetCidr}";

                Hosts.Clear();

                /** TODO(유동주)
                 *  스캔된 네트워크 데이터 연계용 체크박스 필요 HostRow에 추가하여 처리
                 */
                Progress<(int done, int total)> progress = new Progress<(int done, int total)>(p =>
                {
                    ProgressValue = p.total == 0 ? 0 : (int)(p.done * 100.0 / p.total);
                    BusyText = $"스캔 중… {p.done}/{p.total}";
                });

                IList<HostRow> rows = await _scanner.ScanAsync(SubnetCidr, PingTimeoutMs, MaxConcurrency, progress, _scanCts.Token);
                foreach (HostRow r in rows)
                {
                    Hosts.Add(r);
                }

                BusyText = $"완료: {rows.Count}개 호스트";
            }
            catch (OperationCanceledException)
            {
                BusyText = "사용자 취소됨";
            }
            catch (Exception ex)
            {
                BusyText = $"오류: {ex.Message}";
            }
            finally
            {
                await Task.Delay(200);
                IsBusy = false;
                IsIndeterminate = true;
                ProgressValue = 0;
            }
        }
    }
}
