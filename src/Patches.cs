using System;
using System.Linq;
using BetterPlanets.Extensions;
using HarmonyLib;
using JetBrains.Annotations;
using SFS;
using SFS.Parsers.Json;
using SFS.World;
using SFS.World.Legacy;
using SFS.World.PlanetModules;
using SFS.World.Terrain;
using SFS.WorldBase;
using UnityEngine;

namespace BetterPlanets;



[HarmonyPatch]
public static class Patches
{
    [HarmonyPatch(typeof(Planet), "SetupData")]
    public static void Postfix(
        string codeName,
        PlanetData data,
        Shader terrainShader,
        Shader waterShader,
        Shader atmosphereShader,
        Shader frontCloudsShader,
        Shader ringsShader,
        I_MsgLogger log, 
        // ReSharper disable once InconsistentNaming
        Planet __instance)
    {
        foreach (var action in Main.Instance.OnSetupData)
        {
            try
            {
                action.Invoke(__instance, terrainShader, waterShader, atmosphereShader, frontCloudsShader, ringsShader, log);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BetterPlanets] Error in OnSetupData for planet {codeName}: {e}");
            }
        }
    }
    
    [HarmonyPatch(typeof(WorldEnvironment), "CreateEnvironment")]
    public static bool Prefix(
        Planet planet, 
        Double3 viewPosition,
        // ReSharper disable once InconsistentNaming
        WorldEnvironment __instance,
        // ReSharper disable once InconsistentNaming
        out SFS.World.Environment __result)
    {
        SFS.World.Environment environment = new()
        {
            planet = planet,
            holder = new GameObject(planet.codeName + " Environment").transform
        };
        environment.holder.parent = __instance.transform;
        if (planet.data.hasTerrain)
        {
            var rockData = planet.data.terrain.rocks;
            var transform = (rockData != null) ? __instance.rockPrefabs.FirstOrDefault(a => a.name == rockData.rockType) : null;
            environment.terrain = DynamicTerrain.Create(
                planet, 
                viewPosition,
                null,
                1f,
                1f,
                "Default",
                new ValueTuple<Material, Material>(planet.terrainMaterial, planet.waterMaterial),
                true,
                __instance.chunkPrefab,
                (transform != null) ? rockData : null, transform);
            environment.terrain.transform.parent = environment.holder;
        }
        if (planet.HasAtmosphereVisuals)
        {
            environment.atmosphere = Atmosphere.Create(planet, environment.holder, __instance.atmospherePrefab);
        }
        if (planet.HasFrontClouds)
        {
            environment.frontClouds = FrontClouds.Create(planet, environment.holder, __instance.atmospherePrefab, planet.frontCloudsMaterial);
        }
        if (planet.HasRings)
        {
            environment.rings = Rings.Create(planet, environment.holder, __instance.atmospherePrefab, planet.ringsMaterial);
        }
        
        foreach (var action in Main.Instance.OnCreateEnvironment)
        {
            try
            {
                action.Invoke(planet, environment, __instance);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BetterPlanets] Error in OnCreateEnvironment for planet {planet.codeName}: {e}");
            }
        }

        __result = environment;

        return false;
    }
    
    
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(LegacyConverter), "Convert_Planet")]
    public static PlanetData Convert_Planet_Bridge(PlanetData_Old legacyPlanetData)
    {
        throw new NotImplementedException("Stub filled by Harmony at runtime.");
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(LegacyConverter), "FromJson_Old")]
    public static PlanetData_Old FromJson_Old_Bridge(string jsonText)
    {
        throw new NotImplementedException("Stub filled by Harmony at runtime.");
    }
    
    [UsedImplicitly]
    [HarmonyPatch(typeof(LegacyConverter), "CheckAndConvert_Planet")]
    [HarmonyPrefix]
    public static bool Prefix(
        string name, 
        string jsonText, 
        I_MsgLogger log, 
        out bool converted, 
        out bool success, 
        // ReSharper disable once InconsistentNaming
        ref PlanetData __result)
    {
        #if DEBUG
        Debug.Log($"Checking planet {name} for custom data...");
        #endif
        
        var isLegacy = !jsonText.Contains("\"version\": 1.5,") && 
                        !jsonText.Contains("\"version\": \"1.5\",") && 
                        !jsonText.Contains("\"version\":1.5,") && 
                        !jsonText.Contains("\"version\":\"1.5\",");

        if (isLegacy)
        {
            try
            {
                var rawLegacyData = FromJson_Old_Bridge(jsonText);
                var result = Convert_Planet_Bridge(rawLegacyData);
                
                PlanetDataExtensions.TryInjectCustomData(result, jsonText, name);

                converted = true;
                success = true;
                __result = result;
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                log.Log("ERROR: json format or legacy conversion " + name);
                
                converted = false;
                success = false;
                __result = null;
                return false;
            }
        }
        
        converted = false;
        success = true;
        
        var planetData = JsonWrapper.FromJson<PlanetData>(jsonText);
        if (planetData == null)
        {
            log.Log("ERROR: json format: " + name);
            success = false;
            __result = null;
            return false;
        }
        
        PlanetDataExtensions.TryInjectCustomData(planetData, jsonText, name);

        __result = planetData;
        return false;
    }
}