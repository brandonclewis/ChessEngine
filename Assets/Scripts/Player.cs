using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Player
{
    public event System.Action<Moves.Move> MoveSelected; // Move selection event
    
    public abstract void Update (); // Update is called once per frame
    
    public abstract void Notify (); // Event to begin search
    
    protected void MovePiece (Moves.Move move) // Make move
    {
        MoveSelected?.Invoke (move);
    }
}
