using System;
using System.Collections.Generic;
using BetterPlanets.Modules;
using HarmonyLib;
using JetBrains.Annotations;
using ModLoader;
using SFS;
using SFS.WorldBase;
using UnityEngine;

namespace BetterPlanets;

[UsedImplicitly]
public class Main : Mod
{
    public static Main Instance { get; private set; }

    public Main()
    {
        Instance = this;
    }

    public override string ModNameID => "BetterPlanets";
    public override string DisplayName => "Better Planets Beta";
    public override string Author => "kojamori";
    public override string MinimumGameVersionNecessary => "1.6.00.16";
    public override string ModVersion => "0.1.0";
    public override string Description => "A mod that adds more features to planets.";
    public override string IconLink => null;
    public override Action LoadKeybindings => null;

    public override Dictionary<string, string> Dependencies => new();

    public override void Early_Load()
    {
        new Harmony(Instance.ModNameID).PatchAll();
    }

    public override void Load()
    {
    }

    public List<Action<Planet, Shader, Shader, Shader, Shader, Shader, I_MsgLogger>> OnSetupData = 
    [
        BetterFrontClouds.OnSetupData
    ];

    public List<Action<Planet>> OnDestroy =
    [
        BetterFrontClouds.OnDestroy
    ];
    
    public List<Action<Planet, SFS.World.Environment, SFS.World.WorldEnvironment>> OnCreateEnvironment = 
    [
        BetterFrontClouds.OnCreateEnvironment,
        // PlanetRotation.OnCreateEnvironment,
        Wormhole.OnCreateEnvironment
    ];
}