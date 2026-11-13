using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace HeadphoneHub;

public sealed record PcmFormat(int Rate, int Channels, int Bits, bool IsFloat);

public sealed class WebStreamer : IDisposable
{
    private const string SocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private sealed class Listener
    {
        public required TcpClient Tcp { get; init; }
        public required Channel<byte[]> Queue { get; init; }
    }

    private readonly object _gate = new();
    private readonly List<Listener> _listeners = new();
    private TcpListener? _server;
    private CancellationTokenSource? _cts;
    private PcmFormat? _format;
    private string? _themeJson;
    private volatile int _count;
    private float _volume = 1f;

    public event Action? ListenersChanged;

    public bool IsRunning => _server != null;
    public int Port { get; private set; }
    public int ListenerCount => _count;

    public float Volume
    {
        get => Volatile.Read(ref _volume);
        set => Volatile.Write(ref _volume, Math.Clamp(value, 0f, 1f));
    }

    public int Start(int port = 0)
    {
        if (_server != null) return Port;

        var server = new TcpListener(IPAddress.Any, port);
        server.Start();
        _server = server;
        Port = ((IPEndPoint)server.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(server, _cts.Token);
        return Port;
    }

    public void Stop()
    {
        var cts = _cts;
        var server = _server;
        _cts = null;
        _server = null;

        cts?.Cancel();
        try { server?.Stop(); } catch { }

        List<Listener> all;
        lock (_gate)
        {
            all = _listeners.ToList();
            _listeners.Clear();
            _count = 0;
        }
        foreach (var l in all) Close(l);
        ListenersChanged?.Invoke();
    }

    public void SetTheme(string json)
    {
        _themeJson = json;
        Broadcast(TextFrame(ThemeMessage(json)));
    }

    public void SetFormat(PcmFormat format)
    {
        _format = format;
        Broadcast(TextFrame(HeaderJson(format)));
    }

    public void ClearFormat()
    {
        _format = null;
        Broadcast(TextFrame("{\"idle\":true}"));
    }

    public void Push(byte[] buffer, int count)
    {
        var f = _format;
        if (f == null || _count == 0) return;

        int width = f.Bits / 8;
        int block = width * f.Channels;
        int frames = count / block;
        if (frames == 0) return;

        float volume = Volume;
        int size = frames * 4;
        int header = HeaderSize(size);
        var frame = new byte[header + size];
        WriteHeader(frame, 0x2, size);

        for (int i = 0; i < frames; i++)
        {
            int pos = i * block;
            float left = ReadSample(buffer, pos, f);
            float right = f.Channels > 1 ? ReadSample(buffer, pos + width, f) : left;
            WriteSample(frame, header + i * 4, left * volume);
            WriteSample(frame, header + i * 4 + 2, right * volume);
        }

        Broadcast(frame);
    }

    private static string ThemeMessage(string json) => "{\"theme\":" + json + "}";

    private static string HeaderJson(PcmFormat f) => $"{{\"rate\":{f.Rate},\"ch\":2}}";

    private static float ReadSample(byte[] b, int p, PcmFormat f) => f.Bits switch
    {
        32 when f.IsFloat => BitConverter.ToSingle(b, p),
        32 => BitConverter.ToInt32(b, p) / 2147483648f,
        24 => ((b[p] << 8) | (b[p + 1] << 16) | (b[p + 2] << 24)) / 2147483648f,
        16 => BitConverter.ToInt16(b, p) / 32768f,
        8 => (b[p] - 128) / 128f,
        _ => 0f
    };

    private static void WriteSample(byte[] dst, int p, float value)
    {
        int s = (int)Math.Round(Math.Clamp(value, -1f, 1f) * 32767f);
        dst[p] = (byte)s;
        dst[p + 1] = (byte)(s >> 8);
    }

    private static int HeaderSize(int length) => length < 126 ? 2 : length <= ushort.MaxValue ? 4 : 10;

    private static void WriteHeader(byte[] frame, byte opcode, int length)
    {
        frame[0] = (byte)(0x80 | opcode);
        if (length < 126)
        {
            frame[1] = (byte)length;
        }
        else if (length <= ushort.MaxValue)
        {
            frame[1] = 126;
            frame[2] = (byte)(length >> 8);
            frame[3] = (byte)length;
        }
        else
        {
            frame[1] = 127;
            for (int i = 0; i < 8; i++) frame[2 + i] = (byte)((long)length >> (56 - 8 * i));
        }
    }

    private static byte[] TextFrame(string text)
    {
        var body = Encoding.UTF8.GetBytes(text);
        int header = HeaderSize(body.Length);
        var frame = new byte[header + body.Length];
        WriteHeader(frame, 0x1, body.Length);
        body.CopyTo(frame, header);
        return frame;
    }

    private void Broadcast(byte[] frame)
    {
        Listener[] snapshot;
        lock (_gate) snapshot = _listeners.ToArray();
        foreach (var l in snapshot) l.Queue.Writer.TryWrite(frame);
    }

    private async Task AcceptLoop(TcpListener server, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await server.AcceptTcpClientAsync(ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch
            {
                await Task.Delay(50);
                continue;
            }
            _ = Task.Run(() => Serve(tcp, ct));
        }
    }

    private async Task Serve(TcpClient tcp, CancellationToken ct)
    {
        Listener? me = null;
        try
        {
            tcp.NoDelay = true;
            var stream = tcp.GetStream();

            var head = await ReadHead(stream, ct);
            if (head == null) return;

            var lines = head.Split("\r\n");
            var request = lines[0].Split(' ');
            if (request.Length < 2 || request[0] != "GET")
            {
                await Respond(stream, "405 Method Not Allowed", "text/plain", Array.Empty<byte>(), ct);
                return;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                int colon = line.IndexOf(':');
                if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }

            string path = request[1].Split('?')[0];

            if (path == "/stream" && headers.TryGetValue("Sec-WebSocket-Key", out var key))
            {
                var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + SocketGuid)));
                var reply = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {accept}\r\n\r\n");
                await stream.WriteAsync(reply, ct);

                me = new Listener
                {
                    Tcp = tcp,
                    Queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(48)
                    {
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = true
                    })
                };

                lock (_gate)
                {
                    _listeners.Add(me);
                    _count = _listeners.Count;
                }
                ListenersChanged?.Invoke();

                var theme = _themeJson;
                if (theme != null) me.Queue.Writer.TryWrite(TextFrame(ThemeMessage(theme)));
                var format = _format;
                if (format != null) me.Queue.Writer.TryWrite(TextFrame(HeaderJson(format)));

                await Task.WhenAny(Pump(me, stream, ct), Drain(stream, ct));
            }
            else if (path == "/")
            {
                await Respond(stream, "200 OK", "text/html; charset=utf-8", WebPage.Render(_themeJson), ct);
            }
            else if (path == "/favicon.ico")
            {
                await Respond(stream, "204 No Content", "image/x-icon", Array.Empty<byte>(), ct);
            }
            else
            {
                await Respond(stream, "404 Not Found", "text/plain", Array.Empty<byte>(), ct);
            }
        }
        catch { }
        finally
        {
            if (me != null) Remove(me);
            else { try { tcp.Close(); } catch { } }
        }
    }

    private static async Task Pump(Listener l, NetworkStream stream, CancellationToken ct)
    {
        try
        {
            await foreach (var frame in l.Queue.Reader.ReadAllAsync(ct))
                await stream.WriteAsync(frame, ct);
        }
        catch { }
    }

    private static async Task Drain(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[256];
        try
        {
            while (true)
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n == 0 || (buffer[0] & 0x0F) == 8) return;
            }
        }
        catch { }
    }

    private static async Task<string?> ReadHead(NetworkStream stream, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var buffer = new byte[8192];
        int length = 0;
        while (length < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token);
            if (n == 0) return null;
            length += n;

            var text = Encoding.ASCII.GetString(buffer, 0, length);
            int end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0) return text[..end];
        }
        return null;
    }

    private static async Task Respond(NetworkStream stream, string status, string type, byte[] body, CancellationToken ct)
    {
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head, ct);
        if (body.Length > 0) await stream.WriteAsync(body, ct);
    }

    private void Remove(Listener l)
    {
        bool removed;
        lock (_gate)
        {
            removed = _listeners.Remove(l);
            _count = _listeners.Count;
        }
        Close(l);
        if (removed) ListenersChanged?.Invoke();
    }

    private static void Close(Listener l)
    {
        l.Queue.Writer.TryComplete();
        try { l.Tcp.Close(); } catch { }
    }

    public void Dispose() => Stop();
}

public static class LocalNetwork
{
    private static readonly string[] VirtualMarkers =
    {
        "vEthernet", "VMware", "VirtualBox", "Hyper-V", "TAP", "Wintun", "TUN", "VPN",
        "Tailscale", "ZeroTier", "Radmin", "Npcap", "WSL", "Docker", "Bluetooth"
    };

    public static IPAddress? FindAddress()
    {
        IPAddress? best = null;
        int bestScore = int.MinValue;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var props = nic.GetIPProperties();
            var address = props.UnicastAddresses
                .Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                     && !IPAddress.IsLoopback(a) && !IsLinkLocal(a));
            if (address == null) continue;

            int score = 0;
            if (props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                && !g.Address.Equals(IPAddress.Any))) score += 4;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet) score += 2;
            if (IsPrivate(address)) score += 2;

            var label = nic.Name + " " + nic.Description;
            if (VirtualMarkers.Any(m => label.Contains(m, StringComparison.OrdinalIgnoreCase))) score -= 5;

            if (score > bestScore)
            {
                bestScore = score;
                best = address;
            }
        }
        return best;
    }

    private static bool IsLinkLocal(IPAddress a)
    {
        var b = a.GetAddressBytes();
        return b[0] == 169 && b[1] == 254;
    }

    private static bool IsPrivate(IPAddress a)
    {
        var b = a.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
