using Discord;
using Discord.WebSocket;
using TicTacToeDiscordBOT.Models;

namespace TicTacToeDiscordBOT;

class Program
{
    private DiscordSocketClient? _client;
    private List<Player>? _players;
    private List<Lobby>? _lobbies;
    private List<Models.Game>? _games;

    static void Main(string[] args)
        => new Program().MainAsync();

    private async Task MainAsync()
    {
        //initiate all of collections
        _players = new List<Player>();
        _lobbies = new List<Lobby>();
        _games = new List<Models.Game>();

        _client = new DiscordSocketClient();

        _client.MessageReceived += OnMessageRecievedAsync;
        _client.Log += Log;

        string token = string.Empty;

        //get discord token from .txt file
        using (var streamReader = new StreamReader($@"{Environment.CurrentDirectory}\token.txt"))
            token = streamReader.ReadToEnd();

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        Console.ReadLine();
    }

    private async Task<Task> OnMessageRecievedAsync(SocketMessage message)
    {
        try
        {
            IGuildChannel server = message.Channel as IGuildChannel; //get server
            SocketGuild? guild; //declare socket guild variable

            if (server is not null) guild = _client.GetGuild(server.Guild.Id); //assign guild if server is not null
            else guild = null;

            if (!message.Author.IsBot)
            {
                Player? currentPlayer = _players.FirstOrDefault(p => p.Id == message.Author.Id); //get player from list

                //initiate player if it is null
                if (currentPlayer is null)
                {
                    currentPlayer = new Player(message.Author.Username, message.Author.Id);

                    _players.Add(currentPlayer);
                }

                IMessageChannel channel = message.Channel;

                //if lobby is not null bind channel to lobby channel
                if (_lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)) is not null) channel = _client.GetChannel(_lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)).ChannelId) as IMessageChannel;

                //construction for surrender
                if ((currentPlayer.PlayerState == PlayerState.AwaitingQueue || currentPlayer.PlayerState == PlayerState.EnterGorizontalStep || currentPlayer.PlayerState == PlayerState.EnterVerticalStep) && message.Content == "!surrender")
                {
                    Models.Game game = _games.FirstOrDefault(g => g.Lobby.Players.Contains(currentPlayer)); //get game from looby which contains current player

                    Player enemyPlayer = game.Lobby.Players.FirstOrDefault(p => p != currentPlayer); //get enemy player from game

                    EndGameAsync(_client, _lobbies, _games, game, currentPlayer, enemyPlayer, $"**{currentPlayer.Name}** surrendered **{enemyPlayer.Name}** has winned");
                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.AwaitingQueue)
                {
                    if (message.Content == "!reset")
                    {
                        Lobby lobby = _lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)); //get lobby with currentPlayer
                        _lobbies.Remove(lobby);

                        SetLobbyChannelIdAsync(guild, lobby);
                        DeleteChannelAsync(_client, lobby);

                        await message.Channel.SendMessageAsync("Success!");
                        currentPlayer.PlayerState = PlayerState.Basic;
                        return Task.CompletedTask;
                    }

                    await channel.SendMessageAsync("Your game or turn isn't ready");
                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.EnterVerticalStep)
                {
                    if (!IsNumberRight(message, channel).Result) return Task.CompletedTask;

                    currentPlayer.SetIndex("vertical", byte.Parse(message.Content));
                    await channel.SendMessageAsync($"Write horizontal index of your step");
                    currentPlayer.PlayerState = PlayerState.EnterGorizontalStep;
                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.EnterGorizontalStep)
                {
                    //command for return on previous state
                    if (message.Content == "!back")
                    {
                        await channel.SendMessageAsync("Write vertical index of your step");
                        currentPlayer.PlayerState = PlayerState.EnterVerticalStep;
                        return Task.CompletedTask;
                    }

                    if (!IsNumberRight(message, channel).Result) return Task.CompletedTask;

                    byte horizontalIndex = byte.Parse(message.Content);

                    if (horizontalIndex == 2) horizontalIndex += 1;
                    else if (horizontalIndex == 3) horizontalIndex += 2;

                    currentPlayer.SetIndex("horizontal", horizontalIndex);

                    Models.Game game = _games.FirstOrDefault(g => g.Lobby.Players.Contains(currentPlayer)); //game witch contain current player

                    //examination for engaged cell
                    if (game.Field[currentPlayer.VerticalIndex - 1, currentPlayer.HorizontalIndex - 1] != '-')
                    {
                        await channel.SendMessageAsync($"\nThis cell was engaged");
                        await channel.SendMessageAsync("Write vertical index of your step");
                        currentPlayer.PlayerState = PlayerState.EnterVerticalStep;
                        return Task.CompletedTask;
                    }

                    game.MakeStep(currentPlayer.Sign, currentPlayer.VerticalIndex, currentPlayer.HorizontalIndex); //make step with recieved data

                    await channel.SendMessageAsync(game.GetField()); //draw the field

                    Player enemyPlayer = game.Lobby.Players.FirstOrDefault(p => p != currentPlayer); //get enemy player

                    //examination for winned game
                    if (game.IsWin(currentPlayer.Sign))
                    {
                        EndGameAsync(_client, _lobbies, _games, game, currentPlayer, enemyPlayer, $"We have a winner **{currentPlayer.Name}**"); //end of winned game
                        return Task.CompletedTask;
                    }

                    //examination for tied game
                    if (game.IsTie())
                    {
                        EndGameAsync(_client, _lobbies, _games, game, currentPlayer, enemyPlayer, $"We have **TIE**"); //end of tied game
                        return Task.CompletedTask;
                    }

                    await channel.SendMessageAsync($"Now it's your turn **{enemyPlayer.Name}**");
                    await channel.SendMessageAsync($"Write vertical index of your step");

                    enemyPlayer.PlayerState = PlayerState.EnterVerticalStep;
                    currentPlayer.PlayerState = PlayerState.AwaitingQueue;
                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.CreateLobby)
                {
                    if (message.Content == "!reset")
                    {
                        await channel.SendMessageAsync("Success!");
                        currentPlayer.PlayerState = PlayerState.Basic;
                        return Task.CompletedTask;
                    }

                    //examination for existing lobby with the same name
                    if (_lobbies.Any(l => l.LobbyName == message.Content))
                    {
                        await channel.SendMessageAsync("This name has been already engaged. Enter another one");
                        return Task.CompletedTask;
                    }

                    //initiate new lobby and add player
                    Lobby lobby = new(message.Content);
                    lobby.AddPlayer(currentPlayer);

                    CreateChannelAsync(guild, lobby);

                    _lobbies.Add(lobby);
                    currentPlayer.PlayerState = PlayerState.AwaitingQueue;
                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.JoinLobby)
                {
                    //examination for existing lobby
                    if (!_lobbies.Any(l => l.LobbyName == message.Content))
                    {
                        await channel.SendMessageAsync("This lobby doesn't exist. Enter right name");
                        return Task.CompletedTask;
                    }

                    //examination for occupancy of lobby
                    if (_lobbies.Where(l => l.LobbyName == message.Content).Any(l => l.IsFull == true))
                    {
                        await message.Channel.SendMessageAsync("This lobby already full. Enter another one");
                        return Task.CompletedTask;
                    }

                    currentPlayer.SetSign('X'); //setting sign for connected player

                    Lobby lobby = _lobbies.FirstOrDefault(l => l.LobbyName == message.Content); //get lobby
                    lobby.AddPlayer(currentPlayer);
                    lobby.SetFull(true);

                    SetLobbyChannelIdAsync(guild, lobby);

                    await channel.SendMessageAsync($"Lobby was filled. Lobby name: **{lobby.LobbyName}**\nLaunching the game...\n");

                    Models.Game game = new(lobby); //initiate game with current lobby

                    while (_games.Any(g => g.GameId == game.GameId)) game = new(lobby); //if game with the same id exist recreate game

                    IMessageChannel createdChannel = _client.GetChannel(_lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)).ChannelId) as IMessageChannel; //getting channel with current player

                    await createdChannel.SendMessageAsync(game.GetField()); //draw start field
                    _games.Add(game);

                    game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).SetSign('O'); //set sign for enemy player

                    await createdChannel.SendMessageAsync($"\nPlayer with name **{currentPlayer.Name}** it's your turn\nYour sign is **\"X\"**\n\nPlayer with name **{game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).Name}** await for your turn.\nYour sign is **\"O\"**");
                    await createdChannel.SendMessageAsync($"\nWrite vertical index of your step");
                    currentPlayer.PlayerState = PlayerState.EnterVerticalStep;

                    return Task.CompletedTask;
                }

                if (currentPlayer.PlayerState == PlayerState.Basic)
                {
                    switch (message.Content)
                    {
                        case "!create":
                            await channel.SendMessageAsync("Enter lobby name");
                            currentPlayer.PlayerState = PlayerState.CreateLobby;
                            return Task.CompletedTask;
                        case "!join":
                            await channel.SendMessageAsync("Enter lobby name");
                            currentPlayer.PlayerState = PlayerState.JoinLobby;
                            return Task.CompletedTask;
                        case "!faq":
                            await message.Author.SendMessageAsync("This bot contains two commands:\n**!create** - to create lobby\n**!join** - to join into lobby");
                            return Task.CompletedTask;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync("Something went wrong :(\nTry to change your request :)");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        return Task.CompletedTask;
    }

    private Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async void CreateChannelAsync(SocketGuild guild, Lobby lobby) => await guild.CreateTextChannelAsync($"lobby {lobby.LobbyName}");

    private async void DeleteChannelAsync(DiscordSocketClient client, Lobby lobby)
    {
        var channel = client.GetChannel(lobby.ChannelId) as SocketGuildChannel; //lobby channel at discord server
        await channel.DeleteAsync();
    }

    private async void SetLobbyChannelIdAsync(SocketGuild guild, Lobby lobby) => lobby.SetChannelId(guild.Channels.FirstOrDefault(c => c.Name == $"lobby-{lobby.LobbyName.ToLower()}").Id); //setting id of lobby at discord server

    private async void EndGameAsync(DiscordSocketClient client, List<Lobby> lobbies, List<Models.Game> games, Models.Game game, Player currentPlayer, Player enemyPlayer, string text)
    {
        Lobby lobby = lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)); //lobby with current player

        DeleteChannelAsync(client, lobby);

        var channelChat = client.GetChannel(859791278194819072) as IMessageChannel; //id of default channel of discord server
        await channelChat.SendMessageAsync($"{text} in lobby **{lobby.LobbyName}**");

        games.Remove(game); //delete ended game
        lobbies.Remove(_lobbies.FirstOrDefault(p => p.Players.Contains(currentPlayer))); //delete ended lobby
        currentPlayer.PlayerState = PlayerState.Basic;
        enemyPlayer.PlayerState = PlayerState.Basic;
    }

    private async Task<bool> IsNumberRight(SocketMessage message, IMessageChannel channel)
    {

        if (message.Content.Any(a => char.IsLetter(a)))
        {
            await channel.SendMessageAsync("Your message should contains only numbers");
            return false;
        }

        if (message.Content.Length > 1)
        {
            await channel.SendMessageAsync("Your message should contains only one number");
            return false;
        }

        if (byte.Parse(message.Content) < 1 || byte.Parse(message.Content) > 3)
        {
            await channel.SendMessageAsync("Your message should contains number from 1 to 3");
            return false;
        }

        return true;
    }
}