using UnityEngine;

// 課題【サウンドシステム】: BGM/SEを一元管理する、唯一の永続オブジェクト。
// DontDestroyOnLoadでTitle/MainGame間のシーン遷移をまたいで生存させる
// （このプロジェクトの他のUIパネル類は意図的にシーンごとに独立配置しているが、
// 音声だけは連続再生・共通クリック音のため例外的に永続化する）。
// AudioMixerは使わず、マスター音量×カテゴリ音量のシンプルな直接計算で音量を決める。
public class AudioManager : MonoBehaviour
{
  public static AudioManager Instance { get; private set; }

  [Header("BGM再生用（ループ再生・1本のみ）")]
  [SerializeField] private AudioSource bgmSource;

  [Header("SE再生用プール（同時に複数の効果音が重ならないよう、複数本を使い回す）")]
  [SerializeField] private AudioSource[] sePool = new AudioSource[6];
  private int seIndex = 0;

  [Header("SEクリップ（フリー素材を後からここに割り当てる想定）")]
  [SerializeField] private AudioClip seAttack;
  [SerializeField] private AudioClip seDamage;
  [SerializeField] private AudioClip seUiClick;
  [SerializeField] private AudioClip seDeath;
  [SerializeField] private AudioClip seMerge;   // ★1→★2 合成
  [SerializeField] private AudioClip seEvolve;  // ★2→★3 進化（育成履歴分岐）
  [SerializeField] private AudioClip seFusion;  // 異種融合（精鋭騎兵等）

  [Header("BGMクリップ（フリー素材を後からここに割り当てる想定）")]
  [SerializeField] private AudioClip bgmTitle;
  [SerializeField] private AudioClip bgmPrep;
  [SerializeField] private AudioClip bgmBattle;
  [SerializeField] private AudioClip bgmGameOver;

  void Awake()
  {
    if (Instance == null)
    {
      Instance = this;
      DontDestroyOnLoad(gameObject);
    }
    else if (Instance != this)
    {
      Destroy(gameObject);
      return;
    }

    ApplyVolumeSettings();
  }

  // 課題【サウンドシステム】: SettingsManager側の音量変更時に呼ばれる。
  // BGMは再生中も音量を即座に反映する必要があるためここで直接設定する
  // （SEはPlayOneShotのvolumeScaleで再生の都度反映するため、ここでは対象外）。
  public void ApplyVolumeSettings()
  {
    float master = SettingsManager.GetMasterVolume();
    if (bgmSource != null)
    {
      bgmSource.volume = master * SettingsManager.GetBgmVolume();
    }
  }

  public void PlayBgm(AudioClip clip, bool loop = true)
  {
    if (bgmSource == null || clip == null) return;
    if (bgmSource.clip == clip && bgmSource.isPlaying) return; // 同じ曲が既に再生中なら再生し直さない

    bgmSource.clip = clip;
    bgmSource.loop = loop;
    bgmSource.volume = SettingsManager.GetMasterVolume() * SettingsManager.GetBgmVolume();
    bgmSource.Play();
  }

  public void StopBgm()
  {
    if (bgmSource != null) bgmSource.Stop();
  }

  // 汎用SE再生（クリップを直接指定したい場合用）
  public void PlaySE(AudioClip clip)
  {
    if (clip == null || sePool == null || sePool.Length == 0) return;

    AudioSource source = sePool[seIndex];
    seIndex = (seIndex + 1) % sePool.Length;

    float volumeScale = SettingsManager.GetMasterVolume() * SettingsManager.GetSeVolume();
    if (source != null) source.PlayOneShot(clip, volumeScale);
  }

  // よく使うSEを名前で呼び出せる便利メソッド（今後、種類が増えたらここに追加していく）
  public void PlayAttackSE() => PlaySE(seAttack);
  public void PlayDamageSE() => PlaySE(seDamage);
  public void PlayUiClickSE() => PlaySE(seUiClick);
  public void PlayDeathSE() => PlaySE(seDeath);
  public void PlayMergeSE() => PlaySE(seMerge);
  public void PlayEvolveSE() => PlaySE(seEvolve);
  public void PlayFusionSE() => PlaySE(seFusion);

  // 課題【サウンドシステム: BGM接続】: フェーズごとのBGMを名前で呼び出せる便利メソッド。
  // ゲームオーバーBGMのみ、スコア確認中にループし続けると煩わしいためloop: falseにする。
  public void PlayTitleBgm() => PlayBgm(bgmTitle);
  public void PlayPrepBgm() => PlayBgm(bgmPrep);
  public void PlayBattleBgm() => PlayBgm(bgmBattle);
  public void PlayGameOverBgm() => PlayBgm(bgmGameOver, loop: false);
}
