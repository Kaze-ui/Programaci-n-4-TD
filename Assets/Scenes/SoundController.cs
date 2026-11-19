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
    private AudioSource entryAudioSource;

    [Header("Música Temática")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;
    public AudioClip freneticGameMusic;
    public AudioClip bossMusic;
    public AudioClip tutorialMusic;

    [Header("Entradas por Tier")]
    public AudioClip[] tierEntryClips = new AudioClip[4];
    public AudioClip bossEntrySfx;

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
    public AudioClip playerShieldBreakSfx;
    public AudioClip playerDeathSfx;
    public AudioClip upgradeBuySfx;
    public AudioClip upgradeOpenSfx;
    public AudioClip upgradeHoverSfx;
    public AudioClip upgradeFailedSfx;
    public AudioClip upgradeContinueSfx;

    private float sfxVolume = 1.0f;
    private float musicVolume = 1.0f;
    private Coroutine musicFadeCoroutine;
    private AudioClip pendingMusicClip;
    private AudioLowPassFilter musicLowPass;
    private bool isMusicMuffled = false;
    private float currentMuffleCutoff = 22000f;
    private const float MuffledCutoffHz = 560f;
    private const float ClearCutoffHz = 22000f;
    private AudioClip generatedMenuMusic;
    private AudioClip generatedGameMusic;
    private AudioClip generatedFreneticGameMusic;
    private AudioClip generatedBossMusic;
    private AudioClip generatedTutorialMusic;
    private readonly AudioClip[] generatedTierEntryClips = new AudioClip[4];
    private AudioClip generatedBossEntryClip;
    private AudioClip generatedBossLaserClip;
    private AudioClip generatedBossHyperBeamClip;
    private AudioClip generatedBossHyperWarningClip;
    private AudioClip generatedPlayerShieldBreakClip;
    private readonly float[] lastTierEntryTime = { -10f, -10f, -10f, -10f };
    private float lastPlayerDeathTime = -10f;
    private float lastShieldBreakTime = -10f;

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
        if (generatedMenuMusic != null)
        {
            Destroy(generatedMenuMusic);
            generatedMenuMusic = null;
        }
        if (generatedGameMusic != null)
        {
            Destroy(generatedGameMusic);
            generatedGameMusic = null;
        }
        if (generatedFreneticGameMusic != null)
        {
            Destroy(generatedFreneticGameMusic);
            generatedFreneticGameMusic = null;
        }
        if (generatedBossMusic != null)
        {
            Destroy(generatedBossMusic);
            generatedBossMusic = null;
        }
        if (generatedTutorialMusic != null)
        {
            Destroy(generatedTutorialMusic);
            generatedTutorialMusic = null;
        }
        for (int i = 0; i < generatedTierEntryClips.Length; i++)
        {
            if (generatedTierEntryClips[i] != null)
            {
                Destroy(generatedTierEntryClips[i]);
                generatedTierEntryClips[i] = null;
            }
        }
        if (generatedBossEntryClip != null)
        {
            Destroy(generatedBossEntryClip);
            generatedBossEntryClip = null;
        }
        if (generatedBossLaserClip != null)
        {
            Destroy(generatedBossLaserClip);
            generatedBossLaserClip = null;
        }
        if (generatedBossHyperBeamClip != null)
        {
            Destroy(generatedBossHyperBeamClip);
            generatedBossHyperBeamClip = null;
        }
        if (generatedBossHyperWarningClip != null)
        {
            Destroy(generatedBossHyperWarningClip);
            generatedBossHyperWarningClip = null;
        }
        if (_instance == this) _instance = null;
    }

    void Start() => PlayMusicForCurrentScene();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetMusicMuffled(false);
        PlayMusicForCurrentScene();
    }

    private void InitAudioSources()
    {
        if (musicSource == null)
        {
            Transform existingChannel = transform.Find("MusicChannel");
            GameObject musicObj = existingChannel != null ? existingChannel.gameObject : new GameObject("MusicChannel");
            musicObj.transform.SetParent(transform, false);
            musicSource = musicObj.GetComponent<AudioSource>();
            if (musicSource == null)
            {
                musicSource = musicObj.AddComponent<AudioSource>();
            }
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (musicLowPass == null && musicSource != null)
        {
            musicLowPass = musicSource.GetComponent<AudioLowPassFilter>();
            if (musicLowPass == null)
            {
                musicLowPass = musicSource.gameObject.AddComponent<AudioLowPassFilter>();
            }
            musicLowPass.cutoffFrequency = ClearCutoffHz;
            musicLowPass.lowpassResonanceQ = 1.3f;
            musicLowPass.enabled = false;
            currentMuffleCutoff = ClearCutoffHz;
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

        if (entryAudioSource == null)
        {
            entryAudioSource = gameObject.AddComponent<AudioSource>();
            entryAudioSource.playOnAwake = false;
            entryAudioSource.loop = false;
            entryAudioSource.spatialBlend = 0f;
        }
    }

    private void PreloadClips()
    {
        if (generatedMenuMusic == null) generatedMenuMusic = CreateMenuMusicClip();
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
        SetMusicMuffled(false);
        if (menuMusic == null)
        {
            if (generatedMenuMusic == null) generatedMenuMusic = CreateMenuMusicClip();
            menuMusic = generatedMenuMusic;
        }
        SwitchMusicTrack(menuMusic, fade, 0.75f);
    }

    public void PlayTutorialMusic(bool fade = true)
    {
        SetMusicMuffled(false);
        if (tutorialMusic == null)
        {
            tutorialMusic = Resources.Load<AudioClip>("Audio/music_tutorial");
        }
        if (tutorialMusic == null)
        {
            if (generatedTutorialMusic == null)
            {
                generatedTutorialMusic = CreateTutorialMusicClip();
            }
            tutorialMusic = generatedTutorialMusic;
        }
        SwitchMusicTrack(tutorialMusic, fade, 0.72f);
    }

    public void PlayGameMusic(bool fade = true)
    {
        if (gameMusic == null || gameMusic.loadState == AudioDataLoadState.Unloaded)
        {
            if (generatedGameMusic == null) generatedGameMusic = CreateGameMusicClip();
            gameMusic = generatedGameMusic;
        }
        SwitchMusicTrack(gameMusic, fade, 0.72f);
    }

    public void PlayFreneticGameMusic(bool fade = true)
    {
        if (freneticGameMusic == null || freneticGameMusic.loadState == AudioDataLoadState.Unloaded)
        {
            if (generatedFreneticGameMusic == null) generatedFreneticGameMusic = CreateWave3FreneticMusicClip();
            freneticGameMusic = generatedFreneticGameMusic;
        }
        SwitchMusicTrack(freneticGameMusic, fade, 0.75f);
    }

    public void PlayBossMusic(bool fade = true)
    {
        SetMusicMuffled(false);
        if (bossMusic == null || bossMusic.loadState == AudioDataLoadState.Unloaded)
        {
            if (generatedBossMusic == null) generatedBossMusic = CreateBossMusicClip();
            bossMusic = generatedBossMusic;
        }
        SwitchMusicTrack(bossMusic, fade, 0.80f);
    }

    public void SetMusicMuffled(bool muffled)
    {
        isMusicMuffled = muffled;
        if (musicLowPass != null && muffled)
        {
            musicLowPass.enabled = true;
            musicLowPass.lowpassResonanceQ = 1.3f;
        }
    }

    void Update()
    {
        if (musicLowPass == null) return;

        float targetHz = isMusicMuffled ? MuffledCutoffHz : ClearCutoffHz;
        if (!isMusicMuffled && Mathf.Abs(currentMuffleCutoff - ClearCutoffHz) < 600f)
        {
            currentMuffleCutoff = ClearCutoffHz;
            musicLowPass.cutoffFrequency = ClearCutoffHz;
            if (musicLowPass.enabled) musicLowPass.enabled = false;
            return;
        }

        if (!musicLowPass.enabled) musicLowPass.enabled = true;
        float speed = isMusicMuffled ? 11f : 9f;
        float blend = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        float logCur = Mathf.Log(Mathf.Max(80f, currentMuffleCutoff));
        float logDst = Mathf.Log(targetHz);
        currentMuffleCutoff = Mathf.Exp(Mathf.Lerp(logCur, logDst, blend));
        musicLowPass.cutoffFrequency = currentMuffleCutoff;
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip != null) SwitchMusicTrack(clip, false, 0.75f);
    }

    private void SwitchMusicTrack(AudioClip newClip, bool fade, float targetVolumeFactor = 1f)
    {
        if (musicSource == null) InitAudioSources();
        if (newClip == null) return;

        bool alreadyPlayingTarget = musicFadeCoroutine == null && musicSource.clip == newClip && musicSource.isPlaying;
        bool alreadyFadingToTarget = musicFadeCoroutine != null && pendingMusicClip == newClip;
        if (alreadyPlayingTarget || alreadyFadingToTarget) return;

        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = null;
        }

        if (fade && musicSource.isPlaying && gameObject.activeInHierarchy)
        {
            pendingMusicClip = newClip;
            musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(newClip, targetVolumeFactor));
        }
        else
        {
            pendingMusicClip = null;
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
        musicSource.volume = 0f;
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
        pendingMusicClip = null;
    }

    public void StopMusic(bool fade = false)
    {
        SetMusicMuffled(false);
        pendingMusicClip = null;
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

    public void PauseMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Pause();
        }
    }

    public void ResumeMusic()
    {
        if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
        {
            musicSource.UnPause();
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
    // MÚSICA AMBIENTAL PARA EL MODO TUTORIAL
    // ==========================================

    private static float MidiToHz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    /// <summary>
    /// Genera en memoria un bucle ambiental cálido y relajado (8 compases, sin percusión agresiva)
    /// para la pantalla de entrenamiento del Tutorial.
    /// </summary>
    private static AudioClip CreateTutorialMusicClip()
    {
        const int sampleRate = 44100;
        const float bpm = 84f;
        float beatDur = 60f / bpm;
        float barDur = beatDur * 4f;
        const int barCount = 8;
        int totalSamples = Mathf.RoundToInt(barDur * barCount * sampleRate);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];
        float[] delayFeedL = new float[totalSamples];
        float[] delayFeedR = new float[totalSamples];

        int[][] padChords =
        {
            new[] { 53, 60, 64, 67 },
            new[] { 55, 62, 64, 71 },
            new[] { 52, 59, 62, 67 },
            new[] { 57, 60, 64, 71 },
            new[] { 53, 60, 64, 69 },
            new[] { 50, 57, 60, 64 },
            new[] { 48, 55, 59, 64 },
            new[] { 55, 60, 62, 69 }
        };

        int[] bassRoots = { 41, 43, 40, 45, 41, 38, 48, 43 };

        int[][] arpPatterns =
        {
            new[] { 69, 72, 76, 79, 76, 72, 69, -1 },
            new[] { 71, 74, 76, 79, 76, 74, 71, -1 },
            new[] { 67, 71, 74, 79, 76, 74, 71, -1 },
            new[] { 69, 72, 76, 79, 83, 79, 76, -1 },
            new[] { 69, 72, 76, 72, 77, 76, 72, -1 },
            new[] { 69, 72, 74, 76, 74, 72, 69, -1 },
            new[] { 67, 72, 76, 79, 76, 72, 71, -1 },
            new[] { 67, 72, 74, 79, 74, 71, 69, -1 }
        };

        float eighthDur = beatDur * 0.5f;
        for (int bar = 0; bar < barCount; bar++)
        {
            float barStartTime = bar * barDur;
            AddTutorialPadChord(dryL, dryR, sampleRate, totalSamples, barStartTime, barDur, padChords[bar]);

            AddTutorialBassNote(dryL, dryR, sampleRate, totalSamples, barStartTime, beatDur * 1.85f, bassRoots[bar]);
            AddTutorialBassNote(dryL, dryR, sampleRate, totalSamples, barStartTime + beatDur * 2f, beatDur * 1.85f, bassRoots[bar]);

            int[] arp = arpPatterns[bar];
            for (int step = 0; step < arp.Length; step++)
            {
                if (arp[step] < 0) continue;
                float pan = (step % 2 == 0) ? 0.36f : 0.64f;
                AddTutorialPluckNote(dryL, dryR, delayFeedL, delayFeedR, sampleRate, totalSamples,
                    barStartTime + step * eighthDur, arp[step], pan);
            }
        }

        return FinalizeStereoLoopClip("music_tutorial", dryL, dryR, delayFeedL, delayFeedR, sampleRate, totalSamples, beatDur, 0.85f);
    }

    private static AudioClip FinalizeStereoLoopClip(
        string clipName, float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float beatDur, float masterGain)
    {
        int tap1Offset = Mathf.RoundToInt(beatDur * 0.5f * sampleRate);
        int tap2Offset = Mathf.RoundToInt(beatDur * 1.0f * sampleRate);
        int tap3Offset = Mathf.RoundToInt(beatDur * 1.5f * sampleRate);

        float[] stereoData = new float[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            int t1 = (i - tap1Offset + totalSamples) % totalSamples;
            int t2 = (i - tap2Offset + totalSamples) % totalSamples;
            int t3 = (i - tap3Offset + totalSamples) % totalSamples;

            float wetL = feedR[t1] * 0.40f + feedL[t2] * 0.22f + feedR[t3] * 0.12f;
            float wetR = feedL[t1] * 0.40f + feedR[t2] * 0.22f + feedL[t3] * 0.12f;

            float outL = (dryL[i] + wetL) * masterGain;
            float outR = (dryR[i] + wetR) * masterGain;

            stereoData[i * 2] = Mathf.Clamp(outL / (1f + Mathf.Abs(outL) * 0.35f), -0.92f, 0.92f);
            stereoData[i * 2 + 1] = Mathf.Clamp(outR / (1f + Mathf.Abs(outR) * 0.35f), -0.92f, 0.92f);
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 2, sampleRate, false);
        clip.SetData(stereoData, 0);
        return clip;
    }

    // ==========================================
    // NUEVA MÚSICA DE MENÚ ("ASTRAL HORIZON" - NOIR CÓSMICO)
    // ==========================================

    private static AudioClip CreateMenuMusicClip()
    {
        const int sampleRate = 44100;
        const float bpm = 92f;
        float beatDur = 60f / bpm;
        float barDur = beatDur * 4f;
        const int barCount = 8;
        int totalSamples = Mathf.RoundToInt(barDur * barCount * sampleRate);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];
        float[] feedL = new float[totalSamples];
        float[] feedR = new float[totalSamples];

        // Progresión atmosférica oscura con extensiones 9/#11 y cambios mediantes cromáticos
        int[][] padChords =
        {
            new[] { 52, 59, 64, 66, 71 }, // Em9
            new[] { 48, 55, 60, 64, 66 }, // Cmaj7#11
            new[] { 45, 52, 57, 60, 64 }, // Am9
            new[] { 47, 54, 57, 60, 66 }, // B7b9/sus
            new[] { 52, 59, 63, 66, 71 }, // Em(maj9)
            new[] { 47, 55, 62, 66, 71 }, // Gmaj7/B
            new[] { 50, 57, 62, 66, 69 }, // D6/9
            new[] { 48, 55, 59, 62, 67 }  // Cmaj9 -> B tensión
        };

        int[] bassRoots = { 40, 36, 33, 35, 40, 35, 38, 36 };
        int[] bassAlt   = { 47, 43, 40, 42, 47, 42, 45, 35 };

        int[][] chimeMotifs =
        {
            new[] { 76, -1, 79, -1, 78, -1, 71, 74, -1, 76, -1, -1 },
            new[] { 74, -1, 78, -1, 79, -1, 76, -1, 71, -1, 72, -1 },
            new[] { 76, -1, 83, -1, 81, -1, 76, 79, -1, 72, -1, -1 },
            new[] { 74, -1, 76, -1, 78, -1, 75, -1, 71, -1, -1, -1 },
            new[] { 76, -1, 83, -1, 86, -1, 83, -1, 79, 78, -1, -1 },
            new[] { 79, -1, 83, -1, 81, -1, 78, -1, 74, -1, -1, -1 },
            new[] { 76, -1, 78, -1, 81, -1, 79, -1, 74, -1, 76, -1 },
            new[] { 79, -1, 78, -1, 75, -1, 76, -1, 71, -1, -1, -1 }
        };

        float stepDur = barDur / 12f; // Polirritmia ternaria 3-sobre-4 sobre el pulso binario
        for (int bar = 0; bar < barCount; bar++)
        {
            float barStart = bar * barDur;
            AddMenuAnalogPad(dryL, dryR, sampleRate, totalSamples, barStart, barDur, padChords[bar]);

            AddMenuSubBass(dryL, dryR, sampleRate, totalSamples, barStart, beatDur * 1.45f, bassRoots[bar]);
            AddMenuSubBass(dryL, dryR, sampleRate, totalSamples, barStart + beatDur * 1.5f, beatDur * 1.15f, bassRoots[bar] + 12);
            AddMenuSubBass(dryL, dryR, sampleRate, totalSamples, barStart + beatDur * 2.75f, beatDur * 1.15f, bassAlt[bar]);

            AddMusicKick(dryL, dryR, sampleRate, totalSamples, barStart, 95f, 40f, 0.24f, 0.14f);
            AddMusicKick(dryL, dryR, sampleRate, totalSamples, barStart + beatDur * 2.5f, 92f, 40f, 0.22f, 0.11f);
            AddMenuSoftAirBrush(feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 2f, 0.22f);

            int[] motif = chimeMotifs[bar];
            for (int s = 0; s < motif.Length; s++)
            {
                if (motif[s] < 0) continue;
                float pan = 0.28f + 0.44f * ((s % 3) * 0.5f);
                AddMenuCrystalChime(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + s * stepDur, motif[s], pan);
            }
        }

        return FinalizeStereoLoopClip("music_menu_v2", dryL, dryR, feedL, feedR, sampleRate, totalSamples, beatDur, 0.88f);
    }

    // ==========================================
    // NUEVA MÚSICA IN-GAME ("HYPERION VOID" - CYBER COMBAT)
    // ==========================================

    // ==========================================
    // NUEVA MÚSICA PRINCIPAL DEL JUEGO: ROCK SINFÓNICO / ACCIÓN ÉPICA ("VALKYRIE CHARGE")
    // (Batería rock doble bombo + taikos bélicos, guitarra eléctrica rítmica y solista,
    // cuerdas orquestales rápidas, trompas/metales heroicos y coro épico - 148 BPM)
    // ==========================================
    private static AudioClip CreateGameMusicClip()
    {
        return CreateEpicActionBattleTrackClip("music_game_epic_action_v4", 148f, false);
    }

    // ==========================================
    // MÚSICA FRENÉTICA DESDE OLEADA 3: ROCK ORQUESTAL OVERDRIVE ("SUPERNOVA CRUSADE" - 164 BPM)
    // ==========================================
    private static AudioClip CreateWave3FreneticMusicClip()
    {
        return CreateEpicActionBattleTrackClip("music_game_wave3_epic_overdrive_v4", 164f, true);
    }

    private static AudioClip CreateEpicActionBattleTrackClip(string clipName, float bpm, bool overdrive)
    {
        const int sampleRate = 44100;
        float beatDur = 60f / bpm;
        float barDur = beatDur * 4f;
        const int barCount = 16;
        int totalSamples = Mathf.RoundToInt(barDur * barCount * sampleRate);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];
        float[] feedL = new float[totalSamples];
        float[] feedR = new float[totalSamples];

        // Progresión heroica de acción épica en Mi Menor (Em - C - D - Bm | Em - C - Am - B7 | C - D - Em - G | C - Am - B7 - B7)
        // En Overdrive sube a Fa# Menor (+2 semitonos) para todavía más tensión y adrenalina
        int tr = overdrive ? 2 : 0;

        int[] bassRoots =
        {
            40 + tr, 36 + tr, 38 + tr, 35 + tr, // Sección A1: E, C, D, B
            40 + tr, 36 + tr, 33 + tr, 35 + tr, // Sección A2: E, C, A, B
            36 + tr, 38 + tr, 40 + tr, 43 + tr, // Sección B1 (Estribillo épico): C, D, E, G
            36 + tr, 33 + tr, 35 + tr, 35 + tr  // Sección B2 (Clímax ascendente): C, A, B, B
        };

        int[][] epicChords =
        {
            new[] { 52 + tr, 55 + tr, 59 + tr, 64 + tr }, // Em
            new[] { 48 + tr, 55 + tr, 60 + tr, 64 + tr }, // C
            new[] { 50 + tr, 57 + tr, 62 + tr, 66 + tr }, // D
            new[] { 47 + tr, 54 + tr, 59 + tr, 62 + tr }, // Bm
            new[] { 52 + tr, 55 + tr, 59 + tr, 64 + tr }, // Em
            new[] { 48 + tr, 55 + tr, 60 + tr, 64 + tr }, // C
            new[] { 45 + tr, 52 + tr, 57 + tr, 60 + tr }, // Am
            new[] { 47 + tr, 54 + tr, 57 + tr, 63 + tr }, // B7
            new[] { 48 + tr, 55 + tr, 60 + tr, 64 + tr }, // C
            new[] { 50 + tr, 57 + tr, 62 + tr, 66 + tr }, // D
            new[] { 52 + tr, 59 + tr, 64 + tr, 67 + tr }, // Em
            new[] { 55 + tr, 59 + tr, 62 + tr, 67 + tr }, // G
            new[] { 48 + tr, 55 + tr, 60 + tr, 64 + tr }, // C
            new[] { 45 + tr, 52 + tr, 57 + tr, 60 + tr }, // Am
            new[] { 47 + tr, 54 + tr, 59 + tr, 63 + tr }, // B
            new[] { 47 + tr, 54 + tr, 57 + tr, 63 + tr }  // B7
        };

        // Ostinato urgente de cuerdas orquestales rápidas en corcheas (impulso cinematográfico constante)
        int[][] fastStringOstinato =
        {
            new[] { 64, 71, 67, 71, 64, 71, 74, 71 },
            new[] { 60, 67, 64, 67, 60, 67, 72, 67 },
            new[] { 62, 69, 66, 69, 62, 69, 74, 69 },
            new[] { 59, 66, 62, 66, 59, 66, 71, 66 },
            new[] { 64, 71, 67, 71, 76, 71, 74, 71 },
            new[] { 60, 67, 64, 67, 72, 67, 71, 67 },
            new[] { 57, 64, 60, 64, 69, 64, 67, 64 },
            new[] { 59, 63, 66, 71, 75, 71, 66, 63 }
        };

        // Sección A (Compases 0-7): Fanfarria heroica de Metales / Trompas de batalla
        int[][] heroicBrassMelodyA =
        {
            new[] { 71, -1, 74, 76, -1, 74, 71, -1 },
            new[] { 72, -1, 74, 76, -1, 79, 76, -1 },
            new[] { 74, -1, 76, 78, -1, 76, 74, 71 },
            new[] { 71, -1, 66, 69, 71, -1, -1, -1 },
            new[] { 76, -1, 74, 71, -1, 74, 76, 79 },
            new[] { 79, -1, 76, 74, 72, -1, 76, -1 },
            new[] { 76, -1, 74, 72, 71, -1, 69, 71 },
            new[] { 75, -1, 71, 75, 78, -1, 75, -1 }
        };

        // Sección B (Compases 8-15): Estribillo épico a toda orquesta (Guitarra solista + Violines + Metales + Coro)
        int[][] epicChorusLeadB =
        {
            new[] { 79, -1, 76, 72, -1, 76, 79, 81 },
            new[] { 81, -1, 78, 74, -1, 78, 81, 83 },
            new[] { 83, -1, 81, 79, -1, 76, 79, 83 },
            new[] { 86, -1, 83, 79, -1, 83, 81, 79 },
            new[] { 79, 81, 84, -1, 83, 81, 79, 76 },
            new[] { 81, -1, 79, 76, 72, -1, 76, 79 },
            new[] { 78, -1, 81, 83, -1, 81, 78, 75 },
            new[] { 75, 78, 83, -1, 87, -1, 83, -1 }
        };

        float sixteenthDur = beatDur * 0.25f;
        float eighthDur = beatDur * 0.5f;

        for (int bar = 0; bar < barCount; bar++)
        {
            float barStart = bar * barDur;
            int root = bassRoots[bar];
            int[] chord = epicChords[bar];
            bool isChorus = bar >= 8;

            // 1. BATERÍA DE ACCIÓN ÉPICA: Doble bombo galopante + Taikos bélicos + Caja explosiva + Redobles
            float[] kickSteps = (overdrive || isChorus)
                ? new[] { 0f, 0.5f, 0.75f, 1.5f, 2.0f, 2.5f, 2.75f, 3.5f }
                : new[] { 0f, 0.75f, 1.5f, 2.0f, 2.75f, 3.25f };
            for (int k = 0; k < kickSteps.Length; k++)
            {
                bool accent = (k == 0 || kickSteps[k] == 2.0f);
                AddAcousticFunkKick(dryL, dryR, sampleRate, totalSamples,
                    barStart + kickSteps[k] * beatDur, accent ? 0.30f : 0.22f);
            }

            // Taikos cinematográficos en acentos clave para peso de batalla
            AddBossWarTaiko(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart, isChorus ? 0.24f : 0.18f, true);
            AddBossWarTaiko(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 2.5f, 0.17f, false);

            // Caja rock/militar potente en tiempos 2 y 4 + redoble épico cada 4 compases
            AddAcousticFunkSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 1.0f, 0.26f, false);
            AddAcousticFunkSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 3.0f, 0.28f, false);
            if (bar % 4 == 3)
            {
                for (int f = 0; f < 4; f++)
                {
                    AddBossWarTaiko(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + (3.25f + f * 0.1875f) * beatDur, 0.17f + f * 0.03f, false);
                    AddAcousticFunkSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + (3.25f + f * 0.1875f) * beatDur, 0.13f + f * 0.035f, f < 2);
                }
            }

            // Hi-hat / platillo agresivo en corcheas + crash cinematográfico cada 4 compases
            for (int h = 0; h < 8; h++)
            {
                bool openHat = isChorus && (h % 2 == 0);
                AddAcousticHiHat(dryL, dryR, sampleRate, totalSamples,
                    barStart + h * eighthDur,
                    openHat ? 0.12f : ((h % 2 == 0) ? 0.105f : 0.075f),
                    openHat, h % 2 == 0 ? 0.35f : 0.65f);
            }
            if (bar % 4 == 0)
            {
                AddBossAnvilCrash(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart, isChorus ? 0.15f : 0.11f);
            }

            // 2. BAJO ROCK ELÉCTRICO EN GALOPE (corcheas y semicorcheas imparables)
            int[] bassPattern = { 0, 2, 3, 4, 6, 8, 10, 11, 12, 14 };
            for (int b = 0; b < bassPattern.Length; b++)
            {
                int midi = (b == 8 || b == 9) ? root + 7 : ((b == 4) ? root + 12 : root);
                AddAcousticSlapBass(dryL, dryR, sampleRate, totalSamples,
                    barStart + bassPattern[b] * sixteenthDur, sixteenthDur * 1.4f, midi, 0.24f, b == 0 || b == 5);
            }

            // 3. GUITARRA ELÉCTRICA RÍTMICA ROCK CON CRUNCH (galope épico sin saturar la mezcla)
            int[] chugPattern = { 1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1 };
            for (int step = 0; step < 16; step++)
            {
                if (chugPattern[step] == 0) continue;
                bool openPower = (step == 0 || step == 8 || (isChorus && step == 12));
                int gNote = (step >= 14) ? root + 7 : root;
                float gDur = openPower ? sixteenthDur * 1.6f : sixteenthDur * 0.9f;
                AddBossOverdriveGuitar(dryL, dryR, sampleRate, totalSamples,
                    barStart + step * sixteenthDur, gDur, gNote,
                    openPower ? (isChorus ? 0.115f : 0.095f) : 0.072f, openPower);
            }

            // 4. CUERDAS ORQUESTALES SPICCATO URGENTES (motor cinematográfico rápido en corcheas)
            int[] ostinato = fastStringOstinato[bar % 8];
            for (int s = 0; s < ostinato.Length; s++)
            {
                float pan = (s % 2 == 0) ? 0.24f : 0.76f;
                AddBossStringSpiccato(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                    barStart + s * eighthDur, eighthDur * 0.88f, ostinato[s] + tr, pan, isChorus ? 0.085f : 0.095f);
            }

            // 5. METALES ORQUESTALES DE FONDO + CORO ÉPICO EN EL ESTRIBILLO (Sección B)
            if (isChorus)
            {
                // Acordes heroicos de coro épico ("Aah") + potencia armónica
                AddBossFormantChoir(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                    barStart, barDur, new[] { chord[0] + 12, chord[2] + 12 }, true, 0.068f);

                // Golpes de metales graves marcando cada cambio de acorde
                AddBrassHornNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart, beatDur * 1.6f, chord[0], 0.30f, 0.085f);
                AddBrassHornNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart, beatDur * 1.6f, chord[2], 0.70f, 0.085f);
            }

            // 6. MELODÍA HEROICA PRINCIPAL:
            // Sección A = Fanfarria épica de Trompas/Metales + Violín | Sección B = Guitarra Solista Heroica + Violines + Metales
            if (!isChorus)
            {
                int[] melody = heroicBrassMelodyA[bar];
                for (int s = 0; s < melody.Length; s++)
                {
                    if (melody[s] < 0) continue;
                    bool hold = (s + 1 < melody.Length && melody[s + 1] < 0);
                    float dur = hold ? (eighthDur * 1.9f) : (eighthDur * 0.92f);
                    float noteT = barStart + s * eighthDur;
                    int noteMidi = melody[s] + tr;

                    AddBrassHornNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, noteT, dur, noteMidi, 0.62f, 0.125f);
                    AddExpressiveViolinNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, noteT, dur, noteMidi, 0.38f, 0.078f);
                }
            }
            else
            {
                int[] chorus = epicChorusLeadB[bar - 8];
                for (int s = 0; s < chorus.Length; s++)
                {
                    if (chorus[s] < 0) continue;
                    bool hold = (s + 1 < chorus.Length && chorus[s + 1] < 0);
                    float dur = hold ? (eighthDur * 1.95f) : (eighthDur * 0.96f);
                    float noteT = barStart + s * eighthDur;
                    int noteMidi = chorus[s] + tr;

                    // Dúo épico de Guitarra Solista + Violines en octava + Metales
                    AddBossSpaceHeroLead(dryL, dryR, feedL, feedR, sampleRate, totalSamples, noteT, dur, noteMidi, 0.095f);
                    AddExpressiveViolinNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, noteT, dur, noteMidi, 0.30f, 0.105f);
                    AddBrassHornNote(dryL, dryR, feedL, feedR, sampleRate, totalSamples, noteT, dur, noteMidi - 12, 0.72f, 0.090f);
                }
            }
        }

        return FinalizeStereoLoopClip(clipName, dryL, dryR, feedL, feedR, sampleRate, totalSamples, beatDur, 0.86f);
    }

    private static void AddAcousticFunkKick(float[] dryL, float[] dryR, int sampleRate, int totalSamples, float startTime, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(0.24f * sampleRate);
        float phase = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 14.5f);
            float freq = 48f + 98f * Mathf.Exp(-t * 32f);
            phase += twoPi * freq / sampleRate;
            float beater = DeterministicNoise(startSample + i) * Mathf.Exp(-t * 140f) * 0.22f;
            float s = (Mathf.Sin(phase) * 0.88f + Mathf.Sin(phase * 2f) * 0.16f + beater) * env * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s;
            dryR[idx] += s;
        }
    }

    private static void AddAcousticFunkSnare(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float gain, bool ghost)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        float dur = ghost ? 0.09f : 0.18f;
        int count = Mathf.RoundToInt(dur * sampleRate);
        float phase = 0f;
        float lpNoise = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float bodyEnv = Mathf.Exp(-t * (ghost ? 34f : 22f));
            float wireEnv = Mathf.Exp(-t * (ghost ? 28f : 15f));
            float freq = 195f + 75f * Mathf.Exp(-t * 48f);
            phase += twoPi * freq / sampleRate;

            float n = DeterministicNoise(startSample * 7 + i * 3);
            lpNoise = Mathf.Lerp(lpNoise, n, 0.42f);
            float s = (Mathf.Sin(phase) * 0.48f * bodyEnv + (n - lpNoise * 0.35f) * 0.56f * wireEnv) * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s * 0.96f;
            dryR[idx] += s * 1.04f;
            if (!ghost)
            {
                feedL[idx] += s * 0.24f;
                feedR[idx] += s * 0.24f;
            }
        }
    }

    private static void AddAcousticHiHat(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples,
        float startTime, float gain, bool openHat, float pan)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        float dur = openHat ? 0.26f : 0.065f;
        int count = Mathf.RoundToInt(dur * sampleRate);
        float prev = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-t * (openHat ? 11.5f : 48f));
            float n = DeterministicNoise(startSample * 13 + i * 5);
            float hp = (n - prev) * 0.65f;
            prev = n;
            float s = hp * env * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s * (1f - pan);
            dryR[idx] += s * pan;
        }
    }

    private static void AddAcousticSlapBass(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples,
        float startTime, float duration, int midi, float gain, bool slapPop)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float freq = MidiToHz(midi);
        float phase = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.Min(1f, t / 0.005f);
            float release = Mathf.Clamp01((duration - t) / 0.035f);
            float env = attack * release * Mathf.Exp(-t * (slapPop ? 5.8f : 3.2f));
            float bright = Mathf.Exp(-t * (slapPop ? 18f : 12f));

            phase += twoPi * freq / sampleRate;
            float body = Mathf.Sin(phase) * 0.78f
                       + Mathf.Sin(phase * 2f) * (0.28f + bright * 0.24f)
                       + Mathf.Sin(phase * 3f) * (0.10f + bright * (slapPop ? 0.28f : 0.14f))
                       + Mathf.Sin(phase * 4f) * (bright * (slapPop ? 0.18f : 0.06f));

            float thumbClick = slapPop ? (DeterministicNoise(startSample + i) * Mathf.Exp(-t * 160f) * 0.24f) : 0f;
            float s = (body + thumbClick) * env * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s;
            dryR[idx] += s;
        }
    }

    private static void AddRhodesElectricPianoChord(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int[] chord, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;

        for (int n = 0; n < chord.Length; n++)
        {
            float freq = MidiToHz(chord[n]);
            float phase = 0f;
            float pan = Mathf.Lerp(0.30f, 0.70f, chord.Length > 1 ? (float)n / (chord.Length - 1) : 0.5f);
            int noteOffset = n * 240; // Ligero arpegiado natural de piano (~5ms por dedo)

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Min(1f, t / 0.008f);
                float release = Mathf.Clamp01((duration - t) / 0.07f);
                float env = attack * release * Mathf.Exp(-t * 2.1f);
                float tineEnv = Mathf.Exp(-t * 22f);

                phase += twoPi * freq / sampleRate;
                float tremolo = 0.88f + 0.12f * Mathf.Sin(twoPi * 4.5f * t + n);
                // Tono cálido de piano Rhodes: fundamental pura + 2º armónico suave + golpe metálico suave de tine
                float tone = (Mathf.Sin(phase) * 0.78f
                            + Mathf.Sin(phase * 2f) * 0.16f
                            + Mathf.Sin(phase * 7f) * 0.12f * tineEnv) * env * tremolo * gain;

                int idx = (startSample + noteOffset + i) % totalSamples;
                dryL[idx] += tone * (1f - pan);
                dryR[idx] += tone * pan;
                feedL[idx] += tone * (1f - pan) * 0.22f;
                feedR[idx] += tone * pan * 0.22f;
            }
        }
    }

    private static void AddPluckedAcousticGuitarNote(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midi, float pan, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float freq = MidiToHz(midi);
        float phase = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.Min(1f, t / 0.0035f);
            float release = Mathf.Clamp01((duration - t) / 0.03f);
            float env = attack * release * Mathf.Exp(-t * 4.8f);
            float harmDecay = Mathf.Exp(-t * 14f);

            phase += twoPi * freq / sampleRate;
            // Timbre cálido de cuerda pulsada (acústica/nylon-funk)
            float wave = Mathf.Sin(phase) * 0.62f
                       + Mathf.Sin(phase * 2f) * (0.30f + harmDecay * 0.18f)
                       + Mathf.Sin(phase * 3f) * (0.14f + harmDecay * 0.16f)
                       + Mathf.Sin(phase * 4f) * (0.06f + harmDecay * 0.12f)
                       + Mathf.Sin(phase * 5f) * (harmDecay * 0.08f);
            float pick = DeterministicNoise(startSample + i * 2) * Mathf.Exp(-t * 210f) * 0.18f;
            float s = (wave + pick) * env * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s * (1f - pan);
            dryR[idx] += s * pan;
            feedL[idx] += s * (1f - pan) * 0.18f;
            feedR[idx] += s * pan * 0.18f;
        }
    }

    private static void AddBrassHornNote(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midi, float pan, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float baseFreq = MidiToHz(midi);
        float phase1 = 0f;
        float phase2 = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.Min(1f, t / 0.026f);
            float release = Mathf.Clamp01((duration - t) / 0.045f);
            float env = attack * release;
            // Apertura progresiva de armónicos típica de trompeta/saxo ("brass bloom")
            float bloom = Mathf.Clamp01(t / 0.065f) * Mathf.Exp(-t * 1.4f);
            float vib = 1f + Mathf.Sin(twoPi * 5.6f * t) * 0.0035f * Mathf.Clamp01(t / 0.12f);

            phase1 += twoPi * (baseFreq * vib) / sampleRate;
            phase2 += twoPi * (baseFreq * 1.0035f * vib) / sampleRate;

            float horn1 = Mathf.Sin(phase1) * 0.56f
                        + Mathf.Sin(phase1 * 2f) * (0.28f + bloom * 0.16f)
                        + Mathf.Sin(phase1 * 3f) * (0.16f + bloom * 0.18f)
                        + Mathf.Sin(phase1 * 4f) * (0.06f + bloom * 0.12f)
                        + Mathf.Sin(phase1 * 5f) * (bloom * 0.08f);
            float horn2 = Mathf.Sin(phase2) * 0.45f + Mathf.Sin(phase2 * 2f) * 0.22f + Mathf.Sin(phase2 * 3f) * 0.12f;

            float s = (horn1 * 0.65f + horn2 * 0.35f) * env * gain;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s * (1f - pan);
            dryR[idx] += s * pan;
            feedL[idx] += s * (1f - pan) * 0.25f;
            feedR[idx] += s * pan * 0.25f;
        }
    }

    private static void AddExpressiveViolinNote(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midi, float pan, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float baseFreq = MidiToHz(midi);
        float phase = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.Min(1f, t / 0.045f);
            float release = Mathf.Clamp01((duration - t) / 0.055f);
            float env = attack * release;
            float vibDepth = 0.0055f * Mathf.Clamp01((t - 0.04f) / 0.10f);
            float vib = 1f + Mathf.Sin(twoPi * 6.0f * t) * vibDepth;

            phase += twoPi * (baseFreq * vib) / sampleRate;
            // Timbre cálido de violín/cuerda frotada con armónicos naturales balanceados
            float bowed = Mathf.Sin(phase) * 0.54f
                        + Mathf.Sin(phase * 2f) * 0.28f
                        + Mathf.Sin(phase * 3f) * 0.17f
                        + Mathf.Sin(phase * 4f) * 0.09f
                        + Mathf.Sin(phase * 5f) * 0.05f;
            float s = bowed * env * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += s * (1f - pan);
            dryR[idx] += s * pan;
            feedL[idx] += s * (1f - pan) * 0.30f;
            feedR[idx] += s * pan * 0.30f;
        }
    }

    // ==========================================
    // MÚSICA ÚNICA DE JEFE FINAL ("LEVIATHAN CATACLYSM" - ORQUESTAL / METAL CÓSMICO / CORO / ÓRGANO)
    // ==========================================
    private static AudioClip CreateBossMusicClip()
    {
        const int sampleRate = 44100;
        const float bpm = 132f;
        float beatDur = 60f / bpm;
        float barDur = beatDur * 4f;
        const int barCount = 16;
        int totalSamples = Mathf.RoundToInt(barDur * barCount * sampleRate);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];
        float[] feedL = new float[totalSamples];
        float[] feedR = new float[totalSamples];

        // C Menor / Frigio / Armónica (100% distinta al synth-acid de las oleadas):
        // Cm -> Cm -> Db -> G7/B | Cm -> Ab -> Fm -> G7 | Cm -> Eb -> Ab -> G | Fm -> Cm -> F#dim -> G7
        int[] rootNotes =
        {
            36, 36, 37, 35, // Bars 0-3: C2, C2, Db2, B1 (tensión frigia letal)
            36, 32, 41, 43, // Bars 4-7: C2, Ab1, F2, G2
            36, 39, 32, 43, // Bars 8-11: C2, Eb2, Ab1, G2 (clímax trágico-heroico)
            41, 36, 42, 43  // Bars 12-15: F2, C2, F#2, G2
        };

        int[][] organChords =
        {
            new[] { 48, 55, 60, 63 },       // Cm
            new[] { 48, 55, 60, 62 },       // Cm(add9)
            new[] { 49, 56, 61, 65 },       // Db major oscuro
            new[] { 47, 55, 59, 62 },       // G/B
            new[] { 48, 55, 60, 63 },       // Cm
            new[] { 44, 51, 56, 60 },       // Ab
            new[] { 41, 48, 53, 56 },       // Fm
            new[] { 43, 50, 55, 59 },       // G7
            new[] { 48, 55, 60, 63 },       // Cm
            new[] { 51, 58, 63, 67 },       // Eb
            new[] { 44, 51, 56, 63 },       // Abmaj7
            new[] { 43, 50, 55, 59 },       // G
            new[] { 41, 48, 53, 60 },       // Fm7
            new[] { 48, 55, 60, 63 },       // Cm
            new[] { 42, 48, 54, 60 },       // F#dim7 apocalíptico
            new[] { 43, 50, 56, 59 }        // G7b9
        };

        // Ostinato de cuerdas spiccato en Sección A (corcheas tensas)
        int[][] spiccatoStrings =
        {
            new[] { 60, 67, 68, 67, 63, 67, 72, 67 },
            new[] { 60, 67, 68, 67, 62, 67, 71, 67 },
            new[] { 61, 68, 69, 68, 65, 68, 73, 68 },
            new[] { 59, 67, 68, 67, 62, 67, 74, 71 },
            new[] { 60, 67, 72, 67, 63, 67, 75, 67 },
            new[] { 56, 63, 68, 63, 60, 63, 68, 63 },
            new[] { 65, 72, 75, 72, 68, 72, 77, 72 },
            new[] { 67, 71, 74, 77, 79, 77, 74, 71 }
        };

        // Melodía épica final en Sección B (guitarra solista + metal espacial, compases 8..15)
        int[][] sectionBHeroLead =
        {
            new[] { 72, -1, 75, 79, -1, 77, 75, 74 },
            new[] { 75, -1, 79, 82, -1, 79, 75, 74 },
            new[] { 72, -1, 75, 80, -1, 79, 77, 75 },
            new[] { 74, -1, 77, 79, -1, 74, 71, -1 },
            new[] { 77, -1, 80, 84, -1, 83, 80, 77 },
            new[] { 79, -1, 75, 84, -1, 82, 79, 75 },
            new[] { 78, -1, 81, 84, -1, 81, 78, 75 },
            new[] { 79, -1, 83, 86, 88, -1, 83, -1 }
        };

        float sixteenthDur = beatDur * 0.25f;
        float eighthDur = beatDur * 0.5f;

        for (int bar = 0; bar < barCount; bar++)
        {
            float barStart = bar * barDur;
            int root = rootNotes[bar];
            int[] chord = organChords[bar];

            // 1. Percusión bélica orquestal + doble bombo galopante (sin patrón disco 4x4)
            float[] taikoBeats = { 0f, 0.75f, 1.0f, 2.0f, 2.5f, 2.75f, 3.0f, 3.5f };
            for (int k = 0; k < taikoBeats.Length; k++)
            {
                bool heavy = (k == 0 || k == 3 || k == 6);
                AddBossWarTaiko(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                    barStart + taikoBeats[k] * beatDur, heavy ? 0.28f : 0.19f, heavy);
            }

            // Caja metálica militar pesada en tiempos 2 y 4 + redoble cinematográfico cada 4 compases
            AddBossMetalSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 1f, 0.20f);
            AddBossMetalSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 3f, 0.22f);
            if (bar % 4 == 3)
            {
                for (int f = 0; f < 4; f++)
                {
                    AddBossWarTaiko(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + (3.25f + f * 0.1875f) * beatDur, 0.16f + f * 0.03f, false);
                }
            }

            // Yunque / gong espacial metálico al inicio de cada frase de 4 compases
            if (bar % 4 == 0)
            {
                AddBossAnvilCrash(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart, bar == 8 ? 0.16f : 0.13f);
            }

            // 2. Órgano de catedral + Metales graves (Cathedral Pipe Organ + Dark Low Brass)
            AddBossCathedralOrganBrass(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                barStart, barDur, chord, bar >= 8 ? 0.088f : 0.072f);

            // 3. Coro abisal con formantes vocales ("Ooh / Aah") flotando en estéreo
            bool openVowel = (bar >= 8) || (bar % 4 >= 2);
            AddBossFormantChoir(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                barStart, barDur, new[] { chord[0] + 12, chord[2] + 12 }, openVowel, bar >= 8 ? 0.074f : 0.058f);

            // 4. Guitarra eléctrica pesada con distorsión en galope metal-espacial
            int[] gallopPattern = { 1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 1, 0 };
            for (int step = 0; step < 16; step++)
            {
                if (gallopPattern[step] == 0) continue;
                bool openChug = (step == 0 || step == 8 || step == 12);
                int gNote = (step >= 14) ? root + 7 : root;
                float gDur = openChug ? sixteenthDur * 1.45f : sixteenthDur * 0.85f;
                AddBossOverdriveGuitar(dryL, dryR, sampleRate, totalSamples,
                    barStart + step * sixteenthDur, gDur, gNote, openChug ? 0.13f : 0.095f, openChug);
            }

            // 5. Sección A: Cuerdas spiccato urgentes | Sección B: Solo épico guitarra + theremin espacial
            if (bar < 8)
            {
                int[] staccato = spiccatoStrings[bar];
                for (int s = 0; s < staccato.Length; s++)
                {
                    float pan = (s % 2 == 0) ? 0.28f : 0.72f;
                    AddBossStringSpiccato(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + s * eighthDur, eighthDur * 0.92f, staccato[s], pan, 0.085f);
                }
            }
            else
            {
                int[] lead = sectionBHeroLead[bar - 8];
                for (int s = 0; s < lead.Length; s++)
                {
                    if (lead[s] < 0) continue;
                    bool holdLong = (s + 1 < lead.Length && lead[s + 1] < 0);
                    float noteDur = holdLong ? (eighthDur * 1.92f) : (eighthDur * 0.95f);
                    AddBossSpaceHeroLead(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + s * eighthDur, noteDur, lead[s], 0.11f);
                }
            }
        }

        return FinalizeStereoLoopClip("music_boss_cataclysm", dryL, dryR, feedL, feedR, sampleRate, totalSamples, beatDur, 0.88f);
    }

    private static void AddBossWarTaiko(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float gain, bool heavy)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        float duration = heavy ? 0.42f : 0.28f;
        int count = Mathf.RoundToInt(duration * sampleRate);
        float phase = 0f;
        float overPhase = 0f;
        const float twoPi = Mathf.PI * 2f;
        float fStart = heavy ? 102f : 124f;
        float fEnd = heavy ? 34f : 44f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t * (heavy ? 6.8f : 10.5f));
            float freq = fEnd + (fStart - fEnd) * Mathf.Exp(-t * 22f);
            phase += twoPi * freq / sampleRate;
            overPhase += twoPi * (freq * 1.62f) / sampleRate;

            float skinThump = Mathf.Exp(-t * 85f) * DeterministicNoise(startSample + i) * 0.25f;
            float body = Mathf.Sin(phase) * 0.85f + Mathf.Sin(overPhase) * 0.22f * Mathf.Exp(-t * 16f);
            float raw = (body + skinThump) * env;
            float val = (raw / (1f + Mathf.Abs(raw) * 0.45f)) * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val;
            dryR[idx] += val;
            feedL[idx] += val * 0.16f;
            feedR[idx] += val * 0.16f;
        }
    }

    private static void AddBossMetalSnare(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(0.24f * sampleRate);
        const float twoPi = Mathf.PI * 2f;
        float phaseA = 0f, phaseB = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float f = 175f + 90f * Mathf.Exp(-t * 45f);
            phaseA += twoPi * f / sampleRate;
            phaseB += twoPi * (f * 1.53f) / sampleRate;

            float shell = (Mathf.Sin(phaseA) * 0.45f + Mathf.Sin(phaseB) * 0.25f) * Mathf.Exp(-t * 20f);
            float wires = DeterministicNoise(startSample + i * 9) * Mathf.Exp(-t * 13f) * 0.72f;
            float val = (shell + wires) * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val;
            dryR[idx] += val;
            feedL[idx] += val * 0.34f;
            feedR[idx] += val * 0.34f;
        }
    }

    private static void AddBossAnvilCrash(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        const float duration = 1.65f;
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;

        float[] metalRatios = { 1.0f, 1.41f, 1.73f, 2.23f, 3.17f };
        float lp = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float bellEnv = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 2.2f);
            float washEnv = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 2.8f);

            float metallic = 0f;
            for (int r = 0; r < metalRatios.Length; r++)
            {
                metallic += Mathf.Sin(twoPi * (196f * metalRatios[r]) * t) * (1f / (r + 1f));
            }
            float noise = DeterministicNoise(startSample + i * 5);
            lp += (noise - lp) * 0.32f;
            float cymbal = (noise - lp) * 0.75f * washEnv;

            float valL = (metallic * 0.25f * bellEnv + cymbal) * gain * 0.94f;
            float valR = (metallic * 0.25f * bellEnv + cymbal * 0.95f) * gain * 1.06f;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += valL;
            dryR[idx] += valR;
            feedL[idx] += valL * 0.45f;
            feedR[idx] += valR * 0.45f;
        }
    }

    private static void AddBossOverdriveGuitar(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples,
        float startTime, float duration, int midiNote, float gain, bool openChug)
    {
        float freq = MidiToHz(midiNote);
        float fifthFreq = freq * 1.4983f;
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float lpL = 0f, lpR = 0f;
        float cabAlpha = Mathf.Clamp(2f * Mathf.PI * (openChug ? 2350f : 1450f) / sampleRate, 0.04f, 0.48f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float pick = Mathf.Min(1f, t / 0.004f);
            float decay = openChug ? Mathf.Exp(-t * 5.2f) : Mathf.Exp(-t * 16.5f);
            float tail = Mathf.Clamp01((duration - t) / 0.014f);
            float env = pick * decay * tail;

            // Cuerda fundamental + 5ta (power chord) + decaimiento armónico metálico
            float s1 = (((t * freq * 0.997f) % 1f) * 2f - 1f) + 0.65f * (((t * fifthFreq * 0.997f) % 1f) * 2f - 1f);
            float s2 = (((t * freq * 1.003f) % 1f) * 2f - 1f) + 0.65f * (((t * fifthFreq * 1.003f) % 1f) * 2f - 1f);
            float click = DeterministicNoise(startSample + i * 13) * Mathf.Exp(-t * 140f) * 0.35f;

            // Saturación tipo tubo + filtrado de gabinete de guitarra
            float driveL = (s1 + click) * (openChug ? 3.2f : 2.6f);
            float driveR = (s2 - click) * (openChug ? 3.2f : 2.6f);
            driveL = driveL / (1f + Mathf.Abs(driveL));
            driveR = driveR / (1f + Mathf.Abs(driveR));

            lpL += (driveL - lpL) * cabAlpha;
            lpR += (driveR - lpR) * cabAlpha;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += lpL * env * gain;
            dryR[idx] += lpR * env * gain;
        }
    }

    private static void AddBossCathedralOrganBrass(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int[] midiNotes, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float[] freqs = new float[midiNotes.Length];
        for (int n = 0; n < midiNotes.Length; n++) freqs[n] = MidiToHz(midiNotes[n]);

        const float twoPi = Mathf.PI * 2f;
        float lpL = 0f, lpR = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / 0.09f);
            float envOut = Mathf.Clamp01((duration - t) / 0.10f);
            float env = envIn * envOut;

            // Golpe de brillo tipo metal/trompeta al atacar seguido de órgano profundo sostenido
            float brassBite = 0.25f + 0.75f * Mathf.Exp(-t * 3.8f);
            float sumL = 0f, sumR = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float fL = freqs[n] * 0.9975f;
                float fR = freqs[n] * 1.0025f;
                float pL = twoPi * fL * t;
                float pR = twoPi * fR * t;

                sumL += Mathf.Sin(pL) + 0.55f * Mathf.Sin(2f * pL) + 0.35f * Mathf.Sin(3f * pL) * brassBite + 0.20f * Mathf.Sin(4f * pL) * brassBite;
                sumR += Mathf.Sin(pR) + 0.55f * Mathf.Sin(2f * pR) + 0.35f * Mathf.Sin(3f * pR) * brassBite + 0.20f * Mathf.Sin(4f * pR) * brassBite;
            }

            lpL += (sumL - lpL) * 0.24f;
            lpR += (sumR - lpR) * 0.24f;

            float scale = env * gain * 0.38f;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += lpL * scale;
            dryR[idx] += lpR * scale;
            feedL[idx] += lpL * scale * 0.55f;
            feedR[idx] += lpR * scale * 0.55f;
        }
    }

    private static void AddBossFormantChoir(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int[] midiNotes, bool openAah, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float[] freqs = new float[midiNotes.Length];
        for (int n = 0; n < midiNotes.Length; n++) freqs[n] = MidiToHz(midiNotes[n]);

        // Dos resonadores de formante vocal ("Aah" vs "Ooh")
        float f1 = openAah ? 680f : 320f;
        float f2 = openAah ? 1180f : 860f;
        const float twoPi = Mathf.PI * 2f;

        float bp1StateL = 0f, bp1PrevL = 0f, bp2StateL = 0f, bp2PrevL = 0f;
        float bp1StateR = 0f, bp1PrevR = 0f, bp2StateR = 0f, bp2PrevR = 0f;
        float k1 = Mathf.Clamp(2f * Mathf.PI * f1 / sampleRate, 0.01f, 0.45f);
        float k2 = Mathf.Clamp(2f * Mathf.PI * f2 / sampleRate, 0.01f, 0.45f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(t / 0.28f) * Mathf.Clamp01((duration - t) / 0.24f);
            float vib = 1f + 0.0045f * Mathf.Sin(twoPi * 5.1f * t);

            float glottalL = 0f, glottalR = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float cL = ((t * freqs[n] * vib * 0.996f) % 1f) * 2f - 1f;
                float cR = ((t * freqs[n] * vib * 1.004f) % 1f) * 2f - 1f;
                glottalL += cL;
                glottalR += cR;
            }

            // Filtrado paso-banda simple por formantes vocales
            float hpL = glottalL - bp1PrevL * 0.96f; bp1PrevL = glottalL;
            bp1StateL += (hpL - bp1StateL) * k1;
            float hp2L = glottalL - bp2PrevL * 0.92f; bp2PrevL = glottalL;
            bp2StateL += (hp2L - bp2StateL) * k2;

            float hpR = glottalR - bp1PrevR * 0.96f; bp1PrevR = glottalR;
            bp1StateR += (hpR - bp1StateR) * k1;
            float hp2R = glottalR - bp2PrevR * 0.92f; bp2PrevR = glottalR;
            bp2StateR += (hp2R - bp2StateR) * k2;

            float vL = (bp1StateL * 0.72f + bp2StateL * 0.48f) * env * gain;
            float vR = (bp1StateR * 0.72f + bp2StateR * 0.48f) * env * gain;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += vL * 0.65f;
            dryR[idx] += vR * 0.65f;
            feedL[idx] += vL * 0.75f;
            feedR[idx] += vR * 0.75f;
        }
    }

    private static void AddBossStringSpiccato(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midiNote, float pan, float gain)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float gL = Mathf.Cos(pan * Mathf.PI * 0.5f);
        float gR = Mathf.Sin(pan * Mathf.PI * 0.5f);
        float lp = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float bowAttack = Mathf.Clamp01(t / 0.016f);
            float release = Mathf.Clamp01((duration - t) / 0.03f);
            float env = bowAttack * release * Mathf.Exp(-t * 5.8f);

            float ensemble = (((t * freq * 0.996f) % 1f) * 2f - 1f) * 0.5f
                           + (((t * freq * 1.004f) % 1f) * 2f - 1f) * 0.5f;
            float rosin = DeterministicNoise(startSample + i * 3) * Mathf.Exp(-t * 42f) * 0.22f;
            lp += ((ensemble + rosin) - lp) * 0.26f;

            float val = lp * env * gain;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val * gL;
            dryR[idx] += val * gR;
            feedL[idx] += val * 0.48f * gL;
            feedR[idx] += val * 0.48f * gR;
        }
    }

    private static void AddBossSpaceHeroLead(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midiNote, float gain)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;
        float lpL = 0f, lpR = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(t / 0.018f) * Mathf.Clamp01((duration - t) / 0.055f);
            float vib = 1f + 0.005f * Mathf.Sin(twoPi * 5.8f * t) * Mathf.Clamp01(t / 0.10f);
            float f = freq * vib;

            // Guitarra solista saturada + sobretono espacial cristalino a la octava
            float rawL = (((t * f * 0.997f) % 1f) * 2f - 1f) * 1.6f;
            float rawR = (((t * f * 1.003f) % 1f) * 2f - 1f) * 1.6f;
            rawL = rawL / (1f + Mathf.Abs(rawL));
            rawR = rawR / (1f + Mathf.Abs(rawR));

            float spaceOvertone = Mathf.Sin(twoPi * (f * 2f) * t) * 0.24f * Mathf.Min(1f, t / 0.05f);
            lpL += ((rawL + spaceOvertone) - lpL) * 0.30f;
            lpR += ((rawR + spaceOvertone) - lpR) * 0.30f;

            float vL = lpL * env * gain;
            float vR = lpR * env * gain;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += vL * 0.85f;
            dryR[idx] += vR * 0.85f;
            feedL[idx] += vL * 0.65f;
            feedR[idx] += vR * 0.65f;
        }
    }

    private static AudioClip CreateDynamicCombatTrackClip(
        string clipName,
        float bpm,
        int[] bassProgression,
        int[][] powerChords,
        int[][] sectionAArps,
        int[][] sectionBLead,
        bool freneticDrums,
        float leadMixBoost)
    {
        const int sampleRate = 44100;
        float beatDur = 60f / bpm;
        float barDur = beatDur * 4f;
        const int barCount = 16;
        int totalSamples = Mathf.RoundToInt(barDur * barCount * sampleRate);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];
        float[] feedL = new float[totalSamples];
        float[] feedR = new float[totalSamples];

        float sixteenthDur = beatDur * 0.25f;
        float eighthDur = beatDur * 0.5f;

        for (int bar = 0; bar < barCount; bar++)
        {
            float barStart = bar * barDur;
            int root = bassProgression[bar];

            // Batería electrónica de combate (doble bombo y fills extra cuando freneticDrums = true)
            for (int beat = 0; beat < 4; beat++)
            {
                float bTime = barStart + beat * beatDur;
                AddMusicKick(dryL, dryR, sampleRate, totalSamples, bTime, freneticDrums ? 155f : 142f, freneticDrums ? 41f : 46f, 0.26f, freneticDrums ? 0.24f : 0.22f);
                if (beat == 1 || beat == 3)
                {
                    AddCombatSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, bTime, freneticDrums ? 0.18f : 0.16f);
                }
                if (freneticDrums && (beat == 2 || (beat == 3 && bar % 2 == 1)))
                {
                    AddMusicKick(dryL, dryR, sampleRate, totalSamples, bTime + beatDur * 0.5f, 148f, 44f, 0.20f, 0.18f);
                }
            }
            if (!freneticDrums && bar % 4 == 3)
            {
                AddMusicKick(dryL, dryR, sampleRate, totalSamples, barStart + beatDur * 3.5f, 136f, 48f, 0.19f, 0.17f);
            }
            if (bar == 7 || bar == 15 || (freneticDrums && bar % 4 == 3))
            {
                AddCombatSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 3.5f, 0.13f);
                AddCombatSnare(dryL, dryR, feedL, feedR, sampleRate, totalSamples, barStart + beatDur * 3.75f, 0.12f);
            }

            // Hi-hats en semicorcheas
            for (int step = 0; step < 16; step++)
            {
                if (!freneticDrums && step % 4 == 0 && step != 8) continue;
                bool accent = (step % 4 == 2) || (freneticDrums && step % 2 == 1 && step >= 12);
                AddCombatHiHat(dryL, dryR, sampleRate, totalSamples, barStart + step * sixteenthDur, accent);
            }

            // Bajo ácido rodante de 16 semicorcheas con salto de octava/quinta
            for (int step = 0; step < 16; step++)
            {
                int note = (step % 4 == 0 || step % 4 == 3) ? root : (step % 4 == 2 ? root + 12 : root + 7);
                if (bar >= 8 && step == 15) note = root + 12;
                float cutoff = (step % 4 == 2) ? (freneticDrums ? 3100f : 2600f) : (freneticDrums ? 1950f : 1550f);
                AddCombatAcidBassStep(dryL, dryR, sampleRate, totalSamples, barStart + step * sixteenthDur, sixteenthDur * 0.94f, note, cutoff);
            }

            // Acordes estéreo con efecto sidechain
            AddCombatPowerStabPad(dryL, dryR, sampleRate, totalSamples, barStart, barDur, beatDur, powerChords[bar], (bar >= 8 ? 0.056f : 0.042f) * (freneticDrums ? 1.1f : 1f));

            if (bar < 8)
            {
                int[] arp = sectionAArps[bar];
                for (int s = 0; s < arp.Length; s++)
                {
                    float pan = (s % 2 == 0) ? 0.34f : 0.66f;
                    AddCombatSynthPluck(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + s * eighthDur, arp[s], pan, eighthDur * 1.15f, 0.082f * leadMixBoost);
                }
            }
            else
            {
                int[] lead = sectionBLead[bar - 8];
                for (int s = 0; s < lead.Length; s++)
                {
                    if (lead[s] < 0) continue;
                    bool holdLong = (s + 1 < lead.Length && lead[s + 1] < 0);
                    float noteDur = holdLong ? (eighthDur * 1.85f) : (eighthDur * 0.92f);
                    AddCombatLeadTone(dryL, dryR, feedL, feedR, sampleRate, totalSamples,
                        barStart + s * eighthDur, noteDur, lead[s]);
                }
            }
        }

        return FinalizeStereoLoopClip(clipName, dryL, dryR, feedL, feedR, sampleRate, totalSamples, beatDur, 0.90f);
    }

    private static float DeterministicNoise(int seed)
    {
        uint x = (uint)(seed * 747796405 + 2891336453u);
        x = ((x >> ((int)(x >> 28) + 4)) ^ x) * 277803737u;
        x = (x >> 22) ^ x;
        return (x / (float)uint.MaxValue) * 2f - 1f;
    }

    private static void AddMusicKick(
        float[] bufL, float[] bufR, int sampleRate, int totalSamples,
        float startTime, float startFreq, float endFreq, float duration, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float phase = 0f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 14f);
            float f = endFreq + (startFreq - endFreq) * Mathf.Exp(-t * 28f);
            phase += twoPi * f / sampleRate;

            float click = Mathf.Exp(-t * 220f) * 0.35f;
            float sample = Mathf.Sin(phase) * env + click * Mathf.Sin(phase * 3f);
            float outVal = Mathf.Clamp(sample * 1.25f / (1f + Mathf.Abs(sample) * 0.25f), -1f, 1f) * gain;

            int idx = (startSample + i) % totalSamples;
            bufL[idx] += outVal;
            bufR[idx] += outVal;
        }
    }

    private static void AddMenuAnalogPad(
        float[] bufL, float[] bufR, int sampleRate, int totalSamples,
        float startTime, float barDur, int[] midiNotes)
    {
        const float fadeDur = 1.05f;
        float duration = barDur + fadeDur;
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float[] freqs = new float[midiNotes.Length];
        for (int n = 0; n < midiNotes.Length; n++) freqs[n] = MidiToHz(midiNotes[n]);

        const float twoPi = Mathf.PI * 2f;
        float lpL = 0f, lpR = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float fIn = Mathf.Clamp01(t / fadeDur);
            float fOut = Mathf.Clamp01((duration - t) / fadeDur);
            float env = (0.5f - 0.5f * Mathf.Cos(fIn * Mathf.PI)) * (0.5f - 0.5f * Mathf.Cos(fOut * Mathf.PI));

            float sumL = 0f, sumR = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float p1 = twoPi * freqs[n] * 0.9968f * t;
                float p2 = twoPi * freqs[n] * 1.0032f * t;
                sumL += Mathf.Sin(p1) + 0.32f * Mathf.Sin(2f * p1) + 0.14f * Mathf.Sin(3f * p1);
                sumR += Mathf.Sin(p2) + 0.32f * Mathf.Sin(2f * p2) + 0.14f * Mathf.Sin(3f * p2);
            }

            float cutoff = 0.08f + 0.07f * (0.5f + 0.5f * Mathf.Sin(twoPi * 0.18f * t));
            lpL += (sumL - lpL) * cutoff;
            lpR += (sumR - lpR) * cutoff;

            int idx = (startSample + i) % totalSamples;
            float scale = 0.044f * env;
            bufL[idx] += lpL * scale;
            bufR[idx] += lpR * scale;
        }
    }

    private static void AddMenuSubBass(
        float[] bufL, float[] bufR, int sampleRate, int totalSamples,
        float startTime, float duration, int midiNote)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / 0.035f);
            float envOut = Mathf.Clamp01((duration - t) / 0.14f);
            float env = envIn * envOut * Mathf.Exp(-t * 1.15f);

            float p = twoPi * freq * t;
            float val = (Mathf.Sin(p) + 0.30f * Mathf.Sin(2f * p) + 0.08f * Mathf.Sin(3f * p)) * 0.135f * env;
            int idx = (startSample + i) % totalSamples;
            bufL[idx] += val;
            bufR[idx] += val;
        }
    }

    private static void AddMenuSoftAirBrush(
        float[] feedL, float[] feedR, int sampleRate, int totalSamples, float startTime, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(0.24f * sampleRate);
        float lp = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Min(1f, t / 0.015f) * Mathf.Exp(-t * 16f);
            float n = DeterministicNoise(startSample + i * 3);
            lp += (n - lp) * 0.22f;
            float val = (n - lp) * 0.085f * gain * env;
            int idx = (startSample + i) % totalSamples;
            feedL[idx] += val * 0.85f;
            feedR[idx] += val * 1.15f;
        }
    }

    private static void AddMenuCrystalChime(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, int midiNote, float pan)
    {
        const float duration = 1.15f;
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;

        float gL = Mathf.Cos(pan * Mathf.PI * 0.5f);
        float gR = Mathf.Sin(pan * Mathf.PI * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(t / 0.006f) * Mathf.Clamp01((duration - t) / 0.05f) * Mathf.Exp(-t * 3.6f);
            float mod = 1.15f * Mathf.Exp(-t * 9f) * Mathf.Sin(twoPi * (freq * 2.0f) * t);
            float car = Mathf.Sin(twoPi * freq * t + mod) + 0.16f * Mathf.Sin(twoPi * freq * 3f * t) * Mathf.Exp(-t * 11f);

            float val = car * env;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val * 0.082f * gL;
            dryR[idx] += val * 0.082f * gR;
            feedL[idx] += val * 0.088f * gL;
            feedR[idx] += val * 0.088f * gR;
        }
    }

    private static void AddCombatSnare(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(0.19f * sampleRate);
        const float twoPi = Mathf.PI * 2f;
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float f = 140f + 75f * Mathf.Exp(-t * 40f);
            phase += twoPi * f / sampleRate;

            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.45f;
            float noise = DeterministicNoise(startSample + i * 7) * Mathf.Exp(-t * 18f) * 0.72f;
            float val = (body + noise) * gain;

            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val;
            dryR[idx] += val;
            feedL[idx] += val * 0.25f;
            feedR[idx] += val * 0.25f;
        }
    }

    private static void AddCombatHiHat(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples, float startTime, bool accent)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        float dur = accent ? 0.075f : 0.042f;
        int count = Mathf.RoundToInt(dur * sampleRate);
        float prev = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Exp(-t * (accent ? 42f : 85f));
            float n = DeterministicNoise(startSample + i * 11);
            float hp = n - prev * 0.78f;
            prev = n;

            float val = hp * env * (accent ? 0.068f : 0.042f);
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val * 0.90f;
            dryR[idx] += val * 1.10f;
        }
    }

    private static void AddCombatAcidBassStep(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples,
        float startTime, float duration, int midiNote, float peakCutoffHz)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float lp = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / 0.004f);
            float envOut = Mathf.Clamp01((duration - t) / 0.014f);
            float env = envIn * envOut * Mathf.Exp(-t * 4.5f);

            float cyc1 = (t * freq) % 1f;
            float saw = cyc1 * 2f - 1f;
            float sub = Mathf.Sin(Mathf.PI * 2f * freq * t);

            float cutoffHz = 280f + peakCutoffHz * Mathf.Exp(-t * 18f);
            float alpha = Mathf.Clamp(2f * Mathf.PI * cutoffHz / sampleRate, 0.02f, 0.75f);
            lp += (saw - lp) * alpha;

            float val = (lp * 0.72f + sub * 0.45f) * env * 0.17f;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val;
            dryR[idx] += val;
        }
    }

    private static void AddCombatPowerStabPad(
        float[] dryL, float[] dryR, int sampleRate, int totalSamples,
        float startTime, float barDur, float beatDur, int[] midiNotes, float gain)
    {
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(barDur * sampleRate);
        float[] freqs = new float[midiNotes.Length];
        for (int n = 0; n < midiNotes.Length; n++) freqs[n] = MidiToHz(midiNotes[n]);

        float lpL = 0f, lpR = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / 0.03f);
            float envOut = Mathf.Clamp01((barDur - t) / 0.04f);
            float beatFrac = (t % beatDur) / beatDur;
            float sidechain = 1f - 0.58f * Mathf.Exp(-beatFrac * 7.5f);

            float sL = 0f, sR = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float c1 = ((t * freqs[n] * 0.996f) % 1f) * 2f - 1f;
                float c2 = ((t * freqs[n] * 1.004f) % 1f) * 2f - 1f;
                sL += c1;
                sR += c2;
            }
            lpL += (sL - lpL) * 0.16f;
            lpR += (sR - lpR) * 0.16f;

            float scale = envIn * envOut * sidechain * gain;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += lpL * scale;
            dryR[idx] += lpR * scale;
        }
    }

    private static void AddCombatSynthPluck(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, int midiNote, float pan, float duration, float gain)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        float gL = Mathf.Cos(pan * Mathf.PI * 0.5f);
        float gR = Mathf.Sin(pan * Mathf.PI * 0.5f);
        float lp = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((duration - t) / 0.02f) * Mathf.Exp(-t * 9.5f);
            float saw = (((t * freq) % 1f) * 2f - 1f) * 0.7f + (((t * freq * 2.003f) % 1f) * 2f - 1f) * 0.3f;
            lp += (saw - lp) * Mathf.Clamp(0.45f * Mathf.Exp(-t * 8f) + 0.08f, 0.05f, 0.65f);

            float val = lp * env;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += val * gain * gL;
            dryR[idx] += val * gain * gR;
            feedL[idx] += val * gain * 0.65f * gL;
            feedR[idx] += val * gain * 0.65f * gR;
        }
    }

    private static void AddCombatLeadTone(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, float duration, int midiNote)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;
        float lpL = 0f, lpR = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float env = Mathf.Clamp01(t / 0.012f) * Mathf.Clamp01((duration - t) / 0.045f);
            float vib = 1f + 0.0035f * Mathf.Sin(twoPi * 5.6f * t) * Mathf.Clamp01(t / 0.12f);

            float f = freq * vib;
            float sawL = (((t * f * 0.9965f) % 1f) * 2f - 1f) + 0.45f * Mathf.Sin(twoPi * f * t);
            float sawR = (((t * f * 1.0035f) % 1f) * 2f - 1f) + 0.45f * Mathf.Sin(twoPi * f * t);
            lpL += (sawL - lpL) * 0.28f;
            lpR += (sawR - lpR) * 0.28f;

            float vL = lpL * env;
            float vR = lpR * env;
            int idx = (startSample + i) % totalSamples;
            dryL[idx] += vL * 0.088f;
            dryR[idx] += vR * 0.088f;
            feedL[idx] += vL * 0.068f;
            feedR[idx] += vR * 0.068f;
        }
    }

    private static void AddTutorialPadChord(
        float[] bufL, float[] bufR, int sampleRate, int totalSamples,
        float startTime, float barDur, int[] midiNotes)
    {
        const float fadeDur = 0.85f;
        float duration = barDur + fadeDur;
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);

        float[] freqs = new float[midiNotes.Length];
        for (int n = 0; n < midiNotes.Length; n++) freqs[n] = MidiToHz(midiNotes[n]);

        const float twoPi = Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float fIn = Mathf.Clamp01(t / fadeDur);
            float fOut = Mathf.Clamp01((duration - t) / fadeDur);
            float env = (0.5f - 0.5f * Mathf.Cos(fIn * Mathf.PI)) * (0.5f - 0.5f * Mathf.Cos(fOut * Mathf.PI));

            float sumL = 0f;
            float sumR = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float pL = twoPi * (freqs[n] * 0.9982f) * t;
                float pR = twoPi * (freqs[n] * 1.0018f) * t;
                sumL += Mathf.Sin(pL) + 0.18f * Mathf.Sin(2f * pL);
                sumR += Mathf.Sin(pR) + 0.18f * Mathf.Sin(2f * pR);
            }

            int idx = (startSample + i) % totalSamples;
            float scale = 0.048f * env;
            bufL[idx] += sumL * scale;
            bufR[idx] += sumR * scale;
        }
    }

    private static void AddTutorialBassNote(
        float[] bufL, float[] bufR, int sampleRate, int totalSamples,
        float startTime, float duration, int midiNote)
    {
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float attack = 0.045f;
        const float release = 0.18f;
        const float twoPi = Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / attack);
            float envOut = Mathf.Clamp01((duration - t) / release);
            float smooth = (0.5f - 0.5f * Mathf.Cos(envIn * Mathf.PI)) * (0.5f - 0.5f * Mathf.Cos(envOut * Mathf.PI));
            float env = smooth * Mathf.Exp(-t * 1.35f);

            float p = twoPi * freq * t;
            float sample = (Mathf.Sin(p) + 0.25f * Mathf.Sin(2f * p)) * 0.12f * env;

            int idx = (startSample + i) % totalSamples;
            bufL[idx] += sample;
            bufR[idx] += sample;
        }
    }

    private static void AddTutorialPluckNote(
        float[] dryL, float[] dryR, float[] feedL, float[] feedR,
        int sampleRate, int totalSamples, float startTime, int midiNote, float pan)
    {
        const float duration = 0.95f;
        const float attack = 0.008f;
        const float release = 0.04f;
        float freq = MidiToHz(midiNote);
        int startSample = Mathf.RoundToInt(startTime * sampleRate);
        int count = Mathf.RoundToInt(duration * sampleRate);
        const float twoPi = Mathf.PI * 2f;

        float gainL = Mathf.Cos(pan * Mathf.PI * 0.5f);
        float gainR = Mathf.Sin(pan * Mathf.PI * 0.5f);

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float envIn = Mathf.Clamp01(t / attack);
            float envOut = Mathf.Clamp01((duration - t) / release);
            float env = envIn * envOut * Mathf.Exp(-t * 4.5f);

            float p = twoPi * freq * t;
            float tone = Mathf.Sin(p)
                       + 0.26f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 7f)
                       + 0.10f * Mathf.Sin(3f * p) * Mathf.Exp(-t * 12f);

            float val = tone * env;
            int idx = (startSample + i) % totalSamples;

            dryL[idx] += val * 0.095f * gainL;
            dryR[idx] += val * 0.095f * gainR;
            feedL[idx] += val * 0.075f * gainL;
            feedR[idx] += val * 0.075f * gainR;
        }
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

    /// <summary>
    /// Láser normal del Jefe: rayo de plasma profundo, nítido y limpio (sin saturación),
    /// claramente más imponente que el láser de los enemigos Tier 4.
    /// </summary>
    public void PlayBossLaserSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (generatedBossLaserClip == null) generatedBossLaserClip = CreateCleanBossLaserClip();
        if (generatedBossLaserClip != null)
        {
            sfxSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            sfxSource.PlayOneShot(generatedBossLaserClip, 0.92f * sfxVolume);
        }
    }

    /// <summary>
    /// Hiper Rayo de Fase 2: sonido épico de súper cañón orbital / súper rayo de energía pura,
    /// con caída sub-grave limpia, cuerpo armónico envolvente y cola de plasma sin saturación.
    /// </summary>
    public void PlayBossHyperBeamSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (generatedBossHyperBeamClip == null) generatedBossHyperBeamClip = CreateCleanHyperBeamClip();
        if (generatedBossHyperBeamClip != null)
        {
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(generatedBossHyperBeamClip, 0.98f * sfxVolume);
        }
    }

    private static AudioClip CreateCleanBossLaserClip()
    {
        const int sampleRate = 44100;
        const float duration = 2.65f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        const float twoPi = Mathf.PI * 2f;
        float subPhase = 0f;
        float corePhase = 0f;
        float fifthPhase = 0f;
        float sweepPhase = 0f;
        float lp1 = 0f;
        float lp2 = 0f;
        float peak = 0.001f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = Mathf.Clamp01(t / duration);

            float envIn = Mathf.Clamp01(t / 0.025f);
            float envOut = Mathf.Clamp01((duration - t) / 0.36f);
            float env = envIn * (0.5f - 0.5f * Mathf.Cos(envOut * Mathf.PI));
            float ignition = Mathf.Exp(-t * 11f);

            // Barrido inicial limpio de ignición láser (980 Hz -> 185 Hz) + cuerpo de plasma estable
            float subFreq = 68f + 42f * Mathf.Exp(-t * 10f) - u * 6f;
            float coreFreq = 136f + Mathf.Sin(twoPi * 6.5f * t) * 3.5f - u * 10f;
            float fifthFreq = coreFreq * 1.5f;
            float sweepFreq = 190f + 820f * Mathf.Exp(-t * 16f);

            subPhase += twoPi * subFreq / sampleRate;
            corePhase += twoPi * coreFreq / sampleRate;
            fifthPhase += twoPi * fifthFreq / sampleRate;
            sweepPhase += twoPi * sweepFreq / sampleRate;
            if (subPhase > twoPi) subPhase -= twoPi;
            if (corePhase > twoPi) corePhase -= twoPi;
            if (fifthPhase > twoPi) fifthPhase -= twoPi;
            if (sweepPhase > twoPi) sweepPhase -= twoPi;

            // Soplo sedoso de plasma doblemente filtrado (suave, nunca áspero)
            float n = DeterministicNoise(i * 7 + 19);
            lp1 = Mathf.Lerp(lp1, n, 0.14f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.14f);

            float pulse = 0.86f + 0.14f * Mathf.Sin(twoPi * 12f * t);
            float sub = Mathf.Sin(subPhase) * 0.48f;
            float core = (Mathf.Sin(corePhase) * 0.34f + Mathf.Sin(corePhase * 2f) * 0.14f + Mathf.Sin(fifthPhase) * 0.18f) * pulse;
            float sweep = Mathf.Sin(sweepPhase + Mathf.Sin(corePhase) * 0.45f) * (0.10f + ignition * 0.42f);
            float air = lp2 * (0.16f + ignition * 0.24f);

            float v = (sub + core + sweep + air) * env;
            samples[i] = v;
            float a = Mathf.Abs(v);
            if (a > peak) peak = a;
        }

        // Normalización limpia con margen seguro (cero clipping ni saturación)
        float norm = 0.68f / peak;
        for (int i = 0; i < totalSamples; i++) samples[i] *= norm;

        AudioClip clip = AudioClip.Create("boss_clean_laser_sfx", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateCleanHyperBeamClip()
    {
        const int sampleRate = 44100;
        const float duration = 2.95f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        const float twoPi = Mathf.PI * 2f;
        float subPhase = 0f;
        float rootPhase = 0f;
        float fifthPhase = 0f;
        float octPhase = 0f;
        float shimmerPhase = 0f;
        float blastSweepPhase = 0f;
        float lpAir1 = 0f;
        float lpAir2 = 0f;
        float peak = 0.001f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = Mathf.Clamp01(t / duration);

            // Envolvente de súper rayo: estallido instantáneo + flujo continuo masivo + apagado de turbina
            float envIn = Mathf.Clamp01(t / 0.032f);
            float envOut = Mathf.Clamp01((duration - t) / 0.48f);
            float env = envIn * (0.5f - 0.5f * Mathf.Cos(envOut * Mathf.PI));
            float impact = Mathf.Exp(-t * 6.5f);
            float tailDrop = Mathf.Pow(Mathf.Clamp01((u - 0.78f) / 0.22f), 2f);

            // Caída sub-grave limpia tipo cañón orbital + acorde energético majestuoso (fundamental + quinta + octava + brillo)
            float subFreq = (48f + 78f * Mathf.Exp(-t * 7.5f)) * (1f - tailDrop * 0.28f);
            float rootFreq = 96f * (1f - tailDrop * 0.22f) + Mathf.Sin(twoPi * 5.2f * t) * 2.2f;
            float fifthFreq = rootFreq * 1.498f;
            float octFreq = rootFreq * 2.004f;
            float shimmerFreq = 480f + 160f * Mathf.Sin(twoPi * 3.1f * t) + impact * 620f;
            float blastFreq = 160f + 1280f * Mathf.Exp(-t * 11f);

            subPhase += twoPi * subFreq / sampleRate;
            rootPhase += twoPi * rootFreq / sampleRate;
            fifthPhase += twoPi * fifthFreq / sampleRate;
            octPhase += twoPi * octFreq / sampleRate;
            shimmerPhase += twoPi * shimmerFreq / sampleRate;
            blastSweepPhase += twoPi * blastFreq / sampleRate;

            if (subPhase > twoPi) subPhase -= twoPi;
            if (rootPhase > twoPi) rootPhase -= twoPi;
            if (fifthPhase > twoPi) fifthPhase -= twoPi;
            if (octPhase > twoPi) octPhase -= twoPi;
            if (shimmerPhase > twoPi) shimmerPhase -= twoPi;
            if (blastSweepPhase > twoPi) blastSweepPhase -= twoPi;

            // Flujo de aire/plasma filtrado suavemente (efecto chorro de energía colosal sin estática dura)
            float n = DeterministicNoise(i * 11 + 53);
            lpAir1 = Mathf.Lerp(lpAir1, n, 0.16f);
            lpAir2 = Mathf.Lerp(lpAir2, lpAir1, 0.12f);

            float beamUndulation = 0.84f + 0.16f * Mathf.Sin(twoPi * 9.5f * t);
            float sub = Mathf.Sin(subPhase) * (0.56f + impact * 0.25f);
            float chordBeam = (Mathf.Sin(rootPhase) * 0.34f
                             + Mathf.Sin(fifthPhase) * 0.24f
                             + Mathf.Sin(octPhase) * 0.18f
                             + Mathf.Sin(octPhase * 1.5f) * 0.08f) * beamUndulation;
            float crystalline = Mathf.Sin(shimmerPhase + Mathf.Sin(rootPhase) * 0.35f) * (0.11f + impact * 0.14f);
            float blast = Mathf.Sin(blastSweepPhase) * impact * 0.36f;
            float plasmaFlow = lpAir2 * (0.26f + impact * 0.30f);

            float v = (sub + chordBeam + crystalline + blast + plasmaFlow) * env;
            samples[i] = v;
            float a = Mathf.Abs(v);
            if (a > peak) peak = a;
        }

        // Normalización limpia y controlada (cero saturación)
        float norm = 0.74f / peak;
        for (int i = 0; i < totalSamples; i++) samples[i] *= norm;

        AudioClip clip = AudioClip.Create("boss_clean_hyper_beam_sfx", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Alarma/carga limpia previa de 3 segundos para el Hiper Rayo de Fase 2.
    /// </summary>
    public void PlayBossHyperWarningSfx()
    {
        if (entryAudioSource == null) InitAudioSources();
        if (generatedBossHyperWarningClip == null) generatedBossHyperWarningClip = CreateBossHyperWarningClip();
        if (generatedBossHyperWarningClip != null)
        {
            entryAudioSource.PlayOneShot(generatedBossHyperWarningClip, 0.55f * sfxVolume);
        }
    }

    private static AudioClip CreateBossHyperWarningClip()
    {
        const int sampleRate = 44100;
        const float duration = 2.95f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        const float twoPi = Mathf.PI * 2f;
        float humPhase = 0f;
        float pingPhase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = Mathf.Clamp01(t / duration);

            // Carga energética limpia (senoidal suave) + pulsos de alerta nítidos sin distorsión
            float humFreq = Mathf.Lerp(95f, 280f, u * u);
            float pulseRate = Mathf.Lerp(2.2f, 7.5f, u);
            float pulseEnv = 0.25f + 0.75f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(twoPi * pulseRate * t), 2f);
            float pingFreq = Mathf.Lerp(440f, 740f, u);

            humPhase += twoPi * humFreq / sampleRate;
            pingPhase += twoPi * pingFreq / sampleRate;
            if (humPhase > twoPi) humPhase -= twoPi;
            if (pingPhase > twoPi) pingPhase -= twoPi;

            float envIn = Mathf.Clamp01(t / 0.08f);
            float envOut = Mathf.Clamp01((duration - t) / 0.08f);
            float hum = Mathf.Sin(humPhase) * 0.42f + Mathf.Sin(humPhase * 2f) * 0.14f;
            float ping = Mathf.Sin(pingPhase) * 0.22f * pulseEnv;

            float v = (hum * (0.45f + 0.55f * u) + ping) * envIn * envOut;
            samples[i] = Mathf.Clamp(v * 0.70f, -0.80f, 0.80f);
        }

        AudioClip clip = AudioClip.Create("boss_hyper_warning_sfx", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayBossDeathSfx() => PlayClip(ref bossDeathSfx, "Audio/boss_death", 1.15f);

    /// <summary>
    /// Reproduce un sonido de entrada distintivo y suave según el Tier del enemigo,
    /// progresando hacia tonos más graves en los Tiers altos y evitando saturación en oleadas.
    /// </summary>
    public void PlayEnemyEntrySfx(EnemyTier tier)
    {
        if (entryAudioSource == null) InitAudioSources();

        int idx = Mathf.Clamp((int)tier, 0, 3);
        float cooldown = tier switch
        {
            EnemyTier.Tier1 => 0.38f,
            EnemyTier.Tier2 => 0.34f,
            EnemyTier.Tier3 => 0.30f,
            _ => 0.26f
        };

        if (Time.unscaledTime - lastTierEntryTime[idx] < cooldown) return;
        lastTierEntryTime[idx] = Time.unscaledTime;

        AudioClip clip = (tierEntryClips != null && idx < tierEntryClips.Length) ? tierEntryClips[idx] : null;
        if (clip == null)
        {
            if (generatedTierEntryClips[idx] == null)
            {
                generatedTierEntryClips[idx] = CreateTierEntryClip(tier);
            }
            clip = generatedTierEntryClips[idx];
        }
        if (clip == null) return;

        float vol = tier switch
        {
            EnemyTier.Tier1 => 0.56f,
            EnemyTier.Tier2 => 0.62f,
            EnemyTier.Tier3 => 0.68f,
            _ => 0.76f
        };
        entryAudioSource.PlayOneShot(clip, vol * sfxVolume);
    }

    public void PlayBossEntrySfx()
    {
        if (entryAudioSource == null) InitAudioSources();

        if (bossEntrySfx == null)
        {
            if (generatedBossEntryClip == null) generatedBossEntryClip = CreateBossEntryClip();
            bossEntrySfx = generatedBossEntryClip;
        }
        if (bossEntrySfx != null)
        {
            entryAudioSource.PlayOneShot(bossEntrySfx, 0.82f * sfxVolume);
        }
    }

    private static AudioClip CreateTierEntryClip(EnemyTier tier)
    {
        return tier switch
        {
            // Tier 1: campana suave ligera (aguda pero aireada, nada agresiva)
            EnemyTier.Tier1 => SynthesizeEntryTone("entry_tier1", 0.21f, 587.33f, 880.00f, 0f, 0.14f, 0.00f, 0.014f, true),
            // Tier 2: pulso medio-cálido descendente
            EnemyTier.Tier2 => SynthesizeEntryTone("entry_tier2", 0.26f, 349.23f, 261.63f, 0f, 0.24f, 0.08f, 0.016f, true),
            // Tier 3: deslizamiento grave-medio tipo crucero
            EnemyTier.Tier3 => SynthesizeEntryTone("entry_tier3", 0.35f, 220.00f, 146.83f, 330.00f, 0.32f, 0.12f, 0.020f, false),
            // Tier 4: tono profundo y cálido tipo sub-bocina de artillero pesado
            _ => SynthesizeEntryTone("entry_tier4", 0.44f, 116.54f, 82.41f, 174.61f, 0.42f, 0.18f, 0.022f, false)
        };
    }

    private static AudioClip CreateBossEntryClip()
    {
        return SynthesizeEntryTone("entry_boss", 0.72f, 65.41f, 61.74f, 98.00f, 0.45f, 0.22f, 0.075f, false);
    }

    private static AudioClip SynthesizeEntryTone(
        string clipName, float duration,
        float freqStart, float freqEnd, float overtoneFreq,
        float harm2Amp, float harm3Amp, float attackSec, bool steppedMelody)
    {
        const int sampleRate = 44100;
        int totalSamples = Mathf.Max(256, Mathf.RoundToInt(duration * sampleRate));
        float[] samples = new float[totalSamples];

        const float twoPi = Mathf.PI * 2f;
        float phase = 0f;
        float overPhase = 0f;
        float releaseSec = Mathf.Min(0.08f, duration * 0.35f);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = Mathf.Clamp01(t / duration);

            float freq;
            if (steppedMelody)
            {
                // Transición suave en curva S alrededor de la mitad para dos notas limpias sin clics
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.36f) / 0.16f));
                freq = Mathf.Lerp(freqStart, freqEnd, blend);
            }
            else
            {
                freq = Mathf.Lerp(freqStart, freqEnd, Mathf.Pow(u, 0.75f));
            }

            phase += twoPi * freq / sampleRate;
            if (phase > twoPi) phase -= twoPi;

            float envIn = Mathf.Clamp01(t / attackSec);
            float envOut = Mathf.Clamp01((duration - t) / releaseSec);
            float smoothIn = 0.5f - 0.5f * Mathf.Cos(envIn * Mathf.PI);
            float smoothOut = 0.5f - 0.5f * Mathf.Cos(envOut * Mathf.PI);
            float decay = Mathf.Exp(-u * (steppedMelody ? 1.75f : 1.35f));
            float env = smoothIn * smoothOut * decay;

            float wave = Mathf.Sin(phase)
                       + harm2Amp * Mathf.Sin(2f * phase)
                       + harm3Amp * Mathf.Sin(3f * phase);

            if (overtoneFreq > 1f)
            {
                float overCur = overtoneFreq * Mathf.Lerp(1f, freqEnd / Mathf.Max(1f, freqStart), Mathf.Pow(u, 0.75f));
                overPhase += twoPi * overCur / sampleRate;
                if (overPhase > twoPi) overPhase -= twoPi;
                wave += 0.28f * Mathf.Sin(overPhase);
            }

            float v = wave * 0.78f * env;
            samples[i] = Mathf.Clamp(v / (1f + Mathf.Abs(v) * 0.25f), -0.94f, 0.94f);
        }

        AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// Sonido distintivo cuando le bajan todo el escudo a un jugador:
    /// estallido cristalino-energético de sobrecarga, despresurización de plasma en barrido descendente
    /// y pulso de advertencia de escudo colapsado.
    /// </summary>
    public void PlayPlayerShieldBreakSfx()
    {
        if (Time.unscaledTime - lastShieldBreakTime < 0.18f) return;
        lastShieldBreakTime = Time.unscaledTime;

        if (sfxSource == null) InitAudioSources();

        if (playerShieldBreakSfx == null)
        {
            playerShieldBreakSfx = Resources.Load<AudioClip>("Audio/player_shield_break");
        }

        if (playerShieldBreakSfx != null)
        {
            sfxSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            sfxSource.PlayOneShot(playerShieldBreakSfx, 1.15f * sfxVolume);
            return;
        }

        if (generatedPlayerShieldBreakClip == null) generatedPlayerShieldBreakClip = CreateCleanPlayerShieldBreakClip();
        if (generatedPlayerShieldBreakClip != null)
        {
            sfxSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            sfxSource.PlayOneShot(generatedPlayerShieldBreakClip, 1.12f * sfxVolume);
        }
    }

    private static AudioClip CreateCleanPlayerShieldBreakClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.72f;
        int totalSamples = Mathf.RoundToInt(duration * sampleRate);
        float[] samples = new float[totalSamples];

        const float twoPi = Mathf.PI * 2f;
        float crystalPhase1 = 0f;
        float crystalPhase2 = 0f;
        float crystalPhase3 = 0f;
        float dropPhase = 0f;
        float subPhase = 0f;
        float warnPhase = 0f;
        float lpNoise = 0f;
        float peak = 0.001f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = i / (float)sampleRate;
            float u = Mathf.Clamp01(t / duration);

            // 1. Envoltura de impacto inicial ultra rápida (transitorio explosivo de 3ms)
            float envIn = Mathf.Clamp01(t / 0.003f);
            float envOut = Mathf.Clamp01((duration - t) / 0.14f);
            float mainEnv = envIn * envOut;

            // 2. Ruptura de cristal de plasma (tonos metálicos/cristalinos agudos en decadencia exponencial)
            float glassDecay = Mathf.Exp(-t * 18f);
            float freqGlass1 = 2800f * Mathf.Exp(-t * 6f);
            float freqGlass2 = 1940f * Mathf.Exp(-t * 8f);
            float freqGlass3 = 3600f * Mathf.Exp(-t * 22f);

            crystalPhase1 += twoPi * freqGlass1 / sampleRate;
            crystalPhase2 += twoPi * freqGlass2 / sampleRate;
            crystalPhase3 += twoPi * freqGlass3 / sampleRate;
            if (crystalPhase1 > twoPi) crystalPhase1 -= twoPi;
            if (crystalPhase2 > twoPi) crystalPhase2 -= twoPi;
            if (crystalPhase3 > twoPi) crystalPhase3 -= twoPi;

            float crystal = (Mathf.Sin(crystalPhase1) * 0.35f +
                             Mathf.Sin(crystalPhase2) * 0.28f +
                             Mathf.Sin(crystalPhase3) * 0.20f) * glassDecay;

            // 3. Barrido descendente de despresurización de plasma (de 920 Hz a 110 Hz)
            float dropFreq = 110f + 810f * Mathf.Exp(-t * 9.5f);
            dropPhase += twoPi * dropFreq / sampleRate;
            if (dropPhase > twoPi) dropPhase -= twoPi;
            float dropDecay = Mathf.Exp(-t * 6f);
            float drop = Mathf.Sin(dropPhase) * 0.45f * dropDecay;

            // 4. Sub-grave de descarga / explosión del campo electromagnético (160 Hz -> 42 Hz)
            float subFreq = 42f + 118f * Mathf.Exp(-t * 14f);
            subPhase += twoPi * subFreq / sampleRate;
            if (subPhase > twoPi) subPhase -= twoPi;
            float subDecay = Mathf.Exp(-t * 8f);
            float sub = Mathf.Sin(subPhase) * 0.55f * subDecay;

            // 5. Advertencia electrónica de escudo agotado (chirrido bifásico descendente Sol5 -> Do5)
            float warnEnv = 0f;
            if (t > 0.08f && t < 0.55f)
            {
                float tw = t - 0.08f;
                float wDecay = Mathf.Exp(-tw * 7f);
                float warnFreq = (tw < 0.16f) ? 784f : 523f;
                warnPhase += twoPi * warnFreq / sampleRate;
                if (warnPhase > twoPi) warnPhase -= twoPi;
                warnEnv = Mathf.Sin(warnPhase) * 0.25f * wDecay;
            }

            // 6. Chispas de estática de plasma filtradas suavemente
            float noise = DeterministicNoise(i * 11 + 37);
            lpNoise = Mathf.Lerp(lpNoise, noise, Mathf.Clamp01(0.18f * Mathf.Exp(-t * 7f)));
            float fizzle = lpNoise * (0.28f * glassDecay);

            float totalVal = (crystal + drop + sub + warnEnv + fizzle) * mainEnv;
            samples[i] = totalVal;
            float absVal = Mathf.Abs(totalVal);
            if (absVal > peak) peak = absVal;
        }

        // Normalización limpia para volumen potente pero cero distorsión
        float norm = 0.85f / peak;
        for (int i = 0; i < totalSamples; i++)
        {
            samples[i] *= norm;
        }

        AudioClip clip = AudioClip.Create("player_shield_break_sfx", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

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