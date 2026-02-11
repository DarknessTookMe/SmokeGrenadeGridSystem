using System.Collections.Generic;
using UnityEngine;

public class GridVector : MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    
    [Header("Visualization")]
    public bool showGridInGame = true;
    public bool showVectors = true;
    public float vectorScale = 0.5f;
    public Color gridColor = Color.green;
    public Color vectorColor = Color.yellow;
    public Color cellCenterColor = Color.blue;
    
    [Header("Vector Settings")]
    public bool randomizeVectorsOnStart = true;
    public float minVectorMagnitude = 0.1f;
    public float maxVectorMagnitude = 1f;

    public float scale = 5f; 
    
    private GridCell[,] grid;
    private Vector3 gridOrigin;


    [Header("Particles")]
    public GameObject particlePrefab;
    public int initialParticles = 5;
    
    void Awake()
    {
        InitializeGrid();
        
        if (randomizeVectorsOnStart)
        {
            RandomizeAllVectors();
        }


        SpawnParticlesAtCenter();
    }



    void SpawnParticlesAtCenter()
{
    if (particlePrefab == null) return;
    
    Vector2 center = gridOrigin + new Vector3(width * cellSize * 0.5f, height * cellSize * 0.5f, 0);
    
    for (int i = 0; i < initialParticles; i++)
    {
        Vector2 randomPos = center + Random.insideUnitCircle * 2f;
        Instantiate(particlePrefab, randomPos, Quaternion.identity);
    }
}
    
    void InitializeGrid()
    {
        grid = new GridCell[width, height];
        gridOrigin = transform.position;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 worldPos = new Vector2(
                    gridOrigin.x + (x+0.5f) * cellSize,
                    gridOrigin.y + (y+0.5f) * cellSize
                );
                
                grid[x, y] = new GridCell(new Vector2Int(x, y), worldPos);
            }
        }
        
        Debug.Log($"Grid initialized at: {gridOrigin}");
    }
    
    void RandomizeAllVectors()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                RandomizeVectorAt(x, y);
            }
        }
    }
    
    bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    
    void Update()
    {

        // Toggle grid visualization with G key
        if (Input.GetKeyDown(KeyCode.G))
        {
            showGridInGame = !showGridInGame;
            Debug.Log($"Grid visualization: {showGridInGame}");
        }
        
        // Toggle vectors with V key
        if (Input.GetKeyDown(KeyCode.V))
        {
            showVectors = !showVectors;
            Debug.Log($"Vector visualization: {showVectors}");
        }
        
        // Randomize all vectors with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            RandomizeAllVectors();
        }


        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 center = gridOrigin + new Vector3(width * cellSize * 0.5f, height * cellSize * 0.5f, 0);
            Instantiate(particlePrefab, center + Random.insideUnitCircle, Quaternion.identity);
        }
        
        // Clear all particles with C key
        if (Input.GetKeyDown(KeyCode.C))
        {
            GridParticle[] particles = FindObjectsOfType<GridParticle>();
            foreach (GridParticle particle in particles)
            {
                Destroy(particle.gameObject);
            }
        }

    }
    
    void OnDrawGizmos()
    {

        if (showVectors && Application.isPlaying && grid != null)
        {
            DrawVectors();
        }

        if (!showGridInGame) return;
        
        // Draw grid origin
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
        
        DrawGrid();
        
    }
    
    void DrawGrid()
    {

        
        // Draw grid border
        Gizmos.color = gridColor;
        Vector3 bottomLeft = gridOrigin;

        Vector3 topRight = gridOrigin + new Vector3(width * cellSize, height * cellSize, 0);
        
        Gizmos.DrawLine(bottomLeft, bottomLeft + new Vector3(width * cellSize, 0, 0)); // Bottom
        Gizmos.DrawLine(bottomLeft, bottomLeft + new Vector3(0, height * cellSize, 0)); // Left
        Gizmos.DrawLine(topRight, topRight - new Vector3(width * cellSize, 0, 0)); // Top
        Gizmos.DrawLine(topRight, topRight - new Vector3(0, height * cellSize, 0)); // Right
        
        // Draw grid lines
        Gizmos.color = new Color(gridColor.r, gridColor.g, gridColor.b, 0.3f);
        
        // Vertical lines
        for (int x = 0; x <= width; x++)
        {
            Vector3 start = bottomLeft + new Vector3(x * cellSize, 0, 0);
            Vector3 end = start + new Vector3(0, height * cellSize, 0);
            Gizmos.DrawLine(start, end);
        }
        
        // Horizontal lines
        for (int y = 0; y <= height; y++)
        {
            Vector3 start = bottomLeft + new Vector3(0, y * cellSize, 0);
            Vector3 end = start + new Vector3(width * cellSize, 0, 0);
            Gizmos.DrawLine(start, end);
        }
        
        // Draw cell centers (if grid is initialized)
        if (Application.isPlaying && grid != null)
        {
            Gizmos.color = cellCenterColor;
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 cellPos = new Vector3(grid[x, y].worldPosition.x, grid[x, y].worldPosition.y, 0);
                    Gizmos.DrawSphere(cellPos, 0.03f);
                    
                    // Draw small cell outline
                    Gizmos.DrawWireCube(cellPos, new Vector3(cellSize, cellSize, 0));
                }
            }
        }
    }
    
    void DrawVectors()
    {
        Gizmos.color = vectorColor;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GridCell cell = grid[x, y];
                if (cell.force != Vector2.zero)
                {
                    Vector3 startPos = new Vector3(cell.worldPosition.x, cell.worldPosition.y, 0);
                    Vector3 endPos = startPos + new Vector3(cell.force.x, cell.force.y, 0) * vectorScale;
                    
                    // Draw vector line
                    Gizmos.DrawLine(startPos, endPos);
                    
                    // Draw arrow head
                    Vector3 direction = (endPos - startPos).normalized;
                    float arrowSize = 0.1f;
                    
                    // Calculate perpendicular for arrow wings
                    Vector3 perp = new Vector3(-direction.y, direction.x, 0) * arrowSize;
                    
                    // Draw arrow wings
                    Gizmos.DrawLine(endPos, endPos - direction * arrowSize + perp * 0.5f);
                    Gizmos.DrawLine(endPos, endPos - direction * arrowSize - perp * 0.5f);
                    
                    // Draw small sphere at vector start
                    Gizmos.DrawSphere(startPos, 0.02f);
                }
            }
        }
    }
    
    // Public methods for accessing cells
    public GridCell GetCell(int x, int y)
    {
        if (IsValidCell(x, y))
            return grid[x, y];
        return null;
    }
    
    public GridCell GetCellAtWorldPosition(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.y - gridOrigin.y) / cellSize);
        return GetCell(x, y);
    }
    
    public List<GridCell> GetNeighbors(GridCell cell, bool includeDiagonals = false)
    {
        List<GridCell> neighbors = new List<GridCell>();
        Vector2Int pos = cell.gridPosition;
        
        GridCell north = GetCell(pos.x, pos.y + 1);
        GridCell south = GetCell(pos.x, pos.y - 1);
        GridCell east = GetCell(pos.x + 1, pos.y);
        GridCell west = GetCell(pos.x - 1, pos.y);
        
        if (north != null) neighbors.Add(north);
        if (south != null) neighbors.Add(south);
        if (east != null) neighbors.Add(east);
        if (west != null) neighbors.Add(west);
        
        if (includeDiagonals)
        {
            GridCell northEast = GetCell(pos.x + 1, pos.y + 1);
            GridCell northWest = GetCell(pos.x - 1, pos.y + 1);
            GridCell southEast = GetCell(pos.x + 1, pos.y - 1);
            GridCell southWest = GetCell(pos.x - 1, pos.y - 1);
            
            if (northEast != null) neighbors.Add(northEast);
            if (northWest != null) neighbors.Add(northWest);
            if (southEast != null) neighbors.Add(southEast);
            if (southWest != null) neighbors.Add(southWest);
        }
        
        return neighbors;
    }
    
    // Helper method to set vector at specific cell
    public void SetVectorAt(int x, int y, Vector2 force)
    {
        if (IsValidCell(x, y))
        {
            grid[x, y].force = force;
        }
    }
    
    // Clear all vectors
    public void ClearAllVectors()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y].force = Vector2.zero;
            }
        }
    }





    public void RandomizeVectorAt(int x, int y)
    {
        if (IsValidCell(x, y))
        {   
            // Option A: Fixed magnitude
            float baseMagnitude = (minVectorMagnitude + maxVectorMagnitude) * 0.5f;

            // Option B: Use your existing range
            //float baseMagnitude = Random.Range(minVectorMagnitude, maxVectorMagnitude);
            
            // But this would make it random again - defeats Perlin's purpose!
            // Use different offsets for x and y to sample different parts of noise
            float noiseX = Mathf.PerlinNoise(x * scale, 0) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(0, y * scale) * 2f - 1f;
            
            Vector2 perlinDirection = new Vector2(noiseX, noiseY).normalized;
            
            // Option 1: Fixed magnitude
            grid[x, y].force = perlinDirection * baseMagnitude;
            
            // Option 2: Perlin-controlled magnitude too
            float magnitudeNoise = Mathf.PerlinNoise(x * scale * 0.5f, y * scale * 0.5f);
            float magnitude = Mathf.Lerp(minVectorMagnitude, maxVectorMagnitude, magnitudeNoise);
            grid[x, y].force = perlinDirection * magnitude;
        }
    }
}