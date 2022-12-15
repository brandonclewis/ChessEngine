using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading.Tasks;
using System.Threading;

public class EnginePlayerMonteCarlo : Player
{
    private readonly BoardUi _ui; // Game UI
    private readonly SearchMonteCarlo _search; // Monte Carlo tree search instance
    private Moves.Move _move; // Current move
    private bool _moveFound; // Move search status

    public EnginePlayerMonteCarlo(BoardUi ui) // Constructor
    {
        _ui = ui;
        _search = new SearchMonteCarlo(_ui.board);
        _search.OnSearchComplete += OnSearchComplete;
        _move = new Moves.Move(-1, -1, -1, -1);
    }

    public override void Update() // Update is called once per frame
    {
        if (_moveFound)
        {
            _moveFound = false;
            MovePiece(_move);
            _move = new Moves.Move(-1, -1, -1, -1);
        }
    }
    
    private void BeginSearch () // Initializes the search
    {
        _search.BeginSearch();
        _moveFound = true;
    }

    private void OnSearchComplete (Moves.Move m) // Updates _move and _moveFound on event call
    {
        _moveFound = true;
        _move = m;
    }
    
    public override void Notify () // Event to begin search
    {
        _moveFound = false;
        if(_move.StartX == -1)
            BeginSearch();
    }
}