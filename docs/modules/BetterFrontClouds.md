# Better Front Clouds

By default, front clouds in SFS are static. This module lets you make them rotate, and supports multiple cloud layers per planet with different textures and speeds.

## Configuration

Add a `FRONT_CLOUDS_DATA` array inside your planet's `CUSTOM_DATA` block.

```json
"CUSTOM_DATA": 
{
  "FRONT_CLOUDS_DATA": 
  [
    {
      "cloudsTexture": "MyCloudTexture",
      "height": 5000.0,
      "positionZ": -10.0,
      "fadeZoneHeight": 2000.0,
      "sharpenAlpha": false,
      "cloudTextureCutout": 0.8,
      "layerOrder": 200,
      "rotationEnabled": true,
      "rotationUnits": "DegreesPerSecond",
      "rotationSpeed": 0.5
    }
  ]
}
```

### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `cloudsTexture` | string | required | Texture name. Must exist in the solar system's Texture Data folder or built-in resources. |
| `height` | float | required | Height of the cloud layer above the surface, in meters. |
| `positionZ` | float | required | Render depth offset. More negative = further behind. |
| `fadeZoneHeight` | float | required | Distance over which clouds fade in/out as you zoom. |
| `sharpenAlpha` | bool | `false` | Whether to sharpen the cloud edges. |
| `cloudTextureCutout` | float | required | UV cutout value for the texture. |
| `layerOrder` | int | `200` | Sorting order. Higher values render on top. |
| `rotationEnabled` | bool | `false` | Whether this layer rotates. |
| `rotationUnits` | string | `"DegreesPerSecond"` | The unit for rotation speed. See below. |
| `rotationSpeed` | float | `0.0` | Rotation speed. Positive = counter-clockwise, negative = clockwise. |

## Rotation Units

### DegreesPerSecond
The simplest option. The value is degrees per second of in-game time.

```json
"rotationUnits": "DegreesPerSecond",
"rotationSpeed": 2.0
```

A full rotation takes 180 seconds.

### RadiansPerSecond
Same idea, just in radians.

```json
"rotationUnits": "RadiansPerSecond",
"rotationSpeed": 0.035
```

0.035 rad/s is roughly 2 deg/s.

### SurfaceVelocity
Specify a speed in meters per second and the module figures out the angular speed based on the planet's size. The clouds always look like they're drifting at that speed over the ground, no matter how big the planet is.

```json
"rotationUnits": "SurfaceVelocity",
"rotationSpeed": 50.0
```

On a big planet this looks slow. On a tiny moon the same 50 m/s looks fast. That's the point.

## Multiple Layers

You can stack as many layers as you want. Each gets its own texture and speed.

```json
"CUSTOM_DATA": 
{
  "FRONT_CLOUDS_DATA": 
  [
    {
      "cloudsTexture": "HighAltitudeClouds",
      "height": 8000.0,
      "positionZ": -15.0,
      "fadeZoneHeight": 3000.0,
      "sharpenAlpha": false,
      "cloudTextureCutout": 0.8,
      "layerOrder": 201,
      "rotationEnabled": true,
      "rotationUnits": "SurfaceVelocity",
      "rotationSpeed": 80.0
    },
    {
      "cloudsTexture": "LowAltitudeClouds",
      "height": 3000.0,
      "positionZ": -5.0,
      "fadeZoneHeight": 1500.0,
      "sharpenAlpha": true,
      "cloudTextureCutout": 0.6,
      "layerOrder": 199,
      "rotationEnabled": true,
      "rotationUnits": "SurfaceVelocity",
      "rotationSpeed": 30.0
    },
    {
      "cloudsTexture": "StaticHazeLayer",
      "height": 1000.0,
      "positionZ": -2.0,
      "fadeZoneHeight": 500.0,
      "sharpenAlpha": false,
      "cloudTextureCutout": 0.3,
      "layerOrder": 198,
      "rotationEnabled": false
    }
  ]
}
```

Here the high clouds drift faster than the low clouds (wind shear), and the haze stays still. `layerOrder` controls stacking: 201 draws on top of 199 which draws on top of 198.

## Example: Full Planet Config

```json
{
  "BASE_DATA": {
    "radius": 300000.0,
    "gravity": 9.8,
    "timewarpHeight": 10000.0
  },
  "ATMOSPHERE_PHYSICS_DATA": {
    "height": 60000.0,
    "density": 1.0,
    "curve": 7.0
  },
  "CUSTOM_DATA": {
    "FRONT_CLOUDS_DATA": [
      {
        "cloudsTexture": "GasGiantBands",
        "height": 5000.0,
        "positionZ": -10.0,
        "fadeZoneHeight": 2000.0,
        "sharpenAlpha": false,
        "cloudTextureCutout": 0.8,
        "layerOrder": 200,
        "rotationEnabled": true,
        "rotationUnits": "SurfaceVelocity",
        "rotationSpeed": 120.0
      }
    ]
  }
}
```

The `FRONT_CLOUDS_DATA` key must live inside `CUSTOM_DATA`. If you also put it at the top level of the planet file, the base game will create its own static clouds on top of yours. Leave it out of the top level if you only want the rotating layers.

## Notes

- Clouds stay consistent across saves. They snap to the correct position for the current world time when you load.
- If you change `rotationSpeed` and reload, the clouds will jump to the new phase based on the new speed.
- Very high rotation speeds may stutter at low frame rates. Keep speeds reasonable.