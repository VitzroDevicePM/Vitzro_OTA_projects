/**
 * File: CopyProgress.cs
 * Description: 파일 복사 핸들링을 위한 오브젝트 클레스
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model.Object
{
    public class CopyProgress
    {
        public int Done { get; set; }
        public int Total { get; set; }
        public int Percent { get; set; }
        public string CurrentName { get; set; }
    }
}
