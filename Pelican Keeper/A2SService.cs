using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Pelican_Keeper;

public class A2SService : ISendCommand
{
    private UdpClient? _udpClient;
    private IPEndPoint? _endPoint;

    public Task Connect(string ip, int port)
    {
        _udpClient = new UdpClient();
        _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _udpClient.Client.ReceiveTimeout = 3000;
        
        ConsoleExt.WriteLineWithPretext("Connected to A2S server at " + _endPoint);
        return Task.CompletedTask;
    }
    
    public async Task<string> SendCommandAsync(string command)
    {
        byte[] request = BuildA2SInfoPacket();
        
        if (_udpClient == null || _endPoint == null)
            throw new InvalidOperationException("Call Connect() before sending commands.");

        await _udpClient!.SendAsync(request, request.Length, _endPoint);
        ConsoleExt.WriteLineWithPretext("Sent response to A2S server");
        
        UdpReceiveResult result;
        try
        {
            var receiveTask = _udpClient.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(15000)) != receiveTask)
            {
                ConsoleExt.WriteLineWithPretext("Timed out waiting for server response.", ConsoleExt.OutputType.Error);
                return "Timeout or no response";
            }

            result = receiveTask.Result;
            if (Program.Config.Debug)
            {
                ConsoleExt.WriteLineWithPretext("Received response from A2S server.");
                DumpBytes(result.Buffer);
            }
        }
        catch (SocketException ex)
        {
            ConsoleExt.WriteLineWithPretext("No response from server.", ConsoleExt.OutputType.Error, ex);
            return "No response from server.";
        }
        
        string parseResult = ParseA2SInfoResponse(result.Buffer);
        
        if (parseResult == string.Empty)
        {
            ConsoleExt.WriteLineWithPretext("Failed to parse response.", ConsoleExt.OutputType.Error);
            return "Failed to parse response.";
        }
        
        return parseResult;
    }
    
    static byte[] BuildA2SInfoPacket()
    {
        return new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, (byte)'T' }.Concat("Source Engine Query\0"u8.ToArray()).ToArray();
    }

    static string ParseA2SInfoResponse(byte[] buffer)
    {
        int index = 4; // Skips the initial 4 bytes (0xFF 0xFF 0xFF 0xFF)
        byte header = buffer[index++];
        if (header != 0x49)
        {
            ConsoleExt.WriteLineWithPretext("Invalid response.", ConsoleExt.OutputType.Error);
            return string.Empty;
        }

        byte protocol = buffer[index++];
        string name = ReadNullTerminatedString(buffer, ref index);
        string map = ReadNullTerminatedString(buffer, ref index);
        string folder = ReadNullTerminatedString(buffer, ref index);
        string game = ReadNullTerminatedString(buffer, ref index);
        short appId = BitConverter.ToInt16(buffer, index); index += 2;
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

        return players.ToString();
    }
    
    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient = null;
        _endPoint = null;
    }

    static string ReadNullTerminatedString(byte[] buffer, ref int index)
    {
        int start = index;
        while (index < buffer.Length && buffer[index] != 0)
            index++;
        string result = Encoding.UTF8.GetString(buffer, start, index - start);
        index++; // Skip null byte
        return result;
    }
    
    static void DumpBytes(byte[] data)
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