using System.Collections;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class GameStartAnimationController : NetworkBehaviour
{
    [Header("Camera Animation")]
    public Camera gameCamera;
    public float cameraAnimationDuration = 3f;
    public AnimationCurve cameraMovementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Orthographic Camera Settings")]
    public float startCameraSize = 100f;
    public float finalCameraSize = 70f;

    [Header("Camera Position & Rotation (Client-specific)")]
    public Vector3 client1FinalPosition = new Vector3(0f, 90f, 30f); // Player 1 position
    public Vector3 client1FinalRotation = new Vector3(110f, 0f, 180f); // Player 1 rotation
    public Vector3 client2FinalPosition = new Vector3(0f, 90f, -30f); // Player 2 position
    public Vector3 client2FinalRotation = new Vector3(60f, 0f, 0f); // Player 2 rotation
    public Vector3 startPosition = new Vector3(0f, 100f, 0f); // Starting position (higher up, center)
    public Vector3 startRotation = new Vector3(90f, 0f, 0f); // Looking straight down initially

    [Header("LegendStone Animation")]
    public Transform[] legendStoneHatchPositions; // Underground positions
    public Transform[] legendStoneFinalPositions; // Above ground positions
    public float stoneRiseDelay = 1f; // Delay after camera animation starts
    public float stoneRiseDuration = 2f;
    public AnimationCurve stoneRiseCurve = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));

    [Header("Paddle Animation")]
    public float paddleLevitationDelay = 2f; // Delay after stones start rising
    public float paddleLevitationDuration = 1.5f;
    public float paddleLevitationHeight = 0.5f; // How high above ground to levitate
    public AnimationCurve paddleLevitationCurve = new AnimationCurve(new Keyframe(0, 0, 0, 2), new Keyframe(1, 1, 0, 0));

    [Header("Countdown Timer")]
    public TextMeshProUGUI countdownText;
    public float countdownDelay = 3.5f; // Delay after camera animation starts
    public float countdownDuration = 3f; // 3-2-1 countdown
    public AudioSource countdownAudioSource;
    public AudioClip countdownBeep;
    public AudioClip finalBeep;

    [Header("Effects")]
    public ParticleSystem[] dustEffects; // Dust when stones rise from ground
    public AudioSource ambientAudioSource;
    public AudioClip stoneRisingSound;
    public AudioClip paddleLevitationSound;

    private bool animationInProgress = false;
    private LegendStoneController[] legendStones;
    private GameObject[] paddles;

    public System.Action OnAnimationComplete;

    void Start()
    {
        // Initialize countdown text as hidden
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    public void StartGameAnimation()
    {
        if (animationInProgress) return;
        if (!IsServer) return; // Only server controls the animation timing

        animationInProgress = true;

        // Trigger animation on all clients (including server)
        StartGameAnimationClientRpc();

        // Server also starts a timer to trigger completion after expected duration
        StartCoroutine(ServerAnimationTimer());
    }

    private IEnumerator ServerAnimationTimer()
    {
        // Calculate total expected animation duration
        float totalDuration = Mathf.Max(
            cameraAnimationDuration,
            stoneRiseDelay + stoneRiseDuration,
            paddleLevitationDelay + paddleLevitationDuration,
            countdownDelay + countdownDuration
        );

        yield return new WaitForSeconds(totalDuration);

        animationInProgress = false;
        OnAnimationComplete?.Invoke();
    }

    [ClientRpc]
    private void StartGameAnimationClientRpc()
    {
        StartCoroutine(PlayAnimationSequence());
    }

    private IEnumerator PlayAnimationSequence()
    {
        // Wait (briefly) for this client's paddle to spawn so the camera can be
        // placed on the correct side. Falls through after the timeout.
        float wait = 0f;
        while (GetLocalPaddle() == null && wait < 3f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        // Setup initial positions
        SetupInitialPositions();

        // 2. Legend stones rise from underground (delayed)
        yield return new WaitForSeconds(stoneRiseDelay);
        StartCoroutine(AnimateLegendStones());

        // 3. Paddles levitate (delayed)
        yield return new WaitForSeconds(paddleLevitationDelay - stoneRiseDelay);
        StartCoroutine(AnimatePaddles());

        // 4. Countdown timer (delayed)
        yield return new WaitForSeconds(countdownDelay - paddleLevitationDelay);
        yield return StartCoroutine(PlayCountdown());

        // 5. Client animation sequence complete
        // Note: Server handles the OnAnimationComplete callback via ServerAnimationTimer
    }

    /// <summary>
    /// The transform of the paddle owned by this client, or null if it has not spawned yet.
    /// </summary>
    private Transform GetLocalPaddle()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) return null;

        ulong me = nm.LocalClientId;
        foreach (var paddle in FindObjectsByType<PaddleController>(FindObjectsSortMode.None))
        {
            if (paddle != null && paddle.OwnerClientId == me)
                return paddle.transform;
        }
        return null;
    }

    private void SetupInitialPositions()
    {
        // Find legend stones and paddles in scene
        legendStones = FindObjectsByType<LegendStoneController>(FindObjectsSortMode.None);

        // Find paddles from GameManager
        if (GameManager.Instance != null)
        {
            // We'll get paddle references from the GameManager's spawnedPaddles dictionary
            paddles = new GameObject[2];
            var paddleDict = typeof(GameManager).GetField("spawnedPaddles",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(GameManager.Instance) as System.Collections.Generic.Dictionary<ulong, GameObject>;

            if (paddleDict != null)
            {
                int index = 0;
                foreach (var paddle in paddleDict.Values)
                {
                    if (index < paddles.Length)
                    {
                        paddles[index] = paddle;
                        index++;
                    }
                }
            }
        }

        // Set camera to final position and rotation instantly (no animation)
        if (gameCamera != null)
        {
            // Which side is this client on? Prefer the local paddle's actual position
            // (client 1 = +z, client 2 = -z) and fall back to the networked flag.
            Transform localPaddle = GetLocalPaddle();
            bool isClient1 = localPaddle != null ? localPaddle.position.z > 0f : GameManager.IsClientOne;

            Vector3 finalPosition = isClient1 ? client1FinalPosition : client2FinalPosition;
            Vector3 finalRotation = isClient1 ? client1FinalRotation : client2FinalRotation;

            gameCamera.orthographicSize = finalCameraSize;
            gameCamera.transform.position = finalPosition;
            gameCamera.transform.rotation = Quaternion.Euler(finalRotation);
        }

        // Set legend stones to underground positions
        for (int i = 0; i < legendStones.Length && i < legendStoneHatchPositions.Length; i++)
        {
            if (legendStones[i] != null && legendStoneHatchPositions[i] != null)
            {
                legendStones[i].transform.position = legendStoneHatchPositions[i].position;
                legendStones[i].transform.rotation = legendStoneHatchPositions[i].rotation;
            }
        }

        // Set paddles to ground level (disable levitation initially)
        for (int i = 0; i < paddles.Length; i++)
        {
            if (paddles[i] != null)
            {
                Vector3 groundPos = paddles[i].transform.position;
                groundPos.y = 0f; // Ground level
                paddles[i].transform.position = groundPos;
            }
        }
    }

    private IEnumerator AnimateCamera()
    {
        if (gameCamera == null)
        {
            yield break;
        }

        // Determine which client this is and set appropriate final rotation
        bool isClient1 = GameManager.IsClientOne;
        ulong localClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        Vector3 finalRotation = isClient1 ? client1FinalRotation : client2FinalRotation;

        float elapsedTime = 0f;
        float startSize = startCameraSize;
        float endSize = finalCameraSize;
        Quaternion startRot = Quaternion.Euler(startRotation);
        Quaternion endRot = Quaternion.Euler(finalRotation);

        while (elapsedTime < cameraAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / cameraAnimationDuration;
            float curveValue = cameraMovementCurve.Evaluate(progress);

            // Animate orthographic size
            gameCamera.orthographicSize = Mathf.Lerp(startSize, endSize, curveValue);

            // Animate rotation
            gameCamera.transform.rotation = Quaternion.Lerp(startRot, endRot, curveValue);

            yield return null;
        }

        // Ensure final values are set
        gameCamera.orthographicSize = endSize;
        gameCamera.transform.rotation = endRot;
    }

    private IEnumerator AnimateLegendStones()
    {
        // Play rising sound effect
        if (ambientAudioSource != null && stoneRisingSound != null)
        {
            ambientAudioSource.PlayOneShot(stoneRisingSound);
        }

        // Trigger dust effects
        foreach (var dustEffect in dustEffects)
        {
            if (dustEffect != null)
            {
                dustEffect.Play();
            }
        }

        float elapsedTime = 0f;
        Vector3[] startPositions = new Vector3[legendStones.Length];
        Vector3[] endPositions = new Vector3[legendStones.Length];

        // Record start and end positions
        for (int i = 0; i < legendStones.Length && i < legendStoneFinalPositions.Length; i++)
        {
            if (legendStones[i] != null)
            {
                startPositions[i] = legendStones[i].transform.position;
                endPositions[i] = legendStoneFinalPositions[i].position;
            }
        }

        while (elapsedTime < stoneRiseDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / stoneRiseDuration;
            float curveValue = stoneRiseCurve.Evaluate(progress);

            for (int i = 0; i < legendStones.Length && i < legendStoneFinalPositions.Length; i++)
            {
                if (legendStones[i] != null)
                {
                    legendStones[i].transform.position = Vector3.Lerp(startPositions[i], endPositions[i], curveValue);
                }
            }

            yield return null;
        }

        // Ensure final positions are set
        for (int i = 0; i < legendStones.Length && i < legendStoneFinalPositions.Length; i++)
        {
            if (legendStones[i] != null)
            {
                legendStones[i].transform.position = endPositions[i];
            }
        }
    }

    private IEnumerator AnimatePaddles()
    {
        // Play levitation sound effect
        if (ambientAudioSource != null && paddleLevitationSound != null)
        {
            ambientAudioSource.PlayOneShot(paddleLevitationSound);
        }

        float elapsedTime = 0f;
        Vector3[] startPositions = new Vector3[paddles.Length];
        Vector3[] endPositions = new Vector3[paddles.Length];

        // Record start and end positions
        for (int i = 0; i < paddles.Length; i++)
        {
            if (paddles[i] != null)
            {
                startPositions[i] = paddles[i].transform.position;
                endPositions[i] = startPositions[i] + Vector3.up * paddleLevitationHeight;
            }
        }

        while (elapsedTime < paddleLevitationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / paddleLevitationDuration;
            float curveValue = paddleLevitationCurve.Evaluate(progress);

            for (int i = 0; i < paddles.Length; i++)
            {
                if (paddles[i] != null)
                {
                    paddles[i].transform.position = Vector3.Lerp(startPositions[i], endPositions[i], curveValue);
                }
            }

            yield return null;
        }

        // Ensure final positions are set
        for (int i = 0; i < paddles.Length; i++)
        {
            if (paddles[i] != null)
            {
                paddles[i].transform.position = endPositions[i];
            }
        }
    }

    private IEnumerator PlayCountdown()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.fontSize = 120;
            countdownText.color = Color.white;
        }

        for (int i = 3; i >= 1; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = i.ToString();
            }

            // Play countdown beep
            if (countdownAudioSource != null && countdownBeep != null)
            {
                countdownAudioSource.PlayOneShot(countdownBeep);
            }

            // Scale animation for countdown number
            if (countdownText != null)
            {
                StartCoroutine(AnimateCountdownNumber());
            }

            yield return new WaitForSeconds(1f);
        }

        // Final "GO!" or hide countdown
        if (countdownText != null)
        {
            countdownText.text = "GO!";
            countdownText.color = Color.green;

            if (countdownAudioSource != null && finalBeep != null)
            {
                countdownAudioSource.PlayOneShot(finalBeep);
            }

            StartCoroutine(AnimateCountdownNumber());
        }

        yield return new WaitForSeconds(0.5f);

        // Hide countdown text
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateCountdownNumber()
    {
        if (countdownText == null) yield break;

        // Scale up and then down for emphasis
        Vector3 originalScale = countdownText.transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        float duration = 0.2f;
        float elapsedTime = 0f;

        // Scale up
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            countdownText.transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }

        elapsedTime = 0f;

        // Scale down
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            countdownText.transform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            yield return null;
        }

        countdownText.transform.localScale = originalScale;
    }
}