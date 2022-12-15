using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TestLegality : MonoBehaviour
{
    private Board _board; // Game board
    public Moves moves; // Move generator instance
    public bool runTests; // Whether to run tests
    public bool divide; // Whether to print splits
    public string testFen; // Fen string to test
    public int testDepth; // Depth to test
    
    void Start() // Start is called before the first frame update
    {
        if (runTests)
        {
            _board = new Board();
            _board.LoadFen(testFen);
            moves = new Moves();
            for (int i = 0; i < testDepth+1; i++)
            {
                MoveTest(i, divide);
                moves.PerftPrint();
                moves.PerftReset();
            }
        }
    }
    
    void Update() // Update is called once per frame
    {
        
    }
    
    private int MoveTest(int depth, bool divide = false) // Move count for depth
    {
        if (depth == 0)
        {
            return 1;
        }
        
        List<Moves.Move> mvs = moves.GenerateValidMoves(_board);

        if(depth == 1)
            moves.PerftAdd(mvs);
        int n = 0;
        foreach(Moves.Move m in mvs)
        {
            _board.MovePiece(m);
            int curr = MoveTest(depth-1);
            n += curr;

            if(divide)
                print((char)(m.StartX + 'a') + "" + (m.StartY+1) + "" + (char)(m.ToX + 'a') + "" + (m.ToY + 1) + ": " + curr);
            _board.UnMovePiece(m);
        }

        return n;
    }
}
