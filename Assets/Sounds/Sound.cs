using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Sound : MonoBehaviour
{
    public static Sound Instance { get; private set; }

    [Tooltip("Assign the wind AudioClip here")]
    public AudioClip windClip;

    [Range(0f,1f)]
    public float volume =1f;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.volume = volume;
            _audioSource.mute = false;
            _audioSource.spatialBlend =0f; //2D sound

            if (windClip != null)
            {
                _audioSource.clip = windClip;
                _audioSource.Play();
                Debug.Log("[Sound] Playing wind clip in Awake.");
            }
        }
        else if (Instance != this)
        {
            Debug.Log("[Sound] Duplicate instance detected. Destroying this GameObject.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // fallback in case clip was assigned after Awake or on runtime
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        _audioSource.spatialBlend =0f;
        _audioSource.mute = false;
        _audioSource.loop = true;
        _audioSource.volume = volume;
        _audioSource.enabled = true;

        // Log detailed AudioSource state for troubleshooting
        Debug.Log($"[Sound] AudioSource state: enabled={_audioSource.enabled}, activeInHierarchy={gameObject.activeInHierarchy}, clip={( _audioSource.clip != null ? _audioSource.clip.name : "null")}, isPlaying={_audioSource.isPlaying}, volume={_audioSource.volume}, mute={_audioSource.mute}, spatialBlend={_audioSource.spatialBlend}");

        if (windClip != null && !_audioSource.isPlaying)
        {
            _audioSource.clip = windClip;
            _audioSource.Stop();
            _audioSource.Play();
            Debug.Log("[Sound] Playing wind clip in Start (fallback). Forced Play called.");
        }

        if (Object.FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("[Sound] No AudioListener found in the scene. Add an AudioListener (usually on the Main Camera) to hear audio.");
        }
    }

    private void OnValidate()
    {
        // Keep inspector changes in sync
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource != null)
        {
            _audioSource.volume = volume;
            _audioSource.mute = false;
            _audioSource.spatialBlend =0f; // ensure2D in editor
            // Always update the clip in editor when user assigns it
            if (windClip != null && _audioSource.clip != windClip)
            {
                _audioSource.clip = windClip;
            }
        }
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_audioSource != null)
            _audioSource.volume = volume;
    }

    public bool IsPlaying()
    {
        return _audioSource != null && _audioSource.isPlaying;
    }
}
