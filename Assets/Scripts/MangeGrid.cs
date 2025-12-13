// author name: Amir Alrishan
// Description: A ManagerGrid system for top-down 2D games 
// to render and simulate how a smoke grenade will look from that view 
// The script use flood fill algorithm to fill grid cells and if prefab exist 
// the cells will be fille with that prefab will avoiding obstcales in the scene

using System.Collections.Generic; 
using UnityEngine;
using System.Collections; // Add this at the top

public class ManageGrid: MonoBehaviour
{   
    [Header("Grid Settings")]
    public int width = 50;
    public int height = 50;
    public float cellSize = 1f;
    
    [Header("Visualization")]
    public bool showGridInGame = true; // for setting up the grid 
    public Color gridColor = Color.green; //color of the grid if scene is not runnning 
    public Color cellCenterColor = Color.blue; //if scene is running
    
    private Grid[,] grid;
    private Vector3 gridOrigin;

    //when clicking the mouse the flood will start from here 
    private Vector2Int floodStartPosition; // maybe used in the future to clear the smoke



    [Header("Fixed Count Flood Settings")]
    public int maxCellsToFlood = 50; // Fixed number of cells to fill
    public bool useEuclideanDistance = true; // True for circles, false for Manhattan 

    //for the grid and smoke refrence and obstacles positions
    private bool[,] visited;
    private GameObject[,] smokeInstances;
    private bool[,] obstacleGrid;


    [Header("Flood Fill setting")]
    public bool floodActive = true;
    public bool enableMouseClick = true;
    public float floodingSpeed = 0.005f; // flood speed 
    public bool keepSmoke = false;
    private Coroutine floodCoroutine;


    // Get the prefab for the smoke 
    [Header("Smoke Visualization")]
    public GameObject smokePrefab; // Drag your smoke prefab here in Inspector
    public Transform smokeParent; // used in the future to clear the smoke 
    public bool showSmoke = true; 

    void Awake()
    {
        InitializeGrid();
        InitializeObstacle();
    }

    //A step by step flood fill algorithm the fill cells inside a grid 
    private IEnumerator FloodFill(int startX, int startY)
    {
        // Clear previous flood
        ClearFlood();

        if(!keepSmoke)
        {
            DestoryAllSmoke();
        }
        //some lists for rendering
        visited = new bool[width, height];
        smokeInstances = new GameObject[width, height];
        
        // Priority queue that always keeps closest cells first
        List<(float distance, Vector2Int cell)> priorityQueue = new List<(float, Vector2Int)>();
        // Start with the source of the flood cell
        priorityQueue.Add((0f, new Vector2Int(startX, startY)));
        visited[startX, startY] = true;
        
        int cellsFlooded = 0; //counter to compare to maxCellsToFlood
        
        //Start the flooding here
        while (priorityQueue.Count > 0 && cellsFlooded < maxCellsToFlood)
        {

            int minIndex = 0; // for optimization 
            float minDistance = priorityQueue[0].distance;
            // Sorting is expensive so use diffrenet method
            //priorityQueue.Sort((a, b) => a.distance.CompareTo(b.distance));
            for(int i = 1; i < priorityQueue.Count; i++)
            {
                if (priorityQueue[i].distance < minDistance)
                {
                    minDistance = priorityQueue[i].distance;
                    minIndex = i;
                }
            }

            //closest cell after sorting 
            //current info will be used to create a new smoke in a new gird 
            var (currentDistance, current) = priorityQueue[minIndex];
            //priorityQueue.RemoveAt(0); change for opt
            priorityQueue[minIndex] = priorityQueue[priorityQueue.Count - 1];
            priorityQueue.RemoveAt(priorityQueue.Count - 1); //removed from the queue and create smoke there
            
            //Where we are now x and y 
            int x = current.x;
            int y = current.y;
            cellsFlooded++; //keep count to use later and terminate the function

            // we got the smoke prefab ready 
            if (smokePrefab != null && showSmoke == true)
            {
            // Calculate world position for the cell
            Vector3 worldPosition = new Vector3(
                gridOrigin.x + (x * cellSize) + (cellSize / 2f),
                gridOrigin.y + (y * cellSize) + (cellSize / 2f),
                0
            );
            
            // the prefab is getting a worldPosition value
            // which will be used to determine in the shader
            // how the smoke is going to look from that position
            // (every position in the world has a unique shader) 
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
            // waiting intervel after rendering the smoke in the cell
            yield return new WaitForSeconds(floodingSpeed);
            
            // control how many cells will get the smoke and break out
            // if down (you can control the number in the inspection)
            if (cellsFlooded >= maxCellsToFlood) break;//------------------>Break 
            

            // Get all neighbors of the cell at (x,y)
            List<Vector2Int> neighborsToCheck = GetNeighborPositions(x, y, useEuclideanDistance);

            foreach (Vector2Int neighbor in neighborsToCheck)
            {
                // ok lets diagnose every neighbor and see if 
                // they should enter the queue
                int nx = neighbor.x;
                int ny = neighbor.y;
                
                // Skip for now if the smoke in a close shape to stop the rendering
                // (will work on it in the future to make the smoke visit the cells again in close areas) 
                if (!IsValidCell(nx, ny) || visited[nx, ny])
                    continue;
                
                // Diagonals with useEuclideanDistance 
                // when active: only a diagonal nieghbor will have both x and y abs distance
                // as one from the cell we are in
                if (useEuclideanDistance && Mathf.Abs(nx - x) == 1 && Mathf.Abs(ny - y) == 1)
                {
                    //if that is the case this is not enough to put this neighbor in the queue
                    //first we will see if any orthogonal nieghbor it has has already been visited 
                    //for example: we are diagnosing a northeast cell!
                    //(x+1, y+1) we care about it east and north nieghbors 
                    //because is south and west nieghbor are the (x, y) orthogonla niegbors
                    Vector2Int orth1 = new Vector2Int(nx, y); //get the same neighbor (x+1, y) or our example or east
                    Vector2Int orth2 = new Vector2Int(x, ny);
                    
                    if (!IsValidCell(orth1.x, orth1.y) || !visited[orth1.x, orth1.y] ||
                        !IsValidCell(orth2.x, orth2.y) || !visited[orth2.x, orth2.y])
                        continue; // if none is valid then this nieghbor is not ready to be filled yet
                }
                
                // Calculate distance important infomation for the prioirtyQueue
                // based on it the queue will be sorted depending on the distance 
                //if euclidean use Pythagorean theorem else Manhattan distance
                float distanceFromStart = useEuclideanDistance ? 
                    Vector2.Distance(new Vector2(startX, startY), new Vector2(nx, ny)) :
                    Mathf.Abs(nx - startX) + Mathf.Abs(ny - startY);
                //note: manhatten will give us diamond shape
                //so if we are using euclidean then this will be balance
                //to a cicler shape (if diagonal nieghbor is ready to be filled of course)   
                
                visited[nx, ny] = true;
                priorityQueue.Add((distanceFromStart, neighbor)); //put in the queue
            }
        }
        Debug.Log($"The flood has filled {cellsFlooded} cells");
    }

    // method to get neighbors' positions of any (x,y) cell
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
        //flood can't be started outside the grid or on an obstacle 
        if (!IsValidCell(x, y) || obstacleGrid[x, y])
        {
            Debug.LogWarning($"Cannot start flood here");
            return;
        }

        //prevent flooding if the grid cells number is less than the maxCellsToFlood 
        if (maxCellsToFlood > width * height)
        {
            Debug.LogWarning("The amount of cells you want to flood more than the grid cells!");
            return;
        }
        
        // Store the start position
        floodStartPosition = new Vector2Int(x, y);
        
        if (floodActive)
        {
            Debug.Log($"Start flood at ({x}, {y})");
            floodCoroutine = StartCoroutine(FloodFill(x, y));
        }
    }

    void Update()
    {
        // make the flag true so you can use left click to start smoke flooding
        if (enableMouseClick && Input.GetMouseButtonDown(0)) 
        {
            if (Camera.main == null)
            {
                Debug.Log("Camera is not tagged");
                return;
            }  //handle no camera tagged issue
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
    
    //get all needed refrence to the obstacles in the scene
    void InitializeObstacle()
    {
        obstacleGrid = new bool[width, height];
        // layer mask to filter 
        LayerMask obstacleLayerMask = LayerMask.GetMask("Obstacle"); // Set your obstacles to this layer
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 cellCenter = grid[x, y].worldPosition;
                // Check if ANY collider overlaps with the cell area
                Collider2D[] overlappingColliders = Physics2D.OverlapBoxAll(
                    cellCenter, 
                    new Vector2(cellSize, cellSize), // could be changed but for this implementation 
                    0f,                              //and then small grids the smoke go around the obstacle
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

    //you can control cell size to get more detiles 
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

    //importent 
    bool IsValidCell(int x, int y)
    {
        // Check bounds first
        if (x < 0 || x >= width || y < 0 || y >= height) 
            return false;
        // Check if it's an obstacle (if obstacle exists)
        if (obstacleGrid != null && obstacleGrid[x, y]) 
            return false;
        return true;
    }


    //visulize the grid and the flood fill algo used for debuging 
    void OnDrawGizmos()
    {
        if (!showGridInGame) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.1f);
        DrawGrid();
    }
    

    void DrawGrid()
    {
        // Draw grid border so the game dev can use that as a refrence to set up
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
        // when the scene is running
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

        // flood fill algo will be visible in red if everything is working 
        if (Application.isPlaying && visited != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (visited[x, y] == true) //fix this later 
                    {     
                        Vector3 cellPos = new Vector3(grid[x, y].worldPosition.x, grid[x, y].worldPosition.y, 0);
                        Vector3 cellSizeVec = new Vector3(cellSize, cellSize, 0) * 0.8f;
                        
                        Gizmos.color = Color.red;
                        Gizmos.DrawCube(cellPos, cellSizeVec);
                    }
                }
            }
        }
    }   

    // Clear flooded cells when needed
    public void ClearFlood() //fix this one too
    {
        if (visited != null) //going throw all flooded cells to clear them
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    visited[x, y] = false;
                }
            }
        }
    }

    //Destroy all the smoke in the scene
    private void DestoryAllSmoke()
    {
        if (smokeInstances == null) return;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (smokeInstances[x, y] != null)
                {
                    Destroy(smokeInstances[x, y]);
                }
            }
        }
        
        smokeInstances = null;
    }
    
}