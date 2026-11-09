using UnityEngine;

public class Chessman : MonoBehaviour
{
    [Header("References")]
    public GameObject controller;
    public GameObject movePlate;

    [Header("Positions")]
    private int xBoard = -1;
    private int yBoard = -1;

    [Header("Variables")]
    private string player;

    public Sprite black_queen, black_knight, black_bishop, black_king, black_rook, black_pawn;
    public Sprite white_queen, white_knight, white_bishop, white_king, white_rook, white_pawn;


}
