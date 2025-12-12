using System.Collections.Generic;
using UnityEngine;
using System.Collections; // Add this at the top

public class ManageGrid: MonoBehaviour
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



    [Header("Smoke Visualization")]
    public GameObject smokePrefab; // Drag your smoke prefab here in Inspector
    public float smokeHeightOffset = 0.5f; // Height above the cell
    public Transform smokeParent; // Optional: parent object for organization

    void Awake()
    {
        InitializeGrid();
        InitializeObstacle();
    }

    private IEnumerator FloodStepByStep(int startX, int startY)
    {
        // Clear previous flood
        ClearFlood();


        // NEW: Clear previous smoke if it exists
        if (smokeParent != null)
        {
            foreach (Transform child in smokeParent)
            {
                Destroy(child.gameObject);
            }
        }
    
        
        // Initialize arrays
        floodStrengths = new float[width, height];
        visited = new bool[width, height];


        // NEW: Store instantiated smoke objects
        GameObject[,] smokeInstances = new GameObject[width, height];
        
        // Priority queue that always keeps closest cells first
        List<(float distance, Vector2Int cell)> priorityQueue = new List<(float, Vector2Int)>();
        
        // Start with the initial cell
        priorityQueue.Add((0f, new Vector2Int(startX, startY)));
        visited[startX, startY] = true;
        
        int cellsFlooded = 0;
        
        while (priorityQueue.Count > 0 && cellsFlooded < maxCellsToFlood)
        {
            // Sort by distance EACH TIME before taking the next cell
            priorityQueue.Sort((a, b) => a.distance.CompareTo(b.distance));
            
            // Take the closest cell
            var (currentDistance, current) = priorityQueue[0];
            priorityQueue.RemoveAt(0);
            
            int x = current.x;
            int y = current.y;
            

            cellsFlooded++;

            if (smokePrefab != null)
            {
            // Calculate world position for the cell
            Vector3 worldPosition = new Vector3(
                gridOrigin.x + (x * cellSize) + (cellSize / 2f),
                gridOrigin.y + (y * cellSize) + (cellSize / 2f),
                smokeHeightOffset  // This adds height above the grid
            );
            
            // the prefab is getting a worldPosition value
            // which will be used to determine in the shader
            // how the smoke is going to look from that position
            //(every position in the world has a unique shader) 
            GameObject smokeInstance = Instantiate(
                smokePrefab, 
                worldPosition, 
                Quaternion.identity
            );      
        
        
            // Parent it for organization (optional)
            if (smokeParent != null)
            {
                smokeInstance.transform.parent = smokeParent;
            }
            
            // Store reference for later use
            smokeInstances[x, y] = smokeInstance;


        }   
            // Show this cell
            yield return new WaitForSeconds(stepDelay);
            
            // If we've reached our limit, stop
            if (cellsFlooded >= maxCellsToFlood) break;
            
            // Get ORTHOGONAL neighbors (not diagonals)
            List<Vector2Int> orthogonalNeighbors = GetNeighborPositions(x, y, false); // false = no diagonals
            
            // Check and add diagonal neighbors only if their orthogonal prerequisites are met
            List<Vector2Int> diagonalNeighbors = new List<Vector2Int>();
            if (useEuclideanDistance) // Only check diagonals if we're using Euclidean distance
            {
                // Get all 8 neighbors
                List<Vector2Int> allNeighbors = GetNeighborPositions(x, y, true);
                
                // Filter for diagonals
                foreach (Vector2Int neighbor in allNeighbors)
                {
                    int dx = Mathf.Abs(neighbor.x - x);
                    int dy = Mathf.Abs(neighbor.y - y);
                    
                    // If both dx and dy are 1, it's a diagonal
                    if (dx == 1 && dy == 1)
                    {
                        // Check if the two orthogonal cells adjacent to this diagonal are filled
                        // For a diagonal to the northeast (x+1, y+1), check east (x+1, y) and north (x, y+1)
                        Vector2Int orthogonal1 = new Vector2Int(neighbor.x, y); // Same x as diagonal, same y as current
                        Vector2Int orthogonal2 = new Vector2Int(x, neighbor.y); // Same x as current, same y as diagonal
                        
                        // Only add diagonal if BOTH orthogonal neighbors are visited/filled
                        if (IsValidCell(orthogonal1.x, orthogonal1.y) && 
                            IsValidCell(orthogonal2.x, orthogonal2.y) &&
                            visited[orthogonal1.x, orthogonal1.y] && 
                            visited[orthogonal2.x, orthogonal2.y])
                        {
                            diagonalNeighbors.Add(neighbor);
                        }
                    }
                }
            }
            
            // Combine orthogonal and eligible diagonal neighbors
            List<Vector2Int> allValidNeighbors = new List<Vector2Int>();
            allValidNeighbors.AddRange(orthogonalNeighbors);
            allValidNeighbors.AddRange(diagonalNeighbors);
            
            // Add neighbors to priority queue
            foreach (Vector2Int neighbor in allValidNeighbors)
            {
                int nx = neighbor.x;
                int ny = neighbor.y;
                
                if (!IsValidCell(nx, ny) || visited[nx, ny])
                    continue;
                
                if (obstacleGrid[nx, ny])
                    continue;
                
                // Calculate ACTUAL distance from start to this neighbor
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
            Debug.LogWarning($"Cannot start flood here");
            return;
        }
        
        // Store the start position
        floodStartPosition = new Vector2Int(x, y);
        
        if (stepByStep)
        {
            Debug.Log($"Start flood at cell ({x}, {y})");
            floodCoroutine = StartCoroutine(FloodStepByStep(x, y));
        }
    }

    void Update()
    {
        // make the flag true so you can use left click to start smoke flooding
        if (enableMouseClick && Input.GetMouseButtonDown(0)) 
        {
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
        
    }


    void InitializeObstacle()
    {
        obstacleGrid = new bool[width, height];
        
        // Layer mask to filter which layers to check (improves performance)
        LayerMask obstacleLayerMask = LayerMask.GetMask("Obstacle"); // Set your obstacles to this layer
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 cellCenter = grid[x, y].worldPosition;
                
                // Check if ANY collider overlaps with the cell area
                Collider2D[] overlappingColliders = Physics2D.OverlapBoxAll(
                    cellCenter, 
                    new Vector2(cellSize, cellSize), // Slightly smaller than cell
                    0f, // Rotation
                    obstacleLayerMask
                );
                
                // Mark as obstacle if any collider overlaps this cell
                if (overlappingColliders.Length > 0)
                {
                    obstacleGrid[x, y] = true;
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