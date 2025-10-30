/**
 * File: HostRow.cs
 * Description: IP 주소 서칭 핸들링을 위한 오브젝트 클레스
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model.Object
{
    public class ProgressInfo
    {
        public int Done { get; set; }
        public int Total { get; set; }

        public ProgressInfo() { }
        public ProgressInfo(int done, int total)
        {
            Done = done; Total = total;
        }
    }
}
