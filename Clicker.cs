using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

// --- ВСПОМОГАТЕЛЬНЫЕ КЛАССЫ ---

[System.Serializable]
public class Skin {
    public string skinName;
    [TextArea] public string skinDescription;
    public Sprite skinSprite;
    public int price;
    public int clickMultiplier; 
    public bool isBought;
}

[System.Serializable]
public class ClickSoundItem {
    public string soundName;
    public AudioClip clip;
    public int price;
    public int clickBonus; // Бонус к прибыли за клик от звука
    public bool isBought;
}

[System.Serializable]
public class SongFolder {
    public string folderName;
    public AudioClip[] songs;
}

[System.Serializable]
public class BackgroundItem {
    public string bgName;
    public Sprite bgSprite;
    public int price;
    public int passiveBonus; 
    public bool isBought;
}

public enum AchievementType { 
    TotalMoney, 
    ClickLevel, 
    AutoLevel, 
    BuySkin, 
    BuySound, 
    BuyBackground, 
    CollectAllSkins,
    ClicksPerSecond // Достижение за скорость клика
}

[System.Serializable]
public class Achievement {
    public string name;
    public string description;
    public AchievementType type;
    public int goalValue;      
    public int itemIndex;      // Индекс предмета для Buy типов
    public int reward;         
    public bool isUnlocked;    
}

public class Clicker : MonoBehaviour {
    [Header("Экономика")]
    public int money; 
    public TextMeshProUGUI moneyText;
    private int currentSkinBonus = 1; 
    private int currentBgPassiveBonus = 0; 
    private int currentSoundBonus = 0; // Бонус от выбранного звука

    [Header("Система достижений")]
    public Achievement[] achievements;
    public GameObject achievementPanel;
    public TextMeshProUGUI achievementNameText;
    public TextMeshProUGUI achievementDescText;
    public AudioClip achievementSound;

    [Header("Улучшения")]
    public int clickLevel = 0; 
    public int autoLevel = 0;  
    public TextMeshProUGUI clickUpgradeText;
    public TextMeshProUGUI autoUpgradeText;

    [Header("Панели")]
    public GameObject shopPanel;
    public GameObject settingsPanel;
    public GameObject musicMenu;
    public GameObject upgradePanel;
    public GameObject soundShopPanel; 
    public GameObject bgShopPanel; 
    public RectTransform buttonTransform;
    private Vector2 upgradePanelTargetPos;
    private bool isPanelOpen = false;

    [Header("Система фонов")]
    public Image mainBackgroundImage; 
    public Image bgShopPreview; 
    public TextMeshProUGUI bgStatusText, bgNameText, bgBonusText;
    public BackgroundItem[] backgrounds;
    private int selectedBgIndex = 0;
    private int equippedBgIndex = 0;

    [Header("Система скинов")]
    public Image mainButtonImage;
    public Image shopSkinPreview;
    public TextMeshProUGUI shopStatusText, shopSkinNameText, shopSkinDescText;
    public Skin[] skins;
    private int selectedSkinIndex = 0;
    private int equippedSkinIndex = 0;
    private Shadow shopSkinHighlight;
    private bool isSwiping = false;

    [Header("Магазин Звуков")]
    public ClickSoundItem[] clickSounds;
    public TextMeshProUGUI soundShopStatusText, soundShopNameText, soundShopBonusText;
    private int selectedSoundIndex = 0;
    private int equippedSoundIndex = 0;

    [Header("Звуки и Музыка")]
    public AudioSource clickSource, musicSource;
    public AudioClip buySound, equipSound, errorSound, uiClickSound; 
    public SongFolder[] musicFolders;
    public Slider musicSlider; 
    public Slider soundSlider; 

    [Header("Настройки FPS и CPS")]
    public TextMeshProUGUI fpsText;
    public Toggle fpsToggle;
    private bool showFPS = false;
    private float deltaTimeFPS = 0.0f;
    private List<float> clickTimes = new List<float>(); // Для расчета CPS
    private float currentCPS = 0;

    [Header("Социальные сети")]
    public string telegramURL = "https://t.me/your_channel";
    public string discordURL = "https://discord.gg/your_invite";
    public string githubURL = "https://github.com/your_profile";

    private float autoIncomeTimer = 0f;
    private List<AudioClip> allSongsMixed = new List<AudioClip>();
    private bool isMixMode = true;
    private int currentFolderIndex = 0;
    private int lastSongIndex = -1;
    private bool isInitialized = false;

    void Start() {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120; 

        if (upgradePanel != null)
            upgradePanelTargetPos = upgradePanel.GetComponent<RectTransform>().anchoredPosition;

        foreach (SongFolder folder in musicFolders)
            foreach (AudioClip clip in folder.songs)
                if (clip != null) allSongsMixed.Add(clip);

        if (shopSkinPreview != null) shopSkinHighlight = shopSkinPreview.GetComponent<Shadow>();

        LoadGame();
        
        if (musicSlider != null && musicSource != null) musicSlider.value = musicSource.volume / 0.5f; 

        float savedSoundVol = PlayerPrefs.GetFloat("SoundVolume", 1f);
        if (clickSource != null) clickSource.volume = savedSoundVol;
        if (soundSlider != null) soundSlider.value = savedSoundVol;

        if (fpsToggle != null) fpsToggle.isOn = showFPS;
        if (fpsText != null) fpsText.gameObject.SetActive(showFPS);
        if (achievementPanel) achievementPanel.SetActive(false);

        UpdateUI();
        PlayNextRandom();

        shopPanel.SetActive(false);
        settingsPanel.SetActive(false);
        musicMenu.SetActive(false);
        upgradePanel.SetActive(false);
        if(soundShopPanel) soundShopPanel.SetActive(false);
        if(bgShopPanel) bgShopPanel.SetActive(false);

        isInitialized = true;
    }

    void Update() {
        // Авто-доход
        if (autoLevel > 0) {
            autoIncomeTimer += Time.deltaTime;
            if (autoIncomeTimer >= 1f) {
                int autoBonus = (autoLevel >= 30) ? 10 : 0;
                money += (autoLevel + autoBonus + currentBgPassiveBonus); 
                autoIncomeTimer = 0f;
                UpdateUI();
                CheckAchievements();
            }
        }

        // Музыка
        if (musicSource != null && !musicSource.isPlaying && musicSource.time == 0 && musicSource.clip != null) 
            PlayNextRandom();

        // FPS и расчет CPS
        CalculateCPS();
        if (showFPS && fpsText != null) {
            deltaTimeFPS += (Time.unscaledDeltaTime - deltaTimeFPS) * 0.1f;
            float fps = 1.0f / deltaTimeFPS;
            fpsText.text = string.Format("FPS: {0:0.} | CPS: {1:0.}", fps, currentCPS);
            fpsText.color = fps >= 60 ? Color.green : (fps >= 30 ? Color.yellow : Color.red);
        }
    }

    void CalculateCPS() {
        clickTimes.RemoveAll(t => t < Time.time - 1f); // Удаляем клики старше 1 секунды
        currentCPS = clickTimes.Count;
        if (currentCPS > 0) CheckAchievements();
    }

    public void OnClick() {
        // Прибыль = (Уровень клика + Бонус уровня) + Бонус Скина + Бонус Звука
        int clickIncome = (clickLevel + (clickLevel >= 30 ? 10 : 0) + currentSkinBonus + currentSoundBonus);
        money += clickIncome;
        
        clickTimes.Add(Time.time); // Добавляем время клика для CPS

        UpdateUI(); 
        SaveGame();
        CheckAchievements();

        if (clickSource && clickSounds.Length > equippedSoundIndex) 
            clickSource.PlayOneShot(clickSounds[equippedSoundIndex].clip);
        
        StartCoroutine(PunchAnimation());
    }

    // --- СИСТЕМА ДОСТИЖЕНИЙ ---
    void CheckAchievements() {
        foreach (Achievement a in achievements) {
            if (a.isUnlocked) continue;

            bool reachedGoal = false;
            switch (a.type) {
                case AchievementType.TotalMoney: if (money >= a.goalValue) reachedGoal = true; break;
                case AchievementType.ClickLevel: if (clickLevel >= a.goalValue) reachedGoal = true; break;
                case AchievementType.AutoLevel: if (autoLevel >= a.goalValue) reachedGoal = true; break;
                case AchievementType.ClicksPerSecond: if (currentCPS >= a.goalValue) reachedGoal = true; break;
                case AchievementType.BuySkin: if (skins.Length > a.itemIndex && skins[a.itemIndex].isBought) reachedGoal = true; break;
                case AchievementType.BuySound: if (clickSounds.Length > a.itemIndex && clickSounds[a.itemIndex].isBought) reachedGoal = true; break;
                case AchievementType.BuyBackground: if (backgrounds.Length > a.itemIndex && backgrounds[a.itemIndex].isBought) reachedGoal = true; break;
                case AchievementType.CollectAllSkins:
                    reachedGoal = true;
                    foreach(var s in skins) if(!s.isBought) reachedGoal = false;
                    break;
            }

            if (reachedGoal) UnlockAchievement(a);
        }
    }

    void UnlockAchievement(Achievement a) {
        a.isUnlocked = true;
        money += a.reward;
        UpdateUI();
        SaveGame();
        if (achievementPanel) {
            StopCoroutine("ShowAchievementRoutine");
            StartCoroutine(ShowAchievementRoutine(a));
        }
        if (achievementSound && clickSource) clickSource.PlayOneShot(achievementSound);
    }

    IEnumerator ShowAchievementRoutine(Achievement a) {
        achievementNameText.text = a.name;
        achievementDescText.text = a.description + " (+" + a.reward + "$)";
        achievementPanel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < 0.3f) {
            elapsed += Time.unscaledDeltaTime;
            achievementPanel.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, elapsed / 0.3f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(3f);
        elapsed = 0f;
        while (elapsed < 0.3f) {
            elapsed += Time.unscaledDeltaTime;
            achievementPanel.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, elapsed / 0.3f);
            yield return null;
        }
        achievementPanel.SetActive(false);
    }

    // --- МАГАЗИН ФОНОВ ---
    public void NextBg() { PlayUIClick(); selectedBgIndex = (selectedBgIndex + 1) % backgrounds.Length; UpdateBgShopUI(); }
    public void PreviousBg() { PlayUIClick(); selectedBgIndex--; if (selectedBgIndex < 0) selectedBgIndex = backgrounds.Length - 1; UpdateBgShopUI(); }
    public void BuyOrEquipBg() {
        BackgroundItem b = backgrounds[selectedBgIndex];
        if (selectedBgIndex == equippedBgIndex) return;
        if (b.isBought) EquipBackground(b);
        else if (money >= b.price) {
            money -= b.price; b.isBought = true;
            if (buySound) clickSource.PlayOneShot(buySound);
            EquipBackground(b);
            CheckAchievements();
        } else if (errorSound) clickSource.PlayOneShot(errorSound);
        UpdateUI(); SaveGame();
    }
    void EquipBackground(BackgroundItem b) {
        equippedBgIndex = selectedBgIndex;
        if (mainBackgroundImage) mainBackgroundImage.sprite = b.bgSprite;
        currentBgPassiveBonus = b.passiveBonus;
        if (isInitialized && equipSound) clickSource.PlayOneShot(equipSound);
        UpdateBgShopUI();
    }
    void UpdateBgShopUI() {
        if (backgrounds.Length == 0) return;
        BackgroundItem b = backgrounds[selectedBgIndex];
        if (bgShopPreview) bgShopPreview.sprite = b.bgSprite;
        if (bgNameText) bgNameText.text = b.bgName;
        if (bgBonusText) bgBonusText.text = "Пассив: +" + b.passiveBonus + "/сек";
        bgStatusText.text = (selectedBgIndex == equippedBgIndex) ? "Надето" : (b.isBought ? "Надеть" : b.price + "$");
    }

    // --- МАГАЗИН ЗВУКОВ ---
    public void NextSound() { PlayUIClick(); selectedSoundIndex = (selectedSoundIndex + 1) % clickSounds.Length; UpdateSoundShopUI(); }
    public void PreviousSound() { PlayUIClick(); selectedSoundIndex--; if (selectedSoundIndex < 0) selectedSoundIndex = clickSounds.Length - 1; UpdateSoundShopUI(); }
    public void PreviewSound() { if (clickSounds.Length > 0 && clickSource) clickSource.PlayOneShot(clickSounds[selectedSoundIndex].clip); }
    public void BuyOrEquipSound() {
        ClickSoundItem s = clickSounds[selectedSoundIndex];
        if (selectedSoundIndex == equippedSoundIndex) return;
        if (s.isBought) EquipSound(s);
        else if (money >= s.price) {
            money -= s.price; s.isBought = true;
            if (isInitialized && buySound) clickSource.PlayOneShot(buySound);
            EquipSound(s);
            CheckAchievements();
        } else if (isInitialized && errorSound) clickSource.PlayOneShot(errorSound);
        UpdateUI(); SaveGame();
    }
    void EquipSound(ClickSoundItem s) {
        equippedSoundIndex = selectedSoundIndex;
        currentSoundBonus = s.clickBonus; // Применяем бонус звука
        if (isInitialized && equipSound) clickSource.PlayOneShot(equipSound);
        UpdateSoundShopUI();
    }
    void UpdateSoundShopUI() {
        if (clickSounds.Length == 0) return;
        ClickSoundItem s = clickSounds[selectedSoundIndex];
        if (soundShopNameText) soundShopNameText.text = s.soundName;
        if (soundShopBonusText) soundShopBonusText.text = "Бонус: +" + s.clickBonus;
        soundShopStatusText.text = (selectedSoundIndex == equippedSoundIndex) ? "Надето" : (s.isBought ? "Надеть" : s.price + "$");
    }

    // --- МАГАЗИН СКИНОВ ---
    public void NextSkin() { if (isSwiping) return; PlayUIClick(); StartCoroutine(AnimateSkinSwipe(1)); }
    public void PreviousSkin() { if (isSwiping) return; PlayUIClick(); StartCoroutine(AnimateSkinSwipe(-1)); }
    IEnumerator AnimateSkinSwipe(int direction) {
        isSwiping = true;
        RectTransform skinRT = shopSkinPreview.GetComponent<RectTransform>();
        Vector2 startPos = skinRT.anchoredPosition, exitPos = new Vector2(startPos.x - (500 * direction), startPos.y), entryPos = new Vector2(startPos.x + (500 * direction), startPos.y); 
        float duration = 0.15f, elapsed = 0f;
        while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; skinRT.anchoredPosition = Vector2.Lerp(startPos, exitPos, elapsed / duration); yield return null; }
        if (direction > 0) selectedSkinIndex = (selectedSkinIndex + 1) % skins.Length; else { selectedSkinIndex--; if (selectedSkinIndex < 0) selectedSkinIndex = skins.Length - 1; }
        UpdateShopUI();
        skinRT.anchoredPosition = entryPos; elapsed = 0f;
        while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; skinRT.anchoredPosition = Vector2.Lerp(entryPos, startPos, elapsed / duration); yield return null; }
        skinRT.anchoredPosition = startPos; isSwiping = false;
    }
    public void BuyOrEquipSkin() {
        Skin s = skins[selectedSkinIndex];
        if (selectedSkinIndex == equippedSkinIndex) return;
        if (s.isBought) EquipSkin(s);
        else if (money >= s.price) {
            money -= s.price; s.isBought = true;
            if (isInitialized && buySound) clickSource.PlayOneShot(buySound);
            EquipSkin(s);
            CheckAchievements();
        } else if (isInitialized && errorSound) clickSource.PlayOneShot(errorSound);
        UpdateUI(); SaveGame();
    }
    void EquipSkin(Skin s) {
        equippedSkinIndex = selectedSkinIndex;
        mainButtonImage.sprite = s.skinSprite;
        currentSkinBonus = s.clickMultiplier;
        if (isInitialized && equipSound) clickSource.PlayOneShot(equipSound);
        if (shopSkinHighlight != null) StartCoroutine(FlashHighlight());
        UpdateShopUI();
    }
    void UpdateShopUI() {
        if (skins.Length == 0) return;
        Skin s = skins[selectedSkinIndex];
        if (shopSkinPreview) shopSkinPreview.sprite = s.skinSprite;
        if (shopSkinNameText) shopSkinNameText.text = s.skinName;
        if (shopSkinDescText) shopSkinDescText.text = s.skinDescription + "\n<color=yellow>+" + s.clickMultiplier + "</color>";
        shopStatusText.text = (selectedSkinIndex == equippedSkinIndex) ? "Надето" : (s.isBought ? "Надеть" : s.price + "$");
    }

    // --- УЛУЧШЕНИЯ ---
    public void UpgradeClick() {
        int cost = (clickLevel + 1) * 10 * (int)Mathf.Pow(2, clickLevel / 10);
        if (money >= cost) { money -= cost; clickLevel++; if (isInitialized && buySound) clickSource.PlayOneShot(buySound); UpdateUI(); SaveGame(); CheckAchievements(); }
        else if (isInitialized && errorSound) clickSource.PlayOneShot(errorSound);
    }
    public void UpgradeAuto() {
        int cost = (autoLevel + 1) * 15 * (int)Mathf.Pow(2, autoLevel / 10);
        if (money >= cost) { money -= cost; autoLevel++; if (isInitialized && buySound) clickSource.PlayOneShot(buySound); UpdateUI(); SaveGame(); CheckAchievements(); }
        else if (isInitialized && errorSound) clickSource.PlayOneShot(errorSound);
    }

    // --- СИСТЕМА СОХРАНЕНИЯ ---
    public void SaveGame() {
        PlayerPrefs.SetInt("Money", money);
        PlayerPrefs.SetInt("ClickLvl", clickLevel);
        PlayerPrefs.SetInt("AutoLvl", autoLevel);
        PlayerPrefs.SetInt("EquippedSkin", equippedSkinIndex);
        PlayerPrefs.SetInt("EquippedSound", equippedSoundIndex);
        PlayerPrefs.SetInt("EquippedBg", equippedBgIndex);
        PlayerPrefs.SetInt("ShowFPS", showFPS ? 1 : 0);
        for (int i = 0; i < skins.Length; i++) PlayerPrefs.SetInt("Skin_" + i, skins[i].isBought ? 1 : 0);
        for (int i = 0; i < clickSounds.Length; i++) PlayerPrefs.SetInt("Sound_" + i, clickSounds[i].isBought ? 1 : 0);
        for (int i = 0; i < backgrounds.Length; i++) PlayerPrefs.SetInt("Bg_" + i, backgrounds[i].isBought ? 1 : 0);
        for (int i = 0; i < achievements.Length; i++) PlayerPrefs.SetInt("Achiv_" + i, achievements[i].isUnlocked ? 1 : 0);
        PlayerPrefs.Save();
    }
    void LoadGame() {
        money = PlayerPrefs.GetInt("Money", 0);
        clickLevel = PlayerPrefs.GetInt("ClickLvl", 0);
        autoLevel = PlayerPrefs.GetInt("AutoLvl", 0);
        equippedSkinIndex = PlayerPrefs.GetInt("EquippedSkin", 0);
        equippedSoundIndex = PlayerPrefs.GetInt("EquippedSound", 0);
        equippedBgIndex = PlayerPrefs.GetInt("EquippedBg", 0);
        showFPS = PlayerPrefs.GetInt("ShowFPS", 0) == 1;
        for (int i = 0; i < skins.Length; i++) skins[i].isBought = PlayerPrefs.GetInt("Skin_" + i, (i == 0 ? 1 : 0)) == 1;
        for (int i = 0; i < clickSounds.Length; i++) clickSounds[i].isBought = PlayerPrefs.GetInt("Sound_" + i, (i == 0 ? 1 : 0)) == 1;
        for (int i = 0; i < backgrounds.Length; i++) backgrounds[i].isBought = PlayerPrefs.GetInt("Bg_" + i, (i == 0 ? 1 : 0)) == 1;
        for (int i = 0; i < achievements.Length; i++) achievements[i].isUnlocked = PlayerPrefs.GetInt("Achiv_" + i, 0) == 1;

        if (skins.Length > equippedSkinIndex) { mainButtonImage.sprite = skins[equippedSkinIndex].skinSprite; currentSkinBonus = skins[equippedSkinIndex].clickMultiplier; }
        if (backgrounds.Length > equippedBgIndex) { mainBackgroundImage.sprite = backgrounds[equippedBgIndex].bgSprite; currentBgPassiveBonus = backgrounds[equippedBgIndex].passiveBonus; }
        if (clickSounds.Length > equippedSoundIndex) { currentSoundBonus = clickSounds[equippedSoundIndex].clickBonus; }
    }

    // --- ОБЩИЕ ФУНКЦИИ UI ---
    private void PlayUIClick() { if (isInitialized && uiClickSound && clickSource) clickSource.PlayOneShot(uiClickSound); }
    public void ChangeMusicVolume(float v) { if (musicSource) musicSource.volume = v * 0.5f; }
    public void ChangeSoundVolume(float v) { if (clickSource != null) { clickSource.volume = v; PlayerPrefs.SetFloat("SoundVolume", v); } }
    public void ToggleFPS(bool isOn) { showFPS = isOn; if (fpsText != null) fpsText.gameObject.SetActive(isOn); PlayerPrefs.SetInt("ShowFPS", isOn ? 1 : 0); PlayUIClick(); }
    public void SelectMusicFolder(int index) { PlayUIClick(); isMixMode = false; currentFolderIndex = index; lastSongIndex = -1; PlayNextRandom(); }
    public void SetMixMode() { PlayUIClick(); isMixMode = true; lastSongIndex = -1; PlayNextRandom(); }
    void PlayNextRandom() {
        AudioClip[] pool = isMixMode ? allSongsMixed.ToArray() : musicFolders[currentFolderIndex].songs;
        if (pool.Length > 0 && musicSource) {
            int r; if (pool.Length > 1) { do { r = Random.Range(0, pool.Length); } while (r == lastSongIndex); } else r = 0;
            lastSongIndex = r; musicSource.clip = pool[r]; musicSource.Play();
        }
    }
    void UpdateUI() {
        if (moneyText) moneyText.text = money + "$";
        if (clickUpgradeText) clickUpgradeText.text = "Lvl " + clickLevel + "\n" + ((clickLevel + 1) * 10 * (int)Mathf.Pow(2, clickLevel / 10)) + "$";
        if (autoUpgradeText) autoUpgradeText.text = "Lvl " + autoLevel + "\n" + ((autoLevel + 1) * 15 * (int)Mathf.Pow(2, autoLevel / 10)) + "$";
        UpdateShopUI(); UpdateSoundShopUI(); UpdateBgShopUI();
    }
    public void ToggleBgShop(bool open) { if (open && isPanelOpen) return; PlayUIClick(); bgShopPanel.SetActive(open); isPanelOpen = open; if (open) UpdateBgShopUI(); }
    public void ToggleShop(bool open) { if (open && isPanelOpen) return; PlayUIClick(); shopPanel.SetActive(open); isPanelOpen = open; if (open) UpdateShopUI(); }
    public void ToggleSoundShop(bool open) { if (open && isPanelOpen) return; PlayUIClick(); soundShopPanel.SetActive(open); isPanelOpen = open; if (open) UpdateSoundShopUI(); }
    public void ToggleSettings(bool open) { if (open && isPanelOpen) return; PlayUIClick(); StartCoroutine(AnimateScale(settingsPanel, open)); }
    public void ToggleMusicMenu(bool open) { if (open && isPanelOpen) return; PlayUIClick(); StartCoroutine(AnimateScale(musicMenu, open)); }
    public void ToggleUpgrade(bool open) { if (open && isPanelOpen) return; PlayUIClick(); StartCoroutine(AnimateSlide(upgradePanel, open)); }

    // --- СОЦИАЛЬНЫЕ СЕТИ ---
    public void OpenTelegram() { PlayUIClick(); Application.OpenURL(telegramURL); }
    public void OpenDiscord() { PlayUIClick(); Application.OpenURL(discordURL); }
    public void OpenGithub() { PlayUIClick(); Application.OpenURL(githubURL); }

    IEnumerator AnimateScale(GameObject panel, bool open) { if (open) { isPanelOpen = true; panel.SetActive(true); } float duration = 0.2f, elapsed = 0f; Vector3 start = open ? Vector3.zero : Vector3.one, target = open ? Vector3.one : Vector3.zero; while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; panel.transform.localScale = Vector3.Lerp(start, target, elapsed / duration); yield return null; } panel.transform.localScale = target; if (!open) { panel.SetActive(false); isPanelOpen = false; } }
    IEnumerator AnimateSlide(GameObject panel, bool open) { RectTransform rt = panel.GetComponent<RectTransform>(); if (open) { isPanelOpen = true; panel.SetActive(true); } float duration = 0.3f, elapsed = 0f; Vector2 hiddenPos = new Vector2(upgradePanelTargetPos.x, -Screen.height), startPos = open ? hiddenPos : upgradePanelTargetPos, endPos = open ? upgradePanelTargetPos : hiddenPos; while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; rt.anchoredPosition = Vector2.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, elapsed / duration)); yield return null; } rt.anchoredPosition = endPos; if (!open) { panel.SetActive(false); isPanelOpen = false; } }
    IEnumerator PunchAnimation() { if (buttonTransform) { buttonTransform.localScale = Vector3.one * 1.1f; yield return new WaitForSeconds(0.05f); buttonTransform.localScale = Vector3.one; } }
    IEnumerator FlashHighlight() { if (shopSkinHighlight == null) yield break; Color c = shopSkinHighlight.effectColor; float dur = 0.2f, e = 0f; while (e < dur) { e += Time.deltaTime; c.a = Mathf.Lerp(0f, 1f, e / dur); shopSkinHighlight.effectColor = c; yield return null; } e = 0f; while (e < dur) { e += Time.deltaTime; c.a = Mathf.Lerp(1f, 0f, e / dur); shopSkinHighlight.effectColor = c; yield return null; } }
}