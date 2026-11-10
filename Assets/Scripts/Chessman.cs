using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Chessman : MonoBehaviour
{
    //References to objects in our Unity Scene
    public GameObject controller;
    public GameObject movePlate;

    //Position for this Chesspiece on the Board
    private int xBoard = -1;
    private int yBoard = -1;

    //Variable for keeping track of the player it belongs to "black" or "white"
    private string player;

    //References to all the possible Sprites that this Chesspiece could be
    public Sprite black_queen, black_knight, black_bishop, black_king, black_rook, black_pawn;
    public Sprite white_queen, white_knight, white_bishop, white_king, white_rook, white_pawn;

    public void Activate()
    {
        Debug.Log("Chessman.Activate() called for piece: " + gameObject.name);

        //Get the game controller
        controller = GameObject.FindGameObjectWithTag("GameController");
        if (controller == null)
        {
            Debug.LogError("Chessman.Activate(): GameController tag not found in scene!");
        }
        else
        {
            Debug.Log("Chessman.Activate(): GameController found successfully.");
        }

        //Take the instantiated location and adjust transform
        SetCoords();

        //Choose correct sprite based on piece's name
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
            default:
                Debug.LogWarning("Chessman.Activate(): Unrecognized piece name: " + this.name);
                break;
        }
        Debug.Log("Chessman.Activate() finished for " + this.name + " | Assigned Player: " + player);
    }

    public void SetCoords()
    {
        Debug.Log("Chessman.SetCoords() called for " + gameObject.name + " at board pos: (" + xBoard + ", " + yBoard + ")");

        float x = xBoard;
        float y = yBoard;

        x *= 0.66f;
        y *= 0.66f;

        x += -2.3f;
        y += -2.3f;

        this.transform.position = new Vector3(x, y, -1.0f);
        Debug.Log("Chessman.SetCoords() set world position to: " + this.transform.position);
    }

    public int GetXBoard()
    {
        return xBoard;
    }

    public int GetYBoard()
    {
        return yBoard;
    }

    public void SetXBoard(int x)
    {
        xBoard = x;
        Debug.Log("Chessman.SetXBoard() set xBoard to " + x + " for " + gameObject.name);
    }

    public void SetYBoard(int y)
    {
        yBoard = y;
        Debug.Log("Chessman.SetYBoard() set yBoard to " + y + " for " + gameObject.name);
    }

    private void OnMouseUp()
    {
        Debug.Log("Chessman.OnMouseUp() clicked on piece: " + gameObject.name + " (Player: " + player + ")");

        if (controller == null)
        {
            Debug.LogError("Chessman.OnMouseUp(): controller reference is null!");
            return;
        }

        Game gameScript = controller.GetComponent<Game>();
        if (gameScript == null)
        {
            Debug.LogError("Chessman.OnMouseUp(): Game component not found on controller!");
            return;
        }

        bool isGameOver = gameScript.IsGameOver();
        string currentTurn = gameScript.GetCurrentPlayer();
        Debug.Log("Chessman.OnMouseUp(): IsGameOver = " + isGameOver + " | CurrentTurn = " + currentTurn + " | PiecePlayer = " + player);

        if (!isGameOver && currentTurn == player)
        {
            Debug.Log("Chessman.OnMouseUp(): Turn valid. Destroying old move plates and initiating new ones for " + gameObject.name);
            DestroyMovePlates();
            InitiateMovePlates();
        }
        else
        {
            Debug.Log("Chessman.OnMouseUp(): Click ignored. Either game is over or it's not this player's turn.");
        }
    }

    public void DestroyMovePlates()
    {
        Debug.Log("Chessman.DestroyMovePlates() called.");
        GameObject[] movePlates = GameObject.FindGameObjectsWithTag("MovePlate");
        Debug.Log("Chessman.DestroyMovePlates(): Found " + movePlates.Length + " existing MovePlates to destroy.");

        for (int i = 0; i < movePlates.Length; i++)
        {
            Destroy(movePlates[i]);
        }
    }

    public void InitiateMovePlates()
    {
        Debug.Log("Chessman.InitiateMovePlates() called for " + gameObject.name + " (" + player + ") at (" + xBoard + ", " + yBoard + ")");

        switch (this.name)
        {
            case "black_queen":
            case "white_queen":
                Debug.Log("InitiateMovePlates: Spawning queen moves.");
                LineMovePlate(1, 0);
                LineMovePlate(0, 1);
                LineMovePlate(1, 1);
                LineMovePlate(-1, 0);
                LineMovePlate(0, -1);
                LineMovePlate(-1, -1);
                LineMovePlate(-1, 1);
                LineMovePlate(1, -1);
                break;
            case "black_knight":
            case "white_knight":
                Debug.Log("InitiateMovePlates: Spawning knight moves.");
                LMovePlate();
                break;
            case "black_bishop":
            case "white_bishop":
                Debug.Log("InitiateMovePlates: Spawning bishop moves.");
                LineMovePlate(1, 1);
                LineMovePlate(1, -1);
                LineMovePlate(-1, 1);
                LineMovePlate(-1, -1);
                break;
            case "black_king":
            case "white_king":
                Debug.Log("InitiateMovePlates: Spawning king moves.");
                SurroundMovePlate();
                break;
            case "black_rook":
            case "white_rook":
                Debug.Log("InitiateMovePlates: Spawning rook moves.");
                LineMovePlate(1, 0);
                LineMovePlate(0, 1);
                LineMovePlate(-1, 0);
                LineMovePlate(0, -1);
                break;
            case "black_pawn":
                Debug.Log("InitiateMovePlates: Spawning black pawn moves.");
                PawnMovePlate(xBoard, yBoard - 1);
                break;
            case "white_pawn":
                Debug.Log("InitiateMovePlates: Spawning white pawn moves.");
                PawnMovePlate(xBoard, yBoard + 1);
                break;
            default:
                Debug.LogWarning("InitiateMovePlates: Unknown piece name: " + this.name);
                break;
        }
    }

    public void LineMovePlate(int xIncrement, int yIncrement)
    {
        Debug.Log("Chessman.LineMovePlate() called with increment (" + xIncrement + ", " + yIncrement + ")");
        Game sc = controller.GetComponent<Game>();

        int x = xBoard + xIncrement;
        int y = yBoard + yIncrement;

        while (sc.PositionOnBoard(x, y) && sc.GetPosition(x, y) == null)
        {
            Debug.Log("LineMovePlate: Spawning normal move plate at (" + x + ", " + y + ")");
            MovePlateSpawn(x, y);
            x += xIncrement;
            y += yIncrement;
        }

        if (sc.PositionOnBoard(x, y))
        {
            GameObject targetPiece = sc.GetPosition(x, y);
            if (targetPiece != null)
            {
                Chessman targetChessman = targetPiece.GetComponent<Chessman>();
                if (targetChessman != null && targetChessman.player != player)
                {
                    Debug.Log("LineMovePlate: Spawning attack move plate at (" + x + ", " + y + ") targeting enemy " + targetPiece.name);
                    MovePlateAttackSpawn(x, y);
                }
                else
                {
                    Debug.Log("LineMovePlate: Path blocked at (" + x + ", " + y + ") by friendly piece " + targetPiece.name);
                }
            }
        }
        else
        {
            Debug.Log("LineMovePlate: Line check exited board bounds at (" + x + ", " + y + ")");
        }
    }

    public void LMovePlate()
    {
        Debug.Log("Chessman.LMovePlate() called.");
        PointMovePlate(xBoard + 1, yBoard + 2);
        PointMovePlate(xBoard - 1, yBoard + 2);
        PointMovePlate(xBoard + 2, yBoard + 1);
        PointMovePlate(xBoard + 2, yBoard - 1);
        PointMovePlate(xBoard + 1, yBoard - 2);
        PointMovePlate(xBoard - 1, yBoard - 2);
        PointMovePlate(xBoard - 2, yBoard + 1);
        PointMovePlate(xBoard - 2, yBoard - 1);
    }

    public void SurroundMovePlate()
    {
        Debug.Log("Chessman.SurroundMovePlate() called.");
        PointMovePlate(xBoard, yBoard + 1);
        PointMovePlate(xBoard, yBoard - 1);
        PointMovePlate(xBoard - 1, yBoard + 0);
        PointMovePlate(xBoard - 1, yBoard - 1);
        PointMovePlate(xBoard - 1, yBoard + 1);
        PointMovePlate(xBoard + 1, yBoard + 0);
        PointMovePlate(xBoard + 1, yBoard - 1);
        PointMovePlate(xBoard + 1, yBoard + 1);
    }

    public void PointMovePlate(int x, int y)
    {
        Debug.Log("Chessman.PointMovePlate() checked for (" + x + ", " + y + ")");
        Game sc = controller.GetComponent<Game>();
        if (sc.PositionOnBoard(x, y))
        {
            GameObject cp = sc.GetPosition(x, y);

            if (cp == null)
            {
                Debug.Log("PointMovePlate: Position (" + x + ", " + y + ") is empty. Spawning move plate.");
                MovePlateSpawn(x, y);
            }
            else
            {
                Chessman targetChessman = cp.GetComponent<Chessman>();
                if (targetChessman != null && targetChessman.player != player)
                {
                    Debug.Log("PointMovePlate: Position (" + x + ", " + y + ") has enemy piece " + cp.name + ". Spawning attack plate.");
                    MovePlateAttackSpawn(x, y);
                }
                else
                {
                    Debug.Log("PointMovePlate: Position (" + x + ", " + y + ") has friendly piece " + cp.name + ".");
                }
            }
        }
        else
        {
            Debug.Log("PointMovePlate: Position (" + x + ", " + y + ") is off board.");
        }
    }

    public void PawnMovePlate(int x, int y)
    {
        Debug.Log("Chessman.PawnMovePlate() checked for forward pos (" + x + ", " + y + ")");
        Game sc = controller.GetComponent<Game>();
        if (sc.PositionOnBoard(x, y))
        {
            if (sc.GetPosition(x, y) == null)
            {
                Debug.Log("PawnMovePlate: Forward position (" + x + ", " + y + ") is empty. Spawning move plate.");
                MovePlateSpawn(x, y);
            }

            if (sc.PositionOnBoard(x + 1, y))
            {
                GameObject rightPiece = sc.GetPosition(x + 1, y);
                if (rightPiece != null)
                {
                    Chessman rightChessman = rightPiece.GetComponent<Chessman>();
                    if (rightChessman != null && rightChessman.player != player)
                    {
                        Debug.Log("PawnMovePlate: Right diagonal (" + (x + 1) + ", " + y + ") has enemy " + rightPiece.name + ". Spawning attack plate.");
                        MovePlateAttackSpawn(x + 1, y);
                    }
                }
            }

            if (sc.PositionOnBoard(x - 1, y))
            {
                GameObject leftPiece = sc.GetPosition(x - 1, y);
                if (leftPiece != null)
                {
                    Chessman leftChessman = leftPiece.GetComponent<Chessman>();
                    if (leftChessman != null && leftChessman.player != player)
                    {
                        Debug.Log("PawnMovePlate: Left diagonal (" + (x - 1) + ", " + y + ") has enemy " + leftPiece.name + ". Spawning attack plate.");
                        MovePlateAttackSpawn(x - 1, y);
                    }
                }
            }
        }
        else
        {
            Debug.Log("PawnMovePlate: Forward position (" + x + ", " + y + ") is off board.");
        }
    }

    public void MovePlateSpawn(int matrixX, int matrixY)
    {
        Debug.Log("Chessman.MovePlateSpawn() spawning move plate at matrix coords (" + matrixX + ", " + matrixY + ")");
        float x = matrixX;
        float y = matrixY;

        x *= 0.66f;
        y *= 0.66f;

        x += -2.3f;
        y += -2.3f;

        GameObject mp = Instantiate(movePlate, new Vector3(x, y, -3.0f), Quaternion.identity);
        if (mp == null)
        {
            Debug.LogError("Chessman.MovePlateSpawn(): Failed to instantiate movePlate prefab!");
            return;
        }

        MovePlate mpScript = mp.GetComponent<MovePlate>();
        if (mpScript == null)
        {
            Debug.LogError("Chessman.MovePlateSpawn(): MovePlate script missing on instantiated object!");
            return;
        }

        mpScript.SetReference(gameObject);
        mpScript.SetCoords(matrixX, matrixY);
        Debug.Log("Chessman.MovePlateSpawn(): Move plate successfully spawned and initialized.");
    }

    public void MovePlateAttackSpawn(int matrixX, int matrixY)
    {
        Debug.Log("Chessman.MovePlateAttackSpawn() spawning attack plate at matrix coords (" + matrixX + ", " + matrixY + ")");
        float x = matrixX;
        float y = matrixY;

        x *= 0.66f;
        y *= 0.66f;

        x += -2.3f;
        y += -2.3f;

        GameObject mp = Instantiate(movePlate, new Vector3(x, y, -3.0f), Quaternion.identity);
        if (mp == null)
        {
            Debug.LogError("Chessman.MovePlateAttackSpawn(): Failed to instantiate movePlate prefab!");
            return;
        }

        MovePlate mpScript = mp.GetComponent<MovePlate>();
        if (mpScript == null)
        {
            Debug.LogError("Chessman.MovePlateAttackSpawn(): MovePlate script missing on instantiated object!");
            return;
        }

        mpScript.attack = true;
        mpScript.SetReference(gameObject);
        mpScript.SetCoords(matrixX, matrixY);
        Debug.Log("Chessman.MovePlateAttackSpawn(): Attack plate successfully spawned and initialized.");
    }
}