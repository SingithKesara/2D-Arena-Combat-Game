using UnityEngine;

/// <summary>
/// Singleton AudioManager.
/// Assign audio clips from Assets/Audio in the Inspector.
/// Call AudioManager.Instance.Play___() from anywhere.
///
/// Clip assignments (all exist in the project already):
///   lightHit   → 12_Player_Movement_SFX/61_Hit_03.wav
///   heavyHit   → 10_Battle_SFX/15_Impact_flesh_02.wav
///   lightSwing → 10_Battle_SFX/22_Slash_04.wav
///   heavySwing → 10_Battle_SFX/03_Claw_03.wav
///   jump       → 12_Player_Movement_SFX/30_Jump_03.wav
///   land       → 12_Player_Movement_SFX/45_Landing_01.wav
///   death      → 10_Battle_SFX/69_Enemy_death_01.wav
///   roundStart → 10_Battle_SFX/55_Encounter_02.wav
///   uiConfirm  → 10_UI_Menu_SFX/013_Confirm_03.wav
///   battleBGM  → Audio/Music/.../8bit-Battle01_loop.ogg
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ─────────────── Inspector assignments ───────────────────
    [Header("Combat SFX")]
    public AudioClip lightSwingSFX;
    public AudioClip heavySwingSFX;
    public AudioClip lightHitSFX;
    public AudioClip heavyHitSFX;
    public AudioClip deathSFX;

    [Header("Movement SFX")]
    public AudioClip jumpSFX;
    public AudioClip landSFX;
    public AudioClip footstepSFX;

    [Header("UI SFX")]
    public AudioClip roundStartSFX;
    public AudioClip uiConfirmSFX;
    public AudioClip uiDeclineSFX;

    [Header("Music")]
    public AudioClip battleMusicIntro;
    public AudioClip battleMusicLoop;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume   = 0.85f;

    // ─────────────── Audio sources ───────────────────────────
    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    // ─────────────── Unity lifecycle ─────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Create two dedicated AudioSources on this GameObject
        _sfxSource   = gameObject.AddComponent<AudioSource>();
        _musicSource = gameObject.AddComponent<AudioSource>();

        _sfxSource.playOnAwake   = false;
        _sfxSource.volume        = sfxVolume;

        _musicSource.playOnAwake = false;
        _musicSource.loop        = true;
        _musicSource.volume      = musicVolume;
    }

    private void Start()
    {
        PlayBattleMusic();
    }

    // ─────────────── Music ────────────────────────────────────
    private void PlayBattleMusic()
    {
        if (battleMusicIntro != null)
        {
            _musicSource.clip = battleMusicIntro;
            _musicSource.loop = false;
            _musicSource.Play();
            Invoke(nameof(PlayBattleLoop), battleMusicIntro.length);
        }
        else if (battleMusicLoop != null)
        {
            PlayBattleLoop();
        }
    }

    private void PlayBattleLoop()
    {
        if (battleMusicLoop == null) return;
        _musicSource.clip = battleMusicLoop;
        _musicSource.loop = true;
        _musicSource.Play();
    }

    // ─────────────── SFX helpers ─────────────────────────────
    private void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, volumeScale * sfxVolume);
    }

    // ─────────────── Public API ───────────────────────────────
    public void PlayLightSwing()  => PlaySFX(lightSwingSFX);
    public void PlayHeavySwing()  => PlaySFX(heavySwingSFX, 1.2f);
    public void PlayLightHit()    => PlaySFX(lightHitSFX);
    public void PlayHeavyHit()    => PlaySFX(heavyHitSFX, 1.3f);
    public void PlayJump()        => PlaySFX(jumpSFX, 0.7f);
    public void PlayLand()        => PlaySFX(landSFX, 0.6f);
    public void PlayFootstep()    => PlaySFX(footstepSFX, 0.4f);
    public void PlayDeath()       => PlaySFX(deathSFX, 1.4f);
    public void PlayRoundStart()  => PlaySFX(roundStartSFX);
    public void PlayUIConfirm()   => PlaySFX(uiConfirmSFX);
    public void PlayUIDecline()   => PlaySFX(uiDeclineSFX);

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        if (_musicSource != null) _musicSource.volume = musicVolume;
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        if (_sfxSource != null) _sfxSource.volume = sfxVolume;
    }
}
