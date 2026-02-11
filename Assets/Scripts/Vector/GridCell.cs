using UnityEngine;

[System.Serializable]
public class GridCell
{
    public Vector2Int gridPosition;  // X,Y coordinates in the grid
    public Vector2 worldPosition;    // Position in Unity's world space (2D!)
    public Vector2 force;           // The force vector at this cell
    
    public GridCell(Vector2Int gridPos, Vector2 worldPos)
    {
        gridPosition = gridPos;
        worldPosition = worldPos;
        force = Vector2.zero;
    }
}