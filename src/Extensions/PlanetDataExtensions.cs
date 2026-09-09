using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SFS.Stats;
using SFS.World;
using SFS.WorldBase;
using UnityEngine;

namespace BetterPlanets.Extensions;

public static class PlanetDataExtensions
{
    private const string CustomDataKey = "CUSTOM_DATA";
    
    private static readonly ConditionalWeakTable<PlanetData, JObject> CustomDataTable = new();
    
    public static void TryInjectCustomData(PlanetData planetData, string jsonText, string name = "")
    {
        try
        {
            var jsonObject = JObject.Parse(jsonText);
            var customData = jsonObject.TryGetValue(CustomDataKey, out var customDataToken) && customDataToken is JObject value ? value : new JObject();
            CustomDataTable.Add(planetData, customData);
            
            #if DEBUG
            if (!customData.HasValues)
            {
                Debug.Log($"[BetterPlanets] No custom data found for planet {name}");
            }
            else
            {
                Debug.Log($"[BetterPlanets] Custom data injected for planet {name}");
                Debug.Log($"[BetterPlanets] Custom data for planet {name}: {customData.ToString(Newtonsoft.Json.Formatting.Indented)}");
            }
                
            #endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse custom data for planet {name}: {ex.Message}");
        }
    }
    
    extension(PlanetData planetData)
    {
        [UsedImplicitly]
        public JObject CustomData => GetCustomData(planetData);
        
        [UsedImplicitly]
        public JObject GetCustomData()
        {
            if (CustomDataTable.TryGetValue(planetData, out var customData))
            {
                return customData;
            }
        
            customData = new JObject();
            CustomDataTable.Add(planetData, customData);
            return customData;
        }

        [UsedImplicitly]
        public void SetCustomData(JObject data)
        {
            CustomDataTable.Remove(planetData);
            CustomDataTable.Add(planetData, data);
        }
    }
}