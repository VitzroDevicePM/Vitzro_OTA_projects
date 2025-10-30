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
    public class HostRow
    {
        public string IP { get; set; }
        public string Hostname { get; set; }
        public long? LatencyMs { get; set; } // Ping round-trip (ms), 실패 시 null
        public bool IsAlive { get; set; }   // 응답 여부
    }
}
