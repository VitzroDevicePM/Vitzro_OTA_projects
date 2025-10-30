using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Vitzro_OTA_projects.Model.Object;
/**
 * File: NetworkScannerService.cs
 * Description: 연결된 네트워크의 리스트를 출력하기위한 모델
 * Author: 유동주
 * Date: 2025-10-28
 * LastUpdateDate: 2025-10-28
 * Detail: 최초 생성
 */
namespace Vitzro_OTA_projects.Model
{
    public interface INetworkScannerService
    {
        /**
         * \BRIEF 동기형 로그 기록 함수
         * \PARAM folderPath 파일 선택 필터 기능
         * \PARAM message    팝업 호출 시 초기 디렉토리
         * \THROWS System.ArgumentOutOfRangeException 값이 0보다 작은 경우
         * \THROWS System.ArgumentNullException      source 또는 predicate가 null인 경우
         * \RETURN 성공 시 network/prefix, 실패 시 고정값 '192.168.0.0/24'
         */
        string AutoDetectSubnetCidr();

        /**
         * \BRIEF 비동기형 로그 기록 함수 
         * \PARAM folderPath: 파일 선택 필터 기능
         * \PARAM message: 팝업 호출 시 초기 디렉토리
         * \THROWS System.ArgumentOutOfRangeException: 값이 0보다 작은 경우
         * \THROWS System.ArgumentNullException: source 또는 predicate가 null인 경우
         * \THROWS ArgumentException: CIDR 형식이 올바르지 않은 경우
         * \RETURN Adrres List
         */
        Task<IList<HostRow>> ScanAsync(string cidr, int timeoutMs, int maxConcurrency,
                                       IProgress<(int done, int total)> progress,
                                       CancellationToken token);
    }
    public class NetworkScannerService : INetworkScannerService
    {
        public string AutoDetectSubnetCidr()
        {
            // 기본 게이트웨이가 있는 Up NIC 우선 선택
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up
                                     && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                     && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel))
            {
                var ipProps = nic.GetIPProperties();
                if (ipProps.GatewayAddresses == null || ipProps.GatewayAddresses.Count == 0) continue;

                var uni4 = ipProps.UnicastAddresses
                                  .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                if (uni4 == null) continue;
                if (uni4.IPv4Mask == null) continue;

                var prefix = MaskToPrefixLength(uni4.IPv4Mask);
                var network = GetNetworkAddress(uni4.Address, uni4.IPv4Mask);
                return $"{network}/{prefix}";
            }
            // 실패 시 /24로 기본값
            return "192.168.0.0/24";
        }

        public async Task<IList<HostRow>> ScanAsync(string cidr, int timeoutMs, int maxConcurrency, IProgress<(int done, int total)> progress, CancellationToken token)
        {
            var (network, prefix) = ParseCidr(cidr);
            var hosts = EnumerateHosts(network, prefix).ToList();
            int total = hosts.Count, done = 0;

            var results = new List<HostRow>(total);
            var sem = new SemaphoreSlim(maxConcurrency > 0 ? maxConcurrency : 128);

            var tasks = hosts.Select(async ip =>
            {
                await sem.WaitAsync(token);
                try
                {
                    var row = await ProbeAsync(ip, timeoutMs, token);
                    lock (results) results.Add(row);
                }
                finally
                {
                    var curr = Interlocked.Increment(ref done);
                    progress?.Report((curr, total));
                    sem.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
            // IP 정렬
            return results.OrderBy(r => IPToUInt(IPAddress.Parse(r.IP))).ToList();
        }

        // === 내부 유틸 ===
        /**
         * \BRIEF  IP와 서브넷 마스크를 분리하여 리턴하는 함수
         * \PARAM  cidr: IP/서브넷 주소
         * \THROWS System.ArgumentException: CIDR 형식이 올바르지 않은 경우
         * \RETURN (IP, 서브넷마스크)
         */
        private static (IPAddress network, int prefix) ParseCidr(string cidr)
        {
            // 허용: "192.168.0.0/24" 또는 "192.168.0.15/24"
            var parts = cidr.Split('/');
            if (parts.Length != 2) throw new ArgumentException("CIDR 형식이 올바르지 않습니다. 예: 192.168.0.0/24");
            var ip = IPAddress.Parse(parts[0]);
            int prefix = int.Parse(parts[1]);
            var mask = PrefixToMask(prefix);
            var net = GetNetworkAddress(ip, mask);
            return (net, prefix);
        }

        /**
         * \BRIEF  IP와 서브넷 마스크합쳐 IPAddress Class 형태로 리턴하는 함수
         * \PARAM  network: IP/서브넷 주소
         * \PARAM  prefix: IP/서브넷 주소
         * \THROWS System.ArgumentOutOfRangeException: 값이 0보다 작은 경우
         * \RETURN IPAddress 클레스
         */
        private static IEnumerable<IPAddress> EnumerateHosts(IPAddress network, int prefix)
        {
            if (prefix < 0 || prefix > 32) throw new ArgumentOutOfRangeException(nameof(prefix));
            var mask = PrefixToMask(prefix);

            uint net = IPToUInt(GetNetworkAddress(network, mask));
            uint bcast = net | ~IPToUInt(mask);

            // /31,/32 처리는 호스트 없음 처리
            if (prefix >= 31) yield break;

            for (uint u = net + 1; u < bcast; u++)
                yield return UIntToIP(u);
        }

        /**
         * \BRIEF  병렬로 View에서 출력을 위해 사용한 HostRow 클레스 변환 하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  timeoutMs: Ping 응답 시간
         * \PARAM  token: 병렬 처리를 위해 사용되는 토큰 값
         * \THROWS 
         * \RETURN HostRow 클레스(IP, 디바이스 이름, ping 응답 시간, 활성화 여부)
         */
        private static async Task<HostRow> ProbeAsync(IPAddress ip, int timeoutMs, CancellationToken token)
        {
            var ping = new Ping();
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var reply = await ping.SendPingAsync(ip, timeoutMs);
                sw.Stop();

                if (reply.Status == IPStatus.Success)
                {
                    string hostname = null;
                    try
                    {
                        // 역방향 DNS (느릴 수 있으니 필요 시 주석처리)
                        var entry = await Dns.GetHostEntryAsync(ip);
                        hostname = entry?.HostName;
                    }
                    catch { /* DNS 실패 무시 */ }

                    return new HostRow
                    {
                        IP = ip.ToString(),
                        Hostname = hostname,
                        LatencyMs = sw.ElapsedMilliseconds,
                        IsAlive = true
                    };
                }
            }
            catch { /* ping 예외 무시 */ }

            return new HostRow
            {
                IP = ip.ToString(),
                Hostname = null,
                LatencyMs = null,
                IsAlive = false
            };
        }

        /**
         * \BRIEF  IP와 서브넷 마스크 정보를 합쳐 IPAddress로 출력하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  mask: 서브넷 마스크 정보
         * \THROWS 
         * \RETURN IPAddress 클레스(Ip v4, v6 등 정보 저장 개체)
         */
        private static IPAddress GetNetworkAddress(IPAddress ip, IPAddress mask)
        {
            uint uip = IPToUInt(ip);
            uint umask = IPToUInt(mask);
            return UIntToIP(uip & umask);
        }

        /**
         * \BRIEF  IP와 서브넷 마스크 정보를 합쳐 IPAddress로 출력하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  mask: 서브넷 마스크 정보
         * \THROWS 
         * \RETURN Mask IPAddress 클레스
         */
        private static IPAddress PrefixToMask(int prefix)
        {
            uint mask = prefix == 0 ? 0u : 0xffffffffu << (32 - prefix);
            return UIntToIP(mask);
        }

        /**
         * \BRIEF  서브넷 마스크 정보를 Int형으로 출력하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  mask: 서브넷 마스크 정보
         * \THROWS 
         * \RETURN Mask 정보(0~32)
         */
        private static int MaskToPrefixLength(IPAddress mask)
        {
            uint m = IPToUInt(mask);
            int count = 0;
            for (int i = 31; i >= 0; i--)
                if (((m >> i) & 1) == 1) count++;
                else break;
            return count;
        }

        /**
         * \BRIEF  서브넷 마스크 정보를 Int형으로 출력하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  mask: 서브넷 마스크 정보
         * \THROWS 
         * \RETURN Mask 정보
         */
        private static uint IPToUInt(IPAddress ip)
        {
            var bytes = ip.GetAddressBytes(); // big-endian
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        /**
         * \BRIEF  Int형 데이터를 받아 IP 정보로 출력하는 함수
         * \PARAM  ip: 검사한 IPAddress 데이터
         * \PARAM  mask: 서브넷 마스크 정보
         * \THROWS 
         * \RETURN Ip 정보
         */
        private static IPAddress UIntToIP(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return new IPAddress(bytes);
        }
    }
}
