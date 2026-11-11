using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Required for New Input System

public class MovePlate : MonoBehaviour
{
    public GameObject controller;
    GameObject reference = null;

    int matrixX;
    int matrixY;

    public bool attack = false;

    public void Start()
    {
        if (attack)
        {
            SpriteRenderer sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(1.0f, 0.0f, 0.0f, 1.0f); // Set to red for attacks
            }
        }
    }

    private void Update()
    {
        // Check for clicks on the move plate using the New Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                Debug.Log("MovePlate clicked at: (" + matrixX + ", " + matrixY + ")");
                ExecuteMove();
            }
        }
    }

    // Keep OnMouseUp as a fallback just in case
    public void OnMouseUp()
    {
        ExecuteMove();
    }

    void ExecuteMove()
    {
        controller = GameObject.FindGameObjectWithTag("GameController");
        if (controller == null) return;

        Game gameScript = controller.GetComponent<Game>();
        if (gameScript == null) return;

        if (gameScript.IsGameOver()) return;

        if (reference == null) return;

        Chessman chessmanScript = reference.GetComponent<Chessman>();
        if (chessmanScript == null) return;

        // Destroy the victim Chesspiece if attacking.
        // (Kings can never be captured anymore: moves that leave a king in check are
        // filtered out, and checkmate is detected in Game.NextTurn().)
        if (attack)
        {
            GameObject cp = gameScript.GetPosition(matrixX, matrixY);
            if (cp != null)
            {
                Destroy(cp);
            }
        }

        // Set the Chesspiece's original location to be empty
        gameScript.SetPositionEmpty(chessmanScript.GetXBoard(), chessmanScript.GetYBoard());

        // Move reference chess piece to this position
        chessmanScript.SetXBoard(matrixX);
        chessmanScript.SetYBoard(matrixY);
        chessmanScript.SetCoords();

        // Update the matrix
        gameScript.SetPosition(reference);

        // Destroy the move plates including self
        chessmanScript.DestroyMovePlates();

        // Switch Current Player (this also checks for check / checkmate / stalemate)
        gameScript.NextTurn();
    }

    public void SetCoords(int x, int y)
    {
        matrixX = x;
        matrixY = y;
    }

    public void SetReference(GameObject obj)
    {
        reference = obj;
    }

    public GameObject GetReference()
    {
        return reference;
    }
}