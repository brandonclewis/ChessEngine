using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchMonteCarlo
{
	public event Action<Moves.Move> OnSearchComplete; // Search completion event
	
	private readonly Board _board; // Game board
	private readonly System.Random _rand; // Rng instance
	
    private Moves.Move _bestMove; // Current best move
    
    private const double ExploreWeight = 1.5; // Exploration evaluation weight
    private const double DrawScore = 0.5; // Score for draws
    private const int NumSimulations = 100; // Number of simulations
    private const int MaxRollout = 50; // Max depth of rollout
    
    public SearchMonteCarlo(Board b) // Constructor
    {
        _board = b;
        _rand = new System.Random((int)DateTime.Now.Ticks);
    }

    public void BeginSearch() // Initializes the search
    {
	    // Generate root level children
	    TreeNode initialState = new TreeNode(_board, new Moves.Move(-1,-1,-1,-1), _board.WhiteTurn ? 0 : 1, null, 0);
	    initialState.GenerateChildren ();
	    
	    // If any children are winning moves, make them
        foreach (var child in initialState.Children)
        {
	        if (child.GetResult() == child.PlayerIndex)
	        {
		        _bestMove = child.Move;
		        return;
	        }
        }

        if (initialState.Children.Count == 0) // Catch checkmate errors
	        throw new Exception("ERROR: no kids!!! somehow a checkmate wasnt noticed");

        // Simulation loop
        int count = 0;
        while (count < NumSimulations)
        {
	        // Start with bestNode as root state
	        count++;
            TreeNode bestNode = initialState;
            
            // Loop through all child nodes of bestNode
            while(bestNode.Children.Count > 0)
            {
	            double bestScore = -1;
	            int bestIndex = -1;
	            
	            // Repeatedly check for best scoring child of bestNode
	            for(int i = 0; i < bestNode.Children.Count; i++)
	            {
		            // Score evaluation
		            double numWins = bestNode.Children[i].Wins;
		            double numGames = bestNode.Children[i].TotalGames;
		            double score = numGames > 0 ? numWins / numGames : 1.0;
		            
		            // Exploration evaluation (weaker as number of simulations goes up)
		            double exploreRating = ExploreWeight*Math.Sqrt(2*Math.Log(initialState.TotalGames + 1) / (numGames + 0.1));

		            // Sum the scores
		            double totalScore = score+exploreRating;

		            // Update best scorer tally
		            if (!(totalScore > bestScore)) continue;
		            bestScore = totalScore;
		            bestIndex = i;
	            }
	            
	            // Assign best scoring child of bestNode as bestNode
	            bestNode = bestNode.Children[bestIndex];
            }
            
            // Begin rollout from bestNode
            Rollout(bestNode);
        }
        
        // Final move selection
        int mostGames = -1;
        int bestMoveIndex = -1;
        for(int i = 0; i < initialState.Children.Count; i++)
        {
	        // Pick the child with most games played (aka robust child approach)
	        int totalGames = initialState.Children[i].TotalGames;
	        if(totalGames >= mostGames)
	        {
		        mostGames = totalGames;
		        bestMoveIndex = i;
	        }
        }
        _bestMove = initialState.Children[bestMoveIndex].Move;

        // Play the final move
        OnSearchComplete?.Invoke (_bestMove);
    }

    private void Rollout(TreeNode rolloutStart) // Rollout stage of MCTS
    {
	    // Check if game over
        int rolloutStartResult = rolloutStart.GetResult();
        if (rolloutStartResult >= 0)
        {
	        // Evaluate outcome
            if(rolloutStartResult == rolloutStart.PlayerIndex) rolloutStart.AddWin();
            else if(rolloutStartResult == (rolloutStart.PlayerIndex+1)%2) rolloutStart.AddLoss();
            else rolloutStart.AddDraw (DrawScore);
            return;
        }
        
        // Generate children and proceed to terminal state
        bool terminalStateFound = false;
        List<TreeNode> children = rolloutStart.GenerateChildren();
        int loopCount = 0;
        while(!terminalStateFound)
        {
	        // Check rollout loop limit (assign draw if past limit)
            loopCount++;
            if (loopCount >= MaxRollout || children.Count == 0) {
	            rolloutStart.AddDraw (DrawScore);
                break;
            }
            
            // Pick random child node and check if gameover
            int index = _rand.Next(children.Count);
            int endResult = children[index].GetResult();
            if(endResult >= 0)
            {
	            // End if on terminal state
                terminalStateFound = true;
                if (endResult == 2) rolloutStart.AddDraw(DrawScore);
                else if(endResult == rolloutStart.PlayerIndex) rolloutStart.AddWin();
                else rolloutStart.AddLoss();
            } else {
	            // Delve 1 layer deeper
                children = children [index].GenerateChildren();
            }
        }
        
        // Reset children in the original tree (only want to add 1 node to it)
        foreach(TreeNode child in rolloutStart.Children)
	        child.Children = new List<TreeNode>();
    }
}

public class TreeNode // MCTS tree node class
	{
		public double Wins { get; set; } // Win counter
	    public int Losses { get; set; } // Loss counter
	    public int TotalGames { get; set; } // Total number of games
	    public int PlayerIndex { get; set; } // Index of player
	    public int Depth { get; set; } // Depth of this node from root
	    public TreeNode Parent { get; set; } // Parent node
	    public List<TreeNode> Children { get; set; } // Children of this node

	    private readonly Board _board; // Game board
	    public Moves.Move Move; // Move related to this node
	    
	    public TreeNode(Board b, Moves.Move m, int playerIndex, TreeNode parent, int depth) // Constructor
	    {
		    _board = b;
		    Move = m;
		    PlayerIndex = playerIndex;
			Parent = parent;
			Depth = depth;
			Children = new List<TreeNode> ();
			Wins = 0;
			Losses = 0;
			TotalGames = 0;
	    }
	    
		public void AddWin() // Add win to win tally
		{
			Wins++;
			TotalGames++;
			Parent?.AddLoss ();
		}
		
		public void AddLoss() // Add loss to loss tally
		{
			Losses++;
			TotalGames++;
		    Parent?.AddWin ();
		}
		
		public void AddDraw(double value) // Add draw of specific value to win tally
		{
		    Wins += value;
			TotalGames++;
			Parent?.AddDraw (value);
		}
		
		public List<TreeNode> GenerateChildren() // Creates list of all child nodes and assigns it to Children
		{
			List<TreeNode> lst = new List<TreeNode>();
			Moves move = new Moves();
			List<Moves.Move> moves = move.GenerateValidMoves(_board);
			
			foreach (Moves.Move m in moves)
			{
				_board.MovePiece(m, true);
				lst.Add(new TreeNode(_board, m, PlayerIndex == 0 ? 1 : 0, this, Depth+1));
				_board.UnMovePiece(m, true);
			}

			Children = lst;
			return lst;
		}
		
		public int GetResult() // Gets game outcome
		{
			if (GenerateChildren().Count == 0)
			{
				Moves move = new Moves();
				move.GenerateValidMoves(_board);
				if (!move.Check) return 2; // Stalemate draw
				if (PlayerIndex == 0) return 1; // Black wins
				return 0; // White wins
			}
			if (_board.Repetition) return 2; // Repetition draw
			return -1;
		}
	}