using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
/**
 * File: Logger.cs
 * Description: 로그를 기록하기위한 모델
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model
{
    public static class Logger
    {
        private static readonly object _sync = new object();

        /**
         * BRIEF  동기형 로그 기록 함수 
         * PARAM  folderPath: 파일 선택 필터 기능
         *        message: 팝업 호출 시 초기 디렉토리
         * THROWS System.UnauthorizedAccessException: 액세스가 거부되었습니다.
         *        System.ArgumentException: path가 비어 있는 경우 또는 path 시스템 장치 (com1, com2, 및 등)의 이름을 포함합니다.
         *        System.ArgumentNullException: path가 null인 경우
         *        System.IO.DirectoryNotFoundException: 지정된 경로가 잘못되었습니다(예: 매핑되지 않은 드라이브에 있음).
         *        System.IO.IOException: path 파일 이름, 디렉터리 이름 또는 볼륨 레이블 구문이 부정확 하거나 잘못 된 구문이 포함 되어 있습니다.
         *        System.IO.PathTooLongException: 지정된 경로, 파일 이름 또는 둘 다가 시스템에서 정의한 최대 길이를 초과합니다. 예를 들어 Windows 기반 플랫폼에서 경로는 248자를 초과할 수 없고 파일 이름은 260자를 초과할 수 없습니다.
         *        System.Security.SecurityException: 호출자에게 필요한 권한이 없는 경우
         * RETURN void
         */
        public static void AppendLine(string folderPath, string message)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("folderPath is null or empty.", "folderPath");

            Directory.CreateDirectory(folderPath); // 폴더 없으면 생성

            var fileName = DateTime.Now.ToString("yyyyMMdd") + ".log";
            var logPath = Path.Combine(folderPath, fileName);

            var line = string.Format("[{0}] {1}",
                DateTime.Now.ToString("HH:mm:ss.fff"), message ?? string.Empty);

            // UTF-8 (BOM 없음)로 append
            lock (_sync)
            {
                using (var sw = new StreamWriter(logPath, true, new UTF8Encoding(false)))
                {
                    sw.WriteLine(line);
                }
            }
        }

        /**
         * BRIEF  비동기형 로그 기록 함수 
         * PARAM  folderPath: 파일 선택 필터 기능
         *        message: 팝업 호출 시 초기 디렉토리
         * THROWS System.UnauthorizedAccessException: 액세스가 거부되었습니다.
         *        System.ArgumentException: path가 비어 있는 경우 또는 path 시스템 장치 (com1, com2, 및 등)의 이름을 포함합니다.
         *        System.ArgumentNullException: path가 null인 경우
         *        System.IO.DirectoryNotFoundException: 지정된 경로가 잘못되었습니다(예: 매핑되지 않은 드라이브에 있음).
         *        System.IO.IOException: path 파일 이름, 디렉터리 이름 또는 볼륨 레이블 구문이 부정확 하거나 잘못 된 구문이 포함 되어 있습니다.
         *        System.IO.PathTooLongException: 지정된 경로, 파일 이름 또는 둘 다가 시스템에서 정의한 최대 길이를 초과합니다. 예를 들어 Windows 기반 플랫폼에서 경로는 248자를 초과할 수 없고 파일 이름은 260자를 초과할 수 없습니다.
         *        System.Security.SecurityException: 호출자에게 필요한 권한이 없는 경우
         * RETURN void
         */
        public static async Task AppendLineAsync(string folderPath, string message, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("folderPath is null or empty.", "folderPath");

            Directory.CreateDirectory(folderPath);

            var fileName = DateTime.Now.ToString("yyyyMMdd") + ".log";
            var logPath = Path.Combine(folderPath, fileName);

            var line = string.Format("[{0}] {1}",
                DateTime.Now.ToString("HH:mm:ss.fff"), message ?? string.Empty);

            // 파일 공유 이슈 최소화 위해 lock
            lock (_sync)
            {
                using (var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    // token 체크
                    token.ThrowIfCancellationRequested();
                    sw.WriteLine(line);
                    sw.Flush();
                }
            }

            await Task.Yield(); // 형태상 async 유지
        }
    }
}
