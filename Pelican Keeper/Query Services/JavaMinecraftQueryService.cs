using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Pelican_Keeper.Query_Services;

public static class JavaMinecraftQueryService //TODO: This should inherit from the ISendCommand Interface
{
    /// <summary>
    /// Queries a Minecraft server (Java edition) for current/maximum players using the status protocol.
    /// </summary>
    /// <param name="host">Host IP</param>
    /// <param name="port">Port of the Server</param>
    /// <param name="timeoutMs">Timeout in Ms</param>
    /// <param name="protocolVersion">Protocol Version</param>
    /// <returns>String in the format of Players/MaxPlayers</returns>
    /// <exception cref="InvalidDataException">An unexpected packet ID was returned by the Server</exception>
    public static async Task<string> GetPlayerCountsAsync(string host, int port = 25565, int timeoutMs = 5000, int protocolVersion = 760)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cts.Token);
            await using var stream = client.GetStream();

            using (var ms = new MemoryStream())
            {
                WriteVarInt(ms, 0x00); // packet id
                WriteVarInt(ms, protocolVersion); // protocol version (ignored for status, but still has to be included)
                WriteString(ms, host); // server address
                WriteUShort(ms, (ushort)port); // server port
                WriteVarInt(ms, 0x01); // next state = status
                await SendFramedAsync(stream, ms.ToArray(), cts.Token);
            }

            await SendFramedAsync(stream, [0x00], cts.Token);

            var payload = await ReadPacketAsync(stream, cts.Token);
            using var rms = new MemoryStream(payload);

            var packetId = ReadVarInt(rms);
            if (packetId != 0x00) throw new InvalidDataException($"Unexpected packet id {packetId}.");

            var jsonLen = ReadVarInt(rms);
            var jsonBuf = new byte[jsonLen];
            if (await rms.ReadAsync(jsonBuf, 0, jsonLen, cts.Token) != jsonLen) throw new EndOfStreamException();

            var json = Encoding.UTF8.GetString(jsonBuf);

            using var doc = JsonDocument.Parse(json);
            var players = doc.RootElement.GetProperty("players");
            int online = players.GetProperty("online").GetInt32();
            int max = players.GetProperty("max").GetInt32();

            return $"{online}/{max}";
        }
        catch (OperationCanceledException)
        {
            return "Timed out waiting for server response.";
        }
        catch (SocketException)
        {
            return "Could not connect to server.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static async Task SendFramedAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
    {
        // Each MC packet is framed as: [VarInt length][payload]
        using var ms = new MemoryStream();
        WriteVarInt(ms, payload.Length);
        ms.Write(payload, 0, payload.Length);
        var buf = ms.ToArray();
        await stream.WriteAsync(buf, ct);
        await stream.FlushAsync(ct);
    }

    private static void WriteVarInt(Stream s, int value)
    {
        uint u = (uint)value;
        while (true)
        {
            if ((u & ~0x7Fu) == 0) { s.WriteByte((byte)u); return; }
            s.WriteByte((byte)((u & 0x7F) | 0x80));
            u >>= 7;
        }
    }

    private static void WriteUShort(Stream s, ushort v)
    {
        Span<byte> tmp = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(tmp, v);
        s.Write(tmp);
    }

    private static void WriteString(Stream s, string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        WriteVarInt(s, bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }

    private static async Task<int> ReadVarIntAsync(NetworkStream stream, CancellationToken ct)
    {
        int numRead = 0;
        int result = 0;
        byte read;
        do
        {
            read = await ReadByteAsync(stream, ct);
            int value = (read & 0b0111_1111);
            result |= (value << (7 * numRead));
            numRead++;
            if (numRead > 5) throw new InvalidDataException("VarInt too big");
        } while ((read & 0b1000_0000) != 0);
        return result;
    }
    
    private static int ReadVarInt(Stream s)
    {
        int numRead = 0, result = 0, read;
        do
        {
            read = s.ReadByte();
            if (read == -1) throw new EndOfStreamException();
            int value = read & 0x7F;
            result |= value << (7 * numRead++);
            if (numRead > 5) throw new InvalidDataException("VarInt too big");
        } while ((read & 0x80) != 0);
        return result;
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int len, CancellationToken ct)
    {
        var buf = new byte[len];
        int off = 0;
        while (off < len)
        {
            int read = await stream.ReadAsync(buf, off, len - off, ct);
            if (read == 0) throw new EndOfStreamException();
            off += read;
        }
        return buf;
    }
    
    private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, CancellationToken ct)
    {
        var length  = await ReadVarIntAsync(stream, ct);
        return await ReadExactAsync(stream, length, ct);
    }

    private static async Task<byte> ReadByteAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[1];
        int read = await stream.ReadAsync(buffer, 0, 1, ct);
        if (read == 0) throw new EndOfStreamException();
        return buffer[0];
    }
}