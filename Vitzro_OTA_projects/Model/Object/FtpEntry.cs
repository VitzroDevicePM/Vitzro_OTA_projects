/**
 * File: FtpEntry.cs
 * Description: FTP 핸들링을 위한 오브젝트 클레스
 * Author: 유동주
 * Date: 2025-11-18
 * LastUpdateDate: 2025-11-18
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model.Object
{
    class FtpEntry
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
    }

}
