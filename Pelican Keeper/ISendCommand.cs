namespace Pelican_Keeper;

public interface ISendCommand
{
    public Task Connect(string ip, int port);
    
    public Task<string> SendCommandAsync(string command);
}