namespace TicTacToeDiscordBOT.Models
{
    internal class Game
    {
        public int GameId { get; private set; }
        public char[,] Field { get; private set; } = new char[3, 5]
        {
            {'\0', '|', '\0', '|', '\0'},
            {'\0', '|', '\0', '|', '\0'},
            {'\0', '|', '\0', '|', '\0'}
        };

        public Lobby? Lobby { get; private set; }

        public Game(Lobby lobby)
        {
            Lobby = lobby;

            Random random = new();
            GameId = random.Next(0, int.MaxValue);
        }

        public void MakeStep(char sign, byte placeVertical, byte placeHorizontal) => Field[placeVertical - 1, placeHorizontal - 1] = sign;

        public bool IsWin(char sign)
        {
            if (sign == Field[0, 0] && Field[0, 0] == Field[0, 2] && Field[0, 2] == Field[0, 4]) return true;
            else if (sign == Field[1, 0] && Field[1, 0] == Field[1, 2] && Field[1, 2] == Field[1, 4]) return true;
            else if (sign == Field[2, 0] && Field[2, 0] == Field[2, 2] && Field[2, 2] == Field[2, 4]) return true;
            else if (sign == Field[0, 0] && Field[0, 0] == Field[1, 0] && Field[1, 0] == Field[2, 0]) return true;
            else if (sign == Field[0, 2] && Field[0, 2] == Field[1, 2] && Field[1, 2] == Field[2, 2]) return true;
            else if (sign == Field[0, 4] && Field[0, 4] == Field[1, 4] && Field[1, 4] == Field[2, 4]) return true;

            return false;
        }

        public bool IsTie()
        {
            string field = string.Empty;

            for (int i = 0; i < Field.GetLength(0); i++)
            {
                for (int j = 0; j < Field.GetLength(1); j++)
                {
                    field += Field[i, j];
                }
            }

            return !field.Contains('\0');
        }

        public string GetField()
        {
            string field = "Field:\n";

            for (int i = 0; i < Field.GetLength(0); i++)
            {
                for (int j = 0; j < Field.GetLength(1); j++)
                {
                    field += Field[i, j];
                }

                field += Environment.NewLine;
            }

            return field;
        }
    }
}