<p align="center">
  <img src="https://github.com/user-attachments/assets/3eb050aa-40c6-496f-94a3-8404987a6bf6"/><br>
  <a href="https://ko-fi.com/sirzeeno" target="_blank">
    <img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support me on Ko-fi" />
  </a>
</p>

# Pelican Keeper
A Discord bot that will display the status of your Pelican Game Servers in a Discord Channel

This Discord bot is a basic compile-and-run bot built in .NET 8.0.
> [!TIP]
> This bot can be installed and run on the generic C# Egg on Pelican.

## Setup
> [!TIP]
> You will need .NET 8.0 and curl installed.

**Get The Latest Release**
Download the latest release, unzips it
```
curl -L https://github.com/SirZeeno/Pelican-Keeper/releases/latest/download/Release.zip && unzip Release.zip
```

**Do a Restore**
cd into the bot directory and run the restore command
```
cd Pelican Keeper/
dotnet restore
```

**Run the Bot**
> [!WARNING]
> At this point you should create the Secrets.json in the bot directory, otherwise the first run will result in an error.
> After the first run, the bot will have created the Secrets.json. At that point it's your responsibility to fill out with all the necessary information.

while still in the same directory to start the bot you simply run this command
```
dotnet run --project "Pelican Keeper"
```

## Secrets

> [!WARNING]
> Make sure you fill out the Secrets.json file found in the bot install directory, otherwise the bot **WILL NOT** work!

**Secrets.json Structure**
```
{
  "ClientToken": "YOUR_CLIENT_TOKEN",
  "ServerToken": "YOUR_SERVER_TOEKN",
  "ServerUrl": "YOUR_BASIC_SERVER_URL",
  "BotToken": "YOUR_DISCORD_BOT_TOKEN",
  "ChannelId": "THE_CHANNELID_YOU_WANT_THE_BOT_TO_POST_IN",
  "ExternalServerIP": "YOUR_EXTERNAL_SERVER_IP"
}
```
> The ExternalServerIP variable is optional and is used to display the server's public IP alongside the primary game server port. This provides a visible, joinable IP address as part of the server information.
