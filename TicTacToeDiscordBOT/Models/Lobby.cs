namespace TicTacToeDiscordBOT.Models
{
    internal class Lobby
    {
        public List<Player>? Players { get; private set; } = new(2);
        public string? LobbyName { get; private set; }
        public bool IsFull { get; private set; }

        public Lobby(string lobbyName)
        {
            LobbyName = lobbyName;
            IsFull = false;
        }

        public void AddPlayer(Player player) => Players.Add(player);

        public void SetFull(bool isFull) => IsFull = isFull;
    }
}