using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Vitzro_OTA_projects.Model.Object;
/**
 * File: FTPFileUtil.cs
 * Description: FTP를 사용하기위한 유틸 모델
 * Author: 유동주
 * Date: 2025-11-18
 * LastUpdateDate: 2025-11-18
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model
{
    internal class FtpFileUtil
    {
        /**
         * \BRIEF  경로와 파일 명 사이에 구분을 넣어주는 함수
         * \PARAM  parent: 이전 파일 경로
         * \PARAM  name: 파일 명
         * \THROWS 
         * \RETURN string 파일 경로/파일 명
         */
        public static string CombineFtpPath(string parent, string name)
        {
            return string.IsNullOrEmpty(parent) || parent == "/" ? "/" + name.Trim('/') : parent.TrimEnd('/') + "/" + name.Trim('/');
        }

        /**
         * \BRIEF  FTP 경로 내 모든 파일을 경로를 찾는 함수
         * \PARAM  remoteDir: FTP 디렉토리 경로
         * \PARAM  FtpServerIp: FTP IP
         * \PARAM  FtpUser: FTP 유저 아이디
         * \PARAM  FtpPassword: FTP 유저 페스워드
         * \PARAM  token: 비동기 처리 문제 해결용 토큰
         * \THROWS 
         * \RETURN List<FtpEntry> 파일 경로 개체 리스트
         */
        public static async Task<List<FtpEntry>> ListDirectoryDetailsAsync(string remoteDir, string FtpServerIp, string FtpUser, string FtpPassword, CancellationToken token)
        {
            var uri = new Uri($"ftp://{FtpServerIp}{remoteDir}");

            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;

            // FTPS 필요 시 true
            request.EnableSsl = false;

            var result = new List<FtpEntry>();

            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true))
            {
                while (!reader.EndOfStream)
                {
                    token.ThrowIfCancellationRequested();

                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var entry = ParseFtpListLine(line, remoteDir);
                    if (entry != null)
                        result.Add(entry);
                }
            }

            return result;
        }

        /**
         * \BRIEF  FTP 조회 구문을 통하여 경로 개체를 반환하는 함수
         * \PARAM  line: 현 경로
         * \PARAM  parentPath: 상위 경로
         * \THROWS 
         * \RETURN 성공시 FtpEntry 파일 경로 개체, 실패시 null
         */
        public static FtpEntry ParseFtpListLine(string line, string parentPath)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var trimmed = line.Trim();
            var tokens = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return null;

            if (tokens[0].Length >= 10 &&
                (tokens[0][0] == 'd' || tokens[0][0] == '-' || tokens[0][0] == 'l'))
            {
                bool isDir = tokens[0][0] == 'd';

                if (tokens.Length >= 9)
                {
                    string name = string.Join(" ", tokens.Skip(8));
                    if (string.IsNullOrWhiteSpace(name))
                        return null;

                    return new FtpEntry
                    {
                        Name = name,
                        FullPath = CombineFtpPath(parentPath, name),
                        IsDirectory = isDir
                    };
                }
            }

            if (tokens.Length >= 4 &&
                tokens[0].Length == 8 &&
                tokens[0][2] == '-' && tokens[0][5] == '-')
            {
                bool isDir = string.Equals(tokens[2], "<DIR>", StringComparison.OrdinalIgnoreCase);
                int nameIndex = 3;
                string name = string.Join(" ", tokens.Skip(nameIndex));

                if (string.IsNullOrWhiteSpace(name))
                    return null;

                return new FtpEntry
                {
                    Name = name,
                    FullPath = CombineFtpPath(parentPath, name),
                    IsDirectory = isDir
                };
            }

            // 그 외 포맷은 필요 시 추가 처리
            return null;
        }

        /**
         * \BRIEF  재귀로 경로 개체를 수집하는 함수
         * \PARAM  remoteDir: FTP 디렉토리 경로
         * \PARAM  FtpServerIp: FTP IP
         * \PARAM  FtpUser: FTP 유저 아이디
         * \PARAM  FtpPassword: FTP 유저 페스워드
         * \PARAM  token: 비동기 처리 문제 해결용 토큰
         * \THROWS 
         * \RETURN List<FtpEntry> 파일 경로 개체 리스트
         */
        public static async Task<List<FtpEntry>> GetAllFilesRecursiveAsync(string remoteDir, string FtpServerIp, string FtpUser, string FtpPassword, CancellationToken token)
        {
            var result = new List<FtpEntry>();
            var entries = await ListDirectoryDetailsAsync(remoteDir, FtpServerIp, FtpUser, FtpPassword, token);

            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    // 현재/상위 디렉터리 표시는 스킵
                    if (entry.Name == "." || entry.Name == "..")
                        continue;

                    // 하위 디렉터리 재귀 수집
                    var subFiles = await GetAllFilesRecursiveAsync(entry.FullPath, FtpServerIp, FtpUser, FtpPassword, token);
                    result.AddRange(subFiles);
                }
                else
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /**
          * \BRIEF  단일 파일을 다운로드 하는 함수
          * \PARAM  remotePath : FTP 파일 주소
          * \PARAM  localPath : 다운로드 파일 경로
          * \PARAM  progress : 프로세스 전달 함수
          * \PARAM  FtpServerIp: FTP IP
          * \PARAM  FtpUser: FTP 유저 아이디
          * \PARAM  FtpPassword: FTP 유저 페스워드
          * \PARAM  token : 비동기 처리 문제 해결용 토큰
          * \THROWS 
          * \RETURN void
          */
        public static async Task DownloadFtpFileAsync(string remotePath, string localPath, IProgress<int> progress, string FtpServerIp, string FtpUser, string FtpPassword, CancellationToken token)
        {
            var uri = new Uri($"ftp://{FtpServerIp}{remotePath}");

            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.DownloadFile;
            request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
            request.UsePassive = true;
            request.UseBinary = true;
            request.KeepAlive = false;

            request.EnableSsl = false;   // AUTH TLS 사용

            using (var response = (FtpWebResponse)await request.GetResponseAsync())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = new FileStream(
                localPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 8192,
                useAsync: true))
            {
                var buffer = new byte[8192];
                long totalRead = 0;
                long totalBytes = response.ContentLength; // 모르면 -1

                int lastPercent = -1;

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    int read = await responseStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read <= 0)
                        break;

                    await fileStream.WriteAsync(buffer, 0, read, token);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        int percent = (int)(totalRead * 100 / totalBytes);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            progress?.Report(percent);
                        }
                    }
                }

                progress?.Report(100);
            }
        }

    }
}
