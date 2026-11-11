using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Required for New Input System

public class Chessman : MonoBehaviour
{
    public GameObject controller;
    public GameObject movePlate;

    private int xBoard = -1;
    private int yBoard = -1;
    private string player;

    public Sprite black_queen, black_knight, black_bishop, black_king, black_rook, black_pawn;
    public Sprite white_queen, white_knight, white_bishop, white_king, white_rook, white_pawn;

    public void Activate()
    {
        controller = GameObject.FindGameObjectWithTag("GameController");
        SetCoords();

        switch (this.name)
        {
            case "black_queen": this.GetComponent<SpriteRenderer>().sprite = black_queen; player = "black"; break;
            case "black_knight": this.GetComponent<SpriteRenderer>().sprite = black_knight; player = "black"; break;
            case "black_bishop": this.GetComponent<SpriteRenderer>().sprite = black_bishop; player = "black"; break;
            case "black_king": this.GetComponent<SpriteRenderer>().sprite = black_king; player = "black"; break;
            case "black_rook": this.GetComponent<SpriteRenderer>().sprite = black_rook; player = "black"; break;
            case "black_pawn": this.GetComponent<SpriteRenderer>().sprite = black_pawn; player = "black"; break;
            case "white_queen": this.GetComponent<SpriteRenderer>().sprite = white_queen; player = "white"; break;
            case "white_knight": this.GetComponent<SpriteRenderer>().sprite = white_knight; player = "white"; break;
            case "white_bishop": this.GetComponent<SpriteRenderer>().sprite = white_bishop; player = "white"; break;
            case "white_king": this.GetComponent<SpriteRenderer>().sprite = white_king; player = "white"; break;
            case "white_rook": this.GetComponent<SpriteRenderer>().sprite = white_rook; player = "white"; break;
            case "white_pawn": this.GetComponent<SpriteRenderer>().sprite = white_pawn; player = "white"; break;
        }
    }

    private void Update()
    {
        // Check for left click using New Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                Debug.Log("Successfully clicked piece: " + gameObject.name);
                SelectPiece();
            }
        }
    }

    void SelectPiece()
    {
        if (controller == null) return;
        Game gameScript = controller.GetComponent<Game>();

        if (gameScript != null && !gameScript.IsGameOver() && gameScript.GetCurrentPlayer() == player)
        {
            Debug.Log("Selecting piece and creating move plates for: " + gameObject.name);
            DestroyMovePlates();
            InitiateMovePlates();
        }
        else
        {
            Debug.Log("Click ignored. Not " + player + "'s turn or game is over.");
        }
    }

    public void SetCoords()
    {
        float x = xBoard * 0.66f - 2.3f;
        float y = yBoard * 0.66f - 2.3f;
        this.transform.position = new Vector3(x, y, -1.0f);
    }

    public int GetXBoard() { return xBoard; }
    public int GetYBoard() { return yBoard; }
    public void SetXBoard(int x) { xBoard = x; }
    public void SetYBoard(int y) { yBoard = y; }
    public string GetPlayer() { return player; }

    public void DestroyMovePlates()
    {
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        for (int i = 0; i < movePlates.Length; i++)
        {
            Destroy(movePlates[i]);
        }
    }

    // Spawns move plates, but only for LEGAL moves
    // (a move is illegal if it leaves your own king in check).
    public void InitiateMovePlates()
    {
        Game sc = controller.GetComponent<Game>();

        foreach (Vector2Int move in GetPossibleMoves())
        {
            if (sc.MoveLeavesKingInCheck(this, move.x, move.y)) continue;

            if (sc.GetPosition(move.x, move.y) != null) MovePlateAttackSpawn(move.x, move.y);
            else MovePlateSpawn(move.x, move.y);
        }
    }

    // Pseudo-legal moves: how this piece can move, ignoring check.
    // Game.cs filters these down to legal moves.
    public List<Vector2Int> GetPossibleMoves()
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        switch (this.name)
        {
            case "black_queen":
            case "white_queen":
                AddLineMoves(moves, 1, 0); AddLineMoves(moves, 0, 1); AddLineMoves(moves, 1, 1);
                AddLineMoves(moves, -1, 0); AddLineMoves(moves, 0, -1); AddLineMoves(moves, -1, -1);
                AddLineMoves(moves, -1, 1); AddLineMoves(moves, 1, -1);
                break;
            case "black_knight":
            case "white_knight":
                AddKnightMoves(moves);
                break;
            case "black_bishop":
            case "white_bishop":
                AddLineMoves(moves, 1, 1); AddLineMoves(moves, 1, -1);
                AddLineMoves(moves, -1, 1); AddLineMoves(moves, -1, -1);
                break;
            case "black_king":
            case "white_king":
                AddKingMoves(moves);
                break;
            case "black_rook":
            case "white_rook":
                AddLineMoves(moves, 1, 0); AddLineMoves(moves, 0, 1);
                AddLineMoves(moves, -1, 0); AddLineMoves(moves, 0, -1);
                break;
            case "black_pawn":
                AddPawnMoves(moves, xBoard, yBoard - 1);
                break;
            case "white_pawn":
                AddPawnMoves(moves, xBoard, yBoard + 1);
                break;
        }

        return moves;
    }

    private void AddLineMoves(List<Vector2Int> moves, int xIncrement, int yIncrement)
    {
        Game sc = controller.GetComponent<Game>();
        int x = xBoard + xIncrement;
        int y = yBoard + yIncrement;

        while (sc.PositionOnBoard(x, y) && sc.GetPosition(x, y) == null)
        {
            moves.Add(new Vector2Int(x, y));
            x += xIncrement;
            y += yIncrement;
        }

        if (sc.PositionOnBoard(x, y) && sc.GetPosition(x, y).GetComponent<Chessman>().player != player)
        {
            moves.Add(new Vector2Int(x, y));
        }
    }

    private void AddKnightMoves(List<Vector2Int> moves)
    {
        AddPointMove(moves, xBoard + 1, yBoard + 2); AddPointMove(moves, xBoard - 1, yBoard + 2);
        AddPointMove(moves, xBoard + 2, yBoard + 1); AddPointMove(moves, xBoard + 2, yBoard - 1);
        AddPointMove(moves, xBoard + 1, yBoard - 2); AddPointMove(moves, xBoard - 1, yBoard - 2);
        AddPointMove(moves, xBoard - 2, yBoard + 1); AddPointMove(moves, xBoard - 2, yBoard - 1);
    }

    private void AddKingMoves(List<Vector2Int> moves)
    {
        AddPointMove(moves, xBoard, yBoard + 1); AddPointMove(moves, xBoard, yBoard - 1);
        AddPointMove(moves, xBoard - 1, yBoard + 0); AddPointMove(moves, xBoard - 1, yBoard - 1);
        AddPointMove(moves, xBoard - 1, yBoard + 1); AddPointMove(moves, xBoard + 1, yBoard + 0);
        AddPointMove(moves, xBoard + 1, yBoard - 1); AddPointMove(moves, xBoard + 1, yBoard + 1);
    }

    private void AddPointMove(List<Vector2Int> moves, int x, int y)
    {
        Game sc = controller.GetComponent<Game>();
        if (!sc.PositionOnBoard(x, y)) return;

        GameObject cp = sc.GetPosition(x, y);
        if (cp == null || cp.GetComponent<Chessman>().player != player)
        {
            moves.Add(new Vector2Int(x, y));
        }
    }

    // x, y is the square directly in front of the pawn
    private void AddPawnMoves(List<Vector2Int> moves, int x, int y)
    {
        Game sc = controller.GetComponent<Game>();
        if (!sc.PositionOnBoard(x, y)) return;

        if (sc.GetPosition(x, y) == null) moves.Add(new Vector2Int(x, y));

        AddPawnCapture(moves, sc, x + 1, y);
        AddPawnCapture(moves, sc, x - 1, y);
    }

    private void AddPawnCapture(List<Vector2Int> moves, Game sc, int x, int y)
    {
        if (!sc.PositionOnBoard(x, y)) return;

        GameObject target = sc.GetPosition(x, y);
        if (target != null && target.GetComponent<Chessman>().player != player)
        {
            moves.Add(new Vector2Int(x, y));
        }
    }

    public void MovePlateSpawn(int matrixX, int matrixY)
    {
        float x = matrixX * 0.66f - 2.3f;
        float y = matrixY * 0.66f - 2.3f;
        GameObject mp = Instantiate(movePlate, new Vector3(x, y, -3.0f), Quaternion.identity);
        MovePlate mpScript = mp.GetComponent<MovePlate>();
        mpScript.SetReference(gameObject);
        mpScript.SetCoords(matrixX, matrixY);
    }

    public void MovePlateAttackSpawn(int matrixX, int matrixY)
    {
        float x = matrixX * 0.66f - 2.3f;
        float y = matrixY * 0.66f - 2.3f;
        GameObject mp = Instantiate(movePlate, new Vector3(x, y, -3.0f), Quaternion.identity);
        MovePlate mpScript = mp.GetComponent<MovePlate>();
        mpScript.attack = true;
        mpScript.SetReference(gameObject);
        mpScript.SetCoords(matrixX, matrixY);
    }
}