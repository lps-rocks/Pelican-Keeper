using Pelican_Keeper;

namespace Pelican_Keeper_Unit_Testing;

public class A2STesting
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
        if (_secrets == null || _config == null) //its failing at reading the enum from the config file
        {
            Assert.Fail("Secrets or Config not loaded properly.");
            return;
        }

        ConsoleExt.WriteLineWithPretext(_config.MessageFormat);

        if (_secrets.ExternalServerIp != null)
        {
            await PelicanInterface.SendA2SRequest(_secrets.ExternalServerIp, 27051, "listplayers"); // should load these from the secrets file and the information provided by the pelican API
        }
        
        if (ConsoleExt.ExceptionOccurred)
        {
            Assert.Fail($"Test failed due to exception(s): {ConsoleExt.Exceptions}");
        }
        else
        {
            Assert.Pass("A2S request sent successfully.");
        }
    }
}