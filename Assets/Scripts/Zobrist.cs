public static class Zobrist
{
    private static System.Random _rand; // RNG instance
    
    private static ulong[,] _table; // Random longs for each position
    private static ulong _blackToMove; // Random long for color to move
    private static ulong[,] _castle; // Random longs for castling status
    private static ulong[] _passant; // Random longs for passant status
    
    public static void Init() // Initializes random values
    {
        _rand = new System.Random((int)System.DateTime.Now.Ticks);
        _table = new ulong[64,12];
        for (int i = 0; i < 64;i++)
        {
            for (int j = 0; j < 12;j++)
            {
                _table[i,j] = GetRandUlong(0, ulong.MaxValue);
            }
        }
        _passant = new ulong[8];
        for (int i = 0; i < 8; i++)
        {
            _passant[i] = GetRandUlong(0, ulong.MaxValue);
        }
        _castle = new ulong[2, 2];
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                _castle[i,j] = GetRandUlong(0, ulong.MaxValue);
            }
        }
        _blackToMove = GetRandUlong(0, ulong.MaxValue);
    }

    public static ulong Hash(Board board) // Generates Zobrist hash for board
    {
        ulong h = 0;
        if (!board.WhiteTurn)
            h ^= _blackToMove;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                int p = board.Pieces[x, y];
                if (p != 0)
                {
                    int i = x * 8 + y;
                    int j = Piece.Zobrist(p);
                    h ^= _table[i, j];
                }
            }
        }
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                if(board.RookMoved[i,j] == -1)
                    h ^= _castle[i, j];
            }
        }
        for (int i = 0; i < 8; i++)
        {
            int y = board.Passant.y;
            if (y > 0)
            {
                h ^= _passant[y];
            }
        }
        return h;
    }
    
    private static ulong GetRandUlong(ulong min, ulong max) // Gets random ulong
    {
        // Get a random 64 bit number.
        var buf = new byte[sizeof(ulong)];
        _rand.NextBytes(buf);
        ulong n = System.BitConverter.ToUInt64(buf, 0);
        // Scale to between 0 inclusive and 1 exclusive; i.e. [0,1).
        double normalised = n / (ulong.MaxValue + 1.0);
        // Determine result by scaling range and adding minimum.
        double range = (double)max - min;
        return (ulong)(normalised * range) + min;
    }
}
