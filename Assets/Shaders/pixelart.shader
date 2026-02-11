Shader "Unlit/PixelTileableBillowNoise2D"
{
    Properties
    {
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
        _WorldScale ("World Scale", Range(0.001, 0.5)) = 0.5
        _LightDirection ("Light Direction", Vector) = (0.5, 0.5, 0, 0)
        _LightColor ("Light Color", Color) = (1, 0.9, 0.8, 1)
        _LightIntensity ("Light Intensity", Range(0, 5)) = 1.0
        
        // Pixelation controls
        _PixelSize ("Pixel Size", Range(1, 100)) = 8
        _PixelSmoothness ("Pixel Smoothness", Range(0, 1)) = 0.2
        _DitherAmount ("Dither Amount", Range(0, 1)) = 0.3
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent"
            "RenderType" = "Transparent" 
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 worldPos : TEXCOORD1;
                float2 screenPos : TEXCOORD2;
            };
            
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
            float _WorldScale;
            float3 _LightDirection;
            float4 _LightColor;
            float _LightIntensity;
            
            // Pixelation parameters
            float _PixelSize;
            float _PixelSmoothness;
            float _DitherAmount;
            
            // Bayer matrix for dithering
            static const float bayerMatrix[16] = {
                0.0,  8.0,  2.0,  10.0,
                12.0, 4.0,  14.0, 6.0,
                3.0,  11.0, 1.0,  9.0,
                15.0, 7.0,  13.0, 5.0
            };
            
            // Original noise functions kept intact
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
                
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            
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
            
            float billowNoise(float2 uv)
            {
                float n = fbm(uv, 4);
                n = 1.0 - abs(n * 2.0 - 1.0);
                return n;
            }
            
            float PowderSugarEffect(float density)
            {
                return 1.0 - exp(-density * _PowderEffect);
            }
            
            // Function to apply pixelation to UV coordinates
            float2 pixelateUV(float2 uv, float pixelSize)
            {
                // Calculate pixelated UVs
                float2 pixelUV = floor(uv * pixelSize) / pixelSize;
                
                // Optional: Smooth transition between pixels
                if (_PixelSmoothness > 0)
                {
                    float2 fracUV = frac(uv * pixelSize);
                    float2 smoothUV = smoothstep(0.5 - _PixelSmoothness * 0.5, 
                                                0.5 + _PixelSmoothness * 0.5, 
                                                fracUV);
                    pixelUV += smoothUV / pixelSize;
                }
                
                return pixelUV;
            }
            
            // Apply dithering to alpha
            float applyDither(float2 screenPos, float alpha)
            {
                int x = fmod(screenPos.x, 4.0);
                int y = fmod(screenPos.y, 4.0);
                int index = x + y * 4;
                float threshold = bayerMatrix[index] / 16.0;
                
                // Apply dithering
                alpha = step(threshold, alpha * (1.0 - _DitherAmount) + _DitherAmount * 0.5);
                return alpha;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = worldPos.xy;
                
                // Calculate screen position for dithering
                o.screenPos = o.vertex.xy;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Apply pixelation to local UVs
                float2 pixelUV = pixelateUV(i.uv, _PixelSize);
                
                // Keep world UVs unpixelated for seamless tiling
                float2 worldUV = i.worldPos * _WorldScale;
                
                // Base noise UV (using unpixelated world coordinates)
                float2 baseUV = worldUV * _NoiseScale;
                
                // Scrolling with time
                float2 scrollUV = baseUV;
                scrollUV += _ScrollDirection * _Time.y;
                
                // Turbulence
                float timeOffset = _Time.y * 2.0;
                float2 turbulenceUV = scrollUV;
                turbulenceUV.x += sin(scrollUV.y * 3.0 + timeOffset) * _Turbulence * 0.15;
                turbulenceUV.y += cos(scrollUV.x * 2.5 + timeOffset * 0.8) * _Turbulence * 0.1;
                turbulenceUV += hash(i.worldPos * 0.1) * 0.1;

                // Original noise layers
                float noise1 = billowNoise(turbulenceUV);
                float noise2 = billowNoise(turbulenceUV * 1.7 + float2(0.3, 0.7) + timeOffset * 0.4);
                float noise3 = billowNoise(turbulenceUV * 3.2 + float2(0.1, 0.4) + timeOffset * 0.2);
                
                float finalNoise = noise1 * 0.625 + noise2 * 0.25 + noise3 * 0.125;
                finalNoise = pow(saturate(finalNoise), _NoiseIntensity);

                // Circular mask using PIXELATED UVs
                float2 center = float2(0.5, 0.5);
                float dist = distance(pixelUV, center);
                float circleMask = 1.0 - smoothstep(0.0, 0.75 + _EdgeSoftness, dist * 2.0);
                
                // Combine noise with mask
                float density = finalNoise * circleMask;
                
                // Apply powder sugar effect
                float powderAlpha = PowderSugarEffect(density);
                float3 lightDir = normalize(float3(_LightDirection.xy, 0.1));
                float3 lightsEffect = _LightColor * _LightIntensity * powderAlpha;

                // Physical properties
                float absorption = density * _AbsorptionStrength;
                float pathLength = density * _PathLengthScale;
                float transmission = exp(-absorption * pathLength);
                float beerAlpha = 1.0 - transmission;
                
                // Color with gradient using PIXELATED UVs
                float gradient = 1.0 - dist * 1.5;
                gradient *= (1.0 - pixelUV.y * 0.7);
                
                // Final color
                float4 smokeColor = _Color * gradient;
                smokeColor.rgb *= transmission;
                smokeColor.rgb += lightsEffect;
                smokeColor.a = beerAlpha * _Color.a * i.color.a * gradient * powderAlpha;
                
                // Apply dithering to pixel edges
                if (_DitherAmount > 0)
                {
                    smokeColor.a = applyDither(i.screenPos, smokeColor.a);
                }
                
                // Optional: Add pixel grid effect
                if (_PixelSize > 20) // Only show grid for large pixels
                {
                    float2 grid = frac(i.uv * _PixelSize);
                    if (grid.x < 0.05 || grid.y < 0.05)
                    {
                        smokeColor.rgb *= 0.8;
                    }
                }
                
                return smokeColor;
            }
            ENDCG
        }
    }
}