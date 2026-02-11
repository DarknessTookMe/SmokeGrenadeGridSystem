using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SmokeAnimator : MonoBehaviour
{
    private Material smokeMaterial;
    private float startTime;
    
    [Header("Animation Settings")]
    public float lifespan = 3f;
    public Vector2 noiseSpeed = new Vector2(0.1f, 0.2f);
    public float noiseScale = 2f;
    public float turbulence = 0.5f;
    
    [Header("Movement")]
    public Vector2 driftDirection = new Vector2(0, 1);
    public float driftSpeed = 0.5f;
    
    [Header("Size Evolution")]
    public AnimationCurve sizeCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public Vector2 startSize = Vector2.one;
    public Vector2 maxSize = new Vector2(3, 3);
    
    void Start()
    {
        smokeMaterial = GetComponent<MeshRenderer>().material;
        startTime = Time.time;
        
        // Make material instance unique to this smoke puff
        smokeMaterial = Instantiate(smokeMaterial);
        GetComponent<MeshRenderer>().material = smokeMaterial;
        
        // Set initial properties
        transform.localScale = new Vector3(startSize.x, startSize.y, 1);
    }
    
    void Update()
    {
        float elapsed = Time.time - startTime;
        float lifePercent = elapsed / lifespan;
        
        if (lifePercent >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        
        // Animate properties
        float noiseOffset = elapsed * 0.5f;
        smokeMaterial.SetVector("_NoiseSpeed", new Vector4(
            noiseSpeed.x, 
            noiseSpeed.y, 
            0, 0
        ));
        
        // Animate noise scale (gets larger as smoke dissipates)
        float animatedScale = noiseScale * (1 + lifePercent * 2);
        smokeMaterial.SetFloat("_NoiseScale", animatedScale);
        
        // Fade out
        float alpha = 1 - lifePercent;
        Color currentColor = smokeMaterial.GetColor("_Color");
        currentColor.a = alpha;
        smokeMaterial.SetColor("_Color", currentColor);
        
        // Change size over time
        float sizeFactor = sizeCurve.Evaluate(lifePercent);
        Vector2 currentSize = Vector2.Lerp(startSize, maxSize, sizeFactor);
        transform.localScale = new Vector3(currentSize.x, currentSize.y, 1);
        
        // Drift upward
        transform.Translate(driftDirection * driftSpeed * Time.deltaTime);
        
        // Add random wobble
        float wobble = Mathf.Sin(elapsed * 3f) * 0.1f;
        transform.position += new Vector3(wobble, 0, 0) * Time.deltaTime;
        
    }
    
    void OnDestroy()
    {
        // Clean up material instance
        if (smokeMaterial != null)
            Destroy(smokeMaterial);
    }
}
