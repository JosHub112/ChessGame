using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class Game : MonoBehaviour
{

    public GameObject chesspiece;

    [Header("UI References (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI restartText;
    [Tooltip("Optional. Shown while the current player is in check.")]
    [SerializeField] private TextMeshProUGUI checkText;

    //Matrices needed, positions of each of the GameObjects
    private GameObject[,] positions = new GameObject[8, 8];
    private GameObject[] playerBlack = new GameObject[16];
    private GameObject[] playerWhite = new GameObject[16];

    //current turn
    private string currentPlayer = "white";

    //Game Ending
    private bool gameOver = false;
    private int gameOverFrame = -1; // frame in which the game ended (so the same click doesn't instantly restart)

    // Offsets used for attack detection
    private static readonly int[,] KnightOffsets = { { 1, 2 }, { -1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, 1 }, { -2, -1 } };
    private static readonly int[,] StraightDirs = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };
    private static readonly int[,] DiagonalDirs = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

    public void Start()
    {
        Debug.Log("Game.Start() called. Initializing chess match.");

        if (chesspiece == null)
        {
            Debug.LogError("Game.Start(): chesspiece prefab reference is not assigned in the Unity Inspector!");
        }

        playerWhite = new GameObject[] { Create("white_rook", 0, 0), Create("white_knight", 1, 0),
            Create("white_bishop", 2, 0), Create("white_queen", 3, 0), Create("white_king", 4, 0),
            Create("white_bishop", 5, 0), Create("white_knight", 6, 0), Create("white_rook", 7, 0),
            Create("white_pawn", 0, 1), Create("white_pawn", 1, 1), Create("white_pawn", 2, 1),
            Create("white_pawn", 3, 1), Create("white_pawn", 4, 1), Create("white_pawn", 5, 1),
            Create("white_pawn", 6, 1), Create("white_pawn", 7, 1) };
        Debug.Log("Game.Start(): White pieces created (" + playerWhite.Length + " pieces).");

        playerBlack = new GameObject[] { Create("black_rook", 0, 7), Create("black_knight",1,7),
            Create("black_bishop",2,7), Create("black_queen",3,7), Create("black_king",4,7),
            Create("black_bishop",5,7), Create("black_knight",6,7), Create("black_rook",7,7),
            Create("black_pawn", 0, 6), Create("black_pawn", 1, 6), Create("black_pawn", 2, 6),
            Create("black_pawn", 3, 6), Create("black_pawn", 4, 6), Create("black_pawn", 5, 6),
            Create("black_pawn", 6, 6), Create("black_pawn", 7, 6) };
        Debug.Log("Game.Start(): Black pieces created (" + playerBlack.Length + " pieces).");

        //Set all piece positions on the positions board
        for (int i = 0; i < playerBlack.Length; i++)
        {
            SetPosition(playerBlack[i]);
            SetPosition(playerWhite[i]);
        }
        Debug.Log("Game.Start(): All piece positions registered on board matrix successfully.");
    }

    public GameObject Create(string name, int x, int y)
    {
        Debug.Log("Game.Create() called for piece: " + name + " at board coords (" + x + ", " + y + ")");

        if (chesspiece == null)
        {
            Debug.LogError("Game.Create(): chesspiece prefab is null! Cannot instantiate.");
            return null;
        }

        GameObject obj = Instantiate(chesspiece, new Vector3(0, 0, -1), Quaternion.identity);
        if (obj == null)
        {
            Debug.LogError("Game.Create(): Failed to instantiate chesspiece prefab for " + name);
            return null;
        }

        Chessman cm = obj.GetComponent<Chessman>();
        if (cm == null)
        {
            Debug.LogError("Game.Create(): Chessman component missing on instantiated chesspiece prefab!");
            return obj;
        }

        cm.name = name;
        cm.SetXBoard(x);
        cm.SetYBoard(y);
        cm.Activate();

        Debug.Log("Game.Create(): Successfully created and activated " + name);
        return obj;
    }

    public void SetPosition(GameObject obj)
    {
        if (obj == null)
        {
            Debug.LogError("Game.SetPosition(): Attempted to set null GameObject position!");
            return;
        }

        Chessman cm = obj.GetComponent<Chessman>();
        if (cm == null)
        {
            Debug.LogError("Game.SetPosition(): Chessman component missing on object " + obj.name);
            return;
        }

        int x = cm.GetXBoard();
        int y = cm.GetYBoard();
        if (PositionOnBoard(x, y))
        {
            positions[x, y] = obj;
            Debug.Log("Game.SetPosition(): Registered " + obj.name + " at positions[" + x + ", " + y + "]");
        }
        else
        {
            Debug.LogError("Game.SetPosition(): Attempted to set position out of bounds: (" + x + ", " + y + ") for " + obj.name);
        }
    }

    public void SetPositionEmpty(int x, int y)
    {
        Debug.Log("Game.SetPositionEmpty() called for coordinates (" + x + ", " + y + ")");
        if (PositionOnBoard(x, y))
        {
            positions[x, y] = null;
        }
        else
        {
            Debug.LogError("Game.SetPositionEmpty(): Coordinates out of bounds: (" + x + ", " + y + ")");
        }
    }

    public GameObject GetPosition(int x, int y)
    {
        if (PositionOnBoard(x, y))
        {
            return positions[x, y];
        }
        Debug.LogWarning("Game.GetPosition(): Requested coordinates out of bounds: (" + x + ", " + y + ")");
        return null;
    }

    public bool PositionOnBoard(int x, int y)
    {
        if (x < 0 || y < 0 || x >= positions.GetLength(0) || y >= positions.GetLength(1)) return false;
        return true;
    }

    public string GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public bool IsGameOver()
    {
        return gameOver;
    }

    // ------------------------------------------------------------------
    //  CHECK / CHECKMATE / STALEMATE
    // ------------------------------------------------------------------

    private string Opponent(string player)
    {
        return player == "white" ? "black" : "white";
    }

    // Is the given player's king currently attacked?
    public bool IsInCheck(string player)
    {
        int kx, ky;
        if (!FindKing(player, out kx, out ky)) return false;
        return IsSquareAttacked(kx, ky, Opponent(player));
    }

    // Temporarily plays the move on the board matrix, checks if the mover's king
    // is attacked, then puts everything back. Only touches the positions array,
    // never the actual GameObjects.
    public bool MoveLeavesKingInCheck(Chessman piece, int toX, int toY)
    {
        int fromX = piece.GetXBoard();
        int fromY = piece.GetYBoard();

        GameObject movingObj = positions[fromX, fromY];
        GameObject capturedObj = positions[toX, toY];

        positions[toX, toY] = movingObj;
        positions[fromX, fromY] = null;

        bool inCheck = IsInCheck(piece.GetPlayer());

        positions[fromX, fromY] = movingObj;
        positions[toX, toY] = capturedObj;

        return inCheck;
    }

    // True if the player has at least one move that doesn't leave their king in check.
    public bool HasAnyLegalMove(string player)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                GameObject obj = positions[x, y];
                if (obj == null) continue;

                Chessman cm = obj.GetComponent<Chessman>();
                if (cm == null || cm.GetPlayer() != player) continue;

                foreach (Vector2Int move in cm.GetPossibleMoves())
                {
                    if (!MoveLeavesKingInCheck(cm, move.x, move.y)) return true;
                }
            }
        }
        return false;
    }

    private bool FindKing(string player, out int kx, out int ky)
    {
        string kingName = player + "_king";
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (positions[x, y] != null && positions[x, y].name == kingName)
                {
                    kx = x;
                    ky = y;
                    return true;
                }
            }
        }
        kx = -1;
        ky = -1;
        return false;
    }

    // Is square (x, y) attacked by any piece belonging to byPlayer?
    // Works backwards from the square: look for enemy pieces that could reach it.
    private bool IsSquareAttacked(int x, int y, string byPlayer)
    {
        // Pawns (white pawns attack upward, black pawns attack downward)
        int pawnRow = (byPlayer == "white") ? y - 1 : y + 1;
        if (IsPieceAt(x - 1, pawnRow, byPlayer, "pawn") || IsPieceAt(x + 1, pawnRow, byPlayer, "pawn")) return true;

        // Knights
        for (int i = 0; i < KnightOffsets.GetLength(0); i++)
        {
            if (IsPieceAt(x + KnightOffsets[i, 0], y + KnightOffsets[i, 1], byPlayer, "knight")) return true;
        }

        // King (adjacent squares)
        for (int i = 0; i < StraightDirs.GetLength(0); i++)
        {
            if (IsPieceAt(x + StraightDirs[i, 0], y + StraightDirs[i, 1], byPlayer, "king")) return true;
            if (IsPieceAt(x + DiagonalDirs[i, 0], y + DiagonalDirs[i, 1], byPlayer, "king")) return true;
        }

        // Rooks / queens along ranks and files
        for (int i = 0; i < StraightDirs.GetLength(0); i++)
        {
            if (IsAttackedAlongRay(x, y, StraightDirs[i, 0], StraightDirs[i, 1], byPlayer, "rook")) return true;
        }

        // Bishops / queens along diagonals
        for (int i = 0; i < DiagonalDirs.GetLength(0); i++)
        {
            if (IsAttackedAlongRay(x, y, DiagonalDirs[i, 0], DiagonalDirs[i, 1], byPlayer, "bishop")) return true;
        }

        return false;
    }

    private bool IsPieceAt(int x, int y, string owner, string type)
    {
        if (!PositionOnBoard(x, y)) return false;
        GameObject obj = positions[x, y];
        return obj != null && obj.name == owner + "_" + type;
    }

    // Walk outward from (x, y); the first piece we hit decides whether it's attacked.
    private bool IsAttackedAlongRay(int x, int y, int dx, int dy, string byPlayer, string sliderType)
    {
        int cx = x + dx;
        int cy = y + dy;

        while (PositionOnBoard(cx, cy))
        {
            GameObject obj = positions[cx, cy];
            if (obj != null)
            {
                string n = obj.name;
                return n == byPlayer + "_queen" || n == byPlayer + "_" + sliderType;
            }
            cx += dx;
            cy += dy;
        }
        return false;
    }

    // Called after the turn switches: is the new current player in check / checkmated / stalemated?
    private void EvaluatePosition()
    {
        bool inCheck = IsInCheck(currentPlayer);
        bool hasLegalMove = HasAnyLegalMove(currentPlayer);

        if (!hasLegalMove)
        {
            if (inCheck)
            {
                string winner = Opponent(currentPlayer);
                Debug.Log("Game.EvaluatePosition(): CHECKMATE. " + winner + " wins.");
                EndGame("Checkmate! " + winner + " is the winner");
            }
            else
            {
                Debug.Log("Game.EvaluatePosition(): STALEMATE. " + currentPlayer + " has no legal moves but is not in check.");
                EndGame("Stalemate - it's a draw");
            }
            return;
        }

        if (inCheck)
        {
            Debug.Log("Game.EvaluatePosition(): " + currentPlayer + " is in CHECK.");
        }

        if (checkText != null)
        {
            checkText.gameObject.SetActive(inCheck);
            if (inCheck)
            {
                string name = char.ToUpper(currentPlayer[0]) + currentPlayer.Substring(1);
                checkText.text = name + " is in check!";
            }
        }
    }

    // ------------------------------------------------------------------

    public void NextTurn()
    {
        Debug.Log("Game.NextTurn() called. Current player was: " + currentPlayer);
        if (currentPlayer == "white")
        {
            currentPlayer = "black";
        }
        else
        {
            currentPlayer = "white";
        }
        Debug.Log("Game.NextTurn(): Turn switched. Current player is now: " + currentPlayer);

        EvaluatePosition();
    }

    public void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Ignore the click that ended the game, otherwise the scene restarts instantly
            if (gameOver == true && Time.frameCount > gameOverFrame)
            {
                Debug.Log("Game.Update(): Game is over and mouse clicked. Restarting scene 'ChessLife'.");
                gameOver = false;
                SceneManager.LoadScene("ChessLife");
            }
        }
    }

    public void Winner(string playerWinner)
    {
        Debug.Log("Game.Winner() called. Winner declared: " + playerWinner);
        EndGame(playerWinner + " is the winner");
    }

    private void EndGame(string message)
    {
        gameOver = true;
        gameOverFrame = Time.frameCount;

        if (checkText != null)
        {
            checkText.gameObject.SetActive(false);
        }

        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(true); // Activates the GameObject
            winnerText.text = message;
            Debug.Log("Game.EndGame(): WinnerText activated and updated successfully.");
        }
        else
        {
            Debug.LogError("Game.EndGame(): WinnerText reference is not assigned in the Inspector!");
        }

        if (restartText != null)
        {
            restartText.gameObject.SetActive(true); // Activates the GameObject
            Debug.Log("Game.EndGame(): RestartText activated successfully.");
        }
        else
        {
            Debug.LogError("Game.EndGame(): RestartText reference is not assigned in the Inspector!");
        }
    }
}