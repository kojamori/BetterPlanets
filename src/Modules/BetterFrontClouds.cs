using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFS.World;
using SFS.WorldBase;
using UnityEngine;
using Environment = SFS.World.Environment;
using BetterPlanets.Extensions;
using SFS;
using SFS.World.PlanetModules;

namespace BetterPlanets.Modules;

public class FrontCloudsCustomData: FrontCloudsModule
{
    public const string CustomDataKey = "FRONT_CLOUDS_DATA";

    [JsonProperty("layerOrder")] 
    public int SortingOrder { get; set; } = 200;
    
    public enum RotationUnitsType
    {
        DegreesPerSecond,
        [UsedImplicitly]
        RadiansPerSecond,
        [UsedImplicitly]
        SurfaceVelocity
    }
    
    [JsonProperty("rotationEnabled")]
    public bool RotationEnabled { get; set; }
    
    
    
    [JsonProperty("rotationUnits")]
    public string RotationUnitsString { get; set; } = "DegreesPerSecond";
    
    public RotationUnitsType RotationUnits =>
        Enum.TryParse<RotationUnitsType>(RotationUnitsString, true, out var result) 
            ? result 
            : RotationUnitsType.DegreesPerSecond;

    [JsonProperty("rotationSpeed")]
    public float RotationSpeed { get; set; }
    
    public float ConvertToDegreesPerSecond(float planetRadius)
    {
        return RotationUnits switch
        {
            RotationUnitsType.DegreesPerSecond => RotationSpeed,
            RotationUnitsType.RadiansPerSecond => RotationSpeed * Mathf.Rad2Deg,
            RotationUnitsType.SurfaceVelocity => RotationSpeed / (2 * Mathf.PI * planetRadius) * 360f,
            _ => throw new ArgumentOutOfRangeException(nameof(RotationUnits), RotationUnits, null)
        };
    }
}

public class FrontCloudsLayerHandler : MonoBehaviour
{
    private static readonly int Alpha = Shader.PropertyToID("_Alpha");
    private Material _material;
    private float _fadeStart;
    
    public void Initialize(Material material, float positionZ)
    {
        _material = material;
        _fadeStart = Mathf.Abs(positionZ) + 1f;
        
        WorldView.main.scaledSpace.OnChange += OnScaledSpaceChange;
        WorldView.main.viewDistance.OnChange += OnViewDistanceChange;
        
        OnScaledSpaceChange();
        OnViewDistanceChange();
    }
    
    private void OnDestroy()
    {
        if (WorldView.main != null)
        {
            WorldView.main.scaledSpace.OnChange -= OnScaledSpaceChange;
            WorldView.main.viewDistance.OnChange -= OnViewDistanceChange;
        }
    }
    
    private void OnScaledSpaceChange()
    {
        string layer = WorldView.main.scaledSpace.Value ? "Scaled Space" : "Celestial Body";
        gameObject.layer = LayerMask.NameToLayer(layer);
    }
    
    private void OnViewDistanceChange()
    {
        float num = Mathf.Clamp01(Mathf.InverseLerp(_fadeStart, _fadeStart * 1.5f, WorldView.main.viewDistance.Value));
        _material.SetFloat(Alpha, num);
    }
}

public class FrontCloudsRotator : MonoBehaviour
{
    [UsedImplicitly]
    private Planet _planet;
    private FrontCloudsCustomData _customData;
    private float _currentRotation;

    public void Initialize(Planet planet, FrontCloudsCustomData customData)
    {
        _planet = planet;
        _customData = customData;
        
        var initialRotation = WorldTime.main != null 
            ? _customData.ConvertToDegreesPerSecond((float)_planet.data.basics.radius) * WorldTime.main.worldTime
            : 0.0;

        _currentRotation = (float)(initialRotation % 360.0);
    }

    private void Update()
    {
        if (!_customData.RotationEnabled || !WorldTime.main) return;
        
        var timeScale = WorldTime.main.timewarpSpeed;
        
        _currentRotation += (float)(_customData.ConvertToDegreesPerSecond((float)_planet.data.basics.radius) * Time.deltaTime * timeScale);
        transform.localRotation = Quaternion.Euler(0, 0, _currentRotation % 360f);
    }
}

public static class BetterFrontClouds
{
    private static readonly Dictionary<string, Material[]> FrontCloudMaterialsTable = new();
    private static readonly Dictionary<string, FrontClouds[]> FrontCloudsTable = new();
    private static readonly int TextureCutout = Shader.PropertyToID("_TextureCutout");
    private static readonly int FadeZoneM = Shader.PropertyToID("_FadeZoneM");
    private static readonly int SharpenAlpha = Shader.PropertyToID("_SharpenAlpha");
    private static readonly int CloudsTex = Shader.PropertyToID("_CloudsTex");

    public static void OnSetupData(
        Planet planet, 
        Shader terrainShader,
        Shader waterShader,
        Shader atmosphereShader,
        Shader frontCloudsShader,
        Shader ringsShader,
        I_MsgLogger log)
    {
        var customData = planet.data.CustomData;
        if (customData[FrontCloudsCustomData.CustomDataKey] is not JArray frontCloudsJArray)
        {
            #if  DEBUG 
            Debug.LogWarning($"[BetterFrontClouds] No front clouds data found for planet {planet.codeName}.");
            #endif
            
            return;
        }

        var frontCloudsData = frontCloudsJArray.ToObject<FrontCloudsCustomData[]>();
        if (frontCloudsData == null)
        {
            #if DEBUG
            Debug.LogWarning($"[BetterFrontClouds] Failed to parse front clouds data for planet {planet.codeName}.");
            #endif
            
            return;
        }
        
        var materials = new Material[frontCloudsData.Length];

        for (var i = 0; i < frontCloudsData.Length; i++)
        {
            var data = frontCloudsData[i];
            materials[i] = CreateFrontCloudsMaterial(planet, data, frontCloudsShader, log);
        }
        
        FrontCloudMaterialsTable.Add(planet.codeName, materials);
        FrontCloudsTable.Add(planet.codeName, []);
    }
    
    public static void OnCreateEnvironment(Planet planet, Environment environment, WorldEnvironment worldEnvironment)
    {
        var atmospherePrefab = worldEnvironment.atmospherePrefab;
        if (atmospherePrefab == null)
        {
            Debug.LogWarning("[BetterFrontClouds] Could not find atmosphere prefab. Skipping.");
            return;
        }

        var customData = planet.data.CustomData;
        if (customData[FrontCloudsCustomData.CustomDataKey] is not JArray frontCloudsJArray)
        {
            #if  DEBUG 
            Debug.LogWarning($"[BetterFrontClouds] No front clouds data found for planet {planet.codeName}.");
            #endif
            
            return;
        }

        var frontCloudsData = frontCloudsJArray.ToObject<FrontCloudsCustomData[]>();
        if (frontCloudsData == null)
        {
            #if DEBUG
            Debug.LogWarning($"[BetterFrontClouds] Failed to parse front clouds data for planet {planet.codeName}.");
            #endif
            
            return;
        }
        
        if (!FrontCloudsTable.TryGetValue(planet.codeName, out _))
        {
            FrontCloudsTable.Add(planet.codeName, new FrontClouds[frontCloudsData.Length]);
        }
        
        FrontCloudMaterialsTable.TryGetValue(planet.codeName, out var materials);
        
        if (materials == null)
        {
            Debug.LogWarning($"[BetterFrontClouds] No materials found for planet {planet.codeName}. OnSetupData may not have run.");
            return;
        }
        
        
        
        for (var i = 0; i < frontCloudsData.Length; i++)
        {
            CreateFrontClouds(planet, environment, frontCloudsData[i], materials[i], i, atmospherePrefab);
        }
    }
    
    private static void CreateFrontClouds(Planet planet, Environment environment, FrontCloudsCustomData data, Material frontCloudMaterial, int index, Transform atmospherePrefab)
    {
        if (!FrontCloudsTable.TryGetValue(planet.codeName, out var frontClouds))
        {
            frontClouds = [];
            FrontCloudsTable.Add(planet.codeName, frontClouds);
        }
        
            
        if (frontCloudMaterial == null)
        {
            Debug.LogWarning($"[BetterFrontClouds] No material found for planet {planet.codeName}, layer {index}. Skipping.");
            return;
        }

        var frontCloud =
            CreateFrontCloudsInstance(planet, environment.holder, atmospherePrefab, frontCloudMaterial, data);

        if (!frontCloud)
        {
            Debug.LogWarning($"[BetterFrontClouds] No material found for planet {planet.codeName}, layer {index}. Skipping.");
            return;
        }
        
        frontClouds[index] = frontCloud;
            
        frontCloud.GetComponent<MeshRenderer>().sortingOrder = data.SortingOrder;
        frontCloud.gameObject.AddComponent<FrontCloudsLayerHandler>().Initialize(frontCloudMaterial, data.positionZ);

        if (data.RotationEnabled)
        {
            frontCloud.gameObject.AddComponent<FrontCloudsRotator>().Initialize(planet, data);
        }
        else
        {
            #if DEBUG
            Debug.Log($"[BetterFrontClouds] Rotation is disabled for planet {planet.codeName}. No rotation will be applied.");
            #endif
        }
            
        
    }
    
    private static Material CreateFrontCloudsMaterial(Planet planet, FrontCloudsModule input, Shader shader, I_MsgLogger log)
    {
        var material = new Material(shader);
        material.SetTexture(CloudsTex, Base.planetLoader.GetTexture(input.cloudsTexture, log));
        material.SetFloat(TextureCutout, input.cloudTextureCutout);
        material.SetFloat(FadeZoneM, 1f / Mathf.Clamp(input.fadeZoneHeight / ((float)planet.Radius + input.height), 0.0001f, 1f));
        material.SetFloat(SharpenAlpha, input.sharpenAlpha ? 1f : 0f);
        material.renderQueue = 3010;
        return material;
    }
    
    private static FrontClouds CreateFrontCloudsInstance(Planet planet, Transform parent, Transform cloudsPrefab, Material material, FrontCloudsCustomData data)
    {
        var transform = UnityEngine.Object.Instantiate(cloudsPrefab, parent, true);
        transform.name = planet.codeName + " Front Clouds " + data.cloudsTexture;
        transform.localPosition = Vector3.forward * data.positionZ;
        transform.localScale = Vector3.one;
    
        var renderer = transform.GetComponent<MeshRenderer>();
        renderer.material = material;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = data.SortingOrder;
    
        transform.GetComponent<MeshFilter>().sharedMesh = CreateFrontCloudsMesh(planet, data);
    
        return transform.gameObject.AddComponent<FrontClouds>();
    }
    
    private static Mesh CreateFrontCloudsMesh(Planet planet, FrontCloudsCustomData data)
    {
        var radius = (float)planet.Radius + data.height;
        var vertices = new Vector3[502];
        var uv = new Vector2[502];
    
        for (int i = 0; i <= 501; i++)
        {
            float angle = (float)(i * -Math.PI * 2.0 / 501.0);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vertices[i] = dir * radius;
            uv[i] = dir * 0.5f + Vector2.one * 0.5f;
        }
    
        var triangles = new int[1500];
        for (var i = 0; i < 500; i++)
        {
            var idx = i * 3;
            triangles[idx] = 0;
            triangles[idx + 1] = i + 1;
            triangles[idx + 2] = i + 2;
        }
    
        var mesh = new Mesh
        {
            vertices = vertices,
            uv = uv,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        mesh.name = planet.name + " Front Clouds Mesh " + data.cloudsTexture;
        return mesh;
    }

    public static void OnDestroy(Planet planet)
    {
        FrontCloudMaterialsTable.Remove(planet.codeName);
        FrontCloudsTable.Remove(planet.codeName);
    }
}