using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.Experimental.GraphView;
using UnityEditor.PackageManager;
using UnityEngine;
using Object = System.Object;

public class BoardUi : MonoBehaviour
{
    public Moves moves; // Move generator instance

    public Color whiteColor; // Color of white tile
    public Color blackColor; // Color of black tile
    public Color validWhiteColor; // Color of white tile for valid move
    public Color validBlackColor; // Color of black tile for valid move
    public Color originWhiteColor; // Color of white tile you are moving from
    public Color originBlackColor; // Color of black tile you are moving from

    [Range(0.0f, 1.0f)]
    public float localScale = 0.12f; // Piece sprite scale
    public string fen = ""; // Starting board position in fen notation
    public bool debug; // Debug Ui state

    public Board board; // Game board
    
    private MeshRenderer[,] _tileRenderers; // Array of tile quad renderers
    private SpriteRenderer[,] _pieceRenderers; // Array of piece sprite renderers
    private List<Moves.Move> _lastMoves; // Move history
    
    private bool _noUi; // Ui disabled
    
    private const float GrabDepth = -2f; // Z-depth of piece in player's hand
    private const float PieceDepth = -1f; // Z-depth of pieces on the board
    
    public void Start() // Start is called before the first frame update
    {
        DrawBoard();
        board = new Board();
        moves = new Moves();
        board.LoadFen(fen);
        DrawPieces();
        moves.GenerateValidMoves(board);

        _lastMoves = new List<Moves.Move>();
    }

    void Update() // Update is called once per frame
    {

    }
    
    private void DrawBoard() // Renders board
    {
        if (_noUi) return;
        
        Shader tileShader = Shader.Find ("Unlit/Color");
        _tileRenderers = new MeshRenderer[8, 8];
        _pieceRenderers = new SpriteRenderer[8, 8];
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                bool white = (x + y) % 2 != 0;
                Vector2 pos = new Vector3(-3.5f + x, -3.5f + y,PieceDepth);
                
                Transform tile = GameObject.CreatePrimitive (PrimitiveType.Quad).transform;
                tile.name = "Tile [" + x + "," + y + "]";
                tile.transform.position = pos;
                tile.parent = transform;
                Material tileMat = new Material (tileShader);
                
                _tileRenderers[x, y] = tile.gameObject.GetComponent<MeshRenderer> ();
                _tileRenderers[x, y].material = tileMat;
                _tileRenderers[x, y].material.color = white ? whiteColor : blackColor;
                
                SpriteRenderer pieceRenderer = new GameObject ("Piece [" + x + "," + y + "]").AddComponent<SpriteRenderer> ();
                Transform pieceTrans = pieceRenderer.transform;
                pieceTrans.parent = tile;           
                pieceTrans.position = pos;
                pieceTrans.localScale = Vector3.one * localScale;
                _pieceRenderers[x, y] = pieceRenderer;
            }
        }
    }

    private void RecolorBoard() // Sets board to default colors
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                _tileRenderers[x, y].material.color = ((x + y) % 2 != 0) ? whiteColor : blackColor;
            }
        }
    }
    
    private void RecolorBoardDebug() // Sets board to hidden debug colors
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if(moves.Attackable[x,y])
                    _tileRenderers[x, y].material.color = ((x + y) % 2 != 0) ? Color.cyan : Color.blue;
                else
                    _tileRenderers[x, y].material.color = ((x + y) % 2 != 0) ? whiteColor : blackColor;
                
                if(moves.Pinned[x,y])
                    _tileRenderers[x, y].material.color = ((x + y) % 2 != 0) ? Color.yellow : Color.green;
            }
        }
    }

    private void DrawPieces() // Draws all piece sprites
    {
        if (_noUi) return;
        
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
                _pieceRenderers[x, y].sprite = board.Pieces[x, y] == 0 ? null : Piece.GetSprite(board.Pieces[x, y]);
        }
    }
    
    public void GrabPiece(int x, int y, Vector2 pos) // Moves piece to cursor position
    {
        if (board.Pieces[x, y] != 0)
        {
            _pieceRenderers[x, y].transform.position = new Vector3(pos.x, pos.y, GrabDepth);
            List<Moves.Move> vm = moves.ValidMovesTo(x, y);
            foreach (Moves.Move m in vm)
            {
                _tileRenderers[m.StartX, m.StartY].material.color = ((m.StartX + m.StartY) % 2 != 0) ? originWhiteColor : originBlackColor;
                _tileRenderers[m.ToX, m.ToY].material.color = ((m.ToX + m.ToY) % 2 != 0) ? validWhiteColor : validBlackColor;
            }
        }
    }

    public void ResetPiece(int x, int y) // Resets piece to default position
    {
        _pieceRenderers[x, y].transform.position = new Vector3(-3.5f + x, -3.5f + y, PieceDepth);
    }

    public void MovePiece(Moves.Move m) // Make move
    {
        if (_noUi)
        {
            board.MovePiece(m);
            moves.GenerateValidMoves(board);
            _lastMoves.Add(m);
            return;
        }
        
        // Change board color & move sprite back to original spot
        if (!debug)
            RecolorBoard();
        else
            RecolorBoardDebug();
        ResetPiece(m.StartX, m.StartY);
        
        // Check if move is valid
        if (!moves.IsValid(m.StartX, m.StartY, m.ToX, m.ToY, out Moves.Move m2))
            return;
        m = m2;
        
        // En-passant capture
        if ((board.Pieces[m.StartX, m.StartY] & 0b00111) == Piece.Pawn && board.Passant == (m.ToX, m.ToY))
            _pieceRenderers[m.ToX, m.StartY].sprite = null;

        // King-side castle
        if ((board.Pieces[m.StartX, m.StartY] & 0b00111) == Piece.King && m.ToX - m.StartX == 2)
        {
            _pieceRenderers[5, m.ToY].sprite = _pieceRenderers[7, m.ToY].sprite;
            _pieceRenderers[7, m.ToY].sprite = null;
        }

        // Queen-side castle
        if ((board.Pieces[m.StartX, m.StartY] & 0b00111) == Piece.King && m.StartX - m.ToX == 2)
        {
            _pieceRenderers[3, m.ToY].sprite = _pieceRenderers[0, m.ToY].sprite;
            _pieceRenderers[0, m.ToY].sprite = null;
        }

        // Pawn promotion
        if ((board.Pieces[m.StartX, m.StartY] & 0b00111) == Piece.Pawn && m.ToY == (board.WhiteTurn ? 7 : 0))
        {
            _pieceRenderers[m.StartX, m.StartY].sprite =
                Piece.GetSprite((board.WhiteTurn ? Piece.White : Piece.Black) | Piece.ParsePromotion(m));
        }

        // Move sprites
        _pieceRenderers[m.ToX, m.ToY].sprite = _pieceRenderers[m.StartX, m.StartY].sprite;
        _pieceRenderers[m.StartX, m.StartY].sprite = null;

        // Update board
        board.MovePiece(m);
        moves.GenerateValidMoves(board);
        
        _lastMoves.Add(m);
    }

    public void UnMovePiece() // Unmake move
    {
        if (_lastMoves.Count != 0)
        {
            board.UnMovePiece(_lastMoves[_lastMoves.Count - 1]);
            _lastMoves.RemoveAt(_lastMoves.Count - 1);
            DrawPieces();
            moves.GenerateValidMoves(board);
        }
    }

    public void DisableUI()
    {
        if (!_noUi)
        {
            _noUi = true;
            foreach (MeshRenderer m in _tileRenderers)
                Destroy(m);
            foreach (SpriteRenderer s in _pieceRenderers)
                Destroy(s.transform.parent.gameObject);
            _tileRenderers = null;
            _pieceRenderers = null;
        }
    }
}
