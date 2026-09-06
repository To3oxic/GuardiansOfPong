using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public class BeamSettings
{
    [Header("Particle Beam Settings")]
    public Color beamColor = Color.cyan;
    public float beamWidth = 0.1f;
    public float maxBeamWidth = 0.5f;
    public float particleSpeed = 5f;
    public int particlesPerSecond = 100;

    [Header("Dynamic Scaling")]
    public AnimationCurve widthCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f);
    public float pulsationSpeed = 2f;
    public float pulsationIntensity = 0.3f;

    [Header("Paddle Coverage")]
    public float paddleDetectionRadius = 1f;
    public LayerMask paddleLayerMask = 1;
}

public class SpiritBeamController : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem beamParticleSystem;
    public Transform stoneTransform;
    public Transform paddleTransform;

    [Header("Beam Configuration")]
    public BeamSettings beamSettings;

    [Header("Additional Effects")]
    public ParticleSystem paddleImpactEffect; // Particles around the paddle
    public LineRenderer guideLine; // Optional visible line for debugging

    private ParticleSystem.MainModule mainModule;
    private ParticleSystem.ShapeModule shapeModule;
    private ParticleSystem.VelocityOverLifetimeModule velocityModule;
    private ParticleSystem.SizeOverLifetimeModule sizeModule;
    private ParticleSystem.ColorOverLifetimeModule colorModule;

    private float currentDistance;
    private Vector3 beamDirection;
    private bool isInitialized = false;

    // Public properties
    public bool IsInitialized { get { return isInitialized; } }
    public Transform PaddleTransform
    {
        get { return paddleTransform; }
        set { paddleTransform = value; }
    }

    void Start()
    {
        InitializeParticleSystem();
    }

    public void Initialize(Transform stone, Transform paddle, BeamSettings settings = null)
    {
        stoneTransform = stone;
        paddleTransform = paddle;

        if (settings != null)
            beamSettings = settings;

        if (beamParticleSystem == null)
            beamParticleSystem = GetComponent<ParticleSystem>();

        InitializeParticleSystem();
        isInitialized = true;

        // Start the particle system
        if (beamParticleSystem != null && !beamParticleSystem.isPlaying)
        {
            beamParticleSystem.Play();
        }
    }

    public void InitializeWithPaddlePrefab(Transform stone, GameObject paddlePrefab, BeamSettings settings = null)
    {
        stoneTransform = stone;

        if (settings != null)
            beamSettings = settings;

        if (beamParticleSystem == null)
            beamParticleSystem = GetComponent<ParticleSystem>();

        // Calculate expected paddle position based on stone position and game layout
        Vector3 expectedPaddlePosition = CalculateExpectedPaddlePosition(stone.position, paddlePrefab);

        // Create a temporary target for the beam direction
        GameObject tempTarget = new GameObject("TempPaddleTarget");
        tempTarget.transform.position = expectedPaddlePosition;
        tempTarget.transform.SetParent(transform); // Make it a child so it moves with the stone
        paddleTransform = tempTarget.transform;

        InitializeParticleSystem();
        isInitialized = true;

        // Start the particle system
        if (beamParticleSystem != null && !beamParticleSystem.isPlaying)
        {
            beamParticleSystem.Play();
        }
    }

    private Vector3 CalculateExpectedPaddlePosition(Vector3 stonePosition, GameObject paddlePrefab)
    {
        // Simply use the paddle prefab's actual position
        if (paddlePrefab != null)
        {
            return paddlePrefab.transform.position;
        }

        // Fallback to a default direction if no prefab
        return stonePosition + Vector3.right * 5f;
    }

    void InitializeParticleSystem()
    {
        if (beamParticleSystem == null)
        {
            return;
        }

        // Get particle system modules
        mainModule = beamParticleSystem.main;
        shapeModule = beamParticleSystem.shape;
        velocityModule = beamParticleSystem.velocityOverLifetime;
        sizeModule = beamParticleSystem.sizeOverLifetime;
        colorModule = beamParticleSystem.colorOverLifetime;

        // Configure main module
        mainModule.startColor = beamSettings.beamColor;
        mainModule.startSpeed = beamSettings.particleSpeed;
        mainModule.maxParticles = beamSettings.particlesPerSecond * 3; // Allow for longer beams
        mainModule.simulationSpace = ParticleSystemSimulationSpace.Local; // Use local space so particles follow transform rotation

        // Configure emission
        var emission = beamParticleSystem.emission;
        emission.rateOverTime = beamSettings.particlesPerSecond;

        // Configure shape for cone emission pointing forward
        shapeModule.enabled = true;
        shapeModule.shapeType = ParticleSystemShapeType.Cone;
        shapeModule.angle = 10f; // Narrow cone
        shapeModule.radius = beamSettings.beamWidth;
        shapeModule.length = 0.1f; // Short cone length

        // Configure velocity to move towards paddle
        velocityModule.enabled = false; // Disable initially, we'll use startSpeed and rotation instead

        // Configure size over lifetime for beam expansion
        sizeModule.enabled = true;
        sizeModule.size = new ParticleSystem.MinMaxCurve(1f, beamSettings.widthCurve);

        // Configure color with fade
        colorModule.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(beamSettings.beamColor, 0.0f),
                new GradientColorKey(beamSettings.beamColor, 0.8f),
                new GradientColorKey(beamSettings.beamColor, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.2f),
                new GradientAlphaKey(0.7f, 0.8f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorModule.color = gradient;

        // Setup paddle impact effect if available
        if (paddleImpactEffect != null)
        {
            var impactMain = paddleImpactEffect.main;
            impactMain.startColor = beamSettings.beamColor;
        }

        // Setup debug line if available
        if (guideLine != null)
        {
            if (guideLine.material != null)
                guideLine.material.color = beamSettings.beamColor;
            guideLine.startWidth = 0.02f;
            guideLine.endWidth = 0.02f;
        }
    }

    void Update()
    {
        if (!isInitialized)
        {
            //Debug.Log("[SpiritBeamController] Not initialized yet");
            return;
        }

        if (stoneTransform == null)
        {
            return;
        }

        if (paddleTransform == null)
        {
            return;
        }

        // Ensure particle system is playing
        if (beamParticleSystem != null && !beamParticleSystem.isPlaying)
        {
            beamParticleSystem.Play();
        }

        UpdateBeamDirection();
        UpdateParticleSystem();
        UpdatePaddleEffect();
        UpdateDebugLine();
    }

    void UpdateBeamDirection()
    {
        // Calculate direction and distance
        beamDirection = paddleTransform.position - stoneTransform.position;
        currentDistance = beamDirection.magnitude;
        beamDirection.Normalize();

        // Position the particle system at the stone
        transform.position = stoneTransform.position;

        // Orient the particle system towards the paddle
        if (beamDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(beamDirection);

            // Debug the beam direction every few frames
            if (Time.frameCount % 60 == 0) // Every 60 frames (roughly once per second at 60fps)
            {
            }
        }
    }

    void UpdateParticleSystem()
    {
        // Update particle lifetime based on distance and speed
        float requiredLifetime = currentDistance / beamSettings.particleSpeed;
        mainModule.startLifetime = requiredLifetime;

        // Update main particle speed (simpler approach)
        mainModule.startSpeed = beamSettings.particleSpeed;

        // Add pulsation effect
        float pulsation = 1f + Mathf.Sin(Time.time * beamSettings.pulsationSpeed) * beamSettings.pulsationIntensity;
        var emission = beamParticleSystem.emission;
        emission.rateOverTime = beamSettings.particlesPerSecond * pulsation;

        // Update beam width based on distance (closer = narrower, farther = wider)
        float distanceRatio = Mathf.Clamp01(currentDistance / 10f); // Normalize to reasonable range
        float dynamicWidth = Mathf.Lerp(beamSettings.beamWidth, beamSettings.maxBeamWidth, distanceRatio);
        shapeModule.radius = dynamicWidth * pulsation;

        // Update cone angle for better coverage at longer distances
        shapeModule.angle = Mathf.Lerp(2f, 8f, distanceRatio);
    }

    void UpdatePaddleEffect()
    {
        if (paddleImpactEffect == null || paddleTransform == null)
            return;

        // Position the impact effect at the paddle
        paddleImpactEffect.transform.position = paddleTransform.position;

        // Scale the effect based on beam intensity
        float pulsation = 1f + Mathf.Sin(Time.time * beamSettings.pulsationSpeed * 1.5f) * beamSettings.pulsationIntensity;
        var impactMain = paddleImpactEffect.main;
        var impactShape = paddleImpactEffect.shape;

        // Get paddle bounds for dynamic sizing
        Renderer paddleRenderer = paddleTransform.GetComponent<Renderer>();
        if (paddleRenderer != null)
        {
            Bounds paddleBounds = paddleRenderer.bounds;
            impactShape.radius = Mathf.Max(paddleBounds.size.x, paddleBounds.size.z) * 0.6f * pulsation;
        }
        else
        {
            impactShape.radius = beamSettings.paddleDetectionRadius * pulsation;
        }

        // Ensure the paddle effect is playing
        if (!paddleImpactEffect.isPlaying)
        {
            paddleImpactEffect.Play();
        }
    }

    void UpdateDebugLine()
    {
        if (guideLine == null)
            return;

        guideLine.SetPosition(0, stoneTransform.position);
        guideLine.SetPosition(1, paddleTransform.position);
    }

    public void SetBeamColor(Color color)
    {
        beamSettings.beamColor = color;
        mainModule.startColor = color;

        if (paddleImpactEffect != null)
        {
            var impactMain = paddleImpactEffect.main;
            impactMain.startColor = color;
        }

        if (guideLine != null && guideLine.material != null)
        {
            guideLine.material.color = color;
        }
    }

    public void SetBeamIntensity(float intensity)
    {
        beamSettings.pulsationIntensity = intensity;
    }

    public void EnableBeam(bool enable)
    {
        if (beamParticleSystem != null)
        {
            if (enable && !beamParticleSystem.isPlaying)
                beamParticleSystem.Play();
            else if (!enable && beamParticleSystem.isPlaying)
                beamParticleSystem.Stop();
        }

        if (paddleImpactEffect != null)
        {
            if (enable && !paddleImpactEffect.isPlaying)
                paddleImpactEffect.Play();
            else if (!enable && paddleImpactEffect.isPlaying)
                paddleImpactEffect.Stop();
        }
    }

    void OnDrawGizmos()
    {
        if (stoneTransform != null && paddleTransform != null)
        {
            // Draw connection line in scene view
            Gizmos.color = beamSettings.beamColor;
            Gizmos.DrawLine(stoneTransform.position, paddleTransform.position);

            // Draw paddle detection radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(paddleTransform.position, beamSettings.paddleDetectionRadius);
        }
    }
}