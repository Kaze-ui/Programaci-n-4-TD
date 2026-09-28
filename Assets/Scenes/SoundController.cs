using UnityEngine;

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

    void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    [Header("Fuentes de audio")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource chargeSource;

    [Header("Clips del Jugador")]
    public AudioClip shootSfx;       // Disparo normal
    public AudioClip miniShootSfx;   // Disparo pequeño (modo 2 toques rápidos)
    public AudioClip mediumShootSfx; // Disparo intermedio (1s a 2s de carga)
    public AudioClip chargeSfx;      // Sonido continuo de carga de la bala grande
    public AudioClip bigShootSfx;    // Disparo de bala grande cargada
    public AudioClip switchModeSfx;  // Cambio de modo de disparo

    [Header("Otros Clips")]
    public AudioClip backgroundMusic;
    public AudioClip enemyDeathSfx;
    public AudioClip bossDeathSfx;
    public AudioClip bossLaserSfx;
    public AudioClip tier4LaserSfx;
    public AudioClip playerHitSfx;
    public AudioClip playerDeathSfx;
    public AudioClip gameOverSfx;
    public AudioClip upgradeBuySfx;
    public AudioClip upgradeOpenSfx;
    public AudioClip upgradeHoverSfx;
    public AudioClip upgradeFailedSfx;
    public AudioClip upgradeContinueSfx;

    private float sfxVolume = 1.0f;
    private float musicVolume = 1.0f;

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
        LoadAllClips();
    }

    void Start()
    {
        if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    private void InitAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
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

    private void LoadAllClips()
    {
        if (shootSfx == null) shootSfx = LoadOrGenerateNormalShootSfx();
        if (miniShootSfx == null) miniShootSfx = LoadOrGenerateMiniShootSfx();
        if (chargeSfx == null) chargeSfx = LoadOrGenerateChargeSfx();
        if (bigShootSfx == null) bigShootSfx = LoadOrGenerateBigShootSfx();
        if (enemyDeathSfx == null) enemyDeathSfx = LoadOrGenerateEnemyDeathSfx();
        if (upgradeBuySfx == null) upgradeBuySfx = LoadOrGenerateUpgradeBuySfx();
        if (upgradeOpenSfx == null) upgradeOpenSfx = LoadOrGenerateUpgradeOpenSfx();
    }

    // ---- Disparos del Jugador ----

    /// <summary>
    /// Disparo normal: suave, agradable y con sutil variación de tono para no cansar el oído.
    /// </summary>
    public void PlayNormalShootSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (shootSfx == null) shootSfx = LoadOrGenerateNormalShootSfx();

        sfxSource.pitch = Random.Range(0.97f, 1.03f);
        sfxSource.PlayOneShot(shootSfx, 0.70f * sfxVolume);
    }

    /// <summary>
    /// Mini balas: más agudas, rápidas y sutiles.
    /// </summary>
    public void PlayMiniShootSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (miniShootSfx == null) miniShootSfx = LoadOrGenerateMiniShootSfx();

        sfxSource.pitch = Random.Range(1.05f, 1.15f);
        sfxSource.PlayOneShot(miniShootSfx, 0.60f * sfxVolume);
    }

    /// <summary>
    /// Actualiza el sonido de carga ascendente mientras se mantiene presionado el botón.
    /// progress va de 0.0 a 1.0 según el tiempo transcurrido hasta los 2.5s.
    /// </summary>
    public void UpdateChargeSound(float progress)
    {
        if (chargeSource == null) InitAudioSources();
        if (chargeSfx == null) chargeSfx = LoadOrGenerateChargeSfx();

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

        // Incremento suave del volumen y modulación progresiva del tono a medida que se carga la bala gigante
        chargeSource.volume = Mathf.MoveTowards(chargeSource.volume, 0.75f * sfxVolume, Time.deltaTime * 3.5f);
        chargeSource.pitch = Mathf.Lerp(0.85f, 1.75f, progress);
    }

    /// <summary>
    /// Detiene de inmediato el sonido de carga si se dispara o se cancela.
    /// </summary>
    public void StopChargeSound()
    {
        if (chargeSource != null && chargeSource.isPlaying)
        {
            chargeSource.Stop();
            chargeSource.pitch = 1.0f;
            chargeSource.volume = 0f;
        }
    }

    /// <summary>
    /// Disparo de bala gigante: potente, con graves profundos y gran impacto sonoro.
    /// </summary>
    public void PlayBigShootSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (bigShootSfx == null) bigShootSfx = LoadOrGenerateBigShootSfx();

        sfxSource.pitch = Random.Range(0.96f, 1.02f);
        sfxSource.PlayOneShot(bigShootSfx, 1.0f * sfxVolume);
    }

    // Compatibilidad con llamadas previas
    public void PlayShootSfx() => PlayNormalShootSfx();

    public void PlayEnemyDeathSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (enemyDeathSfx == null) enemyDeathSfx = LoadOrGenerateEnemyDeathSfx();

        sfxSource.pitch = Random.Range(0.92f, 1.08f);
        sfxSource.PlayOneShot(enemyDeathSfx, 0.85f * sfxVolume);
    }

    public void PlayPlayerHitSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (playerHitSfx == null) playerHitSfx = LoadOrGeneratePlayerHitSfx();

        sfxSource.pitch = Random.Range(0.95f, 1.05f);
        sfxSource.PlayOneShot(playerHitSfx, 1.0f * sfxVolume);
    }

    private float lastPlayerDeathTime = -10f;
    public void PlayPlayerDeathSfx()
    {
        if (Time.unscaledTime - lastPlayerDeathTime < 1.0f) return;
        lastPlayerDeathTime = Time.unscaledTime;

        if (sfxSource == null) InitAudioSources();
        if (playerDeathSfx == null) playerDeathSfx = LoadOrGeneratePlayerDeathSfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(playerDeathSfx, 1.15f * sfxVolume);
    }

    public void PlayGameOverSfx() => PlayPlayerDeathSfx();

    public void PlayTier4LaserSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (tier4LaserSfx == null) tier4LaserSfx = LoadOrGenerateTier4LaserSfx();

        sfxSource.pitch = Random.Range(0.96f, 1.04f);
        sfxSource.PlayOneShot(tier4LaserSfx, 0.85f * sfxVolume);
    }

    public void PlayUpgradePurchasedSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (upgradeBuySfx == null) upgradeBuySfx = LoadOrGenerateUpgradeBuySfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(upgradeBuySfx, 0.90f * sfxVolume);
    }

    public void PlayUpgradePanelOpenSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (upgradeOpenSfx == null) upgradeOpenSfx = LoadOrGenerateUpgradeOpenSfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(upgradeOpenSfx, 0.90f * sfxVolume);
    }

    public void PlayUpgradeHoverSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (upgradeHoverSfx == null) upgradeHoverSfx = LoadOrGenerateUpgradeHoverSfx();

        sfxSource.pitch = Random.Range(0.98f, 1.02f);
        sfxSource.PlayOneShot(upgradeHoverSfx, 0.60f * sfxVolume);
    }

    public void PlayUpgradeFailedSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (upgradeFailedSfx == null) upgradeFailedSfx = LoadOrGenerateUpgradeFailedSfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(upgradeFailedSfx, 0.85f * sfxVolume);
    }

    public void PlaySwitchModeSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (switchModeSfx == null) switchModeSfx = LoadOrGenerateSwitchModeSfx();

        sfxSource.pitch = Random.Range(0.98f, 1.02f);
        sfxSource.PlayOneShot(switchModeSfx, 0.75f * sfxVolume);
    }

    public void PlayMediumShootSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (mediumShootSfx == null) mediumShootSfx = LoadOrGenerateMediumShootSfx();

        sfxSource.pitch = Random.Range(0.96f, 1.04f);
        sfxSource.PlayOneShot(mediumShootSfx, 0.85f * sfxVolume);
    }

    public void PlayUpgradeContinueSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (upgradeContinueSfx == null) upgradeContinueSfx = LoadOrGenerateUpgradeContinueSfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(upgradeContinueSfx, 0.85f * sfxVolume);
    }

    public void PlayBossLaserSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (bossLaserSfx == null) bossLaserSfx = LoadOrGenerateBossLaserSfx();

        sfxSource.pitch = Random.Range(0.98f, 1.02f);
        sfxSource.PlayOneShot(bossLaserSfx, 1.0f * sfxVolume);
    }

    public void PlayBossDeathSfx()
    {
        if (sfxSource == null) InitAudioSources();
        if (bossDeathSfx == null) bossDeathSfx = LoadOrGenerateBossDeathSfx();

        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(bossDeathSfx, 1.15f * sfxVolume);
    }

    public void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.pitch = 1.0f;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null) return;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
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

    // ---- Carga de Recursos o Generación Procedural de Audio (Fallback inmediato) ----

    private AudioClip LoadOrGenerateNormalShootSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/shoot_normal");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.11f * sampleRate);
        float[] data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 380f * Mathf.Exp(-15f * t) + 200f;
            phase += 2f * Mathf.PI * freq / sampleRate;
            float osc = Mathf.Sin(phase) + 0.22f * Mathf.Sin(2f * phase);
            float attack = Mathf.Min(1f, t / 0.004f);
            float decay = Mathf.Exp(-18f * t);
            data[i] = osc * attack * decay * 0.7f;
        }
        clip = AudioClip.Create("shoot_normal_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateMiniShootSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/shoot_mini");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.075f * sampleRate);
        float[] data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 600f * Mathf.Exp(-22f * t) + 350f;
            phase += 2f * Mathf.PI * freq / sampleRate;
            float osc = Mathf.Sin(phase) + 0.15f * Mathf.Sin(2f * phase);
            float attack = Mathf.Min(1f, t / 0.003f);
            float decay = Mathf.Exp(-25f * t);
            data[i] = osc * attack * decay * 0.6f;
        }
        clip = AudioClip.Create("shoot_mini_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateChargeSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/charge_energy");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = sampleRate; // 1s loop
        float[] data = new float[count];
        float baseFreq = 196f;
        float phase1 = 0f, phase2 = 0f, phase3 = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float vib = 8f * Mathf.Sin(2f * Mathf.PI * 8f * t);
            float freq = baseFreq + vib;
            phase1 += 2f * Mathf.PI * freq / sampleRate;
            phase2 += 2f * Mathf.PI * (freq * 2f) / sampleRate;
            phase3 += 2f * Mathf.PI * (freq * 3f) / sampleRate;
            float osc = Mathf.Sin(phase1) * 0.55f + Mathf.Sin(phase2) * 0.30f + Mathf.Sin(phase3) * 0.15f;
            float tremolo = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 16f * t);
            data[i] = osc * tremolo * 0.5f;
        }
        clip = AudioClip.Create("charge_energy_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateBigShootSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/shoot_big");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.65f * sampleRate);
        float[] data = new float[count];
        float phaseSub = 0f, phaseZap = 0f;
        float lp = 0f;
        System.Random rng = new System.Random(42);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float subFreq = 148f * Mathf.Exp(-12f * t) + 32f;
            phaseSub += 2f * Mathf.PI * subFreq / sampleRate;
            float subOsc = Mathf.Sin(phaseSub) + 0.3f * Mathf.Sin(2f * phaseSub);
            float subEnv = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-5f * t);

            float zapFreq = 1300f * Mathf.Exp(-28f * t) + 100f;
            phaseZap += 2f * Mathf.PI * zapFreq / sampleRate;
            float zapOsc = Mathf.Sin(phaseZap);
            float zapEnv = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-22f * t);

            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float alpha = Mathf.Max(0.04f, 0.45f * Mathf.Exp(-10f * t));
            lp = lp + alpha * (rawNoise - lp);
            float noiseEnv = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-7.5f * t);

            float tail = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-4f * t) * 0.3f;
            data[i] = (subOsc * subEnv * 0.60f + zapOsc * zapEnv * 0.45f + lp * noiseEnv * 0.40f + tail) * 0.9f;
        }
        clip = AudioClip.Create("shoot_big_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateEnemyDeathSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/enemy_explosion");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.38f * sampleRate);
        float[] data = new float[count];
        float lp1 = 0f;
        float lp2 = 0f;
        float phaseSub = 0f;
        System.Random rng = new System.Random(1337);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;

            float freqSub = 112f * Mathf.Exp(-12f * t) + 28f;
            phaseSub += 2f * Mathf.PI * freqSub / sampleRate;
            float subOsc = Mathf.Sin(phaseSub) + 0.35f * Mathf.Sin(2f * phaseSub);
            float subEnv = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-7.0f * t);

            float rawNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = 2650f * Mathf.Exp(-14f * t) + 150f;
            float alpha = Mathf.Min(0.95f, 2f * Mathf.PI * cutoff / sampleRate);
            lp1 += alpha * (rawNoise - lp1);
            lp2 += alpha * (lp1 - lp2);

            float crunched = (float)System.Math.Tanh(lp2 * 2.2);
            float noiseEnv = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-8.5f * t);

            float squareGrit = (i % 60 < 30 ? 1f : -1f) * Mathf.Exp(-16f * t) * 0.15f;

            data[i] = (subOsc * subEnv * 0.70f + crunched * noiseEnv * 0.80f + squareGrit) * 0.85f;
        }

        clip = AudioClip.Create("enemy_explosion_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateUpgradeBuySfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/upgrade_buy");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.28f * sampleRate);
        float[] data = new float[count];
        float phase1 = 0f, phase2 = 0f;
        int split = (int)(0.08f * sampleRate);

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            if (i < split)
            {
                float freq = 659.25f;
                phase1 += 2f * Mathf.PI * freq / sampleRate;
                float osc = Mathf.Sin(phase1) + 0.35f * Mathf.Sin(2f * phase1) + 0.15f * Mathf.Sin(3f * phase1);
                float attack = Mathf.Min(1f, t / 0.003f);
                float decay = Mathf.Exp(-12f * t);
                data[i] = osc * attack * decay * 0.75f;
            }
            else
            {
                float noteT = (float)(i - split) / sampleRate;
                float freq = 987.77f;
                phase2 += 2f * Mathf.PI * freq / sampleRate;
                float osc = Mathf.Sin(phase2) + 0.30f * Mathf.Sin(2f * phase2) + 0.12f * Mathf.Sin(3f * phase2);
                float attack = Mathf.Min(1f, noteT / 0.003f);
                float decay = Mathf.Exp(-10f * noteT);
                data[i] = osc * attack * decay * 0.85f;
            }
        }
        clip = AudioClip.Create("upgrade_buy_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateUpgradeOpenSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/upgrade_open");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.55f * sampleRate);
        float[] data = new float[count];
        float[] notes = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteDuration = 0.08f;
        float phase = 0f;
        float currentFreq = notes[0];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            int noteIdx = Mathf.Min(notes.Length - 1, (int)(t / noteDuration));
            float targetFreq = notes[noteIdx];
            currentFreq += (targetFreq - currentFreq) * 0.15f;
            phase += 2f * Mathf.PI * currentFreq / sampleRate;

            float osc = Mathf.Sin(phase) * 0.60f + Mathf.Sin(2f * phase) * 0.25f + Mathf.Sin(3f * phase) * 0.10f;
            float shimmer = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 18f * t);
            float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-3.5f * t);
            data[i] = osc * shimmer * env * 0.75f;
        }
        clip = AudioClip.Create("upgrade_open_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateUpgradeHoverSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/upgrade_hover");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.08f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 700f - 300f * (t / 0.08f);
            float env = Mathf.Exp(-35f * t);
            float osc = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * freq * t);
            data[i] = osc * env * 0.70f;
        }
        clip = AudioClip.Create("upgrade_hover_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateUpgradeFailedSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/upgrade_failed");
        if (clip != null) return clip;

        int sampleRate = 44100;
        float duration = 0.18f;
        int count = (int)(duration * sampleRate);
        float[] data = new float[count];

        int pulse1 = (int)(0.065f * sampleRate);
        int gap = (int)(0.025f * sampleRate);
        int pulse2 = (int)(0.080f * sampleRate);

        for (int i = 0; i < count; i++)
        {
            float val = 0f;
            if (i < pulse1)
            {
                float t = (float)i / sampleRate;
                float freq = 240f - 40f * (t / 0.065f);
                float env = Mathf.Sin(Mathf.PI * (t / 0.065f));
                val = (Mathf.Sin(2f * Mathf.PI * freq * t) +
                       0.4f * Mathf.Sin(4f * Mathf.PI * freq * t) +
                       0.2f * Mathf.Sin(6f * Mathf.PI * freq * t)) * env;
            }
            else if (i >= pulse1 + gap && i < pulse1 + gap + pulse2)
            {
                float t = (float)(i - pulse1 - gap) / sampleRate;
                float freq = 190f - 50f * (t / 0.080f);
                float env = Mathf.Sin(Mathf.PI * (t / 0.080f));
                val = (Mathf.Sin(2f * Mathf.PI * freq * t) +
                       0.4f * Mathf.Sin(4f * Mathf.PI * freq * t) +
                       0.2f * Mathf.Sin(6f * Mathf.PI * freq * t)) * env;
            }
            data[i] = val * 0.75f;
        }

        clip = AudioClip.Create("upgrade_failed_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateSwitchModeSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/switch_mode");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.09f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 700f + 800f * (t / 0.09f);
            float env = Mathf.Sqrt(Mathf.Sin(Mathf.PI * (t / 0.09f)));
            float osc = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t);
            data[i] = osc * env * 0.70f;
        }

        clip = AudioClip.Create("switch_mode_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateMediumShootSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/shoot_medium");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.14f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 600f - 350f * (t / 0.14f);
            float env = Mathf.Exp(-22f * t);
            float osc = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t) + 0.15f * UnityEngine.Random.Range(-1f, 1f);
            data[i] = osc * env * 0.75f;
        }

        clip = AudioClip.Create("shoot_medium_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateUpgradeContinueSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/upgrade_continue");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.26f * sampleRate);
        float[] data = new float[count];
        float[] notes = { 523.25f, 659.25f, 783.99f };

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            int idx = Mathf.Min(notes.Length - 1, (int)(t / 0.08f));
            float f = notes[idx];
            float env = Mathf.Exp(-6f * (t % 0.08f));
            if (idx == notes.Length - 1) env = Mathf.Exp(-5f * (t - 0.16f));
            float osc = Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t);
            data[i] = osc * env * 0.75f;
        }

        clip = AudioClip.Create("upgrade_continue_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateBossLaserSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/boss_laser");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.75f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float fSub = 95f - 20f * (t / 0.75f);
            float fHigh = 850f + 150f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            float noise = UnityEngine.Random.Range(-0.35f, 0.35f);
            float env = Mathf.Min(1f, t / 0.04f) * (1f - (t / 0.75f) * 0.7f);
            float osc = (Mathf.Sin(2f * Mathf.PI * fSub * t) * 0.55f +
                         Mathf.Sin(2f * Mathf.PI * fHigh * t) * 0.25f +
                         noise) * env;
            data[i] = osc * 0.80f;
        }

        clip = AudioClip.Create("boss_laser_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateBossDeathSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/boss_death");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(1.4f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float fBoom = Mathf.Max(35f, 160f - 100f * (t / 0.6f));
            float boom = Mathf.Sin(2f * Mathf.PI * fBoom * t) * Mathf.Exp(-3.5f * t);
            float noise = UnityEngine.Random.Range(-1f, 1f) * Mathf.Exp(-4.5f * t);
            float rumble = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-1.5f * t) * 0.6f;
            float sub = Mathf.Sin(2f * Mathf.PI * 30f * t) * Mathf.Exp(-1f * t) * 0.4f;
            float val = (boom * 0.6f + noise * 0.5f + rumble + sub) * Mathf.Min(1f, t / 0.015f);
            data[i] = Mathf.Clamp(val * 0.85f, -1f, 1f);
        }

        clip = AudioClip.Create("boss_death_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGenerateTier4LaserSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/tier4_laser");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.24f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float freq = 280f + 1020f * Mathf.Exp(-14f * t);
            float fm = 40f * Mathf.Sin(2f * Mathf.PI * 90f * t);
            float osc = Mathf.Sin(2f * Mathf.PI * (freq + fm) * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * freq * t);
            float env = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-8.5f * t);
            data[i] = osc * env * 0.75f;
        }

        clip = AudioClip.Create("tier4_laser_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGeneratePlayerHitSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/player_hit");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(0.18f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float fThud = 180f * Mathf.Exp(-20f * t) + 60f;
            float thud = Mathf.Sin(2f * Mathf.PI * fThud * t);
            float noise = UnityEngine.Random.Range(-1f, 1f) * Mathf.Exp(-28f * t);
            float ring = Mathf.Sin(2f * Mathf.PI * 480f * t) * Mathf.Exp(-12f * t) * 0.4f;
            float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-10f * t);
            data[i] = (thud * 0.6f + noise * 0.45f + ring) * env * 0.85f;
        }

        clip = AudioClip.Create("player_hit_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip LoadOrGeneratePlayerDeathSfx()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/player_death");
        if (clip != null) return clip;

        int sampleRate = 44100;
        int count = (int)(1.2f * sampleRate);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float fBlast = Mathf.Max(40f, 220f - 160f * (t / 0.4f));
            float blast = Mathf.Sin(2f * Mathf.PI * fBlast * t) * Mathf.Exp(-4f * t);
            float noise = UnityEngine.Random.Range(-1f, 1f) * Mathf.Exp(-5f * t) * 0.6f;

            float tone = 0f;
            float toneSub = 0f;
            if (t > 0.15f)
            {
                float tCue = t - 0.15f;
                float fTone = Mathf.Max(75f, 360f - 180f * (tCue / 0.8f));
                tone = Mathf.Sin(2f * Mathf.PI * fTone * t) * Mathf.Exp(-2.2f * tCue) * 0.45f;
                toneSub = Mathf.Sin(2f * Mathf.PI * (fTone * 0.5f) * t) * Mathf.Exp(-2.0f * tCue) * 0.35f;
            }

            data[i] = Mathf.Clamp((blast * 0.6f + noise + tone + toneSub) * 0.80f, -1f, 1f);
        }

        clip = AudioClip.Create("player_death_proc", count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}