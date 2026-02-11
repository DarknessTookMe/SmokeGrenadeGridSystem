using UnityEngine;

public class GridParticle : MonoBehaviour
{
    [Header("Movement")]
    public float mass = 1f;

    public float maxSpeed = 10f;

    public Vector2 gravity = new Vector2(0, -9.8f); // Default downward gravity
    public bool useGravity = true;   
    
    private Vector2 velocity = Vector2.zero;
    private GridVector gridManager;
    
    void Start()
    {
        gridManager = FindObjectOfType<GridVector>();
        
        // Randomize color
        GetComponent<SpriteRenderer>().color = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f);
    }
    
    void Update()
    {
        if (gridManager == null) return;
        
        // Get current grid cell based on position
        GridCell cell = gridManager.GetCellAtWorldPosition(transform.position);
        Vector2 totalForce = Vector2.zero;
        
        if (cell != null)
        {
            totalForce += cell.force;
        }
        
        // Add gravity if enabled
        if (useGravity)
        {
            totalForce += gravity * mass; // F = m*g
        }
        
        // Apply total force
        if (totalForce != Vector2.zero)
        {
            velocity += totalForce / mass * Time.deltaTime;
        }
                
        if (cell != null && cell.force != Vector2.zero)
        {
            // Apply force (F = ma, so a = F/m)
            velocity += cell.force * gravity / mass * Time.deltaTime;

        }
        
        // Limit speed
        if (velocity.magnitude > maxSpeed)
        {
            velocity = velocity.normalized * maxSpeed;
        }
        
        // Update position
        transform.position += (Vector3)velocity * Time.deltaTime;
        
        // Check if outside grid (simple version)
        CheckBounds();
    }
    
    void CheckBounds()
    {
        // You can make this more sophisticated based on your grid size
        // This is just a simple version that destroys when too far
        float maxDistance = 20f; // Adjust based on your grid size
        if (Vector2.Distance(Vector2.zero, transform.position) > maxDistance)
        {
            Destroy(gameObject);
        }
    }
}