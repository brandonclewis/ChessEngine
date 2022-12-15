using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Game : MonoBehaviour
{
    public enum PlayerType // Player enum
    {
        Human,
        Minimax,
        MonteCarlo,
        Stockfish
    }

    public enum Result // Game status enum
    {
        Ongoing,
        WhiteWins,
        BlackWins,
        Draw
    }
    
    public Player whitePlayer; // White player instance
    public Player blackPlayer; // Black player instance
    public PlayerType whitePlayerType; // White player type
    public PlayerType blackPlayerType; // Black player type
    public Result result; // Current game status
    public bool rand3; // First 3 moves random
    public bool runTests; // Run test suite instead of game Ui
    public int testGames; // Number of games in test suite
    
    private BoardUi _ui; // Ui
    private Moves _moves; // Move generator instance
    
    private int _whiteWins = 0; // Test suite # of white wins
    private int _blackWins = 0; // Test suite # of black wins
    private int _numGames = 0; // Test suite # of games

    private void Start() // Start is called before the first frame update
    {
        // Initialize values
        result = Result.Ongoing;
        _ui = FindObjectOfType<BoardUi> ();
        _moves = new Moves();
        
        // Hide game and randomize first 3 moves if in test suite
        if (runTests)
        {
            rand3 = true;
            _ui.DisableUI();
        }

        // Create white player
        switch (whitePlayerType)
        {
            case PlayerType.Human:
                whitePlayer = new HumanPlayer(_ui);
                break;
            case PlayerType.Minimax:
                whitePlayer = new EnginePlayerMinimax(_ui);
                break;
            case PlayerType.MonteCarlo:
                whitePlayer = new EnginePlayerMonteCarlo(_ui);
                break;
            case PlayerType.Stockfish:
                whitePlayer = new Stockfish(_ui);
                break;
        }
        
        // Create black player
        switch (blackPlayerType)
        {
            case PlayerType.Human:
                blackPlayer = new HumanPlayer(_ui);
                break;
            case PlayerType.Minimax:
                blackPlayer = new EnginePlayerMinimax(_ui);
                break;
            case PlayerType.MonteCarlo:
                blackPlayer = new EnginePlayerMonteCarlo(_ui);
                break;
            case PlayerType.Stockfish:
                blackPlayer = new Stockfish(_ui);
                break;
        }

        // Add players to game
        MakePlayer(ref whitePlayer);
        MakePlayer(ref blackPlayer);

        // Rnadomizes first 3 moves for each side
        if (rand3)
        {
            System.Random r = new System.Random((int)System.DateTime.Now.Ticks);
            for (int i = 0; i < 6; i++)
            {
                List<Moves.Move> m = _moves.GenerateValidMoves(_ui.board);
                _ui.MovePiece(m[r.Next(m.Count)]);
            }
        }
        
        // Call white side to play
        whitePlayer.Notify();
    }

    private void Update() // Update is called once per frame
    {
        // Call override update functions
        if (result == Result.Ongoing)
        {
            if (_ui.board.WhiteTurn)
                whitePlayer.Update();
            else
                blackPlayer.Update();
        }
        
        // Algorithm test suite
        if (runTests)
        {
            if (result != Result.Ongoing)
            {
                _numGames++;
                if (result == Result.BlackWins) _blackWins++;
                if (result == Result.WhiteWins) _whiteWins++;
                if (_numGames < testGames)
                {
                    _ui.Start();
                    Start();
                }
                else
                {
                    runTests = false;
                    Debug.Log("#GAMES: " + _numGames + ", #WHITE WINS: " + _whiteWins + ", #BLACK WINS: " + _blackWins + ", #DRAWS: " + (_numGames - _whiteWins - _blackWins));
                }
            }
        }
    }

    private void PlayMove(Moves.Move move) // Makes move and then asks for next move
    {
        _ui.MovePiece(move);
        result = CheckResult();

        if (result == Result.Ongoing)
        {
            if (_ui.board.WhiteTurn)
                whitePlayer.Notify();
            else
                blackPlayer.Notify();
        }
    }

    private Result CheckResult() // Get current game outcome
    {
        List<Moves.Move> validMoves = _moves.GenerateValidMoves(_ui.board);
        if (validMoves.Count == 0)
        {
            if (_moves.Check)
            {
                print(_ui.board.WhiteTurn ? Result.BlackWins : Result.WhiteWins);
                return _ui.board.WhiteTurn ? Result.BlackWins : Result.WhiteWins;
            }
            print("Stalemate");
            return Result.Draw;
        }
        if (_ui.board.HalfMove >= 100)
        {
            print("Fifty move draw");
            return Result.Draw;
        }

        if (_ui.board.Repetition)
        {
            print("Repetition");
            return Result.Draw;
        }
        return Result.Ongoing;
    }

    private void MakePlayer(ref Player player) // Add player to events
    {
        player.MoveSelected += PlayMove;
    }
}
