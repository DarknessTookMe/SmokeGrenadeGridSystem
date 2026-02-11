Shader "Custom/BillowNoise2D"
{
    Properties
    {
        //declare public variables and control by sliders in the inspector
        _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 2.0
        _NoiseSpeed ("Noise Speed", Vector) = (0.1, 0.2, 0, 0)
        _NoiseIntensity ("Noise Intensity", Range(0, 5)) = 1.0
        _Color ("Smoke Color", Color) = (0.8, 0.8, 0.8, 1)
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.5
        _ScrollDirection ("Scroll Direction", Vector) = (0, 0.5, 0, 0)
        _Turbulence ("Turbulence", Range(0, 2)) = 0.5

        _AbsorptionStrength ("Absorption Strength", Range(0, 5)) = 2.0
        _PathLengthScale ("Path Length Scale", Range(0.1, 5)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            //both Queue and RenderType are transparent
            //other things pillow the smoke should be 
            //rendered first
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha //blender factor (blend using alpha channel)
        // important to sami transparent object 
        ZWrite Off // not to reneder to depth buffer
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            //if we are working with start and update fucnt
            //in unity this should be included to work with 
            //the other side 
            #include "UnityCG.cginc"
            

            //object contain variables
            struct appdata
            {
                float4 vertex : POSITION; //4 floating points for position
                float2 uv : TEXCOORD0; //texture coordinates 
                float4 color : COLOR;
            };
            

            //vertex to fragments
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION; //screen space position
                float4 color : COLOR;
            };
            

            //get all the public varibles needed to render
            //match names with praperties
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _NoiseScale;
            float2 _NoiseSpeed;
            float _NoiseIntensity;
            float4 _Color;
            float _EdgeSoftness;
            float2 _ScrollDirection;
            float _Turbulence;
            float _AbsorptionStrength;
            float _PathLengthScale;
            float2 _Center;
            
            // Simple noise function for 2D
            float random(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // 2D Perlin-like noise
            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                
                // Smooth interpolation
                float2 u = f * f * (3.0 - 2.0 * f);
                
                // Four corners
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                
                // Mix
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            
            // Billow noise (makes it puffy)
            float billowNoise(float2 uv)
            {
                float n = noise(uv * 1.0) * 0.5;
                n += noise(uv * 2.0) * 0.25;
                n += noise(uv * 4.0) * 0.125;
                
                // Billow effect: abs() makes it puffy
                n = 1.0 - abs(n * 2.0 - 1.0);
                
                return n;
            }
            
            v2f vert (appdata v)
            {
                v2f o; //retrun v2f struct
                // local to world to view to clip to screen
                // so take vertex of the model and making it 
                // go throw all translation to clip (and screen space)
                o.vertex = UnityObjectToClipPos(v.vertex); //function provided by unity
                o.uv = TRANSFORM_TEX(v.uv, _MainTex); //tiling and offset applied here
                o.color = v.color;
                return o;
            }
            
            // pass v2f to the fragment function so 
            // it will turn to pixels on the screen
            fixed4 frag (v2f i) : SV_Target
            {
                // Animated UVs - primary upward movement (in world Z direction)
                float2 baseUV = i.uv * _NoiseScale;

                // Main upward scroll (positive Y in UV space = forward/upward in world Z)
                float2 scrollUV = baseUV;
                
                // If _ScrollDirection.y is positive, smoke moves "upward" in top-down view
                // This corresponds to forward/upward in world Z direction
                scrollUV.y += _ScrollDirection.y * _Time.y;
                
                // Optional horizontal drift (world X direction)
                scrollUV.x += _ScrollDirection.x * _Time.y * 0.3;
                
                // Create turbulence/wobble
                float timeOffset = _Time.y * 2.0;
                
                // Base turbulence for organic movement
                float2 turbulenceUV = scrollUV;
                
                // Main turbulence along the flow direction (vertical in UV = Z in world)
                turbulenceUV.y += sin(scrollUV.x * 3.0 + timeOffset) * _Turbulence * 0.15;
                
                // Side-to-side wobble (perpendicular to flow)
                turbulenceUV.x += cos(scrollUV.y * 2.5 + timeOffset * 0.8) * _Turbulence * 0.08;
                
                // Additional high-frequency turbulence
                turbulenceUV.x += sin(scrollUV.y * 5.0 + timeOffset * 1.5) * _Turbulence * 0.04;
                turbulenceUV.y += cos(scrollUV.x * 4.0 + timeOffset * 1.2) * _Turbulence * 0.06;
                
                // Generate layered noise
                float noise1 = billowNoise(turbulenceUV);
                float noise2 = billowNoise(turbulenceUV * 1.7 + float2(0.3, 0.7) + timeOffset * 0.4);
                float noise3 = billowNoise(turbulenceUV * 3.2 + float2(0.1, 0.4) + timeOffset * 0.25);
                
                // Combine noise layers
                float finalNoise = noise1 * 0.5 + noise2 * 0.35 + noise3 * 0.15;
                finalNoise = pow(finalNoise, _NoiseIntensity);
                
                // Create circular smoke shape
                float2 center = _Center;
                float dist = distance(i.uv, center);
                float circleMask = 1.0 - smoothstep(0.0, 1 + _EdgeSoftness, dist * 2.0);
                //float circleMask = exp(-dist * dist * 4.0);  // Gaussian falloff

                
                // Apply mask to noise - this becomes our density/thickness value
                float density = finalNoise * circleMask;
                
                // --- BEER'S LAW IMPLEMENTATION ---
                // Beer's Law: I = I₀ * exp(-σ * x)
                // Where: I = transmitted intensity, I₀ = initial intensity
                //        σ = absorption coefficient (density), x = path length
                
                // For smoke, we'll use density as absorption coefficient
                float absorptionCoefficient = density * _AbsorptionStrength;
                
                // Path length - you could make this more sophisticated
                // For a 2D sprite, we'll use the density itself as proxy for path length
                float pathLength = density * _PathLengthScale;
                
                // Apply Beer's Law to calculate transmission
                float transmission = exp(-absorptionCoefficient * pathLength);
                
                // The alpha should be (1 - transmission) - what gets absorbed
                float beerAlpha = 1.0 - transmission;
                
                // Color gradient
                float gradient = 1.0 - dist * 1.5;
                float flowGradient = 1.0 - (i.uv.y * 0.7);
                gradient *= flowGradient;
                
                // Apply Beer's Law to color too - darker where more light is absorbed
                float4 smokeColor = _Color * gradient * transmission;
                smokeColor.a = beerAlpha * _Color.a * i.color.a * flowGradient;
                
                return smokeColor;
            }
            ENDCG
        }
    }
}