using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using UnityEngine;

public static class Piece
{
    // Bit values
    public const int Empty = 0;
    public const int King = 1;
    public const int Pawn = 2;
    public const int Knight = 3;
    public const int Bishop = 5;
    public const int Rook = 6;
    public const int Queen = 7;
    public const int White = 8;
    public const int Black = 16;

    // Bit masks
    private const int ColorMask = 0b11000;
    private const int TypeMask = 0b00111;
    
    // Color helper functions
    public static bool IsWhite(int piece)
    {
        return (piece & ColorMask) == White;
    }
    public static bool IsBlack(int piece)
    {
        return (piece & ColorMask) == Black;
    }

    // Piece type helper functions
    public static bool IsPawn(int piece)
    {
        return (piece & TypeMask) == Pawn;
    }
    public static bool IsBishop(int piece)
    {
        return (piece & TypeMask) == Bishop;
    }
    public static bool IsKnight(int piece)
    {
        return (piece & TypeMask) == Knight;
    }
    public static bool IsRook(int piece)
    {
        return (piece & TypeMask) == Rook;
    }
    public static bool IsQueen(int piece)
    {
        return (piece & TypeMask) == Queen;
    }
    public static bool IsKing(int piece)
    {
        return (piece & TypeMask) == King;
    }

    public static int Zobrist(int p) // Gets zobrist index for piece
    {
        if (p == (White | Pawn)) return 0;
        if (p == (Black | Pawn)) return 1;

        if (p == (White | Knight)) return 2;
        if (p == (Black | Knight)) return 3;

        if (p == (White | Bishop)) return 4;
        if (p == (Black | Bishop)) return 5;
        
        if (p == (White | Rook)) return 6;
        if (p == (Black | Rook)) return 7;

        if (p == (White | Queen)) return 8;
        if (p == (Black | Queen)) return 9;
        
        if (p == (White | King)) return 10;
        if (p == (Black | King)) return 11;

        throw new Exception("ERROR: not a piece");
    }
    
    public static string ToFen(int p) // Gets fen notation letter for piece
    {
        if (p == (White | Pawn)) return "P";
        if (p == (Black | Pawn)) return "p";
        
        if (p == (White | Knight)) return "N";
        if (p == (Black | Knight)) return "n";

        if (p == (White | Bishop)) return "B";
        if (p == (Black | Bishop)) return "b";
        
        if (p == (White | Rook)) return "R";
        if (p == (Black | Rook)) return "r";
        
        if (p == (White | Queen)) return "Q";
        if (p == (Black | Queen)) return "q";
        
        if (p == (White | King)) return "K";
        if (p == (Black | King)) return "k";

        return "";
    }
    
    public static int FromFen(char fen) // Gets piece value for fen notation letter
    {
        if (fen == 'P') return White | Pawn;
        if (fen == 'p') return Black | Pawn;
        
        if (fen == 'N') return White | Knight;
        if (fen == 'n') return Black | Knight;
        
        if (fen == 'B') return White | Bishop;
        if (fen == 'b') return Black | Bishop;
        
        if (fen == 'R') return White | Rook;
        if (fen == 'r') return Black | Rook;
        
        if (fen == 'Q') return White | Queen;
        if (fen == 'q') return Black | Queen;
        
        if (fen == 'K') return White | King;
        if (fen == 'k') return Black | King;

        return Empty;
    }

    public static Sprite GetSprite(int p) // Gets sprite name for piece
    {
        string texString = "";
        if (p == (White | Pawn)) texString = "WPawn";
        if (p == (Black | Pawn)) texString = "BPawn";
        
        if (p == (White | Knight)) texString = "WKnight";
        if (p == (Black | Knight)) texString = "BKnight";
        
        if (p == (White | Bishop)) texString = "WBishop";
        if (p == (Black | Bishop)) texString = "BBishop";
        
        if (p == (White | Rook)) texString = "WRook";
        if (p == (Black | Rook)) texString = "BRook";
        
        if (p == (White | Queen)) texString = "WQueen";
        if (p == (Black | Queen)) texString = "BQueen";
        
        if (p == (White | King)) texString = "WKing";
        if (p == (Black | King)) texString = "BKing";

        if (texString == "") return null;
            
        var tex = Resources.Load(texString) as Texture2D;
        if (tex is null) return null; 
        var rec = new Rect(0, 0, tex.width, tex.height);
        return Sprite.Create(tex, rec, new Vector2(0.5f,0.5f));
    }
    
    public static string GetType(int p) // Gets name for piece
    {
        p = 0b00111 & p;
        if (p == Pawn) return "Pawn";
        if (p == Knight) return "Knight";
        if (p == Bishop) return "Bishop";
        if (p == Rook) return "Rook";
        if (p == Queen) return "Queen";
        if (p == King) return "King";
        throw new Exception("Invalid piece");
    }
    
    public static int ParsePromotion(Moves.Move m) // Gets piece type from move promotion
    {
        int flag = m.flag;
        if (flag == Moves.Move.Flag.PromoteKnight) return Knight;
        if (flag == Moves.Move.Flag.PromoteBishop) return Bishop;
        if (flag == Moves.Move.Flag.PromoteRook) return Rook;
        if (flag == Moves.Move.Flag.PromoteQueen) return Queen;
        return 0;
    }
}
