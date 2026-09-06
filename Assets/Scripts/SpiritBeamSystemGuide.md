# Spirit Beam System Setup Guide

## Overview
The Spirit Beam System creates dynamic particle effects that connect each LegendStone to its associated paddle, visualizing the spiritual power flow that gives the paddle the ability to move.

## Components

### 1. SpiritBeamController.cs
- Main controller for the particle beam effect
- Handles dynamic beam scaling, direction, and visual effects
- Automatically adjusts beam width based on distance
- Includes pulsation effects and color changes

### 2. LegendStoneController.cs (Updated)
- Integrated with the new spirit beam system
- Maintains backward compatibility with existing particle systems
- Automatically determines beam colors based on player ID
- Handles health-based visual changes

### 3. SpiritBeamPrefabSetup.cs
- Utility script for easy prefab setup in Unity Editor
- Provides templates and presets for different stone types
- Includes editor tools for quick configuration

## Setup Instructions

### Step 1: Prepare Your LegendStone Prefabs

1. **Open your LegendStone prefab** in Unity
2. **Add the SpiritBeamPrefabSetup component** to the root GameObject
3. **Click "Setup Spirit Beam"** button in the inspector
4. This will automatically create:
   - SpiritBeam GameObject with SpiritBeamController
   - Main particle system for the beam
   - PaddleImpact particle system for paddle effects
   - Optional debug line renderer

### Step 2: Configure Beam Settings

Each stone can have unique beam characteristics:

```csharp
// Example beam settings for different stone types
BeamSettings fireStone = new BeamSettings()
{
    beamColor = Color.red,
    beamWidth = 0.15f,
    maxBeamWidth = 0.6f,
    particleSpeed = 8f,
    particlesPerSecond = 150,
    pulsationSpeed = 3f,
    pulsationIntensity = 0.4f
};

BeamSettings iceStone = new BeamSettings()
{
    beamColor = Color.cyan,
    beamWidth = 0.08f,
    maxBeamWidth = 0.4f,
    particleSpeed = 6f,
    particlesPerSecond = 120,
    pulsationSpeed = 1.5f,
    pulsationIntensity = 0.2f
};
```

### Step 3: Customize Particle Materials

1. **Create particle materials** for each stone type:
   - Fire Stone: Warm colors (red, orange, yellow)
   - Ice Stone: Cool colors (cyan, blue, white)
   - Earth Stone: Natural colors (brown, green, gold)
   - Lightning Stone: Electric colors (yellow, purple, white)

2. **Assign materials** to the particle systems:
   - Main beam particle system
   - Paddle impact particle system

### Step 4: Test the System

1. **Enter Play Mode**
2. **Start a match** with different stone types
3. **Observe the connections** between stones and paddles
4. **Move paddles** to see the beams follow dynamically

## Features

### Dynamic Beam Scaling
- Beam starts narrow at the stone
- Expands as it reaches the paddle
- Width adjusts based on distance
- Covers the entire paddle surface

### Visual Feedback
- **Healthy Paddle**: Normal stone color
- **Low Health**: Yellow beam (60% health)
- **Critical Health**: Red beam (30% health)
- **Pulsation**: Intensity varies with paddle condition

### Performance Optimized
- Efficient particle system usage
- Distance-based LOD adjustments
- Automatic cleanup when objects are destroyed

## Customization Options

### Per-Stone Presets
Create presets in SpiritBeamPrefabSetup for each stone:

```csharp
public LegendStonePreset[] stonePresets = new LegendStonePreset[]
{
    new LegendStonePreset()
    {
        stoneName = "FireStone",
        beamColor = Color.red,
        beamSettings = fireStoneSettings
    },
    new LegendStonePreset()
    {
        stoneName = "IceStone", 
        beamColor = Color.cyan,
        beamSettings = iceStoneSettings
    }
};
```

### Runtime Color Changes
```csharp
// Change beam color during gameplay
legendStone.spiritBeam.SetBeamColor(Color.green);

// Adjust beam intensity
legendStone.spiritBeam.SetBeamIntensity(0.8f);

// Enable/disable beam
legendStone.spiritBeam.EnableBeam(true);
```

## Troubleshooting

### Common Issues:

1. **No beam visible**:
   - Check if SpiritBeamController is properly assigned
   - Verify particle system has a material
   - Ensure paddle reference is set correctly

2. **Beam not following paddle**:
   - Check paddle transform reference
   - Verify Update() is being called
   - Check if beam is enabled

3. **Performance issues**:
   - Reduce particlesPerSecond value
   - Lower maxParticles in particle system
   - Disable debug line renderer in builds

### Debug Tools:
- Enable debug line renderer to see connection path
- Use OnDrawGizmos to visualize paddle detection radius
- Check console for initialization messages

## Integration with Existing System

The system maintains full backward compatibility:
- Existing energyBeamEffect particle systems continue to work
- New SpiritBeamController provides enhanced features
- Both systems can run simultaneously for testing

## Mobile Optimization

For mobile platforms:
- Reduce particle count (50-75 per second)
- Use simpler particle materials
- Disable pulsation effects on low-end devices
- Use distance culling for far beams

## Future Enhancements

Potential additions:
- Beam branching for multiple paddles
- Lightning-style jagged beams
- Spell effect overlays
- Sound integration
- Haptic feedback on mobile