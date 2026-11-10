using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovePlate : MonoBehaviour
{
    public GameObject controller;
    GameObject reference = null;

    int matrixX;
    int matrixY;

    public bool attack = false;

    public void Start()
    {
        Debug.Log("MovePlate.Start() called. Attack mode = " + attack);
        if (attack)
        {
            SpriteRenderer sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                Debug.Log("MovePlate.Start(): Set color to red for attack plate.");
            }
            else
            {
                Debug.LogError("MovePlate.Start(): SpriteRenderer component missing on MovePlate!");
            }
        }
    }

    public void OnMouseUp()
    {
        Debug.Log("MovePlate.OnMouseUp() clicked at matrix coords (" + matrixX + ", " + matrixY + ") | Attack = " + attack);

        controller = GameObject.FindGameObjectWithTag("GameController");
        if (controller == null)
        {
            Debug.LogError("MovePlate.OnMouseUp(): GameController not found!");
            return;
        }

        Game gameScript = controller.GetComponent<Game>();
        if (gameScript == null)
        {
            Debug.LogError("MovePlate.OnMouseUp(): Game script not found on controller!");
            return;
        }

        //Destroy the victim Chesspiece
        if (attack)
        {
            Debug.Log("MovePlate.OnMouseUp(): Executing attack on position (" + matrixX + ", " + matrixY + ")");
            GameObject cp = gameScript.GetPosition(matrixX, matrixY);

            if (cp != null)
            {
                Debug.Log("MovePlate.OnMouseUp(): Victim piece found: " + cp.name);
                if (cp.name == "white_king")
                {
                    Debug.Log("MovePlate.OnMouseUp(): White king captured! Black wins.");
                    gameScript.Winner("black");
                }
                if (cp.name == "black_king")
                {
                    Debug.Log("MovePlate.OnMouseUp(): Black king captured! White wins.");
                    gameScript.Winner("white");
                }

                Destroy(cp);
                Debug.Log("MovePlate.OnMouseUp(): Victim piece destroyed.");
            }
            else
            {
                Debug.LogWarning("MovePlate.OnMouseUp(): Attack flagged true, but GetPosition returned null!");
            }
        }

        if (reference == null)
        {
            Debug.LogError("MovePlate.OnMouseUp(): Reference chessman is null!");
            return;
        }

        Chessman chessmanScript = reference.GetComponent<Chessman>();
        if (chessmanScript == null)
        {
            Debug.LogError("MovePlate.OnMouseUp(): Chessman component missing on reference object!");
            return;
        }

        int oldX = chessmanScript.GetXBoard();
        int oldY = chessmanScript.GetYBoard();
        Debug.Log("MovePlate.OnMouseUp(): Clearing old position (" + oldX + ", " + oldY + ")");

        //Set the Chesspiece's original location to be empty
        gameScript.SetPositionEmpty(oldX, oldY);

        //Move reference chess piece to this position
        chessmanScript.SetXBoard(matrixX);
        chessmanScript.SetYBoard(matrixY);
        chessmanScript.SetCoords();

        //Update the matrix
        Debug.Log("MovePlate.OnMouseUp(): Updating board matrix with moved piece: " + reference.name);
        gameScript.SetPosition(reference);

        //Switch Current Player
        Debug.Log("MovePlate.OnMouseUp(): Switching turn via NextTurn()");
        gameScript.NextTurn();

        //Destroy the move plates including self
        Debug.Log("MovePlate.OnMouseUp(): Destroying all move plates.");
        chessmanScript.DestroyMovePlates();
    }

    public void SetCoords(int x, int y)
    {
        matrixX = x;
        matrixY = y;
        Debug.Log("MovePlate.SetCoords() set to (" + x + ", " + y + ")");
    }

    public void SetReference(GameObject obj)
    {
        reference = obj;
        Debug.Log("MovePlate.SetReference() set reference object to: " + (obj != null ? obj.name : "null"));
    }

    public GameObject GetReference()
    {
        return reference;
    }
}