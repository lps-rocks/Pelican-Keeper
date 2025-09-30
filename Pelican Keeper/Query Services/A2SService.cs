using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Pelican_Keeper.Query_Services;

public class A2SService(string ip, int port) : ISendCommand, IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint? _endPoint;

    public Task Connect()
    {
        _udpClient = new UdpClient();
        _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _udpClient.Client.ReceiveTimeout = 3000;

        ConsoleExt.WriteLineWithPretext("Connected to A2S server at " + _endPoint);
        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string? command = null, string? regexPattern = null)
    {
        if (_udpClient == null || _endPoint == null)
            throw new InvalidOperationException("Call Connect() before sending commands.");
        
        var request = BuildA2SInfoPacket();
        await _udpClient.SendAsync(request, request.Length, _endPoint);
        ConsoleExt.WriteLineWithPretext("Sent A2S_INFO request");
        
        var first = await ReceiveWithTimeoutAsync(_udpClient, timeoutMs: 15000);
        if (first == null)
        {
            ConsoleExt.WriteLineWithPretext("Timed out waiting for server response.", ConsoleExt.OutputType.Error);
            return HelperClass.ServerPlayerCountDisplayCleanup(string.Empty);
        }

        if (Program.Config.Debug)
        {
            ConsoleExt.WriteLineWithPretext("Received response from A2S server (first packet).");
            DumpBytes(first);
        }

        // Response header at offset 4
        if (first.Length >= 5)
        {
            byte header = first[4];

            // 0x41 = 'A' = S2C_CHALLENGE
            if (header == 0x41 && first.Length >= 9)
            {
                // bytes 5 to 8 are the challenge
                int challenge = BitConverter.ToInt32(first, 5);
                if (Program.Config.Debug)
                    ConsoleExt.WriteLineWithPretext($"Received challenge: 0x{challenge:X8}");
                
                var challenged = BuildA2SInfoPacket(challenge);
                await _udpClient.SendAsync(challenged, challenged.Length, _endPoint);
                ConsoleExt.WriteLineWithPretext("Sent A2S_INFO request with challenge");
                
                var second = await ReceiveWithTimeoutAsync(_udpClient, timeoutMs: 15000);
                if (second == null)
                {
                    ConsoleExt.WriteLineWithPretext("Timed out waiting for challenged info response.", ConsoleExt.OutputType.Error);
                    return HelperClass.ServerPlayerCountDisplayCleanup(string.Empty);
                }

                if (Program.Config.Debug)
                {
                    ConsoleExt.WriteLineWithPretext("Received challenged info response.");
                    DumpBytes(second);
                }

                return ParseOrFail(second);
            }
            // 0x49 = 'I' = S2A_INFO (immediate info response, no challenge)
            if (header == 0x49)
            {
                return ParseOrFail(first);
            }

            // Some servers may reply multi-packet (0xFE) or other types, but I will treat them as unsupported for now
            if (Program.Config.Debug)
                ConsoleExt.WriteLineWithPretext($"Unexpected response header: 0x{header:X2}", ConsoleExt.OutputType.Warning);
        }

        ConsoleExt.WriteLineWithPretext("Invalid or unexpected response.", ConsoleExt.OutputType.Error);
        return HelperClass.ServerPlayerCountDisplayCleanup(string.Empty);
    }

    private static async Task<byte[]?> ReceiveWithTimeoutAsync(UdpClient udp, int timeoutMs)
    {
        try
        {
            var receiveTask = udp.ReceiveAsync();
            var t = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs));
            if (t != receiveTask) return null;

            var result = receiveTask.Result;
            return result.Buffer;
        }
        catch (SocketException ex)
        {
            ConsoleExt.WriteLineWithPretext("No response from server.", ConsoleExt.OutputType.Error, ex);
            return null;
        }
    }

    private static string ParseOrFail(byte[] buffer)
    {
        string parseResult = ParseA2SInfoResponse(buffer);

        if (string.IsNullOrEmpty(parseResult))
        {
            ConsoleExt.WriteLineWithPretext("Failed to parse response.", ConsoleExt.OutputType.Error);
            return "Failed to parse response.";
        }

        if (Program.Config.Debug)
            ConsoleExt.WriteLineWithPretext($"A2S request response: {parseResult}");

        return parseResult;
    }

    /// <summary>
    /// Builds the A2S info packet with an optional challenge response.
    /// </summary>
    /// <param name="challenge">The Solved Challenge</param>
    /// <returns>Built A2S info Packet</returns>
    private static byte[] BuildA2SInfoPacket(int? challenge = null)
    {
        var head = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, (byte)'T' };
        var query = "Source Engine Query\0"u8.ToArray();

        if (challenge.HasValue)
        {
            var chall = BitConverter.GetBytes(challenge.Value); // little-endian
            return head.Concat(query).Concat(chall).ToArray();
        }

        return head.Concat(query).ToArray();
    }

    /// <summary>
    /// Parses the A2S response.
    /// </summary>
    /// <param name="buffer">The Response</param>
    /// <returns>"players/maxPlayers" or empty on failure.</returns>
    private static string ParseA2SInfoResponse(byte[] buffer)
    {
        // Need at least header + 1 type byte
        if (buffer.Length < 5) return string.Empty;

        int index = 4; // Skips the initial 4 bytes (0xFF 0xFF 0xFF 0xFF)
        byte header = buffer[index++];
        if (header != 0x49) // 'I'
        {
            ConsoleExt.WriteLineWithPretext($"Invalid response (expected 0x49, got 0x{header:X2}).", ConsoleExt.OutputType.Error);
            return string.Empty;
        }

        if (index >= buffer.Length) return string.Empty;

        byte protocol = buffer[index++];

        string name = ReadNullTerminatedString(buffer, ref index);
        string map = ReadNullTerminatedString(buffer, ref index);
        string folder = ReadNullTerminatedString(buffer, ref index);
        string game = ReadNullTerminatedString(buffer, ref index);

        if (index + 2 > buffer.Length) return string.Empty;
        short appId = BitConverter.ToInt16(buffer, index); index += 2;

        if (index + 3 > buffer.Length) return string.Empty;
        byte players = buffer[index++];
        byte maxPlayers = buffer[index++];
        byte bots = buffer[index];

        if (Program.Config.Debug)
        {
            ConsoleExt.WriteLineWithPretext("A2S Info Response:");
            ConsoleExt.WriteLineWithPretext($"Protocol: {protocol}");
            ConsoleExt.WriteLineWithPretext($"App ID: {appId}");
            ConsoleExt.WriteLineWithPretext($"Folder: {folder}");

            ConsoleExt.WriteLineWithPretext("Server Name: " + name);
            ConsoleExt.WriteLineWithPretext("Map: " + map);
            ConsoleExt.WriteLineWithPretext("Game: " + game);
            ConsoleExt.WriteLineWithPretext($"Players: {players}/{maxPlayers}");
            ConsoleExt.WriteLineWithPretext("Bots: " + bots);
        }

        return $"{players}/{maxPlayers}";
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient = null;
        _endPoint = null;
    }

    private static string ReadNullTerminatedString(byte[] buffer, ref int index)
    {
        int start = index;
        while (index < buffer.Length && buffer[index] != 0)
            index++;
        string result = Encoding.UTF8.GetString(buffer, start, index - start);
        if (index < buffer.Length) index++; // Skip null byte safely
        return result;
    }

    private static void DumpBytes(byte[] data)
    {
        Console.WriteLine("[Hex Dump]");
        for (int i = 0; i < data.Length; i += 16)
        {
            Console.Write($"{i:X4}: ");
            for (int j = 0; j < 16 && i + j < data.Length; j++)
                Console.Write($"{data[i + j]:X2} ");
            Console.Write(" | ");
            for (int j = 0; j < 16 && i + j < data.Length; j++)
            {
                char c = (char)data[i + j];
                Console.Write(char.IsControl(c) ? '.' : c);
            }
            Console.WriteLine();
        }
    }
}