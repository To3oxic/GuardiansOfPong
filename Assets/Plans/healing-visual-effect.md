# Project Overview
- Game Title: Battle Pong / Hero Pong
- Render Pipeline: URP

# Game Mechanics
## Healer Visual Effects
When a player's paddle is healed, we play a stunning green line particle effect going through the paddle upwards, accompanied by a bright green glow on the paddle model itself.

# UI
Not directly modified, but visual feedback is rendered overlaying the gameplay field and paddle.

# Key Asset & Context
- `Assets/Scripts/PaddleController.cs`: The paddle class where we listen for health changes. We'll use `OnHPChangedClientRpc` to trigger the local healing VFX only on client views when `damageDealt < 0f`.

# Implementation Steps
## Step 1: Implement particle effect and paddle glow logic in PaddleController.cs
- **Description**: Add `PlayHealVisualEffectCo()` coroutine to `PaddleController.cs` which generates vertical rising lines, and modulates the `_BaseColor` and `_RimColor` of the paddle material to create a green glow.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

```csharp
    [ClientRpc]
    private void OnHPChangedClientRpc(ulong paddleOwnerClientId, float newHP, float maxHP, float damageDealt)
    {
        Debug.Log($"[PaddleController] HP Change RPC received - Owner: {paddleOwnerClientId}, HP: {newHP}/{maxHP}, Damage: {damageDealt}");

        // Notify GameManager about the HP change for UI updates
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnPaddleHPChanged(paddleOwnerClientId, newHP, maxHP, damageDealt);
        }

        // Play green healing visual effect if HP was increased (healing has negative damageDealt)
        if (damageDealt < 0f)
        {
            StartCoroutine(PlayHealVisualEffectCo());
        }
    }

    private IEnumerator PlayHealVisualEffectCo()
    {
        // 1. Create the particle system for rising green lines
        GameObject psObj = new GameObject("HealParticles");
        psObj.transform.SetParent(this.transform, false);
        psObj.transform.localPosition = new Vector3(0f, -0.5f, 0f); // Start slightly below the paddle
        psObj.transform.localRotation = Quaternion.identity;

        ParticleSystem ps = psObj.AddComponent<ParticleSystem>();

        // Setup Main module
        var main = ps.main;
        main.duration = 1.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.05f, 0.15f); // Width of lines
        main.startSizeY = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);  // Length of stretched lines
        main.startColor = new Color(0.2f, 1.0f, 0.4f, 0.7f); // Beautiful semi-transparent neon green
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.playOnAwake = false;

        // Setup Shape module: box shape along the length of the paddle
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(8.0f, 0.2f, 0.8f);

        // Setup Velocity over lifetime (rise upwards along positive Y axis)
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(8f, 16f); // Upwards movement
        velocity.x = 0f;
        velocity.z = 0f;

        // Setup Color Over Lifetime (fade out)
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(0.2f, 1.0f, 0.4f), 0.0f), 
                new GradientColorKey(new Color(0.1f, 0.8f, 0.2f), 1.0f) 
            },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(1.0f, 0.0f), 
                new GradientAlphaKey(0.0f, 1.0f) 
            }
        );
        colorOverLifetime.color = grad;

        // Setup Renderer to support stretched lines
        var psr = psObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null)
        {
            psr.renderMode = ParticleSystemRenderMode.VerticalBillboard; // Perfect for vertical rising lines
            
            Shader particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (particleShader != null)
            {
                psr.material = new Material(particleShader);
            }
        }

        // Configure burst emission
        var emission = ps.emission;
        emission.rateOverTime = 0;
        var burst = new ParticleSystem.Burst(0.0f, 35);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // Play the particles!
        ps.Play();

        // 2. Animate the paddle glow
        Renderer paddleRenderer = GetComponent<Renderer>();
        if (paddleRenderer != null)
        {
            // Use .material to get/create an instance of the material so we don't leak to the project asset
            Material paddleMat = paddleRenderer.material;
            if (paddleMat != null)
            {
                Color originalBaseColor = paddleMat.HasProperty("_BaseColor") ? paddleMat.GetColor("_BaseColor") : Color.white;
                Color originalRimColor = paddleMat.HasProperty("_RimColor") ? paddleMat.GetColor("_RimColor") : Color.clear;

                Color targetBaseColor = new Color(0.3f, 1.0f, 0.5f); // Soft bright green
                Color targetRimColor = new Color(0.1f, 1.0f, 0.3f) * 2f; // Highly intense rim glow

                float elapsed = 0f;
                float duration = 1.2f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / duration;

                    // Quick spike up, smooth fade down
                    float t = 0f;
                    if (progress < 0.15f)
                    {
                        t = progress / 0.15f; // transition to green
                    }
                    else
                    {
                        t = 1.0f - ((progress - 0.15f) / 0.85f); // transition back to original
                    }

                    if (paddleMat.HasProperty("_BaseColor"))
                    {
                        paddleMat.SetColor("_BaseColor", Color.Lerp(originalBaseColor, targetBaseColor, t));
                    }
                    if (paddleMat.HasProperty("_RimColor"))
                    {
                        paddleMat.SetColor("_RimColor", Color.Lerp(originalRimColor, targetRimColor, t));
                    }

                    yield return null;
                }

                // Restore original values
                if (paddleMat.HasProperty("_BaseColor")) paddleMat.SetColor("_BaseColor", originalBaseColor);
                if (paddleMat.HasProperty("_RimColor")) paddleMat.SetColor("_RimColor", originalRimColor);
            }
        }

        Destroy(psObj, 2.0f);
    }
```

# Verification & Testing
1. Heal the paddle (damageDealt < 0f).
2. Check that the particle lines go upwards through the paddle on both clients.
3. Check that the paddle's rim and base color glow green and smoothly fade back to original colors.
