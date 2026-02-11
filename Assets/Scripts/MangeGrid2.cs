using System.Collections.Generic;
using UnityEngine;
using System.Collections; // Add this at the top

public class ManageGrid2: MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    
    [Header("Visualization")]
    public bool showGridInGame = true;
    public bool showVectors = true;
    public Color gridColor = Color.green;
    public Color cellCenterColor = Color.blue;
    
    private Grid[,] grid;
    private Vector3 gridOrigin;


    // Add these fields to your ManageGrid class
    [Header("Flood Fill Settings")]
    public Vector2Int floodStartPosition = new Vector2Int(5, 5);
    public float initialFloodStrength = 1f;
    public float decayPerStep = 0.3f; // How much strength is lost when moving to neighbor
    public float minFloodStrength = 0.1f; // Stop when below this

    private float[,] floodStrengths;
    private bool[,] visited;

    private bool[,] obstacleGrid;

    public bool enableMouseClick = true;

    [Header("Flood Animation")]
    public float floodStepDelay = 0.1f; // Delay between each wave of flood
    private Coroutine currentFloodCoroutine;

    [Header("Prefab Spawning")]
    public GameObject objectPrefab;  // Drag your prefab here
    public float maxLifespan = 3f;   // Maximum lifespan (for flood strength = 0)
    public float minLifespan = 0.5f; // Minimum lifespan (for flood strength = 1)

    

    void Awake()
    {
        InitializeGrid();
        InitializeSomeObstacles();
    }


    public Vector2 GetFlowDirection(int x, int y)
    {
        if (!IsValidCell(x, y) || floodStrengths == null)
            return Vector2.zero;
        
        // Calculate gradient (direction of increasing flood strength)
        float current = floodStrengths[x, y];
        
        // Get flood strengths from neighbors
        float north = GetFloodStrengthAt(x, y + 1);
        float south = GetFloodStrengthAt(x, y - 1);
        float east = GetFloodStrengthAt(x + 1, y);
        float west = GetFloodStrengthAt(x - 1, y);
        
        // Calculate gradient vector
        Vector2 gradient = new Vector2(
            east - west,  // x component
            north - south // y component
        ).normalized;
        
        return gradient;
    }


    // Add this method to check if a cell is an obstacle
    public bool IsObstacle(int x, int y)
    {
        // First check bounds
        if (x < 0 || x >= width || y < 0 || y >= height)
            return true; // Treat out of bounds as obstacle
        
        // Check if obstacleGrid is initialized
        if (obstacleGrid == null)
            return false;
        
        return obstacleGrid[x, y];
    }

    // Initialize obstacles (optional, minimal code)
    void InitializeSomeObstacles()
    {
        obstacleGrid = new bool[width, height];
        
        // Find all colliders in the scene
        Collider2D[] allColliders = FindObjectsOfType<Collider2D>();
        
        foreach (Collider2D collider in allColliders)
        {
            // Skip if it's on the grid object itself
            if (collider.gameObject == this.gameObject) continue;
            
            // Check each grid cell
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector2 cellCenter = grid[x, y].worldPosition;
                    
                    // Use OverlapPoint for accurate shape detection
                    if (collider.OverlapPoint(cellCenter))
                    {
                        obstacleGrid[x, y] = true;
                    }
                }
            }
        }
    }


    private void FloodCell(int x, int y, float strength, float distanceFromSource = 0f)
    {
        // Check bounds
        if (!IsValidCell(x, y)) return;
        
        // Check if visited already
        if (visited[x, y]) return;
        
        // Calculate distance-based strength
        // The further from source, the weaker the flood
        float distanceBasedStrength = initialFloodStrength - (distanceFromSource * decayPerStep);
        
        // Check if strength is too low
        if (distanceBasedStrength < minFloodStrength) return;
        
        // Mark as visited and set strength
        visited[x, y] = true;
        floodStrengths[x, y] = distanceBasedStrength;
        
        // Get neighbors including diagonals for smoother circle
        Grid currentCell = GetCell(x, y);
        if (currentCell != null)
        {
            List<Grid> neighbors = GetNeighbors(currentCell, true); // Include diagonals
            
            foreach (Grid neighbor in neighbors)
            {
                // Calculate Euclidean distance from source to this neighbor
                float neighborDistance = Vector2Int.Distance(
                    new Vector2Int(x, y), 
                    new Vector2Int(neighbor.gridPosition.x, neighbor.gridPosition.y)
                ) + distanceFromSource;
                
                // Recursively flood neighbor with updated distance
                FloodCell(neighbor.gridPosition.x, neighbor.gridPosition.y, strength, neighborDistance);
            }
        }
    }

    public void StartFloodAtCell(int x, int y)
    {
        if (!IsValidCell(x, y) || obstacleGrid[x, y])
        {
            Debug.LogWarning($"Cannot start flood at ({x}, {y}) - invalid or obstacle cell");
            return;
        }

        // Stop any existing flood animation
        if (currentFloodCoroutine != null)
        {
            StopCoroutine(currentFloodCoroutine);
        }
        
        // Start new flood animation
        currentFloodCoroutine = StartCoroutine(AnimatedFlood(x, y));
    }

    private IEnumerator AnimatedFlood(int startX, int startY)
    {
        ClearFlood();
        
        // Initialize arrays
        floodStrengths = new float[width, height];
        visited = new bool[width, height];
        
        // Queue for BFS with distance tracking
        Queue<(Vector2Int position, float distance)> currentWave = new Queue<(Vector2Int, float)>();
        Queue<(Vector2Int position, float distance)> nextWave = new Queue<(Vector2Int, float)>();
        
        // Start with the initial cell
        currentWave.Enqueue((new Vector2Int(startX, startY), 0f));
        floodStrengths[startX, startY] = initialFloodStrength;
        visited[startX, startY] = true;
        
        Debug.Log($"Starting circular flood at cell ({startX}, {startY})");
        
        int waveNumber = 0;
        
        while (currentWave.Count > 0)
        {
            waveNumber++;
            Debug.Log($"Flood wave {waveNumber} - {currentWave.Count} cells to process");
            
            // Process all cells in the current wave
            while (currentWave.Count > 0)
            {
                var (current, distance) = currentWave.Dequeue();
                float currentStrength = floodStrengths[current.x, current.y];
                
                // Get all neighbors (including diagonals for circular pattern)
                List<Grid> neighbors = GetNeighbors(grid[current.x, current.y], true);
                
                foreach (Grid neighbor in neighbors)
                {
                    int nx = neighbor.gridPosition.x;
                    int ny = neighbor.gridPosition.y;
                    
                    // Skip if already visited or is obstacle
                    if (!IsValidCell(nx, ny) || visited[nx, ny] || obstacleGrid[nx, ny])
                        continue;
                    
                    // Calculate Euclidean distance from start to this neighbor
                    float neighborDistance = Vector2Int.Distance(
                        new Vector2Int(startX, startY), 
                        new Vector2Int(nx, ny)
                    );
                    
                    // Calculate strength based on distance
                    float newStrength = initialFloodStrength - (neighborDistance * decayPerStep);
                    
                    // Only add to next wave if strength is enough
                    if (newStrength >= minFloodStrength)
                    {
                        floodStrengths[nx, ny] = newStrength;
                        visited[nx, ny] = true;
                        nextWave.Enqueue((new Vector2Int(nx, ny), neighborDistance));
                    }
                }
            }
            
            // Wait before processing the next wave
            yield return new WaitForSeconds(floodStepDelay);
            
            // Prepare for next wave
            currentWave = new Queue<(Vector2Int, float)>(nextWave);
            nextWave.Clear();
        }
        
        Debug.Log($"Circular flood animation completed after {waveNumber} waves");
        currentFloodCoroutine = null;
    }
    void InitializeGrid()
    {
        grid = new Grid[width, height];
        gridOrigin = transform.position;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 worldPos = new Vector2(
                    gridOrigin.x + (x+0.5f) * cellSize,
                    gridOrigin.y + (y+0.5f) * cellSize
                );
                
                grid[x, y] = new Grid(new Vector2Int(x, y), worldPos);
            }
        }
        
        Debug.Log($"Grid initialized at: {gridOrigin}");
    }
    
    bool IsValidCell(int x, int y)
    {
        // Check bounds
        if (x < 0 || x >= width || y < 0 || y >= height) 
            return false;
        
        // Check if it's an obstacle (if obstacleGrid exists)
        if (obstacleGrid != null && obstacleGrid[x, y]) 
            return false;
            
        return true;
    }

    void Update()
    {
        // Toggle grid visualization with G key
        if (Input.GetKeyDown(KeyCode.G))
        {
            showGridInGame = !showGridInGame;
            Debug.Log($"Grid visualization: {showGridInGame}");
        }

        if (enableMouseClick && Input.GetMouseButtonDown(0)) // Left mouse button
        {
            HandleMouseClick();
        }
        
    }

        void HandleMouseClick()
    {
        // Get mouse position in world coordinates
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0; // Ensure z = 0 for 2D
        
        // Convert to grid coordinates
        Grid clickedCell = GetCellAtWorldPosition(mouseWorldPos);
        
        if (clickedCell != null)
        {
            // Start flood at clicked cell
            StartFloodAtCell(clickedCell.gridPosition.x, clickedCell.gridPosition.y);
        }
    }
    
    void OnDrawGizmos()
    {
        if (!showGridInGame) return;
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

        if (Application.isPlaying && floodStrengths != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (floodStrengths[x, y] > 0)
                    {
                        // Draw flood with transparency based on strength
                        float strength = floodStrengths[x, y];
                        Color floodColor = new Color(0, 0.5f, 1f, strength); // Blue with varying alpha
                        
                        Vector3 cellPos = new Vector3(grid[x, y].worldPosition.x, grid[x, y].worldPosition.y, 0);
                        Vector3 cellSizeVec = new Vector3(cellSize, cellSize, 0) * 0.8f;
                        
                        Gizmos.color = floodColor;
                        Gizmos.DrawCube(cellPos, cellSizeVec);
                    }
                }
            }
        }
    }   



    // Public method to check flood strength
    public float GetFloodStrengthAt(int x, int y)
    {
        if (floodStrengths == null || !IsValidCell(x, y))
            return 0f;
        return floodStrengths[x, y];
    }

    // Clear flood
    public void ClearFlood()
    {
        if (floodStrengths != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    floodStrengths[x, y] = 0f;
                }
            }
        }
    }
    
    // Public methods for accessing cells
    public Grid GetCell(int x, int y)
    {
        if (IsValidCell(x, y))
            return grid[x, y];
        return null;
    }
    
    public Grid GetCellAtWorldPosition(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.y - gridOrigin.y) / cellSize);
        return GetCell(x, y);
    }
    
    public List<Grid> GetNeighbors(Grid cell, bool includeDiagonals = false)
    {
        List<Grid> neighbors = new List<Grid>();
        Vector2Int pos = cell.gridPosition;
        
        Grid north = GetCell(pos.x, pos.y + 1);
        Grid south = GetCell(pos.x, pos.y - 1);
        Grid east = GetCell(pos.x + 1, pos.y);
        Grid west = GetCell(pos.x - 1, pos.y);
        
        if (north != null) neighbors.Add(north);
        if (south != null) neighbors.Add(south);
        if (east != null) neighbors.Add(east);
        if (west != null) neighbors.Add(west);
        
        if (includeDiagonals)
        {
            Grid northEast = GetCell(pos.x + 1, pos.y + 1);
            Grid northWest = GetCell(pos.x - 1, pos.y + 1);
            Grid southEast = GetCell(pos.x + 1, pos.y - 1);
            Grid southWest = GetCell(pos.x - 1, pos.y - 1);
            
            if (northEast != null) neighbors.Add(northEast);
            if (northWest != null) neighbors.Add(northWest);
            if (southEast != null) neighbors.Add(southEast);
            if (southWest != null) neighbors.Add(southWest);
        }
        
        return neighbors;
    }
    

}