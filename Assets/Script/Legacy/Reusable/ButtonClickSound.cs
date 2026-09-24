using UnityEngine;
using UnityEngine.UI;

// 課題【サウンドシステム】: 任意のButtonにアタッチするだけでクリック時にUIクリックSEを
// 鳴らす汎用コンポーネント。各画面のボタンごとにコードを書き換える代わりに、
// Editor上でこのコンポーネントを追加するだけで済むようにする
// （既存のonClickの中身・登録済みのリスナーには一切影響を与えない。
// AddListenerで追加するだけなので、既存の動作に並行して音が鳴るだけ）。
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
  void Start()
  {
    Button button = GetComponent<Button>();
    if (button != null)
    {
      button.onClick.AddListener(PlayClickSound);
    }
  }

  void PlayClickSound()
  {
    if (AudioManager.Instance != null) AudioManager.Instance.PlayUiClickSE();
  }
}
