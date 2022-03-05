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

        _client.MessageReceived += OnMessageRecieved;
        _client.Log += Log;

        string token = "ODU5ODEyMDk3NjU1NTcwNDMy.YNyIag.VFyXKB9LT4cFqI32h7wfoMtyTjM";

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        Console.ReadLine();
    }

    private async Task<Task> OnMessageRecieved(SocketMessage message)
    {
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
                if (!IsNumberRight("horizontal", message).Result) return Task.CompletedTask;

                currentPlayer.SetIndex("horizontal", byte.Parse(message.Content));

                Models.Game game = _games.FirstOrDefault(g => g.Lobby.Players.Contains(currentPlayer));

                if(game.Field[currentPlayer.VerticalIndex - 1, currentPlayer.HorizontalIndex - 1] == game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).Sign)
                {
                    await message.Channel.SendMessageAsync($"\nThis cell was engaged");
                    await message.Channel.SendMessageAsync($"Write vertical index of your step");
                    currentPlayer.PlayerState = PlayerState.EnterVerticalStep;
                    return Task.CompletedTask;
                }

                game.MakeStep(currentPlayer.Sign, currentPlayer.VerticalIndex, currentPlayer.HorizontalIndex);

                await message.Channel.SendMessageAsync(game.GetField());

                Player enemy = game.Lobby.Players.FirstOrDefault(p => p != currentPlayer);               

                if (game.IsWin(currentPlayer.Sign))
                {
                    await message.Channel.SendMessageAsync($"We have a winner **{currentPlayer.Name}**");

                    _games.Remove(game);
                    _lobbies.Remove(_lobbies.FirstOrDefault(p => p.Players.Contains(currentPlayer)));
                    currentPlayer.PlayerState = PlayerState.Basic;
                    enemy.PlayerState = PlayerState.Basic;
                    return Task.CompletedTask;
                }

                if (game.IsTie())
                {
                    await message.Channel.SendMessageAsync($"**TIE**");

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

                _lobbies.Add(lobby);
                await message.Channel.SendMessageAsync($"Succesfully created lobby with name **\"{lobby.LobbyName}\"**. Await for another player");
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

                currentPlayer.SetSign('X');

                var lobby = _lobbies.FirstOrDefault(l => l.LobbyName == message.Content);

                lobby.AddPlayer(currentPlayer);
                lobby.SetFull(true);

                await message.Channel.SendMessageAsync($"Lobby was filled. Lobby name: {lobby.LobbyName}\nLaunching the game...\n");

                Models.Game game = new(lobby);

                while (_games.Any(g => g.GameId == game.GameId)) game = new(lobby);

                await message.Channel.SendMessageAsync(game.GetField());
                _games.Add(game);

                game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).SetSign('O');

                await message.Channel.SendMessageAsync($"\nPlayer with name **{currentPlayer.Name}** it's your turn\nYour sign is **\"X\"**\n\nPlayer with name **{game.Lobby.Players.FirstOrDefault(p => p != currentPlayer).Name}** await your turn.\nYour sign is **\"O\"**");
                await message.Channel.SendMessageAsync($"\nWrite vertical index of your step");
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