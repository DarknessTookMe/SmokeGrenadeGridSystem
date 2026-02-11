using System.Collections.Generic;
using UnityEngine;
using System.Collections; // Add this at the top

public class ManageGrid3: MonoBehaviour
{
    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    
    [Header("Visualization")]
    public bool showGridInGame = true;
    public Color gridColor = Color.green;
    public Color cellCenterColor = Color.blue;
    
    private Grid[,] grid;
    private Vector3 gridOrigin;


    // Add these fields to your ManageGrid class
    [Header("Flood Fill Settings")]
    public Vector2Int floodStartPosition = new Vector2Int(5, 5);
    public float initialFloodStrength = 10f;
    public float decayPerStep = 0.3f; // How much strength is lost when moving to neighbor
    public float minFloodStrength = 0.1f; // Stop when below this

    [Header("Fixed Count Flood Settings")]
    public int maxCellsToFlood = 50; // Fixed number of cells to fill
    public bool useEuclideanDistance = true; // True for circles, false for Manhattan

    private float[,] floodStrengths;
    private bool[,] visited;

    private bool[,] obstacleGrid;

    public bool enableMouseClick = true;


[Header("Step-by-step Flood")]
public bool stepByStep = true;
public float stepDelay = 0.05f; // Delay between each cell
private Coroutine floodCoroutine;

    void Awake()
    {
        InitializeGrid();
        InitializeSomeObstacles();
    }

    private IEnumerator FloodStepByStep(int startX, int startY)
    {
        // Clear previous flood
        ClearFlood();
        
        // Initialize arrays
        floodStrengths = new float[width, height];
        visited = new bool[width, height];
        
        // Priority queue that always keeps closest cells first
        // Using List and sorting each iteration
        List<(float distance, Vector2Int cell)> priorityQueue = new List<(float, Vector2Int)>();
        
        // Start with the initial cell
        priorityQueue.Add((0f, new Vector2Int(startX, startY)));
        visited[startX, startY] = true;
        
        int cellsFlooded = 0;
        
        while (priorityQueue.Count > 0 && cellsFlooded < maxCellsToFlood)
        {
            // CRITICAL: Sort by distance EACH TIME before taking the next cell
            // This ensures we always take the CLOSEST cell to the start
            priorityQueue.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            // Take the closest cell
            var (currentDistance, current) = priorityQueue[0];
            priorityQueue.RemoveAt(0);
            
            int x = current.x;
            int y = current.y;
            
            // Calculate strength for this cell
            float strength = Mathf.Max(minFloodStrength, 
                initialFloodStrength - (currentDistance * decayPerStep));
            
            // Visualize this cell
            floodStrengths[x, y] = strength;
            cellsFlooded++;
            
            // Show this cell
            yield return new WaitForSeconds(stepDelay);
            
            // If we've reached our limit, stop
            if (cellsFlooded >= maxCellsToFlood) break;
            
            // Get all neighbors
            List<Vector2Int> neighbors = GetNeighborPositions(x, y, useEuclideanDistance);
            
            // Add neighbors to priority queue
            foreach (Vector2Int neighbor in neighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                
                if (!IsValidCell(nx, ny) || visited[nx, ny])
                    continue;
                
                if (obstacleGrid[nx, ny])
                    continue;
                
                // Calculate ACTUAL distance from start to this neighbor
                // NOT distance from current cell!
                float distanceFromStart;
                if (useEuclideanDistance)
                {
                    // Euclidean distance from start
                    distanceFromStart = Vector2.Distance(
                        new Vector2(startX, startY),
                        new Vector2(nx, ny)
                    );
                }
                else
                {
                    // Manhattan distance from start
                    distanceFromStart = Mathf.Abs(nx - startX) + Mathf.Abs(ny - startY);
                }
                
                visited[nx, ny] = true;
                priorityQueue.Add((distanceFromStart, neighbor));
            }
        }
        
        Debug.Log($"Step-by-step circle flood completed! Filled {cellsFlooded} cells");
    }


        // Helper method to get neighbor positions
    private List<Vector2Int> GetNeighborPositions(int x, int y, bool includeDiagonals)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>
        {
            new Vector2Int(x + 1, y),     // East
            new Vector2Int(x - 1, y),     // West
            new Vector2Int(x, y + 1),     // North
            new Vector2Int(x, y - 1)      // South
        };
        
        if (includeDiagonals)
        {
            neighbors.Add(new Vector2Int(x + 1, y + 1)); // Northeast
            neighbors.Add(new Vector2Int(x - 1, y + 1)); // Northwest
            neighbors.Add(new Vector2Int(x + 1, y - 1)); // Southeast
            neighbors.Add(new Vector2Int(x - 1, y - 1)); // Southwest
        }
        
        return neighbors;
    }

    public void StartFloodAtCell(int x, int y)
    {
        if (!IsValidCell(x, y) || obstacleGrid[x, y])
        {
            Debug.LogWarning($"Cannot start flood at ({x}, {y}) - invalid or obstacle cell");
            return;
        }
        
    // Stop any running flood coroutine
        if (floodCoroutine != null)
        {
            StopCoroutine(floodCoroutine);
        }
        
        // Store the start position
        floodStartPosition = new Vector2Int(x, y);
        
        if (stepByStep)
        {
            Debug.Log($"Starting step-by-step flood at cell ({x}, {y})");
            floodCoroutine = StartCoroutine(FloodStepByStep(x, y));
        }
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

    
}