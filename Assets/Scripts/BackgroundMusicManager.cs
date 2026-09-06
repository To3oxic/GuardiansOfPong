using UnityEngine;

/// <summary>
/// Manages background music for the scene.
/// Automatically starts playing music on scene load.
/// </summary>
public class BackgroundMusicManager : MonoBehaviour
{
    public static BackgroundMusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Header("Background Music Settings")]
    public AudioClip introMusicClip;      // Plays once at the start
    public AudioClip loopMusicClip;       // Loops after intro finishes
    public float volume = 0.5f;

    private bool isPlayingIntro = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeAudioSource();
            PlayBackgroundMusic();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSource()
    {
        // Get or create AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure audio source
        audioSource.volume = volume;
        audioSource.playOnAwake = false;
    }

    private void PlayBackgroundMusic()
    {
        if (introMusicClip == null)
        {
            return;
        }

        // Start with intro clip (no loop)
        audioSource.clip = introMusicClip;
        audioSource.loop = false;
        audioSource.Play();
        isPlayingIntro = true;
    }

    private void Update()
    {
        // Check if intro finished and loop music is assigned
        if (isPlayingIntro && !audioSource.isPlaying && loopMusicClip != null)
        {
            PlayLoopMusic();
        }
    }

    private void PlayLoopMusic()
    {
        audioSource.clip = loopMusicClip;
        audioSource.loop = true;
        audioSource.Play();
        isPlayingIntro = false;
    }

    /// <summary>
    /// Stop the background music
    /// </summary>
    public void StopMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Pause the background music
    /// </summary>
    public void PauseMusic()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// Resume the background music
    /// </summary>
    public void ResumeMusic()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    /// <summary>
    /// Set the volume of background music (0-1)
    /// </summary>
    public void SetVolume(float newVolume)
    {
        if (audioSource != null)
        {
            audioSource.volume = Mathf.Clamp01(newVolume);
            volume = audioSource.volume;
        }
    }
}
