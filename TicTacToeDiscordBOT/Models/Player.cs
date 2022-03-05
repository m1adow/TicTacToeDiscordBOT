namespace TicTacToeDiscordBOT.Models
{
    internal class Player
    {
        public byte VerticalIndex { get; private set; }
        public byte HorizontalIndex { get; private set; }
        public char Sign { get; private set; }
        public string Name { get; private set; }
        public ulong Id { get; private set; }

        public PlayerState PlayerState;

        public Player(string name, ulong id)
        {
            Name = name;
            Id = id;
            PlayerState = PlayerState.Basic;
        }

        public void SetIndex(string dimension, byte value)
        {
            if (dimension == "vertical") VerticalIndex = value;
            else if (dimension == "horizontal") HorizontalIndex = value;
        }

        public void SetSign(char sign) => Sign = sign;

    }

    public enum PlayerState
    {
        Basic = 0,
        CreateLobby = 10,
        JoinLobby = 11,
        AwaitingQueue = 12,
        EnterVerticalStep = 20,
        EnterGorizontalStep = 21
    }
}