using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFS;
using SFS.UI;
using SFS.World;
using SFS.World.Maps;
using SFS.WorldBase;
using UnityEngine;
using Environment = SFS.World.Environment;
using BetterPlanets.Extensions;

namespace BetterPlanets.Modules;

public class WormholeData
{
    public enum WormholeEntryType 
    {
        /// <summary>
        /// The rocket must be at periapsis (closest approach) to the planet to trigger teleportation.
        /// </summary>
        Periapsis,
        /// <summary>
        /// The rocket must be fully enclosed within the radius of the wormhole to trigger teleportation.
        /// </summary>
        Enclosure
    }
    
    public const string CustomDataKey = "WORMHOLE_DATA";
    public const WormholeEntryType DefaultEntryType = WormholeEntryType.Enclosure;
    
    [JsonProperty("isWormhole")]
    public bool IsWormhole { get; set; }
    
    /// <summary>
    /// useful for making one-way wormholes, or for testing purposes.
    /// i.e. other wormholes can make this the destination, but this wormhole will not teleport rockets at all.
    /// </summary>
    [JsonProperty("shouldTeleport")]
    public bool ShouldTeleport { get; set; } = true;
    
    [JsonProperty("targetBodyCodeName")]
    public string TargetBodyCodeName { get; set; }

    [JsonProperty("entryHeight")]
    public float EntryHeight { get; set; } = 0f;
    
    [JsonProperty("exitHeight")]
    public float ExitHeight { get; set; } = 0f;
    
    [JsonProperty("entryType")]
    public string EntryTypeString { get; set; } = "Enclosure";
    
    public WormholeEntryType EntryType =>
        Enum.TryParse<WormholeEntryType>(EntryTypeString, true, out var result) 
            ? result 
            : DefaultEntryType;
}

public class WormholePlanetHandler : MonoBehaviour
{
    // STATE-BASED LOCKS
    private static readonly Dictionary<int, string> LastDepartedPlanet = new();
    private static readonly Dictionary<int, double> LastTeleportTime = new();
    
    // 5 second cooldown to prevent rapid ping-ponging regardless of spatial checks
    private const double TeleportCooldown = 5.0;

    private Planet _planet;
    private WormholeData _data;
    private Planet _targetPlanet;
    private bool _shouldTeleport = true;

    private const float PeriapsisThresholdDegrees = 3f;
    private const float PeriapsisThresholdRadians = (float)(PeriapsisThresholdDegrees * (Math.PI / 180.0));
    private const float TeleportFadeDuration = 0.5f;
    private const float TeleportDuration = 0.5f;

    public void Initialize(Planet planet, WormholeData data)
    {
        _planet = planet;
        _data = data;
        
        if (!string.IsNullOrEmpty(data.TargetBodyCodeName))
        {
            _targetPlanet = Base.planetLoader.planets.GetValueOrDefault(data.TargetBodyCodeName);
        }
        else
        {
            Debug.LogWarning($"[BetterPlanets] Wormhole on planet {_planet.codeName} has no target body code name specified.");
        }
        
        if (_targetPlanet == null)
        {
            Debug.LogWarning($"[BetterPlanets] Wormhole on planet {_planet.codeName} has an invalid target body code name: {data.TargetBodyCodeName}");
            return;
        }
        
        var targetCustomData = _targetPlanet.data.CustomData;

        if (!targetCustomData.TryGetValue(WormholeData.CustomDataKey, out var targetWormholeToken) ||
            targetWormholeToken is not JObject targetWormholeObject)
        {
            Debug.LogWarning($"[BetterPlanets] Wormhole on planet {_planet.codeName} points to {_targetPlanet.codeName}, which has no wormhole data.");
            _shouldTeleport = false;
            return;
        }
            
        var targetWormholeData = targetWormholeObject.ToObject<WormholeData>();
        if (targetWormholeData is not { IsWormhole: true })
        {
            Debug.LogWarning($"[BetterPlanets] Wormhole on planet {_planet.codeName} points to {_targetPlanet.codeName}, which is not a wormhole.");
            _shouldTeleport = false;
            return;
        }
        
        #if DEBUG
        Debug.Log($"[BetterPlanets] Wormhole on planet {_planet.codeName} points to {_targetPlanet.codeName}, which is also a wormhole.");
        #endif
    }

    private void Update()
    {
        if (!_shouldTeleport || !GameManager.main) return;

        switch (_data.EntryType)
        {
            case WormholeData.WormholeEntryType.Periapsis:
                CheckRocketsForPeriapsisEntry();
                break;
            case WormholeData.WormholeEntryType.Enclosure:
                CheckRocketsForEnclosureEntry();
                break;
        }
    }
    
    private bool IsOutsideWormholeRadius(Rocket rocket, Planet planet, double entryHeight)
    {
        Bounds box = rocket.GetGlobalBoundingBox();

        Double2[] corners =
        [
            WorldView.ToGlobalPosition(new Vector2(box.min.x, box.min.y)),
            WorldView.ToGlobalPosition(new Vector2(box.min.x, box.max.y)),
            WorldView.ToGlobalPosition(new Vector2(box.max.x, box.min.y)),
            WorldView.ToGlobalPosition(new Vector2(box.max.x, box.max.y))
        ];

        double maxHeight = double.NegativeInfinity;
        foreach (Double2 corner in corners)
        {
            double height = corner.magnitude - planet.Radius;
            if (height > maxHeight) maxHeight = height;
        }

        return maxHeight > entryHeight;
    }

    private void CheckRocketsForEnclosureEntry()
    {
        foreach (var rocket in GameManager.main.rockets)
        {
            if (rocket.location.planet.Value != _planet) continue;

            var rocketId = rocket.GetInstanceID();
            var currentTime = WorldTime.main.worldTime;
            
            if (LastTeleportTime.TryGetValue(rocketId, out double lastTime) && (currentTime - lastTime) < TeleportCooldown)
            {
                continue;
            }

            var isOutside = IsOutsideWormholeRadius(rocket, _planet, _data.EntryHeight);
            var isEnclosed = !isOutside;
            
            if (LastDepartedPlanet.TryGetValue(rocketId, out string lastPlanet) && lastPlanet == _targetPlanet.codeName)
            {
                if (!isOutside) continue; // Still inside the danger zone, block it
                LastDepartedPlanet.Remove(rocketId); // Cleared the zone, allow future teleports
            }

            if (!isEnclosed) continue;
            
            #if DEBUG
            Debug.Log($"[BetterPlanets] Wormhole triggered! Rocket '{rocket.rocketName}' enclosed.");
            #endif
            
            StartCoroutine(TeleportSequence(rocket));
            break;
        }
    }
    
    private void CheckRocketsForPeriapsisEntry()
    {
        foreach (var rocket in _planet.GetOrbitingRockets())
        {
            var rocketId = rocket.GetInstanceID();
            var currentTime = WorldTime.main.worldTime;

            
            if (LastTeleportTime.TryGetValue(rocketId, out var lastTime) && (currentTime - lastTime) < TeleportCooldown)
            {
                continue;
            }

            var orbit = Orbit.TryCreateOrbit(rocket.location.Value, true, false, out var success);
            if (!success || orbit == null) continue;

            var anomaly = orbit.GetTrueAnomaly(WorldTime.main.worldTime);
            var distToPeriapsis = Math.Abs(anomaly);
            if (distToPeriapsis > Math.PI) 
            {
                distToPeriapsis = 2 * Math.PI - distToPeriapsis;
            }
            
            var isNearPeriapsis = distToPeriapsis < PeriapsisThresholdRadians;
            var isFullyOutside = IsOutsideWormholeRadius(rocket, _planet, _data.EntryHeight);
            
            if (LastDepartedPlanet.TryGetValue(rocketId, out string lastPlanet) && lastPlanet == _targetPlanet.codeName)
            {
                if (!isFullyOutside)
                {
                    continue; 
                }
                LastDepartedPlanet.Remove(rocketId);
            }

            bool isInDangerZone = isNearPeriapsis && (rocket.location.Height <= _data.EntryHeight);
            if (!isInDangerZone) continue;
            
            #if DEBUG
            Debug.Log($"[BetterPlanets] Wormhole triggered! Anomaly: {Math.Round(distToPeriapsis * 180.0 / Math.PI, 2)}° from periapsis.");
            #endif
                    
            StartCoroutine(TeleportSequence(rocket));
            break;
        }
    }
    
    private IEnumerator TeleportSequence(Rocket rocket)
    { 
        if (!_targetPlanet) yield break;
        
        if (!WorldTime.main.realtimePhysics.Value)
        {
            WorldTime.main.StopTimewarp(false);
            yield return new WaitForFixedUpdate(); 
        }
        
        #if DEBUG
        MsgDrawer.main.Log($"[BetterPlanets] Teleporting rocket {rocket.rocketName} from {_planet.codeName} to {_targetPlanet.codeName} via wormhole.");
        #endif
        
        var isPlayer = rocket == PlayerController.main.player.Value;

        if (isPlayer)
        {
            yield return ScreenFader.Instance.FadeInOut(
                fadeDuration: TeleportFadeDuration, 
                waitAfterFadeIn: TeleportDuration, 
                midAction: () => ExecuteTeleport(rocket)
            );
        }
        else
        {
            ExecuteTeleport(rocket);
        }
    }

    private void ExecuteTeleport(Rocket rocket)
    {
        var entryPos = rocket.location.position.Value;
        var vel = rocket.location.velocity.Value;

        // 1. base angle a between the chord (velocity) and the radius at entry
        var rOut = entryPos.normalized;
        var cosA = Double2.Dot(vel.normalized, -rOut);
        cosA = cosA < -1.0 ? -1.0 : (cosA > 1.0 ? 1.0 : cosA);
        var a = Math.Acos(cosA);
        a = Math.Min(a, Math.PI / 2.0); // clamp to prevent negative central angle

        // 2. central angle of the isosceles triangle (a + a + center = 180)
        var centralAngle = Math.PI - 2.0 * a;

        // 3. sign from cross product so the chord bends the way you're traveling
        var cross = rOut.x * vel.y - rOut.y * vel.x;
        var sign = cross >= 0.0 ? 1.0 : -1.0;

        var exitAngle = entryPos.AngleRadians + sign * centralAngle;

        // 4. spawn at the surface of the target at the exit angle
        var targetRadius = _targetPlanet.Radius + _data.EntryHeight;
        var newLocalPos = Double2.CosSin(exitAngle, targetRadius);


        var newLocation = new Location(WorldTime.main.worldTime, _targetPlanet, newLocalPos, vel);

        var isPlayer = rocket == PlayerController.main.player.Value;
        if (isPlayer) PlayerController.main.player.Value = null;

        rocket.physics.PhysicsMode = false;
        rocket.physics.SetLocationAndState(newLocation, false);
        rocket.physics.PhysicsMode = true;

        if (isPlayer)
        {
            PlayerController.main.player.Value = rocket;
            Map.view.SetViewSmooth(new MapView.View(_targetPlanet.mapPlanet, newLocalPos, Map.view.view.distance));
        }

        int rocketId = rocket.GetInstanceID();
        LastDepartedPlanet[rocketId] = _planet.codeName;
        LastTeleportTime[rocketId] = WorldTime.main.worldTime;
    
        #if DEBUG
        Debug.Log($"[BetterPlanets] Teleport complete. Chord exit at {exitAngle * 180.0 / Math.PI:F1} deg");
        #endif
    }
}

public static class Wormhole
{
    public static void OnCreateEnvironment(Planet planet, Environment environment, WorldEnvironment worldEnvironment)
    {
        var customData = planet.data.CustomData;
        if (!customData.TryGetValue(WormholeData.CustomDataKey, out var wormholeDataToken) ||
            wormholeDataToken is not JObject wormholeDataObject) return;
            
        var wormholeData = wormholeDataObject.ToObject<WormholeData>();
        if (wormholeData is not { IsWormhole: true }) return;
        
        var handler = planet.gameObject.AddComponent<WormholePlanetHandler>();
        handler.Initialize(planet, wormholeData);
    }

    // public static void OnAtmosphericEntry(Rocket rocket, Planet planet)
    // {
    //     var customData = planet.data.CustomData;
    //     if (!customData.TryGetValue(WormholeData.CustomDataKey, out var token) || token is not JObject obj) return;
    //
    //     var data = obj.ToObject<WormholeData>();
    //     if (data is { IsWormhole: true })
    //     { 
    //         Debug.Log($"[BetterPlanets] Rocket {rocket.rocketName} entered wormhole atmosphere of {planet.codeName}");
    //     }
    // }
}