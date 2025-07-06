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

### Currently being worked on
- [x] Finish adding Pagination
- [ ] Customizable Embed Message and Structure
- [ ] Add Joinable IP:Port to the displayed server information

## Setup in Pelican

### Setup Egg

Setup the Generic C# egg in your Pelican Panel like normal.

### Configuration

Set the following variables exactly like this
```
- Git Repo Address: https://github.com/SirZeeno/Pelican-Keeper
- Install Branch: main
- Project Location: /home/container/Pelican Keeper/
- Project File: "Pelican Keeper"
```
### Secrets

Run the bot once, or create the Secrets.json in the base directory you see when opening the Files tab, and fill out all the necessary information.

## Setup Outside of Pelican
> [!TIP]
> You will need .NET 8.0 and curl installed.

### Get The Latest Release

Download the latest release with this command. This command downloads the latest release, unzips it, and removes the zip file.
```
curl -L https://github.com/SirZeeno/Pelican-Keeper/releases/latest/download/Release.zip && unzip Release.zip && rm Release.zip
```

### Do a Restore

cd into the bot directory and run the restore command
```
cd Pelican Keeper/
dotnet restore
```

### Run the Bot
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

## Configuration

### Config File

The config.json file can be found in the installatin folder and for the moment you have two options

```
{
  "ConsolidateEmbeds": true,
  "Paginate": false
}
```
### ConsolidateEmbeds

> Consolidates all the server information into a single embed message if true. And if false, it will create embed message for each game server you have on pelican.
> 
> Note that you will run into discord rate limits if you have this option off and you have a few game servers on pelican.

![image](https://github.com/user-attachments/assets/9ec54b8d-48fa-424c-acd3-5bb12222f2ef)

### Paginate

> Paginates the server information into a single embed message with changable pages.
> 
> Note that the page will be the same serverwide, meaning if someone changed the page it changes it for you as well.

![image](https://github.com/user-attachments/assets/7cb58936-71f7-4378-9256-0a79c5056256)

