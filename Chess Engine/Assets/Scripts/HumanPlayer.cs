using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HumanPlayer : Player
{
    private BoardUi ui; // UI
    private bool _grabbing; // Move in hand
    private int _grabX; // X location of move in hand
    private int _grabY; // Y location of move in hand

    public HumanPlayer(BoardUi ui2) // Constructor
    {
        ui = ui2;
    }

    public override void Update() // Update is called once per frame
    {
        if (!_grabbing)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Input.GetMouseButtonDown(0))
            {
                int x = (int) (mousePos.x + 4);
                int y = (int) (mousePos.y + 4);
                if (0 <= x && x < 8 && 0 <= y && y < 8)
                {
                    _grabbing = true;
                    _grabX = x;
                    _grabY = y;
                    ui.GrabPiece(x, y, mousePos);
                }
            }
        }
        else
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            ui.GrabPiece(_grabX, _grabY, mousePos);
            if (Input.GetMouseButtonUp(0))
            {
                int x = (int) (mousePos.x + 4);
                int y = (int) (mousePos.y + 4);
                if (0 <= x && x < 8 && 0 <= y && y < 8)
                {
                    MovePiece(new Moves.Move(_grabX, _grabY, x, y));
                }
                else
                {
                    ui.ResetPiece(_grabX, _grabY);
                }

                _grabbing = false;
            }
        }

        if (Input.GetKey("space"))
        {
            ui.UnMovePiece();
        }
    }
    
    public override void Notify() // Event to begin search (unused for humans)
    {
        
    }
}
