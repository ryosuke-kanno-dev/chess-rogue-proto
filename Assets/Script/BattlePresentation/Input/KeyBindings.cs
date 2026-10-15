using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// 1つのキー割り当て。key が Key.None のスロットは「未設定」。shift = Shift を押しながら。
[Serializable]
public struct KeyBindingEntry
{
    public Key key;
    public bool shift;

    public KeyBindingEntry(Key key, bool shift = false)
    {
        this.key = key;
        this.shift = shift;
    }
}

// キー割り当て（1つの操作につき2スロット）。変更のたびにPlayerPrefsへJSONで保存し、最初の使用時に読み込む。
// 入力の判定はInput SystemのKeyboard.currentを使う。
public static class KeyBindings
{
    public const int SlotCount = 2;
    private const string PlayerPrefsKey = "Chebyss.KeyBindings.v1";

    [Serializable]
    private class SaveEntry
    {
        public int action;
        public KeyBindingEntry slot0;
        public KeyBindingEntry slot1;
    }

    [Serializable]
    private class SaveData
    {
        public List<SaveEntry> entries = new List<SaveEntry>();
    }

    private static readonly Dictionary<GameAction, KeyBindingEntry[]> bindings = new Dictionary<GameAction, KeyBindingEntry[]>();
    private static bool loaded;

    // 割り当てが変わったときに発火する
    public static event Action Changed;

    // Domain Reload無効の設定でも、再生開始のたびに状態を作り直す（購読者が残らないようにする）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        bindings.Clear();
        Changed = null;
    }

    // ---------------------------------------------------------------
    // 入力の判定
    // ---------------------------------------------------------------

    public static bool WasPressed(GameAction action) => AnySlotMatches(action, wasPressed: true);

    public static bool IsPressed(GameAction action) => AnySlotMatches(action, wasPressed: false);

    private static bool AnySlotMatches(GameAction action, bool wasPressed)
    {
        EnsureLoaded();

        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        var slots = bindings[action];
        for (int i = 0; i < slots.Length; i++)
        {
            if (Matches(keyboard, slots[i], wasPressed)) return true;
        }
        return false;
    }

    // keyが押されていて、shiftがtrueのスロットはShift（左右どちらか）が押されていること、
    // falseのスロットはShiftが押されていないこと
    private static bool Matches(Keyboard keyboard, KeyBindingEntry entry, bool wasPressed)
    {
        if (!IsValidKey(entry.key)) return false;

        var control = keyboard[entry.key];
        bool keyState = wasPressed ? control.wasPressedThisFrame : control.isPressed;
        if (!keyState) return false;

        bool shiftHeld = keyboard.shiftKey.isPressed;
        return entry.shift ? shiftHeld : !shiftHeld;
    }

    private static bool IsValidKey(Key key)
    {
        return key != Key.None && Enum.IsDefined(typeof(Key), key);
    }

    // ---------------------------------------------------------------
    // 割り当ての取得・変更
    // ---------------------------------------------------------------

    public static KeyBindingEntry GetBinding(GameAction action, int slot)
    {
        EnsureLoaded();
        if (slot < 0 || slot >= SlotCount) return default;
        return bindings[action][slot];
    }

    // 割り当てる。同じ内容（keyとshiftが同じ）が他の操作・スロットに既にある場合は、
    // そちらを未設定にして、その操作を返す（無ければnull）。
    public static GameAction? Set(GameAction action, int slot, KeyBindingEntry entry)
    {
        EnsureLoaded();
        if (slot < 0 || slot >= SlotCount) return null;

        GameAction? cleared = null;

        if (IsValidKey(entry.key))
        {
            foreach (var other in AllActions())
            {
                var slots = bindings[other];
                for (int i = 0; i < slots.Length; i++)
                {
                    if (other == action && i == slot) continue;
                    if (slots[i].key == entry.key && slots[i].shift == entry.shift)
                    {
                        slots[i] = default;
                        cleared = other;
                    }
                }
            }
        }
        else
        {
            entry = default;
        }

        bindings[action][slot] = entry;
        SaveAndNotify();
        return cleared;
    }

    public static void Clear(GameAction action, int slot)
    {
        EnsureLoaded();
        if (slot < 0 || slot >= SlotCount) return;

        bindings[action][slot] = default;
        SaveAndNotify();
    }

    public static void ResetToDefault()
    {
        loaded = true;
        SetDefaults();
        SaveAndNotify();
    }

    // ---------------------------------------------------------------
    // 表示用の文字列
    // ---------------------------------------------------------------

    // 設定済みのキーを「 / 」で連結（例：「↑ / W」「Shift+Tab」）。全て未設定なら「未設定」。
    public static string GetLabel(GameAction action)
    {
        EnsureLoaded();

        var parts = new List<string>();
        foreach (var entry in bindings[action])
        {
            if (IsValidKey(entry.key)) parts.Add(FormatEntry(entry));
        }
        return parts.Count > 0 ? string.Join(" / ", parts) : "未設定";
    }

    // スロット1つ分の表示（未設定は「未設定」）
    public static string GetEntryLabel(KeyBindingEntry entry)
    {
        return IsValidKey(entry.key) ? FormatEntry(entry) : "未設定";
    }

    // 4方向の移動キーをまとめた表示（例：「↑↓←→/WSAD」）。スロットごとに、上下左右の順に並べる。
    public static string GetMoveLabel()
    {
        EnsureLoaded();

        var directions = new[] { GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft, GameAction.MoveRight };
        var groups = new List<string>();

        for (int slot = 0; slot < SlotCount; slot++)
        {
            var labels = new List<string>();
            bool allSingleChar = true;

            foreach (var direction in directions)
            {
                var entry = bindings[direction][slot];
                if (!IsValidKey(entry.key)) continue;

                string label = FormatEntry(entry);
                if (label.Length != 1) allSingleChar = false;
                labels.Add(label);
            }

            if (labels.Count > 0) groups.Add(string.Join(allSingleChar ? "" : " ", labels));
        }

        return groups.Count > 0 ? string.Join("/", groups) : "未設定";
    }

    private static string FormatEntry(KeyBindingEntry entry)
    {
        return (entry.shift ? "Shift+" : "") + KeyName(entry.key);
    }

    private static string KeyName(Key key)
    {
        switch (key)
        {
            case Key.UpArrow:    return "↑";
            case Key.DownArrow:  return "↓";
            case Key.LeftArrow:  return "←";
            case Key.RightArrow: return "→";
            case Key.Escape:     return "Esc";
        }

        string name = key.ToString();
        if (name.StartsWith("Digit") && name.Length == 6) return name.Substring(5);
        return name;
    }

    // ---------------------------------------------------------------
    // 初期設定・保存・読み込み
    // ---------------------------------------------------------------

    private static IEnumerable<GameAction> AllActions()
    {
        return (GameAction[])Enum.GetValues(typeof(GameAction));
    }

    private static void SetDefaults()
    {
        bindings.Clear();

        Define(GameAction.MoveUp,       new KeyBindingEntry(Key.UpArrow),    new KeyBindingEntry(Key.W));
        Define(GameAction.MoveDown,     new KeyBindingEntry(Key.DownArrow),  new KeyBindingEntry(Key.S));
        Define(GameAction.MoveLeft,     new KeyBindingEntry(Key.LeftArrow),  new KeyBindingEntry(Key.A));
        Define(GameAction.MoveRight,    new KeyBindingEntry(Key.RightArrow), new KeyBindingEntry(Key.D));
        Define(GameAction.Confirm,      new KeyBindingEntry(Key.Enter),      new KeyBindingEntry(Key.NumpadEnter));
        Define(GameAction.Cancel,       new KeyBindingEntry(Key.Escape),     new KeyBindingEntry(Key.Backspace));
        Define(GameAction.NextPiece,    new KeyBindingEntry(Key.Tab),        default);
        Define(GameAction.PrevPiece,    new KeyBindingEntry(Key.Tab, true),  default);
        Define(GameAction.EndTurn,      new KeyBindingEntry(Key.Space),      default);
        Define(GameAction.OpenSettings, new KeyBindingEntry(Key.F1),         default);
    }

    private static void Define(GameAction action, KeyBindingEntry slot0, KeyBindingEntry slot1)
    {
        bindings[action] = new[] { slot0, slot1 };
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        SetDefaults();

        string json = PlayerPrefs.GetString(PlayerPrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.entries == null) throw new FormatException("KeyBindings save data is empty.");

            foreach (var saved in data.entries)
            {
                if (!Enum.IsDefined(typeof(GameAction), saved.action)) continue;

                bindings[(GameAction)saved.action] = new[] { Sanitize(saved.slot0), Sanitize(saved.slot1) };
            }
        }
        catch (Exception exception)
        {
            // 読み込みに失敗した場合は、初期設定にする
            Debug.LogWarning($"[KeyBindings] 保存されたキー設定を読み込めませんでした。初期設定にします: {exception.Message}");
            SetDefaults();
        }
    }

    // 保存データに無効なキーが入っていた場合は、未設定として扱う
    private static KeyBindingEntry Sanitize(KeyBindingEntry entry)
    {
        return IsValidKey(entry.key) ? entry : default;
    }

    private static void SaveAndNotify()
    {
        var data = new SaveData();
        foreach (var action in AllActions())
        {
            var slots = bindings[action];
            data.entries.Add(new SaveEntry { action = (int)action, slot0 = slots[0], slot1 = slots[1] });
        }

        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        Changed?.Invoke();
    }
}
