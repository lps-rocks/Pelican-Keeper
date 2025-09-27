using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Pelican_Keeper.Query_Services;

public class BedrockMinecraftQueryService(string ip, int port) : ISendCommand, IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint? _endPoint;
    
    // RakNet "magic" bytes used in ping/pong
    private static readonly byte[] Magic =
    {
        0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE,
        0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78
    };

    public Task Connect()
    {
        try
        {
            _udpClient = new UdpClient();
            _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _udpClient.Client.ReceiveTimeout = 3000;
        }
        catch (SocketException ex)
        {
            ConsoleExt.WriteLineWithPretext($"Could not connect to server. {ip}:{port}", ConsoleExt.OutputType.Error, ex);
        }
        
        ConsoleExt.WriteLineWithPretext("Connected to Bedrock Minecraft server at " + _endPoint);
        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string? command = null, string? regexPattern = null)
    {
        if (_udpClient == null || _endPoint == null)
            throw new InvalidOperationException("Call Connect() before sending commands.");
        
        using var cts = new CancellationTokenSource(_udpClient.Client.ReceiveTimeout);

        // Build Unconnected Ping
        // Structure: [0x01][8B time][16B magic][8B client GUID]
        var packet = new byte[1 + 8 + 16 + 8];
        packet[0] = 0x01; // ID_UNCONNECTED_PING
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(1), nowMs);
        Magic.CopyTo(packet, 1 + 8);
        // client GUID can be anything; use random
        var guid = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        BinaryPrimitives.WriteInt64BigEndian(packet.AsSpan(1 + 8 + 16), guid);

        await _udpClient.SendAsync(packet, packet.Length, _endPoint);

        UdpReceiveResult resp;
        try
        {
            var recvTask = _udpClient.ReceiveAsync();
            var completed = await Task.WhenAny(recvTask, Task.Delay(_udpClient.Client.ReceiveTimeout, cts.Token));
            if (completed != recvTask) return "Timed out waiting for server response.";
            resp = recvTask.Result;
        }
        catch (SocketException)
        {
            return "Timed out waiting for server response.";
        }

        var buf = resp.Buffer;
        if (buf.Length < 1 + 8 + 8 + 16 + 2) return "Error: response too short";
        if (buf[0] != 0x1C) return $"Error: unexpected packet id 0x{buf[0]:X2}"; // ID_UNCONNECTED_PONG

        int offset = 1;
        // 8B time
        offset += 8;
        // 8B server GUID
        offset += 8;

        // 16B magic
        if (!buf.AsSpan(offset, 16).SequenceEqual(Magic))
            return "Error: bad magic";
        offset += 16;

        // Remaining is usually a length-prefixed MOTD string (UTF-8).
        // Try to read a 2-byte length (BE or LE). If it doesn't match, treat the rest as raw string.
        int remaining = buf.Length - offset;
        string motdString;

        if (remaining >= 2)
        {
            ushort beLen = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(offset, 2));
            ushort leLen = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(offset, 2));

            if (beLen > 0 && beLen <= remaining - 2)
            {
                motdString = Encoding.UTF8.GetString(buf, offset + 2, beLen);
            }
            else if (leLen > 0 && leLen <= remaining - 2)
            {
                motdString = Encoding.UTF8.GetString(buf, offset + 2, leLen);
            }
            else
            {
                motdString = Encoding.UTF8.GetString(buf, offset, remaining);
            }
        }
        else
        {
            motdString = Encoding.UTF8.GetString(buf, offset, remaining);
        }

        // Expected format (semicolon-separated), e.g.;
        // "MCPE;MOTD;Protocol;Version;Online;Max;ServerId;LevelName;GameMode;GameModeNum;PortV4;PortV6"
        var parts = motdString.Split(';');
        if (parts.Length < 6 || !string.Equals(parts[0], "MCPE", StringComparison.OrdinalIgnoreCase))
            return "Error: invalid Bedrock pong";

        // parts[4] = online, parts[5] = max
        if (!int.TryParse(parts[4], out var online)) online = 0;
        if (!int.TryParse(parts[5], out var max)) max = 0;

        return $"{online}/{max}";
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient = null;
        _endPoint = null;
    }
}