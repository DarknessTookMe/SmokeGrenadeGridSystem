using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Grid
{
    public Vector2Int gridPosition;
    public Vector2 worldPosition;
    public GameObject smokeInstance; // Reference to smoke prefab
    
    public Grid(Vector2Int gridPos, Vector2 worldPos)
    {
        gridPosition = gridPos;
        worldPosition = worldPos;
        smokeInstance = null;
    }
}