﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor.Experimental.GraphView;
using UnityEditor.PackageManager;
using Debug = UnityEngine.Debug;

public class Board
{
    public int TurnNumber = 0; // Internal ply number
    public int HalfMove; // Half move counter (since last pawn)
    public int FullMove; // Full move counter
    public bool WhiteTurn = true; // Current turn color
    public bool Repetition = false; // Repetition draw status
    
    public readonly HashSet<(int x, int y)>[] Pawns; // Pawn location hash set by color
    public readonly HashSet<(int x, int y)>[] Knights; // Knight location hash set by color
    public readonly HashSet<(int x, int y)>[] Bishops; // Bishop location hash set by color
    public readonly HashSet<(int x, int y)>[] Rooks; // Rook location hash set by color
    public readonly HashSet<(int x, int y)>[] Queens; // Queen location hash set by color
    public readonly (int x, int y)[] Kings; // King locations by color

    public readonly int[,] RookMoved; // Castling status for each corner
    public readonly int[,] Pieces; // Piece array
    public (int x, int y) Passant; // Last turn passant location, (-2,-2) if N/A
    
    private readonly List<(int x, int y, int t)> _passants; // History of passant locations and turn number
    private readonly List<(int hm, int t)> _halfMoves; // History of half move timer

    private readonly Dictionary<ulong, uint> _zobristMap; // Past board hashes and their number of occurences
    private readonly List<ulong> _zobristHist; // History of past board hashes
    
    public Board() // Constructor
    {
        Zobrist.Init();
        Pawns = new HashSet<(int x, int y)>[2];
        Knights = new HashSet<(int x, int y)>[2];
        Bishops = new HashSet<(int x, int y)>[2];
        Rooks = new HashSet<(int x, int y)>[2];
        Queens = new HashSet<(int x, int y)>[2];
        Kings = new (int x, int y)[2];
        Passant = (-2, -2);
        _passants = new List<(int x, int y, int t)>();
        _halfMoves = new List<(int hm, int t)>();
        Pieces = new int[8, 8];
        RookMoved = new [,] {{-1,-1},{-1,-1}};
        _zobristMap = new Dictionary<ulong, uint>();
        _zobristHist = new List<ulong>();
        GenerateLists();
    }
    
    public void LoadFen(string fenText) // Load board via fen string representation
    {
        if (fenText.Equals(""))
            fenText = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        string[] spl = fenText.Split(' ');

        // Load board
        char[] arr = spl[0].ToCharArray();
        int x = 0;
        int y = 7;
        for (int i = 0; i < spl[0].Length; i++)
        {
            if (arr[i] == '/')
            {
                x = 0;
                y--;
            }
            else
            {
                if (Char.IsDigit(arr[i]))
                {
                    x += (arr[i]-'0') - 1;
                }
                else
                {
                    Pieces[x, y] = Piece.FromFen(arr[i]);
                    AddToList(Pieces[x,y], x, y);
                }
                
                x++;
            }
        }

        // Active turn
        WhiteTurn = spl[1].Equals("w");

        // Castling
        if (!spl[2].Contains("K"))
            RookMoved[1,0] = -2;
        if (!spl[2].Contains("Q"))
            RookMoved[0,0] = -2;
        if (!spl[2].Contains("k"))
            RookMoved[1,1] = -2;
        if (!spl[2].Contains("q"))
            RookMoved[0,1] = -2;

        // En-passant
        if (spl[3].Equals("-"))
            Passant = (-2, -2);
        else
        {
            Passant = (spl[3][0] - 'a', spl[3][1] - '1');
            _passants.Add((spl[3][0] - 'a', spl[3][1] - '1',0));
        }

        // Clocks
        HalfMove = Int32.Parse(spl[4]);
        FullMove = Int32.Parse(spl[5]);

        ulong zobrist = Zobrist.Hash(this);
        _zobristHist.Add(zobrist);
        _zobristMap.Add(zobrist,1);
    }

    private void GenerateLists() // Create hash sets
    {
        for (int i = 0; i < 2; i++)
        {
            Pawns[i] = new HashSet<(int x, int y)>();
            Knights[i] = new HashSet<(int x, int y)>();
            Bishops[i] = new HashSet<(int x, int y)>();
            Rooks[i] = new HashSet<(int x, int y)>();
            Queens[i] = new HashSet<(int x, int y)>();
            Kings[0] = (-2, -2);
            Kings[1] = (-2, -2);
        }
    }
    
    private void AddToList(int p, int x, int y) // Add piece to hash sets
    {
        int color = Piece.IsWhite(p) ? 0 : 1;
        p = p & 0b00111;
        if (p == Piece.Pawn)
            Pawns[color].Add((x,y));
        if (p == Piece.Knight)
            Knights[color].Add((x,y));
        if (p == Piece.Bishop)
            Bishops[color].Add((x,y));
        if (p == Piece.Rook)
            Rooks[color].Add((x,y));
        if (p == Piece.Queen)
            Queens[color].Add((x,y));
        if (p == Piece.King)
            Kings[color] = (x, y);
    }
    
    private void RemoveFromList(int p, int x, int y) // Remove piece from hash sets
    {
        int color = Piece.IsWhite(p) ? 0 : 1;
        p = p & 0b00111;
        if (p == Piece.Pawn)
            Pawns[color].Remove((x,y));
        if (p == Piece.Knight)
            Knights[color].Remove((x, y));
        if (p == Piece.Bishop)
            Bishops[color].Remove((x,y));
        if (p == Piece.Rook)
            Rooks[color].Remove((x,y));
        if (p == Piece.Queen)
            Queens[color].Remove((x,y));
        if (p == Piece.King)
            return;
    }
    
    public void MovePiece(Moves.Move m, bool search = false) // Make move
    {
        int color = WhiteTurn ? 0 : 1;
        int startPiece = Pieces[m.StartX, m.StartY];
        bool pawn = Piece.IsPawn(startPiece);
        
        // King moved
        if (Piece.IsKing(startPiece) && m.StartX == 4 && m.StartY == (WhiteTurn ? 0 : 7))
        {
            if (RookMoved[0, color] == -1) RookMoved[0, color] = TurnNumber;
            if (RookMoved[1, color] == -1) RookMoved[1, color] = TurnNumber;
        }

        // King-side castle
        if (m.flag == Moves.Move.Flag.Castle && m.ToX - m.StartX == 2)
        {
            // Move rook
            RemoveFromList(Pieces[7, m.ToY], 7, m.ToY);
            AddToList(Pieces[7, m.ToY], 5, m.ToY);
            Pieces[5, m.ToY] = Pieces[7, m.ToY];
            Pieces[7, m.ToY] = 0;
            RookMoved[1,color] = TurnNumber;
        }
        
        // Queen-side castle
        if (m.flag == Moves.Move.Flag.Castle && m.StartX - m.ToX == 2)
        {
            // Move rook
            RemoveFromList(Pieces[0, m.ToY], 0, m.ToY);
            AddToList(Pieces[0, m.ToY], 3, m.ToY);
            Pieces[3, m.ToY] = Pieces[0, m.ToY];
            Pieces[0, m.ToY] = 0;
            RookMoved[0, color] = TurnNumber;
        }
        
        // Pawn promotion
        if (m.flag == Moves.Move.Flag.PromoteKnight || m.flag == Moves.Move.Flag.PromoteBishop || m.flag == Moves.Move.Flag.PromoteRook || m.flag == Moves.Move.Flag.PromoteQueen)
        {
            RemoveFromList(startPiece, m.StartX, m.StartY);
            Pieces[m.StartX, m.StartY] = (WhiteTurn ? Piece.White : Piece.Black) | Piece.ParsePromotion(m);
            AddToList(Pieces[m.StartX, m.StartY], m.StartX, m.StartY);
        }
        
        // En-passant capture
        if (m.flag == Moves.Move.Flag.EnPassant)
        {
            RemoveFromList(m.Capture, m.ToX, m.StartY);
            Pieces[m.ToX, m.StartY] = 0;
        }
        
        // Keep track of last pawn double move for en-passant
        if (m.flag == Moves.Move.Flag.MoveTwo)
        {
            Passant = (m.ToX, (m.StartY + m.ToY) / 2);
            _passants.Add((m.ToX,(m.StartY + m.ToY) / 2, TurnNumber));
        }
        else
            Passant = (-2, -2);

        // Rook movement for castling
        if (Piece.IsRook(startPiece))
        {
            if (m.StartX == 0 && m.StartY == 0 && RookMoved[0,0] == -1)
                RookMoved[0, 0] = TurnNumber;
            if (m.StartX == 0 && m.StartY == 7 && RookMoved[0,1] == -1)
                RookMoved[0, 1] = TurnNumber;
            if (m.StartX == 7 && m.StartY == 0 && RookMoved[1,0] == -1)
                RookMoved[1, 0] = TurnNumber;
            if (m.StartX == 7 && m.StartY == 7 && RookMoved[1,1] == -1)
                RookMoved[1, 1] = TurnNumber;
        }
        
        // Remove captured piece
        if (m.Capture != 0 && m.flag != Moves.Move.Flag.EnPassant)
        {
            RemoveFromList(m.Capture, m.ToX, m.ToY);
            if (Piece.IsRook(m.Capture))
            {
                if (m.ToX == 0 && m.ToY == 0 && RookMoved[0,0] == -1)
                    RookMoved[0,0] = TurnNumber;
                if (m.ToX == 7 && m.ToY == 0 && RookMoved[1,0] == -1)
                    RookMoved[1,0] = TurnNumber;
                if (m.ToX == 0 && m.ToY == 7 && RookMoved[0,1] == -1)
                    RookMoved[0,1] = TurnNumber;
                if (m.ToX == 7 && m.ToY == 7 && RookMoved[1,1] == -1)
                    RookMoved[1,1] = TurnNumber;
            }
        }

        // Update piece lists
        RemoveFromList(Pieces[m.StartX, m.StartY], m.StartX, m.StartY);
        AddToList(Pieces[m.StartX, m.StartY], m.ToX, m.ToY);
        
        // Update piece array
        Pieces[m.ToX, m.ToY] = Pieces[m.StartX, m.StartY];
        Pieces[m.StartX, m.StartY] = 0;

        // Change turns
        if (!WhiteTurn) FullMove++;
        WhiteTurn = !WhiteTurn;
        TurnNumber++;

        // Update half move counter
        if (pawn || m.Capture != 0)
        {
            _halfMoves.Add((HalfMove, TurnNumber-1));
            HalfMove = 0;
        }
        else
            HalfMove++;

        // TODO: This can be optimized by keepin a running hash and just XORing based on the move
        // Zobrist hashes
        ulong zobrist = Zobrist.Hash(this);
        if(_zobristMap.ContainsKey(zobrist))
            _zobristMap[zobrist] += 1;
        else
            _zobristMap.Add(zobrist,1);
        _zobristHist.Add(zobrist);
        
        if (_zobristMap[zobrist] >= 3)
            Repetition = true;
    }
    
    public void UnMovePiece(Moves.Move m, bool search = false) // Unmake move
    {
        // Reset repetition flag
        if (Repetition)
            Repetition = false;
        
        // Undo hashes
        if (_zobristHist.Count > 0)
        {
            ulong zobrist = _zobristHist[_zobristHist.Count - 1];
            if (_zobristMap[zobrist] > 1)
                _zobristMap[zobrist] -= 1;
            else
                _zobristMap.Remove(zobrist);
            _zobristHist.RemoveAt(_zobristHist.Count-1);
        }
        
        // Reset turn info
        if (WhiteTurn) FullMove--;
        WhiteTurn = !WhiteTurn;
        TurnNumber--;
   
        // Reset halfmove counter
        if (HalfMove == 0)
        {
            if (_halfMoves.Count != 0)
            {
                (int hm, int t) = _halfMoves[_halfMoves.Count - 1];
                if (t == TurnNumber)
                    HalfMove = hm;
                _halfMoves.RemoveAt(_halfMoves.Count - 1);
            }
        }
        else
            HalfMove--;


        // Reset double moves
        if (m.flag == Moves.Move.Flag.MoveTwo)
        {
            Passant = (-2, -2);
            _passants.RemoveAt(_passants.Count - 1);
        }

        // Reset castling timer
        if (RookMoved[0, 0] == TurnNumber)
            RookMoved[0, 0] = -1;
        if (RookMoved[1, 0] == TurnNumber)
            RookMoved[1, 0] = -1;
        if (RookMoved[0, 1] == TurnNumber)
            RookMoved[0, 1] = -1;
        if (RookMoved[1, 1] == TurnNumber)
            RookMoved[1, 1] = -1;
        
        // Keep track of last passant
        if (_passants.Count != 0)
        {
            (int x, int y, int t) = _passants[_passants.Count - 1];
            if (t == TurnNumber-1)
                Passant = (x, y);
        }

        // Uncastle
        if (m.flag == Moves.Move.Flag.Castle)
        {
            if (m.ToX == 6)
            {
                RemoveFromList(Pieces[5, m.ToY], 5,m.ToY);
                Pieces[7, m.ToY] = Pieces[5, m.ToY];
                Pieces[5, m.ToY] = 0;
                AddToList(Pieces[7, m.ToY],7,m.ToY);
            }
            
            if (m.ToX == 2)
            {
                RemoveFromList(Pieces[3, m.ToY], 3,m.ToY);
                Pieces[0, m.ToY] = Pieces[3, m.ToY];
                Pieces[3, m.ToY] = 0;
                AddToList(Pieces[0, m.ToY],0,m.ToY);
            }
        }

        // Fix lists & arrays
        Pieces[m.StartX, m.StartY] = Pieces[m.ToX, m.ToY];
        AddToList(Pieces[m.StartX, m.StartY],m.StartX,m.StartY);
        RemoveFromList(Pieces[m.ToX, m.ToY], m.ToX,m.ToY);
        if (m.flag != Moves.Move.Flag.EnPassant)
        {
            Pieces[m.ToX, m.ToY] = m.Capture;
            AddToList(m.Capture,m.ToX,m.ToY);
        } else {
            Pieces[m.ToX, m.StartY] = m.Capture;
            AddToList(m.Capture,m.ToX,m.StartY);
            Pieces[m.ToX, m.ToY] = 0;
        }

        // Revert promotions
        if (m.flag == Moves.Move.Flag.PromoteKnight || m.flag == Moves.Move.Flag.PromoteBishop
            || m.flag == Moves.Move.Flag.PromoteRook || m.flag == Moves.Move.Flag.PromoteQueen)
        {
            RemoveFromList(Pieces[m.StartX, m.StartY], m.StartX,m.StartY);
            Pieces[m.StartX, m.StartY] = (WhiteTurn ? Piece.White : Piece.Black) | Piece.Pawn;
            AddToList(Pieces[m.StartX,m.StartY],m.StartX,m.StartY);
        }
    }
}
