using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class Moves
{
    private List<Move> _moves; // Move history
    private HashSet<int> _kingPinDirs; // Directions king is pinned from
    private Board _board; // Game board
    private bool _capturesOnly; // Quiescence search flag
    private readonly (int x, int y)[] _cardinalDirs = {(0, 1), (1, 0), (0, -1), (-1, 0), (1, 1), (1, -1), (-1, -1), (-1, 1)}; // Cardinal directions
    private readonly (int x, int y)[] _knightDirs = {(2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (-1, 2), (1, -2), (-1, -2)}; // Knight directions
    
    public bool[,] Attackable; // Attackable bit map
    public bool[,] Pinned; // Pinned position bit map
    public bool Check; // Check status
    private bool _doubleCheck; // Double check status
    private (int x, int y) _checker; // Piece checking king
    
    public readonly struct Move // Move representation
    {
        public readonly struct Flag // Special move flags
        {
            public const int None = 0;
            public const int EnPassant = 1;
            public const int Castle = 2;
            public const int PromoteQueen = 3;
            public const int PromoteKnight = 4;
            public const int PromoteRook = 5;
            public const int PromoteBishop = 6;
            public const int MoveTwo = 7;
        }

        public Move(int x, int y, int x2, int y2, int cap = 0, int f = Flag.None) // Constructor
        {
            StartX = x;
            StartY = y;
            ToX = x2;
            ToY = y2;
            Capture = cap;
            flag = f;
        }

        public readonly int StartX, StartY, ToX, ToY, Capture, flag; // Values
    }

    public List<Move> GenerateValidMoves(Board b, bool cap = false) // Generate list of legal moves from board position
    {
        _board = b;
        _capturesOnly = cap;
        _moves = new List<Move>();
        GenerateAttackable();
        AddKingMoves();
        if (_doubleCheck) return _moves;
        AddSliderMoves();
        AddKnightMoves();
        AddPawnMoves();
        return _moves;
    }

    
    private void AddKingMoves() // Adds legal king moves
    {
        int color = _board.WhiteTurn ? 0 : 1;
        (int x, int y) = _board.Kings[color];
        for (int d = 0; d < 8; d++)
        {
            (int currX, int currY) = _cardinalDirs[d];
            if (InBoard(x + currX, y + currY))
            {
                if (_capturesOnly && !IsEnemy(x + currX, y + currY))
                    continue;
                if (IsFriend(x + currX, y + currY))
                    continue;
                if (Attackable[x + currX, y + currY])
                    continue;
                if (_kingPinDirs.Contains(d) && !EnemySlider(x + currX, y + currY, d))
                    continue;
                _moves.Add(new Move(x, y, x + currX, y + currY, _board.Pieces[x + currX, y + currY]));
            }
        }

        if (!Check)
        {
            if (x == 4 && y == color*7)
            {
                if (_board.RookMoved[0,color] == -1 && Piece.IsRook(_board.Pieces[0,y]))
                {
                    if (IsEmpty(3,y) && IsEmpty(2,y) && IsEmpty(1,y) && !Attackable[3, y] && !Attackable[2, y])
                    {
                        _moves.Add(new Move(4, y, 2, y,0,Move.Flag.Castle)); // Queen-side castle
                    }
                }

                if (_board.RookMoved[1,color] == -1 && Piece.IsRook(_board.Pieces[7,y]))
                {
                    if (IsEmpty(5,y) && IsEmpty(6,y) && !Attackable[5,y] && !Attackable[6,y])
                    {
                        _moves.Add(new Move(4, y, 6, y,0,Move.Flag.Castle)); // King-side castle
                    }
                }
            }
        }
    }

    private void AddSliderMoves() // Adds legal queen, rook, and bishop moves
    {
        int color = _board.WhiteTurn ? 0 : 1;
        foreach ((int x, int y) in _board.Rooks[color])
            AddSliderMovesDir(x, y, 0, 4);

        foreach ((int x, int y) in _board.Bishops[color])
            AddSliderMovesDir(x, y, 4, 8);

        foreach ((int x, int y) in _board.Queens[color])
            AddSliderMovesDir(x, y, 0, 8);
    }

    private bool PinAligned(int d, int x, int y, int toX, int toY) // Whether a position is aligned with a pin
    {
        if (d == -1) return false;
        
        int signX;
        if (toX - x == 0) signX = 0;
        else signX = (toX - x) / Math.Abs(toX - x);
        
        int signY;
        if (toY - y == 0) signY = 0;
        else signY = (toY - y) / Math.Abs(toY - y);
        
        (int currX, int currY) = _cardinalDirs[d];
        return (signX == currX && signY == currY) || (signX == -1 * currX && signY == -1 * currY);
    }

    private void AddSliderMovesDir(int x, int y, int start, int end) // Adds slider type piece moves
    {
        if (Pinned[x,y] && Check) return;
        
        int color = _board.WhiteTurn ? 0 : 1;
        (int kingX, int kingY) = _board.Kings[color];
        
        for (int d = start; d < end; d++) {
            if (Pinned[x, y] && !PinAligned(d, x, y, kingX, kingY))
                continue;
            
            (int currX, int currY) = _cardinalDirs[d];
            int i = 1;
            while (true)
            {
                if (InBoard(x+currX*i,y+currY*i))
                {
                    if (IsFriend(x + currX * i, y + currY * i))
                        break;
                    if (!Check || _checker.x == x + currX * i && _checker.y == y + currY * i || BlocksCheck(x + currX * i, y + currY * i))
                    {
                        if (!_capturesOnly || IsEnemy(x + currX*i, y + currY*i)) 
                            _moves.Add(new Move(x,y,x+currX*i,y+currY*i, _board.Pieces[x+currX*i,y+currY*i]));
                    }

                    if (!IsEmpty(x + currX * i, y + currY * i) ||
                        Check && _checker.x == x + currX * i && _checker.y == y + currY * i ||
                        Check && BlocksCheck(x + currX * i, y + currY * i))
                        break;
                    i++;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void AddKnightMoves() // Adds legal knight moves
    {
        int color = _board.WhiteTurn ? 0 : 1;
        foreach ((int x, int y) in _board.Knights[color])
        {
            if (Pinned[x, y])
                continue;
            
            for (int d = 0; d < 8; d++)
            {
                (int currX, int currY) = _knightDirs[d];
                if (InBoard(x + currX, y + currY))
                {
                    if (_capturesOnly && !IsEnemy(x + currX, y + currY))
                        continue;
                    if (Check && _checker.x == x + currX && _checker.y == y + currY)
                        _moves.Add(new Move(x, y, x + currX, y + currY,_board.Pieces[x+currX,y+currY]));
                    else if (Check && BlocksCheck(x + currX, y + currY))
                        _moves.Add(new Move(x, y, x + currX, y + currY));
                    else if (!Check && (IsEmpty(x+currX,y+currY) || IsEnemy(x+currX,y+currY)))
                        _moves.Add(new Move(x, y, x + currX, y + currY, _board.Pieces[x+currX,y+currY]));
                }
            }
        }
    }
    
    private void AddPawnMoves() // Adds legal pawn moves
    {
        int color = _board.WhiteTurn ? 0 : 1;
        int startY = _board.WhiteTurn ? 1 : 6;
        int promoY = _board.WhiteTurn ? 7 : 0;
        int signY = _board.WhiteTurn ? 1 : -1;
        (int kingX, int kingY) = _board.Kings[color];
        int[] xDirs = {-1, 1};
        foreach ((int x, int y) in _board.Pawns[color])
        {
            if (Pinned[x,y] && Check) continue;
            
            // Forwards
            if (!_capturesOnly && InBoard(x, y + signY) && IsEmpty(x, y + signY))
            {
                if (!Pinned[x, y] || PinAligned(DirIndexOf(0, signY), x, y, kingX, kingY))
                {
                    if (!Check || BlocksCheck(x, y + signY))
                    {
                        if (y + signY == promoY)
                        {
                            _moves.Add(new Move(x, y, x, y + signY, 0, Move.Flag.PromoteKnight));
                            _moves.Add(new Move(x, y, x, y + signY, 0, Move.Flag.PromoteBishop));
                            _moves.Add(new Move(x, y, x, y + signY, 0, Move.Flag.PromoteRook));
                            _moves.Add(new Move(x, y, x, y + signY, 0, Move.Flag.PromoteQueen));
                        }
                        else
                        {
                            _moves.Add(new Move(x, y, x, y + signY));
                        }
                    }

                    if (y == startY)
                    {
                        if (IsEmpty(x, y + signY*2))
                        {
                            if (!Check || BlocksCheck(x, y + signY*2))
                                _moves.Add(new Move(x, y, x, y + signY * 2,0,Move.Flag.MoveTwo));
                        }
                    }
                }
            }

            // Captures
            for (int d = 0; d < 2; d++)
            {
                if (InBoard(x + xDirs[d], y + signY))
                {
                    if (Pinned[x, y] && !PinAligned(DirIndexOf(xDirs[d], signY), x, y, kingX, kingY))
                        continue;
                    
                    if (IsEnemy(x + xDirs[d], y + signY))
                    {
                        if (Check && !BlocksCheck(x + xDirs[d], y + signY) && !(_checker.x == x+xDirs[d] && _checker.y == y+signY))
                            continue;
                        
                        int enemy = _board.Pieces[x + xDirs[d], y + signY];
                        if (y + signY == promoY)
                        {
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, enemy, Move.Flag.PromoteKnight));
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, enemy, Move.Flag.PromoteBishop));
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, enemy, Move.Flag.PromoteRook));
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, enemy, Move.Flag.PromoteQueen));
                        }
                        else
                        {
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, enemy));
                        }
                    }
                    
                    if (x + xDirs[d] == _board.Passant.x && y+signY == _board.Passant.y)
                    {
                        if(!PinnedPassant(x,y,x+xDirs[d],y+signY))
                            _moves.Add(new Move(x, y, x+xDirs[d], y + signY, _board.Pieces[x+xDirs[d],y], Move.Flag.EnPassant));
                    }
                }
            }
        }
    }

    public bool IsValid(int x, int y, int toX, int toY, out Move m2) // Checks if move is valid (for human players)
    {
        Move moveToCheck = new Move(x, y, toX, toY);
        foreach (Move m in _moves)
        {
            if (m.StartX == x && m.StartY == y && m.ToX == toX && m.ToY == toY)
            {
                m2 = m;
                if (m2.flag == Move.Flag.PromoteKnight) // TODO: Players need a UI for this
                    m2 = new Move(m2.StartX, m.StartY, m2.ToX, m2.ToY, m2.Capture, Move.Flag.PromoteQueen);
                return true;
            }
        }

        m2 = moveToCheck;
        return false;
    }

    public List<Move> ValidMovesTo(int x, int y) // List of valid moves to a position
    {
        List<Move> m2 = new List<Move>();
        foreach (Move m in _moves)
        {
            if (m.StartX == x && m.StartY == y)
                m2.Add(m);
        }
        return m2;
    }

    private bool InBoard(int x, int y) // If a location is in the board
    {
        return InBoard(x) && InBoard(y);
    }

    private bool InBoard(int x) // If a coordinate value is in board
    {
        return 0 <= x && x < 8;
    }

    private bool IsEmpty(int x, int y) // If a location is empty
    {
        return _board.Pieces[x, y] == Piece.Empty;
    }
    
    private bool IsEnemy(int x, int y) // If an enemy piece is in a location
    {
        if (IsEmpty(x,y)) return false;
        return Piece.IsWhite(_board.Pieces[x,y]) != _board.WhiteTurn;
    }
    
    private bool IsFriend(int x, int y) // If a friendly piece is in a location
    {
        if (IsEmpty(x,y)) return false;
        return Piece.IsWhite(_board.Pieces[x,y]) == _board.WhiteTurn;
    }

    private void GenerateAttackable() // Generates attackable bit map
    {
        Check = false;
        _doubleCheck = false;
        _checker = (-2, -2);
        Attackable = new bool[8,8];
        _kingPinDirs = new HashSet<int>();
        
        int color = !_board.WhiteTurn ? 0 : 1;
        (int kingX, int kingY) = _board.Kings[color];
        for (int d = 0; d < 8; d++)
        {
            (int currX, int currY) = _cardinalDirs[d];
            AddToCheck(kingX,kingY,kingX+currX,kingY+currY);
        }
        
        foreach ((int x, int y) in _board.Rooks[color])
        {
            GenerateAttackableSlider(x, y, 0, 4);
        }
        
        foreach ((int x, int y) in _board.Bishops[color])
        {
            GenerateAttackableSlider(x, y, 4, 8);
        }
        
        foreach ((int x, int y) in _board.Queens[color])
        {
            GenerateAttackableSlider(x, y, 0, 8);
        }
        
        foreach ((int x, int y) in _board.Knights[color])
        {
            for (int d = 0; d < 8; d++)
            {
                (int currX, int currY) = _knightDirs[d];
                AddToCheck(x,y,x+currX,y+currY);
            }
        }

        foreach ((int x, int y) in _board.Pawns[color])
        {
            int dY = _board.WhiteTurn ? -1 : 1;
            AddToCheck(x,y,x+1,y+dY);
            AddToCheck(x,y,x-1,y+dY);
        }
        
        GeneratePinned();
    }

    private void GeneratePinned() // Generates pinned map
    {
        Pinned = new bool[8, 8];
        int color = _board.WhiteTurn ? 0 : 1;
        (int x, int y) = _board.Kings[color];
        for (int d = 0; d < 8; d++) {
            (int currX, int currY) = _cardinalDirs[d];
            int i = 1;
            int j = 1;
            bool recheck = false;
            bool firstFriend = true;
            while (true)
            {
                if (InBoard(x+currX*i,y+currY*i) && EnemySlider(x+currX*i,y+currY*i,d))
                {
                    recheck = true;
                    if (firstFriend)
                    {
                        _kingPinDirs.Add(d);
                        _kingPinDirs.Add(OppositeDir(d));
                    }
                    break;
                }
                if (InBoard(x+currX*i,y+currY*i))
                {
                    if (IsEnemy(x + currX * i, y + currY * i))
                    {
                        break;
                    }

                    
                    if (IsFriend(x + currX * i, y + currY * i))
                    {
                        if (!firstFriend) break;
                        firstFriend = false;
                    }

                    i++;
                }
                else
                {
                    break;
                }
            }

            while (recheck && j<=i)
            {
                if (IsEmpty(x + currX * j, y + currY * j) || IsFriend(x + currX * j, y + currY * j))
                {
                    Pinned[x + currX * j, y + currY * j] = true;
                }
                j++;
            }
        }
    }
    
    private bool PinnedPassant(int meX, int meY, int toX, int toY) // Checks en-passant pins
    {
        // TODO: This can be optimized
        int color = _board.WhiteTurn ? 0 : 1;
        (int x, int y) = _board.Kings[color];
        for (int d = 0; d < 8; d++) {
            (int currX, int currY) = _cardinalDirs[d];
            int i = 1;
            while (true)
            {
                if (InBoard(x+currX*i,y+currY*i))
                {
                    if (EnemySlider(x + currX * i, y + currY * i, d)) // If enemy slider is LOS, you're bad
                        return true;
                    
                    if (IsFriend(x + currX * i, y + currY * i) && !(x + currX * i == meX && y + currY * i == meY)) // If a friend is in the way and its not you, you're good
                        break;

                    if (x+currX*i == toX && y+currY*i == toY) // If the square you are moving to is in the way, you're good
                        break;

                    if (IsEnemy(x + currX * i, y + currY * i) && !(x + currX * i == toX && y + currY * i == meY)) // If a enemy is in the way and its not what you're capturing, you're good
                        break;

                    i++;
                }
                else
                {
                    break;
                }
            }
        }

        return false;
    }

    private bool EnemySlider(int x, int y, int d) // If a location is held by an enemy slider
    {
        int piece = _board.Pieces[x, y];
        
        if (IsEnemy(x, y) && Piece.IsBishop(piece) && d >= 4)
            return true;
            
        if (IsEnemy(x, y) && Piece.IsRook(piece) && d < 4)
            return true;

        if (IsEnemy(x, y) && Piece.IsQueen(piece))
            return true;

        return false;
    }

    private void GenerateAttackableSlider(int x, int y, int start, int end) // Generates attackable paths for sliders
    {
        for (int d = start; d < end; d++) {
            (int currX, int currY) = _cardinalDirs[d];
            int i = 1;
            while (true)
            {
                if (InBoard(x+currX*i,y+currY*i))
                {
                    AddToCheck(x,y,x+currX*i,y+currY*i);
                    if (!IsEmpty(x + currX * i, y + currY * i))
                        break;
                    i++;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void AddToCheck(int x, int y, int toX, int toY) // Calculates check status for attackable position
    {
        if (InBoard(toX, toY))
        {
            Attackable[toX, toY] = true;
            if (IsFriend(toX, toY) && Piece.IsKing(_board.Pieces[toX,toY]))
            {
                _doubleCheck = Check;
                Check = true;
                _checker = (x, y);
            }
        }
    }

    private bool BlocksCheck(int toX, int toY) // Calculates whether a position is in the path of the checker
    {
        if (!Check)
            throw new Exception("Invalid BlocksCheck call");
        int color = _board.WhiteTurn ? 0 : 1;
        (int x,int y) = _board.Kings[color];

        if (x == toX && toX == _checker.x && (y < toY && toY < _checker.y || y > toY && toY > _checker.y))
            return true;
        if (y == toY && toY == _checker.y && (x < toX && toX < _checker.x || x > toX && toX > _checker.x))
            return true;

        if (Math.Abs(toX - x) == Math.Abs(toY - y) && Math.Abs(_checker.x - toX) == Math.Abs(_checker.y - toY))
        {
            if ((y < toY && toY < _checker.y || y > toY && toY > _checker.y) && (x < toX && toX < _checker.x || x > toX && toX > _checker.x))
                return true;
        }
        
        return false;
    }

    private int DirIndexOf(int x, int y) // Converts from cardinal direction to index
    {
        for (int i = 0; i < _cardinalDirs.Length; i++)
        {
            if (_cardinalDirs[i].x == x && _cardinalDirs[i].y == y)
                return i;
        }

        return -1;
    }

    private int OppositeDir(int d) // Index of opposite direction index
    {
        if (d == 0) return 2;
        if (d == 2) return 0;
        if (d == 1) return 3;
        if (d == 3) return 1;
        if (d == 4) return 6;
        if (d == 6) return 4;
        if (d == 5) return 7;
        if (d == 7) return 5;
        throw new Exception("Invalid cardinal direction");
    }

    public static List<Move> SortMoves(List<Move> moves) // Sorts moves based on rough move ordering heuristic
    {
        int[] scores = new int[moves.Count];
        for (int i=0;i<moves.Count; i++)
        {
            scores[i] = Evaluator.EvaluateMove(moves[i]);
        }
        
        for (int i = 0; i < moves.Count - 1; i++) {
            for (int j = i + 1; j > 0; j--) {
                int swap = j - 1;
                if (scores[swap] < scores[j]) {
                    (moves[j], moves[swap]) = (moves[swap], moves[j]);
                    (scores[j], scores[swap]) = (scores[swap], scores[j]);
                }
            }
        }
        
        return moves;
    }

    
    
    
    // Perft statistics
    
    private int _nodes;
    private int _captures;
    private int _eps;
    private int _castles;
    private int _promotions;
    
    public void PerftPrint() // Prints perft values
    {
        Debug.Log("Moves:" + _nodes + ",caps:" + _captures + ",eps:" + _eps + ",cast:" + _castles + ",prom:" + _promotions);
    }
    
    public void PerftReset() // Resets perft values
    {
        _nodes = 0;
        _captures = 0;
        _eps = 0;
        _castles = 0;
        _promotions = 0;
    }
    
    public void PerftAdd(List<Move> mvs) // Adds move list to perft stats
    {
        _nodes += mvs.Count;
        foreach (var m in mvs)
        {
            if (IsEnemy(m.ToX,m.ToY))
                _captures++;

            switch (m.flag)
            {
                case Move.Flag.EnPassant:
                    _captures++; 
                    _eps++;
                    break;
                case Move.Flag.Castle:
                    _castles++;
                    break;
                case Move.Flag.PromoteKnight:
                    _promotions++;
                    break;
                case Move.Flag.PromoteRook:
                    _promotions++;
                    break;
                case Move.Flag.PromoteBishop:
                    _promotions++;
                    break;
                case Move.Flag.PromoteQueen:
                    _promotions++;
                    break;
                    
            }
        }
    }
}
