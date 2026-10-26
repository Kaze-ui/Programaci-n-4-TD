using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Controlador central de audio (Música, Efectos de Sonido y Carga Continua).
/// Implementa el patrón Singleton persistente (DontDestroyOnLoad).
/// </summary>
public class SoundController : MonoBehaviour
{
    private static SoundController _instance;
    private static bool isApplicationQuitting = false;

    public static bool HasInstance => _instance != null && !isApplicationQuitting;

    public static SoundController Instance
    {
        get
        {
            if (isApplicationQuitting) return null;

            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SoundController>();
                if (_instance == null && !isApplicationQuitting)
                {
                    GameObject go = new GameObject("SoundController");
                    _instance = go.AddComponent<SoundController>();
                }
            }
            return _instance;
        }
    }

    [Header("Fuentes de audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource chargeSource;

    [Header("Música Temática")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;

    [Header("Clips de Efectos")]
    public AudioClip shootSfx;
    public AudioClip miniShootSfx;
    public AudioClip mediumShootSfx;
    public AudioClip chargeSfx;
    public AudioClip bigShootSfx;
    public AudioClip switchModeSfx;
    public AudioClip enemyDeathSfx;
    public AudioClip bossDeathSfx;
    public AudioClip bossLaserSfx;
    public AudioClip tier4LaserSfx;
    public AudioClip playerHitSfx;
    public AudioClip playerDeathSfx;
    public AudioClip upgradeBuySfx;
    public AudioClip upgradeOpenSfx;
    public AudioClip upgradeHoverSfx;
    public AudioClip upgradeFailedSfx;
    public AudioClip upgradeContinueSfx;

    private float sfxVolume = 1.0f;
    private float musicVolume = 1.0f;
    private Coroutine musicFadeCoroutine;
    private float lastPlayerDeathTime = -10f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitAudioSources();
        PreloadClips();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnApplicationQuit() => isApplicationQuitting = true;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Start() => PlayMusicForCurrentScene();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayMusicForCurrentScene();

    private void InitAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        if (chargeSource == null)
        {
            chargeSource = gameObject.AddComponent<AudioSource>();
            chargeSource.playOnAwake = false;
            chargeSource.loop = true;
        }
    }

    private void PreloadClips()
    {
        if (menuMusic == null) menuMusic = Resources.Load<AudioClip>("Audio/music_menu");
        if (gameMusic == null) gameMusic = Resources.Load<AudioClip>("Audio/music_game");
    }

    // ==========================================
    // MÚSICA Y TRANSICIONES (CROSSFADE)
    // ==========================================

    public void PlayMusicForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "MenúPrincipal" || sceneName.Contains("Menu"))
        {
            PlayMenuMusic();
        }
        else
        {
            PlayGameMusic();
        }
    }

    public void PlayMenuMusic(bool fade = true)
    {
        if (menuMusic == null) menuMusic = Resources.Load<AudioClip>("Audio/music_menu");
        SwitchMusicTrack(menuMusic, fade, 0.75f);
    }

    public void PlayGameMusic(bool fade = true)
    {
        if (gameMusic == null || gameMusic.loadState == AudioDataLoadState.Unloaded)
            gameMusic = Resources.Load<AudioClip>("Audio/music_game");
        SwitchMusicTrack(gameMusic, fade, 0.75f);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip != null) SwitchMusicTrack(clip, false, 0.75f);
    }

    private void SwitchMusicTrack(AudioClip newClip, bool fade, float targetVolumeFactor = 1f)
    {
        if (musicSource == null) InitAudioSources();
        if (newClip == null || (musicSource.clip == newClip && musicSource.isPlaying)) return;

        if (musicFadeCoroutine != null) StopCoroutine(musicFadeCoroutine);

        if (fade && musicSource.isPlaying && gameObject.activeInHierarchy)
        {
            musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(newClip, targetVolumeFactor));
        }
        else
        {
            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.volume = musicVolume * targetVolumeFactor;
            musicSource.Play();
        }
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip newClip, float targetVolumeFactor)
    {
        float startVol = musicSource.volume;
        float fadeOutTime = 0.5f;
        float t = 0f;

        while (t < fadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeOutTime);
            yield return null;
        }

        musicSource.Stop();
        musicSource.clip = newClip;
        musicSource.loop = true;
        musicSource.Play();

        float fadeInTime = 0.6f;
        t = 0f;
        float targetVol = musicVolume * targetVolumeFactor;

        while (t < fadeInTime)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVol, t / fadeInTime);
            yield return null;
        }

        musicSource.volume = targetVol;
        musicFadeCoroutine = null;
    }

    public void StopMusic(bool fade = false)
    {
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = null;
        }

        if (musicSource == null) return;

        if (fade && musicSource.isPlaying && gameObject.activeInHierarchy)
        {
            musicFadeCoroutine = StartCoroutine(FadeOutAndStopRoutine(0.5f));
        }
        else
        {
            musicSource.Stop();
            musicSource.clip = null;
        }
    }

    private IEnumerator FadeOutAndStopRoutine(float fadeDuration)
    {
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeDuration);
            yield return null;
        }
        musicSource.Stop();
        musicSource.clip = null;
        musicFadeCoroutine = null;
    }

    // ==========================================
    // REPRODUCTOR GENÉRICO DE EFECTOS (DRY)
    // ==========================================

    private void PlayClip(ref AudioClip clip, string resourcePath, float volume = 1f, float minPitch = 1f, float maxPitch = 1f)
    {
        if (sfxSource == null) InitAudioSources();
        if (clip == null) clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null) return;

        sfxSource.pitch = (minPitch == maxPitch) ? minPitch : Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, volume * sfxVolume);
    }

    // Disparos y Carga
    public void PlayNormalShootSfx() => PlayClip(ref shootSfx, "Audio/shoot_normal", 0.70f, 0.97f, 1.03f);
    public void PlayMiniShootSfx() => PlayClip(ref miniShootSfx, "Audio/shoot_mini", 0.60f, 1.05f, 1.15f);
    public void PlayMediumShootSfx() => PlayClip(ref mediumShootSfx, "Audio/shoot_medium", 0.85f, 0.96f, 1.04f);
    public void PlayBigShootSfx() => PlayClip(ref bigShootSfx, "Audio/shoot_big", 1.0f, 0.96f, 1.02f);
    public void PlayShootSfx() => PlayNormalShootSfx();
    public void PlaySwitchModeSfx() => PlayClip(ref switchModeSfx, "Audio/upgrade_buy", 0.85f, 0.88f, 0.95f);

    public void UpdateChargeSound(float progress)
    {
        if (chargeSource == null) InitAudioSources();
        if (chargeSfx == null) chargeSfx = Resources.Load<AudioClip>("Audio/charge_energy");
        if (chargeSfx == null) return;

        if (chargeSource.clip != chargeSfx)
        {
            chargeSource.clip = chargeSfx;
            chargeSource.loop = true;
        }

        if (!chargeSource.isPlaying)
        {
            chargeSource.pitch = 0.85f;
            chargeSource.volume = 0.15f * sfxVolume;
            chargeSource.Play();
        }

        chargeSource.volume = Mathf.MoveTowards(chargeSource.volume, 0.75f * sfxVolume, Time.deltaTime * 3.5f);
        chargeSource.pitch = Mathf.Lerp(0.85f, 1.75f, progress);
    }

    public void StopChargeSound()
    {
        if (chargeSource != null && chargeSource.isPlaying)
        {
            chargeSource.Stop();
            chargeSource.pitch = 1.0f;
            chargeSource.volume = 0f;
        }
    }

    // Combate y Daño
    public void PlayEnemyDeathSfx() => PlayClip(ref enemyDeathSfx, "Audio/enemy_explosion", 0.85f, 0.92f, 1.08f);
    public void PlayPlayerHitSfx() => PlayClip(ref playerHitSfx, "Audio/player_hit", 1.0f, 0.95f, 1.05f);
    public void PlayTier4LaserSfx() => PlayClip(ref tier4LaserSfx, "Audio/tier4_laser", 0.85f, 0.96f, 1.04f);
    public void PlayBossLaserSfx() => PlayClip(ref bossLaserSfx, "Audio/boss_laser", 1.0f, 0.98f, 1.02f);
    public void PlayBossDeathSfx() => PlayClip(ref bossDeathSfx, "Audio/boss_death", 1.15f);

    public void PlayPlayerDeathSfx()
    {
        if (Time.unscaledTime - lastPlayerDeathTime < 1.0f) return;
        lastPlayerDeathTime = Time.unscaledTime;
        PlayClip(ref playerDeathSfx, "Audio/player_death", 1.15f);
    }

    public void PlayGameOverSfx() => PlayPlayerDeathSfx();

    // Tienda y Mejoras
    public void PlayUpgradePurchasedSfx() => PlayClip(ref upgradeBuySfx, "Audio/upgrade_buy", 0.90f);
    public void PlayUpgradePanelOpenSfx() => PlayClip(ref upgradeOpenSfx, "Audio/upgrade_open", 0.90f);
    public void PlayUpgradeHoverSfx() => PlayClip(ref upgradeHoverSfx, "Audio/upgrade_hover", 0.60f, 0.98f, 1.02f);
    public void PlayUpgradeFailedSfx() => PlayClip(ref upgradeFailedSfx, "Audio/upgrade_failed", 0.85f);
    public void PlayUpgradeContinueSfx() => PlayClip(ref upgradeContinueSfx, "Audio/upgrade_continue", 0.85f);

    public void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (chargeSource != null) chargeSource.volume = sfxVolume;
    }
}