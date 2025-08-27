using Pelican_Keeper;

namespace Pelican_Keeper_Unit_Testing;

public class RconTesting
{
    TemplateClasses.Secrets? _secrets;
    TemplateClasses.Config? _config;
    
    [SetUp]
    public async Task Setup()
    {
        _secrets = await FileManager.ReadSecretsFile();
        _config = await FileManager.ReadConfigFile();
    }

    [Test]
    public async Task SendCommandTest()
    {
        if (_secrets == null || _config == null)
        {
            Assert.Fail("Secrets or Config not loaded properly.");
            return;
        }
        
        await PelicanInterface.SendGameServerCommandRcon(_secrets.ExternalServerIp, 7777, "YouSuck", "listplayers"); // should load these from the secrets file and the information provided by the pelican API
        
        if (ConsoleExt.ExceptionOccurred)
        {
            Assert.Fail($"Test failed due to exception(s): {ConsoleExt.Exceptions}");
        }
        else
        {
            Assert.Pass("Rcon command sent successfully.");
        }
    }
    
    
}