using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;
using Debug = UnityEngine.Debug;

public class Stockfish : Player
{
    private readonly BoardUi _ui; // UI
    private readonly Process _stockfishProcess; // Process for Stockfish application
    private Moves.Move _move; // Current move
    private bool _moveFound; // Move search status
    
    private const int MoveTime = 100; // Time to make move

    public Stockfish(BoardUi ui) // Constructor
    {
        _ui = ui;
        
        _stockfishProcess = new Process();
        _stockfishProcess.StartInfo.FileName = Application.dataPath + "/stockfishExecutable.exe";
        _stockfishProcess.StartInfo.UseShellExecute = false;
        _stockfishProcess.StartInfo.RedirectStandardInput = true;
        _stockfishProcess.StartInfo.RedirectStandardOutput = true;
        _stockfishProcess.StartInfo.CreateNoWindow = true;
        _move = new Moves.Move(-1, -1, -1, -1,0,0);
    }

    public override void Update() // Update is called once per frame
    {
        if (_moveFound)
        {
            _moveFound = false;
            MovePiece(_move);
            _move = new Moves.Move(-1, -1, -1, -1,0,0);
        }
    }

    void BeginSearch() // Initializes the search
    {
        string fen = GetFen();
        string mString = GetBestMove(fen);
        Moves.Move m = new Moves.Move(mString[9] - 'a',mString[10] - '0' - 1,mString[11] - 'a',mString[12] - '0' - 1);
        if (!_ui.moves.IsValid(m.StartX, m.StartY, m.ToX, m.ToY, out _move))
            return;
        _moveFound = true;
    }
    
    private string GetFen() // Gets fen string of current board state
    {
        string fen = "";
        
        for (int y = 7; y >= 0; y--)
        {
            int spacer = 0;
            for (int x = 0; x < 8; x++)
            {
                if (_ui.board.Pieces[x, y] == 0)
                {
                    spacer++;
                }
                else
                {
                    if (spacer != 0)
                        fen += spacer.ToString();
                    spacer = 0;
                }
                fen += Piece.ToFen(_ui.board.Pieces[x,y]);
            }
            if (spacer != 0)
                fen += spacer.ToString();
            fen += "/";
        }

        fen += " ";
        fen += _ui.board.WhiteTurn ? "w" : "b";
        fen += " ";
        if (_ui.board.RookMoved[1, 0] == -1)
            fen += "K";
        if (_ui.board.RookMoved[0, 0] == -1)
            fen += "Q";
        if (_ui.board.RookMoved[1, 1] == -1)
            fen += "k";
        if (_ui.board.RookMoved[0, 1] == -1)
            fen += "q";
        if (fen[fen.Length - 1] == ' ')
            fen += "-";
        fen += " ";
        if (_ui.board.Passant.x == -2 && _ui.board.Passant.y == -2)
            fen += "-";
        else
            fen += (char) (_ui.board.Passant.x + 'a') + "" + (_ui.board.Passant.y + 1);
        fen += " 1 1 "; // TODO: change this later
        return fen;
    }

    string GetBestMove(string fen) // Gets move from Stockfish output
    {
        _stockfishProcess.Start();
        string setupString = "position fen "+fen;
        _stockfishProcess.StandardInput.WriteLine(setupString);
        // Process for 5 seconds
        string processString = "go movetime " + MoveTime;
    
        // Process 20 deep
        // string processString = "go depth 20";
         
        _stockfishProcess.StandardInput.WriteLine(processString);
        Thread.Sleep(MoveTime);
        
        string bm = _stockfishProcess.StandardOutput.ReadLine();
        
        while (!bm.Substring(0,4).Equals("best"))
        {
            bm = _stockfishProcess.StandardOutput.ReadLine();
        }
        _stockfishProcess.Close();
        
        return bm;
    }
    
    public override void Notify () // Event to begin search
    {
        _moveFound = false;
        if(_move.StartX == -1)
            BeginSearch();
    }
}