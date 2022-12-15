using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;

public class EnginePlayerMinimax : Player
{
    private readonly BoardUi _ui; // Game UI
    private readonly SearchMinimax _search; // Minimax search instance
    private Moves.Move _move; // Current move
    private bool _moveFound; // Move search status

    public EnginePlayerMinimax(BoardUi ui) // Constructor
    {
        _ui = ui;
        _search = new SearchMinimax (_ui.board);
        _search.OnSearchComplete += OnSearchComplete;
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
    
    void BeginSearch () // Initializes the search
    {
        _search.BeginSearch();
        _moveFound = true;
    }

    void OnSearchComplete (Moves.Move m) // Updates _move and _moveFound on event call
    {
        _move = m;
        _moveFound = true;
    }
    
    public override void Notify () // Event to begin search
    {
        _moveFound = false;
        if(_move.StartX == -1)
            BeginSearch();
    }
}