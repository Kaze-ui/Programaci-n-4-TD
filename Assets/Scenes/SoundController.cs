using UnityEngine;

public class SoundController : MonoBehaviour
{
    public static SoundController Instance { get; private set; }

    [Header("Fuentes de audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips (asignar cuando tengas los archivos)")]
    public AudioClip backgroundMusic;
    public AudioClip shootSfx;
    public AudioClip enemyDeathSfx;
    public AudioClip playerHitSfx;
    public AudioClip gameOverSfx;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;

        sfxSource.PlayOneShot(clip);
    }

    // Atajos para los sonidos más comunes del juego,
    // así los demás scripts no necesitan conocer los AudioClips directamente.
    public void PlayShootSfx() => PlaySfx(shootSfx);
    public void PlayEnemyDeathSfx() => PlaySfx(enemyDeathSfx);
    public void PlayPlayerHitSfx() => PlaySfx(playerHitSfx);
    public void PlayGameOverSfx() => PlaySfx(gameOverSfx);

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(volume);
        }
    }

    public void SetSfxVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(volume);
        }
    }
}