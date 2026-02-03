using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RTSP_Translation_Gremm
{
    public static class RtspProbe
    {
        public static async Task<bool> IsRtspAliveAsync(string rtspUrl, int timeoutMs)
        {
            Uri uri;
            if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out uri))
                return false;

            if (!string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
                return false;

            string host = uri.Host;
            int port = uri.Port > 0 ? uri.Port : 554;

            var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(host, port);
                    using (NetworkStream stream = client.GetStream())
                    {
                        string req =
                            "OPTIONS " + rtspUrl + " RTSP/1.0\r\n" +
                            "CSeq: 1\r\n" +
                            "User-Agent: RtspLauncher\r\n\r\n";

                        byte[] bytes = Encoding.ASCII.GetBytes(req);
                        await stream.WriteAsync(bytes, 0, bytes.Length, cts.Token);

                        byte[] buf = new byte[2048];
                        int read = await stream.ReadAsync(buf, 0, buf.Length, cts.Token);
                        if (read <= 0) return false;

                        string resp = Encoding.ASCII.GetString(buf, 0, read);
                        return resp.IndexOf("RTSP/1.0 200", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}