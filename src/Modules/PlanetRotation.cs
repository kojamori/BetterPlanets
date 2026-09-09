using System;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFS.World;
using SFS.WorldBase;
using UnityEngine;
using Environment = SFS.World.Environment;
using BetterPlanets.Extensions;

namespace BetterPlanets.Modules;

public class PlanetRotationCustomData
{
    public const string CustomDataKey = "PLANET_ROTATION_DATA";
    
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
            RotationUnitsType.SurfaceVelocity => (RotationSpeed / (2 * Mathf.PI * planetRadius)) * 360f,
            _ => throw new ArgumentOutOfRangeException(nameof(RotationUnits), RotationUnits, null)
        };
    }
}

public class PlanetRotator : MonoBehaviour
{
    [UsedImplicitly]
    private Planet _planet;
    private PlanetRotationCustomData _customData;
    private float _currentRotation;
    private float _degreesPerSecond;

    public void Initialize(Planet planet, PlanetRotationCustomData customData)
    {
        _planet = planet;
        _customData = customData;
        _degreesPerSecond = _customData.ConvertToDegreesPerSecond((float)_planet.data.basics.radius);
        
        float initialRotation = WorldTime.main != null 
            ? _degreesPerSecond * (float)WorldTime.main.worldTime
            : 0f;
        
        _currentRotation = initialRotation % 360f;
        transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
    }

    private void Update()
    {
        if (!_customData.RotationEnabled || !WorldTime.main) return;
        
        var timeScale = (float)WorldTime.main.timewarpSpeed;
        _currentRotation += _degreesPerSecond * Time.deltaTime * timeScale;
        
        transform.localRotation = Quaternion.Euler(0, 0, _currentRotation % 360f);
    }
}


public static class PlanetRotation
{
    public static void OnCreateEnvironment(Planet planet, Environment environment, WorldEnvironment worldEnvironment)
    {
        if (planet == null || environment?.holder == null) return;

        var customData = planet.data.CustomData;
        if (customData[PlanetRotationCustomData.CustomDataKey] is not JObject rotationJObject)
        {
            #if DEBUG
            Debug.Log($"[PlanetRotation] No rotation data found for planet {planet.codeName}");
            #endif
            return;
        }

        var rotationData = rotationJObject.ToObject<PlanetRotationCustomData>();
        if (rotationData == null)
        {
            #if DEBUG
            Debug.LogWarning($"[PlanetRotation] Failed to parse rotation data for planet {planet.codeName}");
            #endif
            return;
        }

        if (!rotationData.RotationEnabled)
        {
            #if DEBUG
            Debug.Log($"[PlanetRotation] Rotation is disabled for planet {planet.codeName}");
            #endif
            return;
        }

        var rotator = environment.holder.gameObject.AddComponent<PlanetRotator>();
        rotator.Initialize(planet, rotationData);
        
        #if DEBUG
        Debug.Log($"[PlanetRotation] Planet visual rotator added for {planet.codeName}");
        #endif
    }
}