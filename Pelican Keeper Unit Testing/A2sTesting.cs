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
        _config = TestConfigCreator.CreateDefaultConfigInstance();
    }

    [Test]
    public async Task SendCommandTest()
    {
        if (_secrets == null || _config == null)
        {
            Assert.Fail("Secrets or Config not loaded properly.");
            return;
        }

        ConsoleExt.WriteLineWithPretext(_config.MessageFormat);

        if (_secrets.ExternalServerIp != null)
        {
            await PelicanInterface.SendA2SRequest(_secrets.ExternalServerIp, 27051);
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