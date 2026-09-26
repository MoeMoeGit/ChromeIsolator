using System.Net;
using System.Net.Sockets;

namespace ChromeIsolator.Services;

public static class PortAllocator
{
    public static int FindAvailablePort(int preferred, int attempts = 10, IEnumerable<int>? reserved = null)
    {
        for (var offset = 0; offset < attempts; offset++)
        {
            var port = preferred + offset;
            if (port is > 0 and <= 65535 && !(reserved?.Contains(port) ?? false) && IsAvailable(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException($"无法找到可用端口（尝试范围：{preferred}-{preferred + attempts - 1}）");
    }

    private static bool IsAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
