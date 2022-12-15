﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SearchMinimax
{
    public event Action<Moves.Move> OnSearchComplete; // Search completion event
    
    private readonly Board _board; // Game board
    private readonly Moves _moves; // Move generator instance
    private bool _abort; // Flag to abort search early

    private int _iterDepth; // Current iterative deepening depth
    private int _nodeCount; // Number of nodes in minimax tree
    private int _leafCount; // Number of leaves in the minimax tree
    private int _captureNodeCount; // Number of nodes in quiescence search tree (also same as number of evaluations)
    private int _transpositionCount; // Number of transpositions used

    private Dictionary<ulong, (int, int, Moves.Move)> _transpositionMap; // Map of transpositions
    
    private Moves.Move _bestMove; // Current best move
    private Moves.Move _bestMoveIter; // Current best move in current iterative deepening layer
    private int _bestScore; // Score of _bestMove
    private int _bestScoreIter; // Score of _bestMoveIter
    
    private const int MateScore = 1000000; // Score for being in mate (arbitrary high number)
    private const int MaxEvalCount = 100000; // Maximum number of positions evaluated
    private const int MaxNodeCount = 1000000; // Maximum number of nodes evaluated
    private const int MinDepth = 4; // Minimum search depth
    private const int MaxDepth = 50; // Maximum search depth
    
    public SearchMinimax(Board b) // Constructor
    {
        _board = b;
        _moves = new Moves();
    }

    public void BeginSearch() // Initializes the search
    {
        // Set all values to default
        _abort = false;
        _leafCount = 0;
        _nodeCount = 0;
        _captureNodeCount = 0;
        _transpositionCount = 0;
        _bestScoreIter = 0;
        _bestScore = 0;
        _bestMove = new Moves.Move(-1, -1, -1, -1);
        _bestMoveIter = new Moves.Move(-1, -1, -1, -1);
        _iterDepth = 0;
        _transpositionMap = new Dictionary<ulong, (int, int, Moves.Move)>();
        
        // Iterative deepening
        for (int searchDepth = 1; searchDepth <= MaxDepth; searchDepth++)
        {
            if (searchDepth > MinDepth && (_captureNodeCount > MaxEvalCount || _nodeCount > MaxNodeCount)) // End if MinDepth search went past MaxNodeCount
                break;
            SearchStep (searchDepth, 0, -999999999, 999999999);
            if (_abort) // End early
                break;
            
            _iterDepth = searchDepth;
            _bestMove = _bestMoveIter;
            _bestScore = _bestScoreIter;
        }
        //Debug.Log("SEARCH COMPLETE: " + _iterDepth + " " + _nodeCount + " " + _transpositionCount + " " + _captureNodeCount);
        
        // Play best move
        OnSearchComplete?.Invoke (_bestMove);
    }

    public void EndSearch() // Cancel the search
    {
        _abort = true;
    }

    private int SearchStep(int depth, int rootDepth, int alpha, int beta) // Minimax search step w/ AB pruning
    {
        if (_iterDepth>=MinDepth && (_captureNodeCount > MaxEvalCount || _nodeCount > MaxNodeCount)) _abort = true; // End search early for performance
        
        if (_abort) return 0; // Early cancel

        if (rootDepth > 0)
        {
            if (_board.Repetition) return 0; // Assign draw score to repetitions

            if (beta > MateScore - rootDepth) // Can never be better than mating
                beta = MateScore - rootDepth;
            if (-MateScore + rootDepth > alpha) // Can never be worse than being mated
                alpha = -MateScore + rootDepth;
            if (alpha >= beta)
                return alpha;
        }

        // Check if position has been evaluated before
        ulong hash = Zobrist.Hash(_board);
        if (_transpositionMap.ContainsKey(hash))
        {
            (int d, int s, Moves.Move m) = _transpositionMap[hash];
            if (d >= depth) // Only if to >= depth
            {
                _transpositionCount++;
                if (rootDepth == 0)
                {
                    _bestMoveIter = m;
                    _bestScoreIter = s;
                }
                return s;
            }
            _transpositionMap.Remove(hash);
        }
        
        // Evaluate leaf nodes with quiescence search
        if (depth == 0)
        {
            _leafCount++;
            return CaptureSearch(alpha, beta);
        }

        // Generate moves and sort with capture/promo heuristic
        List<Moves.Move> moveList = _moves.GenerateValidMoves(_board);
        moveList = Moves.SortMoves(moveList);
        
        if (moveList.Count == 0) // Game end
        {
            if (_moves.Check) // Assign mate score if checkmate
                return -MateScore + rootDepth;
            return 0; // Assign draw score if stalemate
        }

        Moves.Move bestMovePos = new Moves.Move(-1, -1, -1, -1); // Keep track of best move for transpositions
        
        // Search child nodes
        for (int i = 0; i < moveList.Count; i++)
        {
            // Play each child recursively
            _board.MovePiece(moveList[i], true);
            int score = -SearchStep(depth-1, rootDepth+1,-beta, -alpha);
            _board.UnMovePiece(moveList[i], true);
            _nodeCount++;

            // Pruning
            if (score >= beta)
            {
                if (_transpositionMap.ContainsKey(hash))
                    _transpositionMap.Remove(hash);
                _transpositionMap.Add(hash, (depth, beta, moveList[i]));
                return beta;
            }
            if (score > alpha)
            {
                bestMovePos = moveList[i];
                alpha = score;
                if (rootDepth == 0) // Update best move if you are at the root
                {
                    _bestMoveIter = moveList[i];
                    _bestScoreIter = score;
                }
            }
        }

        if (_transpositionMap.ContainsKey(hash))
            _transpositionMap.Remove(hash);
        _transpositionMap.Add(hash, (depth, alpha, bestMovePos));
        return alpha;
    }
    
    private int CaptureSearch(int alpha, int beta) // Quiescence search (only looks at captures)
    {
        int score = Evaluator.Evaluate(_board);
        
        // Pruning
        if (score >= beta)
            return beta;
        if (score > alpha)
            alpha = score;
        
        // Generate moves and sort with capture/promo heuristic
        List<Moves.Move> moveList = _moves.GenerateValidMoves(_board, true);
        moveList = Moves.SortMoves(moveList);
        
        // Search child nodes
        for (int i = 0; i < moveList.Count; i++)
        {
            // Play each child recursively
            _board.MovePiece(moveList[i], true);
            score = -CaptureSearch(-beta, -alpha);
            _board.UnMovePiece(moveList[i], true);
            _captureNodeCount++;
            
            // Pruning
            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }
}