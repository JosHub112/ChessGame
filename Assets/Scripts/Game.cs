using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    //Reference from Unity IDE
    public GameObject chesspiece;

    //Matrices needed, positions of each of the GameObjects
    private GameObject[,] positions = new GameObject[8, 8];
    private GameObject[] playerBlack = new GameObject[16];
    private GameObject[] playerWhite = new GameObject[16];

    //current turn
    private string currentPlayer = "white";

    //Game Ending
    private bool gameOver = false;

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
    }

    public void Update()
    {
        if (gameOver == true && Input.GetMouseButtonDown(0))
        {
            Debug.Log("Game.Update(): Game is over and mouse clicked. Restarting scene 'Game'.");
            gameOver = false;
            SceneManager.LoadScene("Game");
        }
    }

    public void Winner(string playerWinner)
    {
        Debug.Log("Game.Winner() called. Winner declared: " + playerWinner);
        gameOver = true;

        GameObject winnerTextObj = GameObject.FindGameObjectWithTag("WinnerText");
        if (winnerTextObj != null)
        {
            winnerTextObj.GetComponent<Text>().enabled = true;
            winnerTextObj.GetComponent<Text>().text = playerWinner + " is the winner";
            Debug.Log("Game.Winner(): WinnerText UI updated successfully.");
        }
        else
        {
            Debug.LogError("Game.Winner(): WinnerText object with tag 'WinnerText' not found in scene!");
        }

        GameObject restartTextObj = GameObject.FindGameObjectWithTag("RestartText");
        if (restartTextObj != null)
        {
            restartTextObj.GetComponent<Text>().enabled = true;
            Debug.Log("Game.Winner(): RestartText UI enabled successfully.");
        }
        else
        {
            Debug.LogError("Game.Winner(): RestartText object with tag 'RestartText' not found in scene!");
        }
    }
}