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
        _players = new List<Player>();
        _lobbies = new List<Lobby>();
        _games = new List<Models.Game>();

        _client = new DiscordSocketClient();
      
        _client.MessageReceived += OnMessageRecievedAsync;
        _client.Log += Log;

        string token = string.Empty;

        using (var streamReader = new StreamReader($@"{Environment.CurrentDirectory}\token.txt"))
            token = streamReader.ReadToEnd();

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        Console.ReadLine();
    }

    private async Task<Task> OnMessageRecievedAsync(SocketMessage message)
    {
        SocketGuild guild = _client.GetGuild(488362951087226880); //initiate guild with server id

        if (!message.Author.IsBot)
        {
            Player? currentPlayer = _players.FirstOrDefault(p => p.Id == message.Author.Id);

            if (currentPlayer is null)
            {
                currentPlayer = new Player(message.Author.Username, message.Author.Id);

                _players.Add(currentPlayer);
            }

            if (currentPlayer.PlayerState == PlayerState.AwaitingQueue)
            {
                await message.Channel.SendMessageAsync("Your game or turn isn't ready");
                return Task.CompletedTask;
            }

            if (currentPlayer.PlayerState == PlayerState.EnterVerticalStep)
            {
                if (!IsNumberRight("vertical", message).Result) return Task.CompletedTask;

                currentPlayer.SetIndex("vertical", byte.Parse(message.Content));
                await message.Channel.SendMessageAsync($"Write gorizontal index of your step");
                currentPlayer.PlayerState = PlayerState.EnterGorizontalStep;
                return Task.CompletedTask;
            }

            if (currentPlayer.PlayerState == PlayerState.EnterGorizontalStep)
            {
                if (message.Content == "!back")
                {
                    await message.Channel.SendMessageAsync("Write vertical index of your step");
                    currentPlayer.PlayerState = PlayerState.EnterVerticalStep;
                    return Task.CompletedTask;
                }

                if (!IsNumberRight("horizontal", message).Result) return Task.CompletedTask;

                currentPlayer.SetIndex("horizontal", byte.Parse(message.Content));

                Models.Game game = _games.FirstOrDefault(g => g.Lobby.Players.Contains(currentPlayer));

                if (game.Field[currentPlayer.VerticalIndex - 1, currentPlayer.HorizontalIndex - 1] == game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).Sign)
                {
                    await message.Channel.SendMessageAsync($"\nThis cell was engaged");
                    await message.Channel.SendMessageAsync("Write vertical index of your step");
                    currentPlayer.PlayerState = PlayerState.EnterVerticalStep;
                    return Task.CompletedTask;
                }

                game.MakeStep(currentPlayer.Sign, currentPlayer.VerticalIndex, currentPlayer.HorizontalIndex);

                await message.Channel.SendMessageAsync(game.GetField());

                Player enemy = game.Lobby.Players.FirstOrDefault(p => p != currentPlayer);

                if (game.IsWin(currentPlayer.Sign))
                {
                    Lobby lobby = _lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)); //lobby with current player

                    DeleteChannelAsync(lobby);

                    var channel = _client.GetChannel(859791278194819072) as IMessageChannel; //id of default channel of discord server
                    await channel.SendMessageAsync($"We have a winner **{currentPlayer.Name}** in lobby **{lobby.LobbyName}**");

                    _games.Remove(game); //delete ended game
                    _lobbies.Remove(_lobbies.FirstOrDefault(p => p.Players.Contains(currentPlayer))); //delete ended lobby
                    currentPlayer.PlayerState = PlayerState.Basic;
                    enemy.PlayerState = PlayerState.Basic;
                    return Task.CompletedTask;
                }

                if (game.IsTie())
                {
                    Lobby lobby = _lobbies.FirstOrDefault(l => l.Players.Contains(currentPlayer)); //lobby with current player

                    DeleteChannelAsync(lobby); 

                    var channel = _client.GetChannel(859791278194819072) as IMessageChannel; //id of default channel of discord server
                    await channel.SendMessageAsync($"**TIE** in lobby **{lobby.LobbyName}**");

                    currentPlayer.PlayerState = PlayerState.Basic;
                    enemy.PlayerState = PlayerState.Basic;
                    return Task.CompletedTask;
                }

                await message.Channel.SendMessageAsync($"Now it's your turn **{enemy.Name}**");
                await message.Channel.SendMessageAsync($"Write vertical index of your step");

                enemy.PlayerState = PlayerState.EnterVerticalStep;
                currentPlayer.PlayerState = PlayerState.AwaitingQueue;
                return Task.CompletedTask;
            }

            if (currentPlayer.PlayerState == PlayerState.CreateLobby)
            {
                if (_lobbies.Any(l => l.LobbyName == message.Content))
                {
                    await message.Channel.SendMessageAsync("This name has been already engaged. Enter another one");
                    return Task.CompletedTask;
                }

                Lobby lobby = new(message.Content);
                lobby.AddPlayer(currentPlayer);

                CreateChannelAsync(guild, lobby);

                _lobbies.Add(lobby);
                currentPlayer.PlayerState = PlayerState.AwaitingQueue;
                return Task.CompletedTask;
            }

            if (currentPlayer.PlayerState == PlayerState.JoinLobby)
            {
                if (!_lobbies.Any(l => l.LobbyName == message.Content))
                {
                    await message.Channel.SendMessageAsync("This lobby doesn't exist. Enter right name");
                    return Task.CompletedTask;
                }

                if (_lobbies.Where(l => l.LobbyName == message.Content).Any(l => l.IsFull == true))
                {
                    await message.Channel.SendMessageAsync("This lobby already full. Enter another one");
                    return Task.CompletedTask;
                }

                currentPlayer.SetSign('X'); //setting sign for connected player

                var lobby = _lobbies.FirstOrDefault(l => l.LobbyName == message.Content);

                lobby.AddPlayer(currentPlayer);
                lobby.SetFull(true);
                SetLobbyChannelId(guild, lobby);
                
                var channel = _client.GetChannel(lobby.ChannelId) as IMessageChannel; //lobby channel at discord server
                await channel.SendMessageAsync($"Lobby was filled. Lobby name: {lobby.LobbyName}\nLaunching the game...\n");

                Models.Game game = new(lobby);

                while (_games.Any(g => g.GameId == game.GameId)) game = new(lobby);

                await channel.SendMessageAsync(game.GetField());
                _games.Add(game);

                game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).SetSign('O');

                await channel.SendMessageAsync($"\nPlayer with name **{currentPlayer.Name}** it's your turn\nYour sign is **\"X\"**\n\nPlayer with name **{game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).Name}** await for your turn.\nYour sign is **\"O\"**");
                await channel.SendMessageAsync($"\nWrite vertical index of your step");
                currentPlayer.PlayerState = PlayerState.EnterVerticalStep;

                return Task.CompletedTask;
            }

            if (currentPlayer.PlayerState == PlayerState.Basic)
            {
                switch (message.Content)
                {
                    case "!create":
                        await message.Channel.SendMessageAsync("Enter lobby name");
                        currentPlayer.PlayerState = PlayerState.CreateLobby;
                        return Task.CompletedTask;
                    case "!join":
                        await message.Channel.SendMessageAsync("Enter lobby name");
                        currentPlayer.PlayerState = PlayerState.JoinLobby;
                        return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }

    private Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async void CreateChannelAsync(SocketGuild guild, Lobby lobby) => await guild.CreateTextChannelAsync($"lobby {lobby.LobbyName}");

    private async void DeleteChannelAsync(Lobby lobby)
    {
        var channel = _client.GetChannel(lobby.ChannelId) as SocketGuildChannel; //lobby channel at discord server
        await channel.DeleteAsync();
    }

    private async void SetLobbyChannelId(SocketGuild guild, Lobby lobby) => lobby.SetChannelId(guild.Channels.FirstOrDefault(c => c.Name == $"lobby-{lobby.LobbyName}").Id); //setting id of lobby at discord server

    private async Task<bool> IsNumberRight(string dimension, SocketMessage message)
    {

        if (message.Content.Any(a => char.IsLetter(a)))
        {
            await message.Channel.SendMessageAsync("Your message should contains only numbers");
            return false;
        }

        if (message.Content.Length > 1)
        {
            await message.Channel.SendMessageAsync("Your message should contains only one number");
            return false;
        }

        if (dimension == "vertical" && (byte.Parse(message.Content) < 1 || byte.Parse(message.Content) > 3))
        {
            await message.Channel.SendMessageAsync("Your message should contains number from 1 to 3");
            return false;
        }

        if (dimension == "horizontal" && (byte.Parse(message.Content) < 1 || byte.Parse(message.Content) > 5))
        {
            await message.Channel.SendMessageAsync("Your message should contains number from 1 to 5");
            return false;
        }

        if (dimension == "horizontal" && (byte.Parse(message.Content) == 2 || byte.Parse(message.Content) == 4))
        {
            await message.Channel.SendMessageAsync("Your message shouldn't equal 2 and 4");
            return false;
        }

        return true;
    }
}