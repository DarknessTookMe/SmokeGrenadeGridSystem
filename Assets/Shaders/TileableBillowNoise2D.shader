Shader "Unlit/TileableBillowNoise2D"
{
    Properties
    {
        //declare public variables and control by sliders in the inspector
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.1, 50)) = 2.0
        _NoiseIntensity ("Noise Intensity", Range(0, 5)) = 1.0
        _Color ("Smoke Color", Color) = (0.8, 0.8, 0.8, 1)
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.5
        _ScrollDirection ("Scroll Direction", Vector) = (0, 0.5, 0, 0)
        _Turbulence ("Turbulence", Range(0, 2)) = 0.5
        _AbsorptionStrength ("Absorption Strength", Range(0, 5)) = 2.0
        _PathLengthScale ("Path Length Scale", Range(0.1, 5)) = 1.0
        _PowderEffect ("Powder Sugar Effect", Range(0, 3)) = 1.0 
        // World-space tiling parameters
        _WorldScale ("World Scale", Range(0.001, 0.5)) = 0.5 // zome in large cloud, zoom out detailed

        _LightDirection ("Light Direction", Vector) = (0.5, 0.5, 0, 0)
        _LightColor ("Light Color", Color) = (1, 0.9, 0.8, 1)
        _LightIntensity ("Light Intensity", Range(0, 5)) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            //both Queue and RenderType are transparent
            //other things below the smoke should be 
            //rendered first
            "Queue" = "Transparent" //render after opaque objects
            "RenderType" = "Transparent" 
            "IgnoreProjector" = "True"// no projection 
            "PreviewType" = "Plane"
        }
        

        Blend SrcAlpha OneMinusSrcAlpha //blender factor (blend using alpha channel)
         // important to sami transparent object 
        ZWrite Off // not to reneder to depth buffer
        Cull Off //render both sides 
        
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
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0; //texture coord
                float4 color : COLOR;
            };
            

            //vertex to fragments
            struct v2f
            {
                float2 uv : TEXCOORD0;  //local UV
                float4 vertex : SV_POSITION; //clip position
                float4 color : COLOR; // pass through vertex
                float2 worldPos : TEXCOORD1; //to make the tiling seamless and uncut
            };
            

            //get all the public varibles needed to render
            //match names with praperties
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _NoiseScale;
            float _NoiseIntensity;
            float4 _Color;
            float _EdgeSoftness;
            float2 _ScrollDirection;
            float _Turbulence;
            float _AbsorptionStrength;
            float _PathLengthScale;
            float _PowderEffect;
            
            // World-space tiling
            float _WorldScale;

            float3 _LightDirection;
            float4 _LightColor;
            float _LightIntensity;
            
            //hash function for randomness
            float hash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                          dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            
            
            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                
                // Quintic interpolation curve
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                
                // Gradients (4 corner sampling)
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            
            // FBM for billow noise
            // nice whispy shapes, but it lacks those bulges and billows 
            float fbm(float2 uv, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise(uv * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value;
            }
            

            // Billow noise (makes it puffy)
            float billowNoise(float2 uv)
            {
                // FBM noise
                float n = fbm(uv, 4); //4 octaves
                
                // using inverted abs value on a noise 
                // to create the puffy billow effect
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);//tiling and offset applied here
                o.color = v.color;
                
                // Get world position for seamless tiling
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos.xy;  //2d need X and Y only
                
                return o;
            }
            
            float PowderSugarEffect(float density)
            {
                return 1.0 - exp(-density * _PowderEffect);
            }
            

            // pass v2f to the fragment function so 
            // it will turn to pixels on the screen            
            fixed4 frag (v2f i) : SV_Target
            {
                //(World Noise) because we want to connect all tiles (flood grids) together
                //convert world position to noise space
                float2 worldUV = i.worldPos * _WorldScale;
                //i.worldPos becomes the coord system of the noise
                //so moving prefab is like reveling noise underneath                
                
                // Base noise UV
                float2 baseUV = worldUV * _NoiseScale;
                
                // scrolling (_Time.y is time)
                float2 scrollUV = baseUV;
                scrollUV += _ScrollDirection * _Time.y;
                
                // turbulence using world coord
                float timeOffset = _Time.y * 2.0;
                float2 turbulenceUV = scrollUV;
                
                // Use different frequencies for turbulence
                turbulenceUV.x += sin(scrollUV.y * 3.0 + timeOffset) * _Turbulence * 0.15;
                turbulenceUV.y += cos(scrollUV.x * 2.5 + timeOffset * 0.8) * _Turbulence * 0.1;
                
                // small offset to break symmetry
                // per-instance noise variation
                turbulenceUV += hash(i.worldPos * 0.1) * 0.1;
                
                //3 layers of noise with diffrent freq
                float noise1 = billowNoise(turbulenceUV);
                float noise2 = billowNoise(turbulenceUV * 1.7 + float2(0.3, 0.7) + timeOffset * 0.4);
                float noise3 = billowNoise(turbulenceUV * 3.2 + float2(0.1, 0.4) + timeOffset * 0.2);
                
                // Combine with different weights
                float finalNoise = noise1 * 0.625 + noise2 * 0.25 + noise3 * 0.125;
                
                // apply intensity
                finalNoise = pow(saturate(finalNoise), _NoiseIntensity);
                

                //local mask calculation
                float2 localUV = i.uv;
                float2 center = float2(0.5, 0.5);
                float dist = distance(localUV, center);
                
                // Circular mask with soft edges
                float circleMask = 1.0 - smoothstep(0.0, 0.75 + _EdgeSoftness, dist * 2.0);
                

                //Beer's Law: how much light get absorbed  
                // Combine world noise with local mask
                // density is not constant everywhere inside the smoke
                float density = finalNoise * circleMask;
                
                //this code be added!!!
                //add anisotropic scattering (scatting forward)
                //light doesn't scatter evenly inside the smoke
                //frostbite engine (EA) 
                //mixing between forward and backward henyey greenstein if light source exist
                //float phaseFunction = henyeygreenstenin(C, angle);
                //float phasefunction = mix(HenyeyGreenstein(-C, angel), HenyeyGreenstein(C, angel), K);
                //back scatting component

                //add powder suger effect by guerrilla games
                //cloud have darker edges and lighter creases
                //1-exp(-d * 2) //balance the absorption aspect 
                float powderAlpha = PowderSugarEffect(density);
                float3 lightDir = normalize(float3(_LightDirection.xy, 0.1));
                
                //make the smoke react to light and give it the powdery effect
                float3 lightsEffect = _LightColor * _LightIntensity * powderAlpha;

                // Apply smoke physical properties:
                // how much the light absorpt of the light 
                float absorption = density * _AbsorptionStrength; 
                float pathLength = density * _PathLengthScale; // how deep the light travel
                //all light passes to all light abosorbed
                float transmission = exp(-absorption * pathLength); //Beers low T = exp(-sigma * L)
                float beerAlpha = 1.0 - transmission; // convert to alpha
                
                // Color with gradient
                float gradient = 1.0 - dist * 1.5;
                gradient *= (1.0 - localUV.y * 0.7);
                
                // Final color
                float4 smokeColor = _Color * gradient;
                smokeColor.rgb *= transmission;
                smokeColor.rgb += lightsEffect;
                smokeColor.a = beerAlpha * _Color.a * i.color.a * gradient * powderAlpha;
                
                return smokeColor;
            }

            ENDCG
        }
    }
}